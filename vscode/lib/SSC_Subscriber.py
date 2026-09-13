"""Recepción de comandos MQTT para la cámara PTU y el SSC."""

# Recibe comandos cloud para sensores, camara, PTU e iluminacion.
import json
import logging
import time
from datetime import datetime
from datetime import timezone
from fischertechnik.mqtt.Constants import CONTROLLER_ID
from fischertechnik.mqtt.FTCloudClient import FTCloudClient
from lib.Factory_Variables import *
from lib.SSC_PTU import *
from lib.Time import *

bme680_period = None
ldr_period = None
camera_fps = None
cmd = None
degree = None
keep_alive_timestamp = None
camera_on = None
msgk = None


def mqtt_callback(message):
  """
  Atiende una orden de la nube para cambiar cada cuanto se lee el ambiente.

  Args:
    message: Mensaje MQTT con el nuevo periodo de lectura del sensor
      ambiental (BME680).

  Returns:
    None.
  """
  global bme680_period, ldr_period, camera_fps, cmd, degree, keep_alive_timestamp, camera_on, msgk
  logging.log(logging.TRACE_FCL, '-')
  if (get_init_finished()) and not not len(message.payload.decode("utf-8")):
    msg = json.loads(message.payload.decode("utf-8"))
    bme680_period = msg['period']
    set_bme680_period(bme680_period)



def mqtt_callback2(message):
  """
  Atiende una orden de la nube para cambiar cada cuanto se lee la luz.

  Args:
    message: Mensaje MQTT con el nuevo periodo de lectura del sensor de
      luz (LDR).

  Returns:
    None.
  """
  global bme680_period, ldr_period, camera_fps, cmd, degree, keep_alive_timestamp, camera_on, msgk
  logging.log(logging.TRACE_FCL, '-')
  if (get_init_finished()) and not not len(message.payload.decode("utf-8")):
    msg= json.loads(message.payload.decode("utf-8"))
    ldr_period = msg['period']
    set_ldr_period(ldr_period)



def mqtt_callback3(message):
  """
  Atiende una orden de la nube para configurar la camara.

  Args:
    message: Mensaje MQTT con los fotogramas por segundo que debe capturar
      la camara y si debe estar encendida o apagada.

  Returns:
    None.
  """
  global bme680_period, ldr_period, camera_fps, cmd, degree, keep_alive_timestamp, camera_on, msgk
  logging.log(logging.TRACE_FCL, '-')
  if (get_init_finished()) and not not len(message.payload.decode("utf-8")):
    msg= json.loads(message.payload.decode("utf-8"))
    camera_fps = msg['fps']
    camera_on = msg['on']
    set_camera_fps(camera_fps)
    set_camera_on(camera_on)



def mqtt_callback4(message):
  """
  Atiende una orden de la nube para mover la camara.

  Args:
    message: Mensaje MQTT con el comando de movimiento y, si aplica, los
      grados a mover.

  Returns:
    None.
  """
  global bme680_period, ldr_period, camera_fps, cmd, degree, keep_alive_timestamp, camera_on, msgk
  logging.log(logging.TRACE_FCL, '-')
  if (get_init_finished()) and not not len(message.payload.decode("utf-8")):
    msg= json.loads(message.payload.decode("utf-8"))
    cmd = msg['cmd']
    if 'degree' in msg:
        degree = msg['degree']
    processCmd(cmd, degree)



def mqtt_callback5(message):
  """
  Atiende los avisos de "sigo aqui" de la nube por MQTT.

  Marca que la nube esta activa y, si el mensaje es un aviso de
  'keep-alive', actualiza cuando fue el ultimo, para saber que la
  conexion sigue viva.

  Args:
    message: Mensaje MQTT con la fecha y el tipo de aviso.

  Returns:
    None.
  """
  global bme680_period, ldr_period, camera_fps, cmd, degree, keep_alive_timestamp, camera_on, msgk
  logging.log(logging.TRACE_FCL, '-')
  set_cloud_active(True)
  if keep_alive_timestamp == None:
    logging.log(logging.DEBUG_FCL, 'set keep alive value')
    set_keep_alive(time.time())
  if (get_init_finished()) and not not len(message.payload.decode("utf-8")):
    msg= json.loads(message.payload.decode("utf-8"))
    last_msg = msg["ts"]
    msgk = msg["message"]
    if msgk == 'keep-alive':
      set_keep_alive(to_datetime_utc(last_msg).timestamp())



FTCloudClient.getInstance().subscribe("/j1/txt/" + str(CONTROLLER_ID) + "/c/bme680", mqtt_callback)
FTCloudClient.getInstance().subscribe("/j1/txt/" + str(CONTROLLER_ID) + "/c/ldr", mqtt_callback2)
FTCloudClient.getInstance().subscribe("/j1/txt/" + str(CONTROLLER_ID) + "/c/cam", mqtt_callback3)
FTCloudClient.getInstance().subscribe("/j1/txt/" + str(CONTROLLER_ID) + "/o/ptu", mqtt_callback4)
FTCloudClient.getInstance().subscribe("/j1/txt/" + str(CONTROLLER_ID) + "/o/broadcast", mqtt_callback5)



