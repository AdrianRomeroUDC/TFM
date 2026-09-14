"""Publicación MQTT de los estados de entrada y salida de la estación DPS."""

# Este adaptador traduce estados DPS a mensajes MQTT.
import logging
from fischertechnik.mqtt.Constants import CONTROLLER_ID
from fischertechnik.mqtt.FTCloudClient import FTCloudClient
from lib.Factory_Variables import *
from lib.Time import *

_code_dsi = None
_active_dsi = None
_code_dso = None
_active_dso = None
payload_dsi = None
payload_dso = None


def publish_state_DSI(_code_dsi, _active_dsi):
  """Publica el estado del sensor de entrada DSI en el broker cloud.

  Args:
    _code_dsi: Codigo de estado que identifica la condicion del sensor.
    _active_dsi: Indicador de si el estado DSI esta activo.

  Returns:
    None. Si el cloud esta activo, envia un payload con marca temporal.
  """
  global _code_dso, _active_dso, payload_dsi, payload_dso
  logging.log(logging.TRACE_FCL, '%d, %d', _code_dsi, _active_dsi)
  if get_cloud_active():
    payload_dsi = '{{"ts":"{}","station":"dsi","code":{},"active":{}}}'.format(timestamp_utcnow(), _code_dsi, _active_dsi)
    logging.log(logging.DEBUG_FCL, payload_dsi)
    FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/f/i/state/dsi", payload_dsi)


def publish_state_DSO(_code_dso, _active_dso):
  """Publica el estado del sensor de salida DSO en el broker cloud.

  Args:
    _code_dso: Codigo de estado que identifica la condicion del sensor.
    _active_dso: Indicador de si el estado DSO esta activo.

  Returns:
    None. Si el cloud esta activo, envia un payload con marca temporal.
  """
  global _code_dsi, _active_dsi, payload_dsi, payload_dso
  logging.log(logging.TRACE_FCL, '%d, %d', _code_dso, _active_dso)
  if get_cloud_active():
    payload_dso = '{{"ts":"{}","station":"dso","code":{},"active":{}}}'.format(timestamp_utcnow(), _code_dso, _active_dso)
    logging.log(logging.DEBUG_FCL, payload_dso)
    FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/f/i/state/dso", payload_dso)


