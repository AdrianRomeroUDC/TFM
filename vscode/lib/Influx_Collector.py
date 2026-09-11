# lib/Influx_Collector.py
from datetime import datetime, timezone
import json
import threading
import time
import queue
import os

try:
    from urllib.request import Request, urlopen
    from urllib.error import HTTPError, URLError
except ImportError:
    from urllib2 import Request, urlopen, HTTPError, URLError

from fischertechnik.mqtt.MqttClient import MqttClient

INFLUX_URL = 'https://eu-central-1-1.aws.cloud2.influxdata.com/api/v2/write?org=fischertechnik&bucket=factory_TFM&precision=ms'
INFLUX_TOKEN = 'tYzrHx9kwepkwm5ZwAGFbKA_aSok9i_OQBue_zAmXZY-5FxfBFxNoVLvcIzUCc0G1RDcLKY9DNtBI5Lbe0gWAg=='
MQTT_HOST = '10.113.36.36'
MQTT_PORT = 1884
MQTT_USER = 'LearningFactory'
MQTT_PASSWORD = 'Fischertechnik1'

WORKERS = 1
BATCH_SIZE = 100
FLUSH_INTERVAL = 1
MAX_RETRIES = 5
RETRY_BACKOFF_BASE = 1

stop_event = threading.Event()
influx_client = None

_influx_queue = queue.Queue(maxsize=5000)
_worker_threads = []


def _now_ms():
    return int(time.time() * 1000)


def _to_ms(ts_value):
    try:
        if isinstance(ts_value, (int, float)):
            if ts_value < 1000000000000:
                return int(ts_value * 1000)
            if ts_value < 1000000000000000:
                return int(ts_value)

        if isinstance(ts_value, str):
            text = ts_value.strip()
            if text.endswith('Z'):
                text = text[:-1]

            if '.' in text:
                base, frac = text.split('.', 1)
                frac = ''.join(ch for ch in frac if ch.isdigit())
                frac = (frac + '000')[:3]
                dt = datetime.strptime(base, '%Y-%m-%dT%H:%M:%S').replace(tzinfo=timezone.utc)
                return int(dt.timestamp() * 1000) + int(frac)

            dt = datetime.strptime(text, '%Y-%m-%dT%H:%M:%S').replace(tzinfo=timezone.utc)
            return int(dt.timestamp() * 1000)
    except Exception:
        pass

    return _now_ms()


def _escape_tag(value):
    return str(value).replace('\\', '\\\\').replace(' ', '\\ ').replace(',', '\\,').replace('=', '\\=')


def _escape_field_string(value):
    return '"' + str(value).replace('\\', '\\\\').replace('"', '\\"') + '"'


def _field_value(value):
    if isinstance(value, bool):
        return 'true' if value else 'false'
    if isinstance(value, int) and not isinstance(value, bool):
        return str(value) + 'i'
    if isinstance(value, float):
        return str(value)
    return _escape_field_string(value)


def _format_line(measurement, tags, fields, ts_ms):
    tag_part = ''
    for key, value in (tags or {}).items():
        if value is None:
            continue
        tag_part += ',' + _escape_tag(key) + '=' + _escape_tag(value)

    field_items = []
    for key, value in (fields or {}).items():
        if value is None:
            continue
        field_items.append(_escape_tag(key) + '=' + _field_value(value))

    if not field_items:
        return None

    line = _escape_tag(measurement) + tag_part + ' ' + ','.join(field_items) + ' ' + str(int(ts_ms))
    return line


def _post_payload(payload):
    req = Request(
        INFLUX_URL,
        data=payload.encode('utf-8'),
        headers={
            'Authorization': 'Token ' + INFLUX_TOKEN,
            'Content-Type': 'text/plain; charset=utf-8',
        },
        method='POST'
    )
    response = urlopen(req, timeout=5)
    try:
        response.read()
    finally:
        try:
            response.close()
        except Exception:
            pass


def _flush_buffer(lines):
    if not lines:
        return True
    payload = '\n'.join(lines)
    attempt = 0
    while attempt <= MAX_RETRIES and not stop_event.is_set():
        try:
            _post_payload(payload)
            print('Influx flush: sent {} lines'.format(len(lines)))
            return True
        except HTTPError as e:
            code = getattr(e, 'code', None)
            if code and 400 <= code < 500 and code != 429:
                break
        except URLError:
            pass
        except Exception:
            pass
        attempt += 1
        time.sleep(RETRY_BACKOFF_BASE * (2 ** (attempt - 1)))
    print('Influx flush failed: dropping {} lines'.format(len(lines)))
    return False


def _worker_loop():
    buffer = []
    last_flush = time.time()
    while not stop_event.is_set() or not _influx_queue.empty() or buffer:
        try:
            timeout = max(0.0, FLUSH_INTERVAL - (time.time() - last_flush))
            item = _influx_queue.get(timeout=timeout)
            buffer.append(item)
            _influx_queue.task_done()
            if len(buffer) >= BATCH_SIZE:
                _flush_buffer(buffer)
                buffer = []
                last_flush = time.time()
        except queue.Empty:
            if buffer and (time.time() - last_flush) >= FLUSH_INTERVAL:
                _flush_buffer(buffer)
                buffer = []
                last_flush = time.time()
            continue
    if buffer:
        _flush_buffer(buffer)


def _monitor_loop():
    while not stop_event.is_set():
        try:
            qsize = _influx_queue.qsize()
            print('Influx queue size:', qsize)
            if qsize >= int(_influx_queue.maxsize * 0.8):
                print('WARNING: Influx queue >= 80% full')
        except Exception:
            pass
        time.sleep(5)


def _load_persisted():
    return


def _start_workers():
    _load_persisted()
    for _ in range(WORKERS):
        t = threading.Thread(target=_worker_loop)
        t.daemon = True
        t.start()
        _worker_threads.append(t)
    monitor = threading.Thread(target=_monitor_loop)
    monitor.daemon = True
    monitor.start()
    _worker_threads.append(monitor)


def write_influx_line(measurement, tags, fields, ts_ms):
    line = _format_line(measurement, tags, fields, ts_ms)
    if not line:
        return
    try:
        _influx_queue.put_nowait(line)
    except queue.Full:
        try:
            _influx_queue.get_nowait()  # drop oldest to avoid blocking the MQTT callback
        except Exception:
            pass
        try:
            _influx_queue.put_nowait(line)
        except Exception:
            pass


def _payload_as_dict(message):
    try:
        return json.loads(message.payload.decode('utf-8'))
    except Exception:
        return None


def _common_fields(payload, payload_text):
    fields = {'payload': payload_text}
    if isinstance(payload, dict) and 'ts' in payload:
        fields['ts'] = payload.get('ts')
    return fields


def mqtt_callback_bme680(message):
    try:
        topic = getattr(message, 'topic', 'i/bme680')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['t'] = payload.get('t')
            fields['rt'] = payload.get('rt')
            fields['h'] = payload.get('h')
            fields['rh'] = payload.get('rh')
            fields['p'] = payload.get('p')
            fields['iaq'] = payload.get('iaq')
            fields['aq'] = payload.get('aq')
            fields['gr'] = payload.get('gr')

        write_influx_line('bme680', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (bme680):', e)


def mqtt_callback_ldr(message):
    try:
        topic = getattr(message, 'topic', 'i/ldr')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['br'] = payload.get('br')
            fields['ldr'] = payload.get('ldr')

        write_influx_line('ldr', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (ldr):', e)


def mqtt_callback_stock(message):
    try:
        topic = getattr(message, 'topic', 'f/i/stock')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = {'payload': payload_text}
        stock_list = []

        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['ts'] = payload.get('ts')

            if 'stockItems' in payload and isinstance(payload['stockItems'], list):
                stock_list = payload['stockItems']
            elif 'stock' in payload and isinstance(payload['stock'], list):
                stock_list = payload['stock']
        elif isinstance(payload, list):
            stock_list = payload

        if stock_list:
            fields['stock_count'] = len(stock_list)
            for idx, item in enumerate(stock_list):
                if isinstance(item, dict):
                    location = item.get('location')
                    workpiece = item.get('workpiece')
                    fields['loc_%d' % idx] = location
                    if isinstance(workpiece, dict):
                        fields['id_%d' % idx] = workpiece.get('id')
                        fields['type_%d' % idx] = workpiece.get('type')
                        fields['state_%d' % idx] = workpiece.get('state')
                    else:
                        fields['id_%d' % idx] = None
                        fields['type_%d' % idx] = str(workpiece) if workpiece is not None else None
                        fields['state_%d' % idx] = None
                elif isinstance(item, list):
                    for jdx, val in enumerate(item):
                        fields['item_%d_%d' % (idx, jdx)] = str(val) if val is not None else None
                else:
                    fields['stock_%d' % idx] = str(item) if item is not None else None

        write_influx_line('stock', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (stock):', e)


def mqtt_callback_vgr_pos(message):
    try:
        topic = getattr(message, 'topic', 'dt/vgr/pos')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['rotation'] = payload.get('rotation')
            fields['vertical'] = payload.get('vertical')
            fields['extend'] = payload.get('extend')

        write_influx_line('vgr_pos', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (vgr_pos):', e)


def mqtt_callback_vgr_grip(message):
    try:
        topic = getattr(message, 'topic', 'dt/vgr/grip')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['active'] = payload.get('active')

        write_influx_line('vgr_grip', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (vgr_grip):', e)


def mqtt_callback_hbw_pos(message):
    try:
        topic = getattr(message, 'topic', 'dt/hbw/pos')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['horizontal'] = payload.get('horizontal')
            fields['vertical'] = payload.get('vertical')
            fields['extend'] = payload.get('extend')

        write_influx_line('hbw_pos', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (hbw_pos):', e)


def mqtt_callback_hbw_belt(message):
    try:
        topic = getattr(message, 'topic', 'dt/hbw/belt')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['belt_speed'] = payload.get('belt_speed')
            fields['isTriggeredIn'] = payload.get('isTriggeredIn')
            fields['isTriggeredOut'] = payload.get('isTriggeredOut')
            fields['rot_direction'] = payload.get('rot_direction')

        write_influx_line('hbw_belt', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (hbw_belt):', e)


def mqtt_callback_dps_dsi(message):
    try:
        topic = getattr(message, 'topic', 'dt/dps/dsi')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['dsi_sensor'] = payload.get('dsi_sensor')

        write_influx_line('dps_dsi', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (dps_dsi):', e)


def mqtt_callback_dps_dso(message):
    try:
        topic = getattr(message, 'topic', 'dt/dps/dso')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['dso_sensor'] = payload.get('dso_sensor')

        write_influx_line('dps_dso', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (dps_dso):', e)


def mqtt_callback_dps_color(message):
    try:
        topic = getattr(message, 'topic', 'dt/dps/color')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['color'] = payload.get('color')

        write_influx_line('dps_color', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (dps_color):', e)


def mqtt_callback_dps_nfc(message):
    try:
        topic = getattr(message, 'topic', 'dt/dps/nfc')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['id'] = payload.get('id')
            fields['nfc'] = payload.get('nfc')

        write_influx_line('dps_nfc', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (dps_nfc):', e)


def mqtt_callback_sld_belt(message):
    try:
        topic = getattr(message, 'topic', 'dt/sld/belt')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['cylinder_sensor'] = payload.get('cylinder_sensor')
            fields['entry_sensor'] = payload.get('entry_sensor')
            fields['speed'] = payload.get('speed')

        write_influx_line('sld_belt', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (sld_belt):', e)


def mqtt_callback_sld_cylinder(message):
    try:
        topic = getattr(message, 'topic', 'dt/sld/cylinder')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['cyl_color'] = payload.get('cyl_color')
            fields['active'] = payload.get('active')
            fields['is_white'] = payload.get('is_white')
            fields['is_red'] = payload.get('is_red')
            fields['is_blue'] = payload.get('is_blue')

        write_influx_line('sld_cylinder', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (sld_cylinder):', e)


def mqtt_callback_mpo_belt(message):
    try:
        topic = getattr(message, 'topic', 'dt/mpo/belt')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['active'] = payload.get('active')
            fields['exit_sensor'] = payload.get('exit_sensor')

        write_influx_line('mpo_belt', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (mpo_belt):', e)


def mqtt_callback_mpo_oven(message):
    try:
        topic = getattr(message, 'topic', 'dt/mpo/oven')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['oven_sensor'] = payload.get('oven_sensor')
            fields['close_door'] = payload.get('close_door')
            fields['lights'] = payload.get('lights')
            fields['move2Ref5'] = payload.get('move2Ref5')
            fields['move2Ref6'] = payload.get('move2Ref6')
            fields['open_door'] = payload.get('open_door')

        write_influx_line('mpo_oven', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (mpo_oven):', e)


def mqtt_callback_mpo_arm(message):
    try:
        topic = getattr(message, 'topic', 'dt/mpo/arm')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['move2Ref3'] = payload.get('move2Ref3')
            fields['move2Ref4'] = payload.get('move2Ref4')
            fields['lowering'] = payload.get('lowering')
            fields['vacuum'] = payload.get('vacuum')

        write_influx_line('mpo_arm', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (mpo_arm):', e)


def mqtt_callback_mpo_turntable(message):
    try:
        topic = getattr(message, 'topic', 'dt/mpo/turntable')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['eject'] = payload.get('eject')
            fields['move2Ref7'] = payload.get('move2Ref7')
            fields['move2Ref8'] = payload.get('move2Ref8')
            fields['move2Ref9'] = payload.get('move2Ref9')
            fields['move2Ref10'] = payload.get('move2Ref10')
            fields['rotation'] = payload.get('rotation')
            fields['saw'] = payload.get('saw')

        write_influx_line('mpo_turntable', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (mpo_turntable):', e)


def mqtt_callback_ssc_leds(message):
    try:
        topic = getattr(message, 'topic', 'dt/ssc/leds')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['led_online'] = payload.get('led_online')
            fields['leds_semaphore'] = payload.get('leds_semaphore')

        write_influx_line('ssc_leds', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (ssc_leds):', e)


def mqtt_callback_ssc_camera(message):
    try:
        topic = getattr(message, 'topic', 'dt/ssc/camera')
        payload_text = message.payload.decode('utf-8')
        payload = _payload_as_dict(message)
        ts_ms = _now_ms()

        fields = _common_fields(payload, payload_text)
        if isinstance(payload, dict):
            ts_ms = _to_ms(payload.get('ts'))
            fields['pan'] = payload.get('pan')
            fields['tilt'] = payload.get('tilt')

        write_influx_line('ssc_camera', {'topic': topic}, fields, ts_ms)
    except Exception as e:
        print('Influx write error (ssc_camera):', e)


def start_influx_collector():
    global influx_client
    max_retries = 5
    retry_count = 0

    while retry_count < max_retries:
        try:
            print('[{}/{}] Intentando conectar a MQTT...'.format(retry_count + 1, max_retries))
            influx_client = MqttClient(client_id='influx-collector-' + str(int(time.time())))
            influx_client.connect(
                host=MQTT_HOST,
                port=MQTT_PORT,
                user=MQTT_USER,
                password=MQTT_PASSWORD
            )

            time.sleep(1)

            print('Influx MQTT connected:', influx_client.is_connected())

            if influx_client.is_connected():
                influx_client.subscribe(topic='i/bme680', callback=mqtt_callback_bme680, qos=2)
                influx_client.subscribe(topic='i/ldr', callback=mqtt_callback_ldr, qos=2)
                influx_client.subscribe(topic='f/i/stock', callback=mqtt_callback_stock, qos=2)

                influx_client.subscribe(topic='dt/vgr/pos', callback=mqtt_callback_vgr_pos, qos=2)
                influx_client.subscribe(topic='dt/vgr/grip', callback=mqtt_callback_vgr_grip, qos=2)

                influx_client.subscribe(topic='dt/hbw/pos', callback=mqtt_callback_hbw_pos, qos=2)
                influx_client.subscribe(topic='dt/hbw/belt', callback=mqtt_callback_hbw_belt, qos=2)

                influx_client.subscribe(topic='dt/dps/dsi', callback=mqtt_callback_dps_dsi, qos=2)
                influx_client.subscribe(topic='dt/dps/dso', callback=mqtt_callback_dps_dso, qos=2)
                influx_client.subscribe(topic='dt/dps/color', callback=mqtt_callback_dps_color, qos=2)
                influx_client.subscribe(topic='dt/dps/nfc', callback=mqtt_callback_dps_nfc, qos=2)

                influx_client.subscribe(topic='dt/sld/belt', callback=mqtt_callback_sld_belt, qos=2)
                influx_client.subscribe(topic='dt/sld/cylinder', callback=mqtt_callback_sld_cylinder, qos=2)

                influx_client.subscribe(topic='dt/mpo/belt', callback=mqtt_callback_mpo_belt, qos=2)
                influx_client.subscribe(topic='dt/mpo/oven', callback=mqtt_callback_mpo_oven, qos=2)
                influx_client.subscribe(topic='dt/mpo/arm', callback=mqtt_callback_mpo_arm, qos=2)
                influx_client.subscribe(topic='dt/mpo/turntable', callback=mqtt_callback_mpo_turntable, qos=2)

                influx_client.subscribe(topic='dt/ssc/leds', callback=mqtt_callback_ssc_leds, qos=2)
                influx_client.subscribe(topic='dt/ssc/camera', callback=mqtt_callback_ssc_camera, qos=2)

                print('Influx MQTT subscribed to all DT topics')

                _start_workers()
                return influx_client
            else:
                raise Exception("Conexion rechazada o timeout")

        except Exception as e:
            retry_count += 1
            print('Error en intento {}: {}'.format(retry_count, e))
            if retry_count < max_retries:
                wait_time = 2 ** retry_count
                print('  Reintentando en {} segundos...'.format(wait_time))
                time.sleep(wait_time)
            else:
                print('No se pudo conectar a MQTT despues de varios intentos')
                return None


def thread_InfluxCollector():
    start_influx_collector()
    while not stop_event.is_set():
        time.sleep(1)
    stop_event.set()
    for t in _worker_threads:
        t.join(timeout=2)