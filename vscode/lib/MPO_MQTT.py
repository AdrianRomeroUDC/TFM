"""Publicación MQTT del estado de la estación MPO."""

import logging
from fischertechnik.mqtt.Constants import CONTROLLER_ID
from fischertechnik.mqtt.FTCloudClient import FTCloudClient
from lib.Factory_Variables import *
from lib.Time import *

_code = None
_active = None
payload = None


def publish_state_MPO(_code, _active):
  """Publica el estado de la estacion MPO en MQTT cloud.

  Args:
    _code: Codigo de estado de la estacion.
    _active: Indicador de actividad de la estacion.

  Returns:
    None. Construye el mensaje con timestamp y lo publica si el cloud esta activo.
  """
  global payload
  logging.log(logging.TRACE_FCL, '%d, %d', _code, _active)
  if get_cloud_active():
    payload = '{{"ts":"{}","station":"mpo","code":{},"active":{}}}'.format(timestamp_utcnow(), _code, _active)
    logging.log(logging.DEBUG_FCL, payload)
    FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/f/i/state/mpo", payload)


