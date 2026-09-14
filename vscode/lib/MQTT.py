"""Conexiones MQTT locales y cloud y sus hilos de servicio."""

import logging
import threading
import time
from fischertechnik.mqtt.Constants import CONTROLLER_ID
from fischertechnik.mqtt.FTCloudClient import FTCloudClient
from fischertechnik.mqtt.MqttClient import MqttClient
from lib.SSC_Subscriber import mqtt_callback3
from lib.display import *
from lib.Factory import *
from lib.Factory_Variables import *
from lib.HBW_MQTT import *
from lib.HBW_Storage import *
from lib.MQTT_Subscriber import *
from lib.Nfc_MQTT import *
from lib.SSC_Publisher import *
# Callbacks específicos para telemetría ambiental, luminosidad y cámara.
from lib.SSC_Subscriber import (
  mqtt_callback as mqtt_callback_bme680,
  mqtt_callback2 as mqtt_callback_ldr,
  mqtt_callback3 as mqtt_callback_cam,
)
from lib.Time import *

_tr0 = None
_tr = None
_dg = None
state = None
type2 = None
client_local = None
is_cloud_connected = None
ts_publishStorage = None
last_movement_alarm = None
tsdiff = None
payload_order = None
last_temperature_alarm = None
last_humidity_alarm = None
ts = None
_type = None

def connectLocal():
  """
  Conecta la fabrica con el broker MQTT local del PC de la sala.

  Reintenta hasta 10 veces, y si consigue conectarse avisa por difusion y
  se suscribe a las ordenes locales criticas (reset, ACK, aparcado,
  pedidos, NFC, camara) y a la telemetria ambiental y de luz.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, state, type2, client_local, is_cloud_connected, ts_publishStorage, last_movement_alarm, tsdiff, payload_order, last_temperature_alarm, last_humidity_alarm, ts, _type
  logging.log(logging.TRACE_FCL, '-')
  # El cliente local recibe órdenes de operación y publica estados de planta.
  client_local = MqttClient(client_id='factory-main-txt-') # Cliente local
  set_client_local(client_local)
  for count in range(10): # Reintenta la conexión hasta 10 veces
    logging.log(logging.DEBUG_FCL, 'connecting local ...')

    # TODO:
    client_local.connect(
      host='ChangeMe', # Cambiar por la IP o dominio del broker MQTT 
      port=1884, # Cambiar por el puerto del broker MQTT
      user='ChangeMe', # Cambiar por el usuario del broker MQTT
      password='ChangeMe' # Cambiar por la contraseña del broker MQTT
      ) # Conexión al broker del PC
    
    # client_local.connect(host='localhost', port=2883, user='', password='') # Conexión al broker interno (Mosquitto)
    if client_local.is_connected():
      publish_broadcast('init') # Anuncia el inicio al sistema local
      # Suscripción a comandos críticos (Reset, Ack, Parking, Pedidos, NFC, Cámara)
      client_local.subscribe(topic='o/broadcast', callback=mqtt_callback_broadcast, qos=2)
      client_local.subscribe(topic='gtyp_Setup/x_Clean_Rack_HBW', callback=mqtt_callback_hbw_reset, qos=2)
      client_local.subscribe(topic='gtyp_Setup/x_AcknowledgeButton', callback=mqtt_callback_ack, qos=2)
      client_local.subscribe(topic='gtyp_Setup/x_Park_Position', callback=mqtt_callback_park, qos=2)
      client_local.subscribe(topic='f/o/order', callback=mqtt_callback_order, qos=2)
      client_local.subscribe(topic='f/o/nfc/ds', callback=mqtt_callback_nfc, qos=2)
      client_local.subscribe(topic='o/ptu', callback=mqtt_callback_ptu, qos=2)

      # Suscripciones de telemetría ambiental, luminosidad y cámara.
      client_local.subscribe(topic='c/bme680', callback=mqtt_callback_bme680, qos=2)
      client_local.subscribe(topic='c/ldr', callback=mqtt_callback_ldr, qos=2)
      client_local.subscribe(topic='c/cam', callback=mqtt_callback_cam, qos=2)
      break
    time.sleep(1)


def initlog_FCL(_tr0, _tr, _dg):
  """
  Da de alta los niveles de registro propios de las conexiones MQTT.

  Crea las etiquetas TRACE0_FCL, TRACE_FCL y DEBUG_FCL para que los
  mensajes de la nube y del broker local se puedan filtrar aparte.

  Args:
    _tr0: Nivel numérico para el trazado mas detallado.
    _tr: Nivel numérico para el trazado normal.
    _dg: Nivel numérico para los mensajes de depuración.

  Returns:
    None.
  """
  global state, type2, client_local, is_cloud_connected, ts_publishStorage, last_movement_alarm, tsdiff, payload_order, last_temperature_alarm, last_humidity_alarm, ts, _type
  logging.TRACE0_FCL = _tr0
  logging.addLevelName(logging.TRACE0_FCL , 'TRACE0_FCL')
  logging.TRACE_FCL = _tr
  logging.addLevelName(logging.TRACE_FCL , 'TRACE_FCL')
  logging.DEBUG_FCL = _dg
  logging.addLevelName(logging.DEBUG_FCL, 'DEBUG_FCL')

def connectCloud():
  """
  Conecta la fabrica con la nube de fischertechnik.

  Reintenta hasta 10 veces, y si consigue conectarse manda el estado
  inicial (pedido en espera, posicion de la camara y datos NFC) y
  enciende el indicador de conexion a la nube en pantalla.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, state, type2, client_local, is_cloud_connected, ts_publishStorage, last_movement_alarm, tsdiff, payload_order, last_temperature_alarm, last_humidity_alarm, ts, _type
  logging.log(logging.TRACE_FCL, '-')
  # El broker cloud puede tardar en estar disponible, por eso se reintenta.
  is_cloud_connected = False
  for count2 in range(10):
    logging.log(logging.DEBUG_FCL, 'connecting cloud ...')
    FTCloudClient.getInstance().connect() # Conecta usando las credenciales del dispositivo
    if FTCloudClient.getInstance().is_connected():
      logging.log(logging.DEBUG_FCL, 'cloud connected')
      is_cloud_connected = True
      #Reset view on connect
      # Sincroniza el estado inicial con el Dashboard de la nube
      publish_state_order('WAITING_FOR_ORDER', 'NONE')
      publish_ptu_pos()
      publish_Nfc_Data()
      display.set_attr("txt_status_indicator_cloud_connected.active", str(True).lower())
      break
    time.sleep(1)

def thread_Local():
  """
  Mantiene viva la conexion con el broker local, en su propio hilo.

  Se conecta y, mientras siga conectado, publica el inventario del
  almacen cada 20 segundos para que nadie se quede con datos viejos.

  Returns:
    None. Cuando se pierde la conexion, apaga el indicador en pantalla y
    termina.
  """
  global _tr0, _tr, _dg, state, type2, client_local, is_cloud_connected, ts_publishStorage, last_movement_alarm, tsdiff, payload_order, last_temperature_alarm, last_humidity_alarm, ts, _type
  logging.log(logging.TRACE_FCL, '-')
  connectLocal()
  ts_publishStorage = time.time()
  # El inventario se publica periódicamente para mantener sincronizados los
  # consumidores MQTT aunque no llegue una orden nueva.
  while client_local.is_connected():
    if time.time() - ts_publishStorage >= 20: # Cada 20 segundos envía el inventario actualizado
      ts_publishStorage = time.time()
      publish_state_Storage(get_list_storage())
    time.sleep(1)
  display.set_attr("txt_status_indicator_local_connected.active", str(False).lower()) # Apaga LED si se pierde conexión

def thread_ftCloud():
  """
  Mantiene viva la conexion con la nube, en su propio hilo.

  Se conecta, lanza los hilos que publican temperatura/humedad/aire, luz
  y camara, y mientras siga conectado publica el inventario cada 20
  segundos; si pasan 5 minutos sin actividad, reduce la frecuencia de
  envio para ahorrar datos.

  Returns:
    None. Cuando se pierde la conexion, apaga el indicador en pantalla y
    termina.
  """
  global _tr0, _tr, _dg, state, type2, client_local, is_cloud_connected, ts_publishStorage, last_movement_alarm, tsdiff, payload_order, last_temperature_alarm, last_humidity_alarm, ts, _type
  logging.log(logging.TRACE_FCL, '-')
  connectCloud()
  # Cada fuente de telemetría tiene su propio ritmo y no bloquea este bucle.
  camera_image = []
  last_movement_alarm = 0
  last_temperature_alarm = 0
  last_humidity_alarm = 0
  publish_broadcast('init')
  threading.Thread(target=publish_bme680, daemon=True).start()  # Temperatura, humedad, aire
  threading.Thread(target=publish_ldr, daemon=True).start() # Intensidad de luz
  threading.Thread(target=publish_camera, daemon=True).start() # Streaming de imagen
  ts_publishStorage = time.time()
  while FTCloudClient.getInstance().is_connected():
    if (get_cloud_active()) == True:
      # Si pasan 5 minutos (300s) sin actividad, reduce la frecuencia de envío para ahorrar ancho de banda
      if time.time() - (get_keep_alive()) >= 300:
        decrease_frequency_MQTT()
        set_cloud_active(False)
      if time.time() - ts_publishStorage >= 20: # Publicación de stock cada 20s
        ts_publishStorage = time.time()
        publish_state_Storage(get_list_storage())
    time.sleep(1)
  display.set_attr("txt_status_indicator_cloud_connected.active", str(False).lower())

def init_config_MQTT():
  """
  Fija los valores de fabrica de la telemetria hacia la nube.

  Establece cada cuanto se lee la luz y el sensor ambiental, los
  fotogramas por segundo de la camara, apaga la camara si nadie mira, fija
  el tiempo de espera antes de disparar una alarma, y marca que la
  inicializacion ha terminado.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, state, type2, client_local, is_cloud_connected, ts_publishStorage, last_movement_alarm, tsdiff, payload_order, last_temperature_alarm, last_humidity_alarm, ts, _type
  logging.log(logging.TRACE_FCL, '-')
  set_ldr_period(60)  # Establece el tiempo cada cuanto se lee la LDR (segundos)
  set_bme680_period(60)  # Establece el tiempo cada cuanto se lee el BME680 (segundos)
  set_camera_fps(1)  # Establece los FPS de la cámara
  set_camera_on(False)  # Apaga cámara si no hay nadie mirando (Keep-alive vencido)
  set_alarm_timer(5)  # Establece el valor de tiempo que puede transcurrir sin que salte una alarma (segundos)
  set_init_finished(True)  # Establece el estado de la inicialización

def decrease_frequency_MQTT():
  """
  Reduce la frecuencia de la telemetria para ahorrar datos.

  Se llama cuando la nube lleva 5 minutos sin actividad: alarga el tiempo
  entre lecturas de luz y del sensor ambiental, baja los fotogramas de la
  camara a 1 por segundo y la apaga si nadie la esta mirando.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, state, type2, client_local, is_cloud_connected, ts_publishStorage, last_movement_alarm, tsdiff, payload_order, last_temperature_alarm, last_humidity_alarm, ts, _type
  logging.log(logging.TRACE_FCL, '-')
  set_ldr_period(60)  # Establece el tiempo cada cuanto se lee la LDR (segundos)
  set_bme680_period(60)  # Establece el tiempo cada cuanto se lee el BME680 (segundos)
  set_camera_fps(1)  # Establece los FPS de la cámara
  set_camera_on(False)  # Apaga cámara si no hay nadie mirando (Keep-alive vencido)

def mqtt_callback(message):
  """
  Recibe de la nube la confirmacion de un cambio de estado del pedido.

  Args:
    message: Mensaje MQTT con la marca de tiempo de la confirmacion.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, state, type2, client_local, is_cloud_connected, ts_publishStorage, last_movement_alarm, tsdiff, payload_order, last_temperature_alarm, last_humidity_alarm, ts, _type
  logging.log(logging.TRACE_FCL, '-')
  if (get_init_finished()) and not not len(message.payload.decode("utf-8")):
    msg= json.loads(message.payload.decode("utf-8"))
    ts = msg["ts"]  # Marca de tiempo del mensaje de confirmación
    print(ts)


def mqtt_callback2(message):
  """
  Atiende un pedido de pieza llegado desde la nube.

  Si el pedido tiene menos de 60 segundos de antiguedad y pide un color
  valido (blanco, rojo o azul), se lo pasa al almacen para que saque una
  pieza de ese color.

  Args:
    message: Mensaje MQTT con la fecha del pedido y el color pedido.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, state, type2, client_local, is_cloud_connected, ts_publishStorage, last_movement_alarm, tsdiff, payload_order, last_temperature_alarm, last_humidity_alarm, ts, _type
  logging.log(logging.TRACE_FCL, '-')
  if (get_init_finished()) and not not len(message.payload.decode("utf-8")):
    msg= json.loads(message.payload.decode("utf-8"))
    #print(message.payload.decode("utf-8"))
    tsstr = msg["ts"] # Marca de tiempo del pedido
    #print(tsstr)
    # Convierte la fecha recibida a timestamp de Python
    ts = datetime.strptime(tsstr[:-1], "%Y-%m-%dT%H:%M:%S.%f").replace(tzinfo=timezone.utc).timestamp()
    #print(ts)
    _type= msg["type"]  # Color de la pieza pedida
    tsdiff = time.time() - ts # Calcula hace cuánto se envió la orden
    if tsdiff < 60 and (_type == 'WHITE' or _type == 'RED' or _type == 'BLUE'):
      logging.log(logging.DEBUG_FCL, _type)
      if reqHBW_fetchWP([time.time(), '', _type, True]):
        pass
    else:
      print(tsstr, _type, tsdiff)


def publish_state_order(state, type2):
  """
  Envia a la nube en que fase esta el pedido actual.

  Args:
    state: Fase del pedido: 'WAITING_FOR_ORDER', 'ORDERED', 'IN_PROCESS'
      o 'SHIPPED'.
    type2: Color de la pieza del pedido.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, client_local, is_cloud_connected, ts_publishStorage, last_movement_alarm, tsdiff, payload_order, last_temperature_alarm, last_humidity_alarm, ts, _type
  logging.log(logging.TRACE_FCL, '-')
  if get_init_finished():
    #states: WAITING_FOR_ORDER, ORDERED, IN_PROCESS, SHIPPED
    # Construye el JSON manualmente con el ID del controlador y el estado del pedido
    payload_order = '{{"ts":"{}","state":"{}","type":"{}"}}'.format(timestamp_utcnow(), state, type2)
    logging.log(logging.DEBUG_FCL, payload_order)
    FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/f/i/order", payload_order)


FTCloudClient.getInstance().subscribe("/j1/txt/" + str(CONTROLLER_ID) + "/f/o/state/ack", mqtt_callback)
FTCloudClient.getInstance().subscribe("/j1/txt/" + str(CONTROLLER_ID) + "/f/o/order", mqtt_callback2)



