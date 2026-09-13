"""Callbacks MQTT para órdenes, reinicios y peticiones de la fábrica."""

# Punto de entrada de las ordenes recibidas desde MQTT local.
import logging
import time
from fischertechnik.mqtt.MqttClient import MqttClient
from lib.display import *
from lib.Factory import *
from lib.Factory_Variables import *
from lib.HBW import *
from lib.HBW_Storage import *
from lib.Sound import *
from lib.SSC_PTU import *
from lib.SSC_Publisher import *

res = None
keep_alive_timestamp = None
cmd = None
degree = None
color = None
msgk = None


def mqtt_callback_broadcast(message):
  """
  Atiende los avisos de "sigo aqui" del sistema local por MQTT.

  Marca que la nube esta activa y, si el mensaje es un aviso de
  'keep-alive', actualiza cuando fue el ultimo y responde con otro para
  mantener la conexion viva; enciende el indicador de conexion local.

  Args:
    message: Mensaje MQTT con la fecha y el tipo de aviso.

  Returns:
    None.
  """
  global res, keep_alive_timestamp, cmd, degree, color, msgk
  logging.log(logging.TRACE_FCL, '-')
  set_cloud_active(True)
  if keep_alive_timestamp == None:
    logging.log(logging.DEBUG_FCL, 'set keep alive value')
    set_keep_alive(time.time())
  if (get_init_finished()) and not not len(message.payload.decode("utf-8")):
    msg= json.loads(message.payload.decode("utf-8"))
    print(msg)
    last_msg = msg["ts"]
    msgk = msg["message"]
    if msgk == 'keep-alive':
      set_keep_alive(to_datetime_utc(last_msg).timestamp())
      publish_broadcast('keep-alive')
    display.set_attr("txt_status_indicator_local_connected.active", str(True).lower())



def mqtt_callback_order(message):
  """
  Atiende un pedido de pieza llegado por MQTT desde la red local.

  Pide al almacen que saque una pieza del color indicado en el mensaje.

  Args:
    message: Mensaje MQTT con el color pedido.

  Returns:
    None.
  """
  global res, keep_alive_timestamp, cmd, degree, color, msgk
  logging.log(logging.TRACE_FCL, '-')
  msg = json.loads(message.payload.decode("utf-8"))
  color = msg["type"]
  print(color)
  if reqHBW_fetchWP([time.time(), '', color, True]):
    pass



def mqtt_callback_hbw_reset(message):
  """
  Vacia el inventario del almacen al recibir la orden por MQTT.

  Borra lo que hay guardado en el almacen, actualiza el contador de
  piezas en pantalla y hace sonar un pitido de confirmacion.

  Args:
    message: Mensaje MQTT que dispara el reinicio (sin usar).

  Returns:
    None.
  """
  global res, keep_alive_timestamp, cmd, degree, color, msgk
  logging.log(logging.TRACE_FCL, '-')
  resetStorage()
  updateStorageState()
  beep()



def mqtt_callback_park(message):
  """
  Manda aparcar toda la fabrica al recibir la orden por MQTT.

  Args:
    message: Mensaje MQTT que dispara el aparcado (sin usar).

  Returns:
    None.
  """
  global res, keep_alive_timestamp, cmd, degree, color, msgk
  logging.log(logging.TRACE_FCL, '-')
  parkFactory()



def mqtt_callback_ack(message):
  """
  Confirma la alarma al recibir la orden de ACK por MQTT.

  Borra el error activo de la fabrica, igual que si se hubiera pulsado el
  boton ACK en la pantalla.

  Args:
    message: Mensaje MQTT que dispara la confirmacion (sin usar).

  Returns:
    None.
  """
  global res, keep_alive_timestamp, cmd, degree, color, msgk
  logging.log(logging.TRACE_FCL, '-')
  set_factory_error_state(None)



def mqtt_callback_nfc(message):
  """
  Atiende una orden de leer o borrar NFC llegada por MQTT.

  Args:
    message: Mensaje MQTT con la orden ('read', 'read_uid' o 'delete').

  Returns:
    None.
  """
  global res, keep_alive_timestamp, cmd, degree, color, msgk
  logging.log(logging.TRACE_FCL, '-')
  msg = json.loads(message.payload.decode("utf-8"))
  cmd = msg["cmd"]
  print(cmd)
  res = reqVGR_Nfc(cmd)



def mqtt_callback_ptu(message):
  """
  Atiende una orden de mover la camara llegada por MQTT.

  Args:
    message: Mensaje MQTT con el comando de movimiento y, si aplica, los
      grados a mover.

  Returns:
    None.
  """
  global res, keep_alive_timestamp, cmd, degree, color, msgk
  logging.log(logging.TRACE_FCL, '-')
  if not not len(message.payload.decode("utf-8")):
    msg= json.loads(message.payload.decode("utf-8"))
    cmd = msg['cmd']
    if 'degree' in msg:
        degree = msg['degree']
    processCmd(cmd, degree)



