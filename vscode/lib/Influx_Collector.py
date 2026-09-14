"""Cola, serialización y envío de métricas MQTT a InfluxDB."""

# lib/Influx_Collector.py
# El colector desacopla callbacks MQTT del envio HTTP a InfluxDB.
from datetime import datetime, timezone
import json
import threading
import time
import queue
import os

try:
    from urllib.request import Request, urlopen
    from urllib.error import HTTPError, URLError
    from urllib.parse import quote
except ImportError:
    from urllib2 import Request, urlopen, HTTPError, URLError

from fischertechnik.mqtt.MqttClient import MqttClient

INFLUX_BASE_URL = 'ChangeMe'  # Ej: https://eu-central-1-1.aws.cloud2.influxdata.com
INFLUX_ORG = 'ChangeMe' # Cambiar por el nombre de la organizacion en InfluxDB
INFLUX_BUCKET = 'ChangeMe' # Cambiar por el nombre del bucket en InfluxDB
INFLUX_TOKEN = 'ChangeMe' # Cambiar por el token de acceso a InfluxDB
INFLUX_URL = '{0}/api/v2/write?org={1}&bucket={2}&precision=ms'.format(INFLUX_BASE_URL, quote(INFLUX_ORG), quote(INFLUX_BUCKET))

MQTT_HOST = 'ChangeMe' # Cambiar por la IP o dominio del broker MQTT
MQTT_PORT = 1884 # Cambiar por el puerto del broker MQTT
MQTT_USER = 'ChangeMe' # Cambiar por el usuario del broker MQTT
MQTT_PASSWORD = 'ChangeMe' # Cambiar por la contraseña del broker MQTT

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
    """
    Devuelve el instante actual en milisegundos.

    Returns:
      El numero de milisegundos transcurridos desde 1970.
    """
    return int(time.time() * 1000)


def _to_ms(ts_value):
    """
    Convierte una fecha, venga como venga, a milisegundos desde 1970.

    Acepta un numero en segundos, un numero ya en milisegundos, o un texto
    con formato de fecha ISO. Si no consigue interpretarlo, usa la hora
    actual como respaldo.

    Args:
      ts_value: Fecha recibida, en segundos, milisegundos o texto ISO.

    Returns:
      El numero de milisegundos correspondiente.
    """
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
    """
    Prepara un valor para poder usarlo como etiqueta de InfluxDB.

    Escapa las barras invertidas, los espacios, las comas y los signos
    igual, que tienen un significado especial en ese formato.

    Args:
      value: Valor a convertir en etiqueta.

    Returns:
      El texto ya escapado.
    """
    return str(value).replace('\\', '\\\\').replace(' ', '\\ ').replace(',', '\\,').replace('=', '\\=')


def _escape_field_string(value):
    """
    Prepara un texto para poder usarlo como campo de InfluxDB.

    Lo envuelve en comillas y escapa las comillas y barras invertidas que
    pueda llevar dentro.

    Args:
      value: Valor a convertir en texto de campo.

    Returns:
      El texto ya entrecomillado y escapado.
    """
    return '"' + str(value).replace('\\', '\\\\').replace('"', '\\"') + '"'


def _field_value(value):
    """
    Convierte un valor de Python al formato de campo que espera InfluxDB.

    Los booleanos se escriben como true/false, los enteros llevan una 'i'
    detras, los decimales se dejan tal cual y cualquier otra cosa se
    escribe como texto entrecomillado.

    Args:
      value: Valor a convertir (booleano, entero, decimal o cualquier otro).

    Returns:
      El texto listo para insertar en la linea de InfluxDB.
    """
    if isinstance(value, bool):
        return 'true' if value else 'false'
    if isinstance(value, int) and not isinstance(value, bool):
        return str(value) + 'i'
    if isinstance(value, float):
        return str(value)
    return _escape_field_string(value)


def _format_line(measurement, tags, fields, ts_ms):
    """
    Construye una linea de texto en el formato que entiende InfluxDB.

    Junta el nombre de la medicion, sus etiquetas, sus campos con valor y
    la fecha, todo en una sola linea de texto.

    Args:
      measurement: Nombre de la medicion de InfluxDB.
      tags: Etiquetas de la medicion.
      fields: Campos y valores de la medicion.
      ts_ms: Fecha en milisegundos.

    Returns:
      La linea de texto lista para enviar, o ``None`` si no hay ningun
      campo con valor.
    """
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
    """
    Envia un bloque de lineas a InfluxDB por HTTP.

    Args:
      payload: Texto con una o varias lineas en formato InfluxDB.

    Returns:
      None. Si la peticion falla, la excepcion sube a quien haya llamado.
    """
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
    """
    Envia a InfluxDB las lineas acumuladas, reintentando si falla.

    Si el envio falla, espera un poco mas cada vez (hasta 5 intentos) antes
    de rendirse y descartar el lote.

    Args:
      lines: Lista de lineas de InfluxDB pendientes de enviar.

    Returns:
      True si el envio se confirmo, False si se agotaron los reintentos.
    """
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
    """
    Va sacando lineas de la cola y las envia a InfluxDB por lotes.

    Junta lineas hasta llegar a 100 o hasta que pase un segundo desde el
    ultimo envio, lo que ocurra antes, y entonces las manda todas juntas.

    Returns:
      None. Termina cuando se pide parar y la cola queda vacia.
    """
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
    """
    Vigila cada 5 segundos cuanto se ha llenado la cola de envio a InfluxDB.

    Avisa por consola si la cola supera el 80% de su capacidad, para
    detectar a tiempo que los datos se estan acumulando mas rapido de lo
    que se pueden enviar.

    Returns:
      None. Es un bucle que corre hasta que se pide parar.
    """
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
    """
    Punto reservado para recuperar datos guardados si el envio se corto.

    Por ahora no hace nada; esta aqui para poder añadir mas adelante la
    carga de lineas que se hubieran quedado sin enviar.

    Returns:
      None.
    """
    return


def _start_workers():
    """
    Lanza los hilos que envian datos a InfluxDB y vigilan la cola.

    Returns:
      None.
    """
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
    """
    Deja un dato listo en la cola para que se envie a InfluxDB.

    No lo envia directamente: lo mete en una cola para que los hilos de
    envio se encarguen, y si la cola esta llena, descarta el dato mas
    antiguo para no bloquear al que llama.

    Args:
      measurement: Nombre de la medicion de InfluxDB.
      tags: Etiquetas de la medicion.
      fields: Campos y valores de la medicion.
      ts_ms: Fecha en milisegundos.

    Returns:
      None.
    """
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
    """
    Interpreta el contenido de un mensaje MQTT como un diccionario JSON.

    Args:
      message: Mensaje MQTT recibido.

    Returns:
      El diccionario con los datos del mensaje, o ``None`` si no se pudo
      interpretar como JSON.
    """
    try:
        return json.loads(message.payload.decode('utf-8'))
    except Exception:
        return None


def _common_fields(payload, payload_text):
    """
    Prepara los campos que llevan todas las medidas: el texto y la fecha.

    Args:
      payload: Datos del mensaje MQTT ya interpretados como diccionario.
      payload_text: El mensaje MQTT tal cual, en texto.

    Returns:
      Un diccionario con el texto original y, si lo trae, la fecha del
      mensaje.
    """
    fields = {'payload': payload_text}
    if isinstance(payload, dict) and 'ts' in payload:
        fields['ts'] = payload.get('ts')
    return fields


def mqtt_callback_bme680(message):
    """
    Guarda en InfluxDB la lectura del sensor ambiental (BME680).

    Args:
      message: Mensaje MQTT con temperatura, humedad, presion y calidad
        del aire.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB la lectura del sensor de luz ambiente (LDR).

    Args:
      message: Mensaje MQTT con el brillo medido.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB una foto del inventario completo del almacen.

    Desmonta la lista de casillas del almacen en columnas separadas
    (ubicacion, UID, color y estado de cada una) para poder consultarlas
    facilmente despues.

    Args:
      message: Mensaje MQTT con el inventario del almacen.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB la posicion de los tres ejes del brazo VGR.

    Args:
      message: Mensaje MQTT con la rotacion, la altura y la extension del
        brazo.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB si la ventosa del VGR esta agarrando una pieza.

    Args:
      message: Mensaje MQTT con el estado de la ventosa.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB la posicion del brazo del almacen.

    Args:
      message: Mensaje MQTT con la posicion horizontal, vertical y de
        extension del brazo del almacen.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB el estado de la cinta del almacen.

    Args:
      message: Mensaje MQTT con la velocidad de la cinta, el sentido de
        giro y sus dos sensores de entrada y salida.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB el sensor de entrada de la DPS.

    Args:
      message: Mensaje MQTT con el estado del sensor de entrada.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB el sensor de salida de la DPS.

    Args:
      message: Mensaje MQTT con el estado del sensor de salida.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB el color de pieza detectado en la DPS.

    Args:
      message: Mensaje MQTT con el color leido.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB el dato NFC leido en la DPS.

    Args:
      message: Mensaje MQTT con el identificador y el dato del chip NFC.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB el estado de la cinta de clasificacion (SLD).

    Args:
      message: Mensaje MQTT con la velocidad de la cinta y sus sensores de
        entrada y de los cilindros expulsores.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB el estado de los cilindros expulsores de la SLD.

    Args:
      message: Mensaje MQTT con el color que se esta expulsando y si cada
        cilindro (blanco, rojo, azul) esta activo.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB el estado de la cinta de salida de la MPO.

    Args:
      message: Mensaje MQTT con si la cinta esta activa y el sensor del
        final de la cinta.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB el estado del horno de la MPO.

    Args:
      message: Mensaje MQTT con si la puerta esta abierta o cerrada, la
        luz del horno y el sensor de entrada del horno.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB el estado del brazo del horno de la MPO.

    Args:
      message: Mensaje MQTT con si el brazo esta bajado y si la ventosa
        esta haciendo vacio.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB el estado de la mesa giratoria y la sierra de la MPO.

    Args:
      message: Mensaje MQTT con si el cilindro expulsor esta activo y el
        estado del giro de la mesa y de la sierra.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB el estado de los pilotos de la estacion de la camara.

    Args:
      message: Mensaje MQTT con el estado del piloto de conexion y del
        semaforo de alarmas.

    Returns:
      None.
    """
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
    """
    Guarda en InfluxDB la posicion de pan y tilt de la camara.

    Args:
      message: Mensaje MQTT con el angulo de giro y de inclinacion de la
        camara.

    Returns:
      None.
    """
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
    """
    Abre su propia conexion MQTT y se suscribe a todos los temas de telemetria.

    Influx_Collector.py es un suscriptor MQTT independiente del resto de
    modulos del controlador: crea y conecta su propio cliente
    (``fischertechnik.mqtt.MqttClient``), reintentando hasta 10 veces si el
    broker aun no esta disponible. Esto mantiene un aislamiento total de
    responsabilidades respecto a Digital_Twin.py: ninguno de los dos modulos
    depende de la conexion del otro, y este colector podria ejecutarse en
    otro proceso o incluso otra maquina sin que Digital_Twin.py cambiara en
    absoluto. Una vez conectado, se suscribe a la telemetria ambiental, de
    luz, de inventario y de todas las estaciones fisicas, y arranca los
    hilos que envian los datos a InfluxDB.

    Returns:
      El cliente MQTT propio ya conectado, o ``None`` si no llego a estarlo.
    """
    global influx_client
    max_retries = 10
    retry_count = 0

    while retry_count < max_retries:
        try:
            client = MqttClient(client_id='influx-collector-' + str(int(time.time())))
            client.connect(host=MQTT_HOST, port=MQTT_PORT, user=MQTT_USER, password=MQTT_PASSWORD)
        except Exception:
            client = None

        if client is not None and client.is_connected():
            influx_client = client

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

            print('Influx Collector conectado con su propio cliente MQTT')

            _start_workers()
            return influx_client

        retry_count += 1
        print('[{}/{}] Reintentando conexion propia al broker MQTT...'.format(retry_count, max_retries))
        time.sleep(1)

    print('Influx Collector no logro conectarse al broker: no arranca')
    return None


def thread_InfluxCollector():
    """
    Punto de entrada para lanzar el colector de InfluxDB en un hilo aparte.

    Returns:
      None.
    """
    start_influx_collector()
    while not stop_event.is_set():
        time.sleep(1)
    stop_event.set()
    for t in _worker_threads:
        t.join(timeout=2)