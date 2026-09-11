import base64
import cv2
# import fischertechnik.utility.math as ft_math
import json
import logging
import math
import os
import time
from datetime import datetime
from fischertechnik.mqtt.Constants import CONTROLLER_ID
from fischertechnik.mqtt.FTCloudClient import FTCloudClient
from fischertechnik.mqtt.MqttClient import MqttClient
from lib.Axes1Ref import *
from lib.camera import *
from lib.controller import *
from lib.display import *
from lib.Factory_Variables import *
from lib.SSC_PTU_Axes1Ref import *
from lib.Time import *

message = None
camera_image = None
payload_bme680 = None
payload_ldr = None
payload_broadcast = None
pos_pan = None
last_humidity_alarm = None
last_movement_alarm = None
last_temperature_alarm = None
pos_tilt = None
payload_cam = None
controller_name = None
limit_pan = None
limit_tilt = None
rel_pan = None
rel_tilt = None

# Publicar los datos del sensor BME680
def publish_bme680():
  global message, camera_image, payload_bme680, payload_ldr, payload_broadcast, pos_pan, last_humidity_alarm, last_movement_alarm, last_temperature_alarm, pos_tilt, payload_cam, controller_name, limit_pan, limit_tilt, rel_pan, rel_tilt
  logging.log(logging.TRACE_FCL, '-')
  while True:
    # -4°C correction for the temperature sensor of the BME680, because it is located near the CPU and heats up
    # Formato payload_bme680: {"ts":"2024-06-01T12:00:00Z","t":20.5,"rt":0,"h":50.0,"rh":0,"p":1013.25,"iaq":25,"aq":3,"gr":0}
    payload_bme680 = '{{"ts":"{}","t":{:.1f},"rt":{:.1f},"h":{:.1f},"rh":{:.1f},"p":{:.1f},"iaq":{},"aq":{},"gr":{}}}'.format(timestamp_utcnow(), (TXT_SSC_M_I2C_1_environment_sensor.get_temperature()) - 4, 0, TXT_SSC_M_I2C_1_environment_sensor.get_humidity(), 0, TXT_SSC_M_I2C_1_environment_sensor.get_pressure(), TXT_SSC_M_I2C_1_environment_sensor.get_indoor_air_quality_as_number(), TXT_SSC_M_I2C_1_environment_sensor.get_accuracy(), 0)
    # Envío a la nube si está activa
    if get_cloud_active():
      FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/i/bme680", payload_bme680)
    # Envío local si el cliente local está conectado
    if (get_client_local()) != None:
      get_client_local().publish(topic='i/bme680', payload=payload_bme680, qos=0, retain=True)
    # Esperar el tiempo definido antes de la siguiente lectura
    time.sleep((get_bme680_period()))

# Publicar los datos del sensor LDR
def publish_ldr():
  global message, camera_image, payload_bme680, payload_ldr, payload_broadcast, pos_pan, last_humidity_alarm, last_movement_alarm, last_temperature_alarm, pos_tilt, payload_cam, controller_name, limit_pan, limit_tilt, rel_pan, rel_tilt
  logging.log(logging.TRACE_FCL, '-')
  while True:
    # Formato payload_ldr: {"ts":"2024-06-01T12:00:00Z","br":75.0,"ldr":500}
    # El br (brightness) se calcula como un porcentaje de la resistencia del fotorresistor, donde 0% es 65000 ohmios (oscuridad total) y 100% es 0 ohmios (luz total).
    payload_ldr = '{{"ts":"{}", "br":{:.1f}, "ldr":{}}}'.format(timestamp_utcnow(), round((65000 - TXT_SSC_M_I3_photo_resistor.get_resistance()) / 650, 1), TXT_SSC_M_I3_photo_resistor.get_resistance())
    # Envío a la nube si está activa
    if get_cloud_active():
      FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/i/ldr", payload_ldr)
    # Envío local si el cliente local está conectado
    if (get_client_local()) != None:
      get_client_local().publish(topic='i/ldr', payload=payload_ldr, qos=0, retain=True)
    # Esperar el tiempo definido antes de la siguiente lectura
    time.sleep((get_ldr_period()))

# Captura imágenes de la cámara, las convierte a Base64 y las publica
def publish_camera():
  global message, camera_image, payload_bme680, payload_ldr, payload_broadcast, pos_pan, last_humidity_alarm, last_movement_alarm, last_temperature_alarm, pos_tilt, payload_cam, controller_name, limit_pan, limit_tilt, rel_pan, rel_tilt
  global online_led_state
  logging.log(logging.TRACE_FCL, '-')
  while True:
    if not not len(camera_image): # Convertir a booleano si la cámara ha capturado una imagen
      # Si la cámara está activa
      if get_camera_on():
        # Formato del payload_cam: {"ts":"2024-06-01T12:00:00Z","data":"texto en Base64 de la imagen capturada"}
        payload_cam = '{{"ts":"{}","data":"{}"}}'.format(timestamp_utcnow(), frame_to_base64(camera_image))
        # Envío a la nube si está activa
        if get_cloud_active():
          FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/i/cam", payload_cam)
        # Envío local si el cliente local está conectado
        if (get_client_local()) != None:
          get_client_local().publish(topic='i/cam', payload=payload_cam, qos=0, retain=False)
        # Parpadeo del LED rojo de la cámara para indicar que se ha capturado una imagen y se ha publicado
        # TXT_SSC_M_O5_led.set_brightness(int(512))
        # TXT_SSC_M_O5_led.set_brightness(int(0))
        if online_led_state == 0: # TODO: añadido por mí, las dos líneas anteriores son las originales, el else también es añadido por mí
          _set_online_led(512)
        else:
          _set_online_led(0)
        # Espera los fotogramas por segundo definidos antes de capturar la siguiente imagen
        time.sleep((1 / (get_camera_fps())))

# Supervisa la humedad y dispara una alarma si supera el 80%
def callback(event):
  global message, camera_image, payload_bme680, payload_ldr, payload_broadcast, pos_pan, last_humidity_alarm, last_movement_alarm, last_temperature_alarm, pos_tilt, payload_cam, controller_name, limit_pan, limit_tilt, rel_pan, rel_tilt
  if TXT_SSC_M_I2C_1_environment_sensor.get_humidity() > 80:
    logging.log(logging.TRACE0_FCL, '-')
    # La alarma se envía con el período especificado por (get_alarm_timer()), para evitar que se envíe constantemente
    if time.time() - last_humidity_alarm >= (get_alarm_timer()):
      publish_humidity_alarm()
      last_humidity_alarm = time.time()


# Actualiza la imagen cada vez que la cámara detecta un cambio
def image_callback(event):
  global message, camera_image, payload_bme680, payload_ldr, payload_broadcast, pos_pan, last_humidity_alarm, last_movement_alarm, last_temperature_alarm, pos_tilt, payload_cam, controller_name, limit_pan, limit_tilt, rel_pan, rel_tilt
  camera_image = event.value

# Función que se ejecuta cada vez que se detecta movimiento frente a la cámara
def motion_callback(event):
  global message, camera_image, payload_bme680, payload_ldr, payload_broadcast, pos_pan, last_humidity_alarm, last_movement_alarm, last_temperature_alarm, pos_tilt, payload_cam, controller_name, limit_pan, limit_tilt, rel_pan, rel_tilt
  logging.log(logging.TRACE0_FCL, '-')
  # FIXME: Nunca se entra en este if, porque esta variable no se modifica en ninguna parte del código
  if last_movement_alarm != None:
    # La alarma se envía con el período especificado por (get_alarm_timer()), para evitar que se envíe constantemente
    if time.time() - last_movement_alarm >= (get_alarm_timer()):
      publish_movement_alarm()
      last_movement_alarm = time.time()

# Supervisa la temperatura y dispara una alarma si baja de 4°C
def callback2(event):
  global message, camera_image, payload_bme680, payload_ldr, payload_broadcast, pos_pan, last_humidity_alarm, last_movement_alarm, last_temperature_alarm, pos_tilt, payload_cam, controller_name, limit_pan, limit_tilt, rel_pan, rel_tilt
  if TXT_SSC_M_I2C_1_environment_sensor.get_temperature() < 4:
    logging.log(logging.TRACE0_FCL, '-')
    # La alarma se envía con el período especificado por (get_alarm_timer()), para evitar que se envíe constantemente
    if time.time() - last_temperature_alarm >= (get_alarm_timer()):
      publish_temperature_alarm()
      last_temperature_alarm = time.time()


# Publica la alarma de MOVIMIENTO con el código 100, en el topic i/alert
def publish_movement_alarm():
  global message, camera_image, payload_bme680, payload_ldr, payload_broadcast, pos_pan, last_humidity_alarm, last_movement_alarm, last_temperature_alarm, pos_tilt, payload_cam, controller_name, limit_pan, limit_tilt, rel_pan, rel_tilt
  logging.log(logging.TRACE_FCL, '-')
  FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/i/alert", '{{"ts":"{}","id":"{}","data":"{}","code":{}}}'.format(timestamp_utcnow(), 'cam', frame_to_base64(camera_image), '100'))

# Publica la alarma de TEMPERATURA con el código 200, en el topic i/alert
def publish_temperature_alarm():
  global message, camera_image, payload_bme680, payload_ldr, payload_broadcast, pos_pan, last_humidity_alarm, last_movement_alarm, last_temperature_alarm, pos_tilt, payload_cam, controller_name, limit_pan, limit_tilt, rel_pan, rel_tilt
  logging.log(logging.TRACE_FCL, '-')
  FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/i/alert", '{{"ts":"{}","id":"{}","data":"{}","code":{}}}'.format(timestamp_utcnow(), 'bme680/t', TXT_SSC_M_I2C_1_environment_sensor.get_temperature(), '200'))

# Publica la alarma de HUMEDAD con el código 300, en el topic i/alert
def publish_humidity_alarm():
  global message, camera_image, payload_bme680, payload_ldr, payload_broadcast, pos_pan, last_humidity_alarm, last_movement_alarm, last_temperature_alarm, pos_tilt, payload_cam, controller_name, limit_pan, limit_tilt, rel_pan, rel_tilt
  logging.log(logging.TRACE_FCL, '-')
  FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/i/alert", '{{"ts":"{}","id":"{}","data":"{}","code":{}}}'.format(timestamp_utcnow(), 'bme680/h', TXT_SSC_M_I2C_1_environment_sensor.get_humidity(), '300'))

# Publica información general sobre el estado del hardware y software de la fábrica
def publish_broadcast(message):
  global camera_image, payload_bme680, payload_ldr, payload_broadcast, pos_pan, last_humidity_alarm, last_movement_alarm, last_temperature_alarm, pos_tilt, payload_cam, controller_name, limit_pan, limit_tilt, rel_pan, rel_tilt
  logging.log(logging.TRACE_FCL, '-')
  controller_name = os.uname()[1] # El nombre del controlador se obtiene del sistema operativo, por ejemplo "TXT-12345678"
  # Formato del mensaje broadcast: {"ts":"2024-06-01T12:00:00Z","hardwareId":"TXT-12345678","hardwareModel":"TXT 4.0","softwareName":"FactoryMain","softwareVersion":"1.0.0","message":"Texto del mensaje"}
  payload_broadcast = '{{"ts":"{}","hardwareId":"{}","hardwareModel":"{}","softwareName":"{}","softwareVersion":"{}","message":"{}"}}'.format(timestamp_utcnow(), controller_name, 'TXT 4.0', 'FactoryMain', display.get_attr("txt_label_version.text"), message)
  # Lo publica en el topic i/broadcast
  if get_cloud_active():
    FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/i/broadcast", payload_broadcast)
  if (get_client_local()) != None:
    get_client_local().publish(topic='i/broadcast', payload=payload_broadcast, qos=2, retain=False)

###########################################################################################
# TODO: Esta función sustituye a la función ft_math.map() que estaba comentada al principio
# del archivo ya que no se puede importar desde la librería fischertechnik.utility.math,
# porque no está incluida en la nueva versión del firmware 3.1.11
def map_value(x, in_min, in_max, out_min, out_max):
  if in_max == in_min:
    return out_min
  return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min
###########################################################################################

# Envía la posición actual de la cámara (Pan/Tilt) normalizada entre -1.0 y 1.0, donde 0.0 es la posición central
def publish_ptu_pos():
  global message, camera_image, payload_bme680, payload_ldr, payload_broadcast, pos_pan, last_humidity_alarm, last_movement_alarm, last_temperature_alarm, pos_tilt, payload_cam, controller_name, limit_pan, limit_tilt, rel_pan, rel_tilt
  logging.log(logging.TRACE_FCL, '-')
  if get_cloud_active():
    pos_pan = get_abspos_SSC_pan()  # Se obtiene la posición absoluta del Pan desde el SSC
    pos_tilt = get_abspos_SSC_tilt()  # Se obtiene la posición absoluta del Tilt desde el SSC
    if pos_pan != None and pos_tilt != None:
      limit_pan = (get_ABSLIMIT())[5] # Se obtiene el límite máximo del Pan desde el SSC
      limit_tilt = (get_ABSLIMIT())[6]  # Se obtiene el límite máximo del Tilt desde el SSC
      # La posición se normaliza a un rango de -1.0 a 1.0, donde 0.0 es la posición central, utilizando una función de mapeo que convierte el rango de 0 a limit_pan (o limit_tilt) en un rango de -1 a 1.
      # TODO:###################################################################
      # rel_pan = (ft_math.map(pos_pan, 0, limit_pan, 0, 200) - 100) / 100
      # rel_tilt = (ft_math.map(pos_tilt, 0, limit_tilt, 0, 200) - 100) / 100
      rel_pan = (map_value(pos_pan, 0, limit_pan, 0, 200) - 100) / 100
      rel_tilt = (map_value(pos_tilt, 0, limit_tilt, 0, 200) - 100) / 100
      ##########################################################################
      logging.log(logging.DEBUG_FCL, '%d (%d) %d (%d) %f %f', pos_pan, limit_pan, pos_tilt, limit_tilt, rel_pan, rel_tilt)
      print('publish_ptu_pos', pos_pan, limit_pan, pos_tilt, limit_tilt, rel_pan, rel_tilt)
      FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/i/ptu/pos", '{{"ts":"{}", "pan":{:.2f}, "tilt":{:.2f}}}'.format(timestamp_utcnow(), rel_pan, rel_tilt))

# Se añaden los listeners para detectar cambios en los sensores y la cámara, y ejecutar las funciones de callback correspondientes
TXT_SSC_M_I2C_1_environment_sensor.add_change_listener("humidity", callback)
TXT_SSC_M_USB1_1_camera.add_change_listener("image", image_callback)
TXT_SSC_M_I2C_1_environment_sensor.add_change_listener("temperature", callback2)
# motion_detector.add_detection_listener(motion_callback)

# Función auxiliar para convertir el formato de imagen de OpenCV a un String Base64 para JSON
def frame_to_base64(frame):
  result = ""
  # Comprime la imagen en JPEG con calidad 30 para reducir el peso del mensaje MQTT, y luego la codifica en Base64 para incluirla en el payload JSON
  success, image = cv2.imencode(".jpeg", frame, [1, 30])
  if success:
    result = "data:image/jpeg;base64," + base64.b64encode(image).decode("utf-8")
  return result



###########################################################################################
# TODO:
###########################################################################################
online_led_state = 0

def get_online_led_state():
  return int(online_led_state)

def _set_online_led(brightness):
  global online_led_state
  TXT_SSC_M_O5_led.set_brightness(int(brightness))
  online_led_state = 1 if int(brightness) > 0 else 0
###########################################################################################
###########################################################################################