"""Publicación del estado de la fábrica al broker para el gemelo digital."""

# Este servicio publica el estado de la planta para que Unity pueda representarlo.
# Librerias de Python
import json
import threading
import time

# Librerías de la Learning Factory
from lib.Axes1Ref import *
from lib.Axes2Ref import *
from lib.Factory_Variables import get_client_local
from lib.Time import timestamp_utcnow
from lib.HBW import *
from lib.VGR import *
from lib.DPS import *
from lib.MPO import *
from lib.Nfc import *
from lib.SSC_Lights import *
from lib.SSC_PTU_Axes1Ref import *
from lib.HBW_Storage import get_list_storage
from lib.SSC_Publisher import TXT_SSC_M_I2C_1_environment_sensor, TXT_SSC_M_I3_photo_resistor

stop_event = threading.Event()


# Publicar mensajes en el broker establecido en MQTT.py
def publish_dt(topic, data, qos=0, retain=True):
    """Publica un valor del gemelo digital en el topic indicado.

    Args:
        topic: Topic MQTT donde se publica el estado.
        data: Diccionario con los valores del estado.
        qos: Nivel de calidad de servicio MQTT.
        retain: Indica si el broker debe conservar el último mensaje.

    Returns:
        None.
    """
    client = get_client_local()
    if client is not None and client.is_connected():
        payload = {
            "ts": timestamp_utcnow(),
            **data,
        }
        client.publish(topic=topic, payload=json.dumps(payload), qos=qos, retain=retain)


# Callback para rebotar instantáneamente el paquete dt/ping recibido de Unity
def on_ping_received(message):
    """Procesa un ping del gemelo digital y actualiza su conexión.

    Args:
        message: Mensaje MQTT recibido desde Unity.

    Returns:
        None.
    """
    client = get_client_local()
    if client is not None and client.is_connected():
        try:
            payload = message.payload.decode('utf-8') if hasattr(message, 'payload') else str(message)
            client.publish(topic="dt/pong", payload=payload, qos=0, retain=False)
        except Exception as e:
            print("Error en callback ping:", e)


def thread_DigitalTwin_fast():
    """Publica periódicamente variables rápidas del gemelo digital.

    Returns:
        None. El hilo permanece activo hasta recibir la señal de parada.
    """
    last_pos_vgr = None
    last_pos_hbw = None
    max_jumps_vgr = [200, 100, 100]
    max_jumps_hbw = [200, 150]
    last_pos_camera = None

    while not stop_event.is_set():
        update_live()

        # dt/vgr/pos
        pos_vgr = (
            get_live_abspos(1),
            get_live_abspos(2),
            get_live_abspos(3),
        )
        if last_pos_vgr is None:
            publish_vgr = True
        else:
            d0 = abs(pos_vgr[0] - last_pos_vgr[0])
            d1 = abs(pos_vgr[1] - last_pos_vgr[1])
            d2 = abs(pos_vgr[2] - last_pos_vgr[2])
            publish_vgr = d0 <= max_jumps_vgr[0] and d1 <= max_jumps_vgr[1] and d2 <= max_jumps_vgr[2]

        if publish_vgr and pos_vgr != last_pos_vgr:
            publish_dt("dt/vgr/pos", {
                "rotation": pos_vgr[0],
                "vertical": pos_vgr[1],
                "extend": pos_vgr[2],
            }, qos=0, retain=True)
            last_pos_vgr = pos_vgr

        # dt/hbw/pos
        pos_hbw = (
            get_live_abspos(4),
            get_live_abspos(5),
            int(TXT_HBW_E1_M3_motor.get_speed()),
        )
        if last_pos_hbw is None:
            publish_hbw = True
        else:
            d0 = abs(pos_hbw[0] - last_pos_hbw[0])
            d1 = abs(pos_hbw[1] - last_pos_hbw[1])
            publish_hbw = d0 <= max_jumps_hbw[0] and d1 <= max_jumps_hbw[1]

        if publish_hbw and pos_hbw != last_pos_hbw:
            publish_dt("dt/hbw/pos", {
                "horizontal": pos_hbw[0],
                "vertical": pos_hbw[1],
                "extend": pos_hbw[2],
            }, qos=0, retain=True)
            last_pos_hbw = pos_hbw

        # dt/ssc/camera
        current_ssc_camera = {
            "pan": get_live_abspos(6),
            "tilt": get_live_abspos(7),
        }

        if current_ssc_camera != last_pos_camera:
            publish_dt("dt/ssc/camera", current_ssc_camera, qos=0, retain=True)
            last_pos_camera = current_ssc_camera

        stop_event.wait(timeout=0.1)


def thread_DigitalTwin_slow():
    """Publica periódicamente variables lentas del gemelo digital.

    Returns:
        None. El hilo permanece activo hasta recibir la señal de parada.
    """
    last_grip_active = None
    last_hbw_belt_state = None
    last_state_dsi = None
    last_state_dso = None
    last_nfc = None
    last_oven_state = None
    last_arm_state = None
    last_turntable = None
    last_mpo_belt_state = None
    last_sld_belt_state = None
    last_sld_cylinder_state = None
    last_ssc_leds_state = None

    while not stop_event.is_set():
        move2ref_state = get_move2ref_state()

        # dt/vgr/grip
        grip_active = get_grip_active()
        if grip_active != last_grip_active:
            publish_dt("dt/vgr/grip", {
                "active": grip_active,
            }, qos=1, retain=True)
            last_grip_active = grip_active

        # dt/hbw/belt
        belt_hbw_speed_raw = int(TXT_HBW_E1_M1_motor.get_speed())
        belt_hbw_speed = abs(belt_hbw_speed_raw)
        belt_hbw_state = (
            belt_hbw_speed,
            bool(isTriggeredIn()),
            bool(isTriggeredOut()),
            "CW" if belt_hbw_speed_raw < 0 else "CCW" if belt_hbw_speed_raw > 0 else "STOP",
        )
        if belt_hbw_state != last_hbw_belt_state:
            publish_dt("dt/hbw/belt", {
                "belt_speed": belt_hbw_state[0],
                "isTriggeredIn": belt_hbw_state[1],
                "isTriggeredOut": belt_hbw_state[2],
                "rot_direction": belt_hbw_state[3],
            }, qos=1, retain=True)
            last_hbw_belt_state = belt_hbw_state

        # dt/dps/dsi
        current_state_dsi = 1 if is_dsi() else 0
        if current_state_dsi != last_state_dsi:
            publish_dt("dt/dps/dsi", {
                "dsi_sensor": bool(current_state_dsi),
            }, qos=1, retain=True)
            last_state_dsi = current_state_dsi

        # dt/dps/dso
        current_state_dso = 1 if is_dso() else 0
        if current_state_dso != last_state_dso:
            publish_dt("dt/dps/dso", {
                "dso_sensor": bool(current_state_dso),
            }, qos=1, retain=True)
            last_state_dso = current_state_dso

        # dt/dps/color
        current_color = get_color_event()
        if current_color is not None:
            publish_dt("dt/dps/color", {
                "color": current_color,
            }, qos=1, retain=True)
            clear_color_event()

        # dt/dps/nfc
        current_nfc = (
            get_nfc_data_uid(),
            get_nfc_data_state_str(),
        )
        if current_nfc != last_nfc and current_nfc[0] is not None and current_nfc[1] is not None:
            publish_dt("dt/dps/nfc", {
                "id": current_nfc[0],
                "nfc": current_nfc[1],
            }, qos=1, retain=True)
            last_nfc = current_nfc

        # dt/mpo/oven
        oven_state = get_oven_state()
        current_oven = (
            bool(isOvenTriggered()),
            oven_state["close_door"],
            oven_state["lights"],
            move2ref_state["move2Ref5"],
            move2ref_state["move2Ref6"],
            oven_state["open_door"],
        )

        if current_oven != last_oven_state:
            publish_dt("dt/mpo/oven", {
                "oven_sensor": current_oven[0],
                "close_door": current_oven[1],
                "lights": current_oven[2],
                "move2Ref5": current_oven[3],
                "move2Ref6": current_oven[4],
                "open_door": current_oven[5],
            }, qos=1, retain=True)
            last_oven_state = current_oven

        # dt/mpo/arm
        arm_state = get_arm_state()
        current_arm = (
            move2ref_state["move2Ref3"],
            move2ref_state["move2Ref4"],
            arm_state["lowering"],
            arm_state["vacuum"],
        )

        if current_arm != last_arm_state:
            publish_dt("dt/mpo/arm", {
                "move2Ref3": current_arm[0],
                "move2Ref4": current_arm[1],
                "lowering": current_arm[2],
                "vacuum": current_arm[3],
            }, qos=1, retain=True)
            last_arm_state = current_arm

        # dt/mpo/turntable
        current_turntable = {
            "eject": get_turntable_state()["eject"],
            "move2Ref7": move2ref_state["move2Ref7"],
            "move2Ref8": move2ref_state["move2Ref8"],
            "move2Ref9": move2ref_state["move2Ref9"],
            "move2Ref10": move2ref_state["move2Ref10"],
            "rotation": -1 if move2ref_state["move2Ref7"] or move2ref_state["move2Ref8"] else 1 if move2ref_state["move2Ref9"] or move2ref_state["move2Ref10"] else 0,
            "saw": int(TXT_MPOmi_E4_M2_motor.get_speed()),
        }

        if current_turntable != last_turntable:
            publish_dt("dt/mpo/turntable", current_turntable, qos=1, retain=True)
            last_turntable = current_turntable

        # dt/mpo/belt
        belt_mpo_active = TXT_MPOmi_E4_M3_motor.get_speed() != 0
        current_mpo_belt = {
            "active": belt_mpo_active,
            "exit_sensor": isEndConveyorBeltTriggered(),
        }

        if current_mpo_belt != last_mpo_belt_state:
            publish_dt("dt/mpo/belt", current_mpo_belt, qos=1, retain=True)
            last_mpo_belt_state = current_mpo_belt

        # dt/sld/belt
        belt_sld_speed_raw = int(TXT_SLD_E5_M1_encodermotor.get_speed())
        belt_sld_speed = abs(belt_sld_speed_raw)
        current_sld_belt = {
            "cylinder_sensor": bool(isEjectionTriggered()),
            "entry_sensor": bool(isColorSensorTriggered()),
            "speed": belt_sld_speed,
        }

        if current_sld_belt != last_sld_belt_state:
            publish_dt("dt/sld/belt", current_sld_belt, qos=1, retain=True)
            last_sld_belt_state = current_sld_belt

        # dt/sld/cylinder
        current_sld_cylinder = {
            "cyl_color": get_sld_cylinder_state()["cyl_color"],
            "active": get_sld_cylinder_state()["active"],
            "is_white": bool(isWhite()),
            "is_red": bool(isRed()),
            "is_blue": bool(isBlue()),
        }

        if current_sld_cylinder != last_sld_cylinder_state:
            publish_dt("dt/sld/cylinder", current_sld_cylinder, qos=1, retain=True)
            last_sld_cylinder_state = current_sld_cylinder

        # dt/ssc/leds
        current_ssc_leds = {
            "led_online": get_online_led_state(),
            "leds_semaphore": int(get_lights_mode()),
        }

        if current_ssc_leds != last_ssc_leds_state:
            publish_dt("dt/ssc/leds", current_ssc_leds, qos=1, retain=True)
            last_ssc_leds_state = current_ssc_leds

        stop_event.wait(timeout=0.2)


def publish_initial_dt_state():
    """Publica el estado inicial de estaciones y actuadores.

    Returns:
        None.
    """
    try:
        # VGR
        publish_dt("dt/vgr/pos", {
            "rotation": get_live_abspos(1),
            "vertical": get_live_abspos(2),
            "extend": get_live_abspos(3),
        }, qos=1, retain=True)
        publish_dt("dt/vgr/grip", {"active": get_grip_active()}, qos=1, retain=True)

        # HBW
        publish_dt("dt/hbw/pos", {
            "horizontal": get_live_abspos(4),
            "vertical": get_live_abspos(5),
            "extend": int(TXT_HBW_E1_M3_motor.get_speed()),
        }, qos=1, retain=True)
        publish_dt("dt/hbw/belt", {
            "belt_speed": abs(int(TXT_HBW_E1_M1_motor.get_speed())),
            "isTriggeredIn": bool(isTriggeredIn()),
            "isTriggeredOut": bool(isTriggeredOut()),
            "rot_direction": "CW" if int(TXT_HBW_E1_M1_motor.get_speed()) < 0 else "CCW" if int(TXT_HBW_E1_M1_motor.get_speed()) > 0 else "STOP",
        }, qos=1, retain=True)

        # DPS
        publish_dt("dt/dps/dsi", {"dsi_sensor": bool(is_dsi())}, qos=1, retain=True)
        publish_dt("dt/dps/dso", {"dso_sensor": bool(is_dso())}, qos=1, retain=True)
        publish_dt("dt/dps/color", {"color": get_color_event() or "WHITE"}, qos=1, retain=True)
        uid = get_nfc_data_uid()
        state = get_nfc_data_state_str()
        if uid is not None and state is not None:
            publish_dt("dt/dps/nfc", {"id": uid, "nfc": state}, qos=1, retain=True)

        # SLD
        publish_dt("dt/sld/belt", {
            "cylinder_sensor": bool(isEjectionTriggered()),
            "entry_sensor": bool(isColorSensorTriggered()),
            "speed": int(TXT_SLD_E5_M1_encodermotor.get_speed()),
        }, qos=1, retain=True)
        sld = get_sld_cylinder_state()
        publish_dt("dt/sld/cylinder", {
            "cyl_color": sld.get("cyl_color"),
            "active": sld.get("active"),
            "is_white": bool(isWhite()),
            "is_red": bool(isRed()),
            "is_blue": bool(isBlue()),
        }, qos=1, retain=True)

        # MPO
        publish_dt("dt/mpo/belt", {
            "active": TXT_MPOmi_E4_M3_motor.get_speed() != 0,
            "exit_sensor": isEndConveyorBeltTriggered(),
        }, qos=1, retain=True)
        publish_dt("dt/mpo/oven", get_oven_state(), qos=1, retain=True)
        publish_dt("dt/mpo/arm", get_arm_state(), qos=1, retain=True)
        publish_dt("dt/mpo/turntable", get_turntable_state(), qos=1, retain=True)

        # SSC
        publish_dt("dt/ssc/leds", {
            "led_online": get_online_led_state(),
            "leds_semaphore": int(get_lights_mode()),
        }, qos=1, retain=True)
        publish_dt("dt/ssc/camera", {
            "pan": get_live_abspos(6),
            "tilt": get_live_abspos(7),
        }, qos=1, retain=True)

        # Sensors / inventory (no retain, qos=0)
        try:
            stock = get_list_storage()
            publish_dt("f/i/stock", {"stockItems": stock}, qos=0, retain=False)
        except Exception:
            pass

        try:
            temp = TXT_SSC_M_I2C_1_environment_sensor.get_temperature() - 4
            hum = TXT_SSC_M_I2C_1_environment_sensor.get_humidity()
            pres = TXT_SSC_M_I2C_1_environment_sensor.get_pressure()
            iaq = TXT_SSC_M_I2C_1_environment_sensor.get_indoor_air_quality_as_number()
            acc = TXT_SSC_M_I2C_1_environment_sensor.get_accuracy()
            publish_dt("i/bme680", {
                "t": float(round(temp, 1)),
                "rt": 0,
                "h": float(round(hum, 1)),
                "rh": 0,
                "p": float(round(pres, 1)),
                "iaq": int(iaq),
                "aq": int(acc),
                "gr": 0,
            }, qos=0, retain=False)
        except Exception:
            pass

        try:
            res = TXT_SSC_M_I3_photo_resistor.get_resistance()
            br = round((65000 - res) / 650, 1)
            publish_dt("i/ldr", {
                "br": float(br),
                "ldr": int(res),
            }, qos=0, retain=False)
        except Exception:
            pass

    except Exception:
        pass


# Publicar el estado de conexión de la fábrica
def publish_factory_connection_status():
    """Publica el estado de conexión de la fábrica al gemelo digital.

    Returns:
        None.
    """
    publish_dt("dt/factory", {
        "connected": True,
    }, qos=1, retain=True)


def thread_DigitalTwin_connection():
    """Mantiene la publicación periódica de conexión MQTT.

    Returns:
        None. El hilo permanece activo hasta recibir la señal de parada.
    """
    while not stop_event.is_set():
        publish_factory_connection_status()
        stop_event.wait(timeout=2.0)


def thread_DigitalTwin():
    """Arranca los hilos de conexión y publicación del gemelo digital.

    Returns:
        None. El servicio permanece activo durante la ejecución de la fábrica.
    """
    while not get_axes_ready():
        time.sleep(0.1)

    # Suscribirse al tópico dt/ping para responder a los pings de Unity
    client = get_client_local()
    if client is not None and client.is_connected():
        try:
            client.subscribe(topic="dt/ping", callback=on_ping_received, qos=0)
            print("Escuchando dt/ping para responder pings...")
        except Exception as e:
            print("Error suscribiendo a dt/ping:", e)

    publish_initial_dt_state()
    threading.Thread(target=thread_DigitalTwin_connection, daemon=True).start()
    threading.Thread(target=thread_DigitalTwin_fast, daemon=True).start()
    threading.Thread(target=thread_DigitalTwin_slow, daemon=True).start()

    while not stop_event.is_set():
        stop_event.wait(timeout=1.0)