"""Publicación MQTT del estado y destino actual del brazo VGR."""

# Publica estado, destino y actividad del VGR.
import logging
from fischertechnik.mqtt.Constants import CONTROLLER_ID
from fischertechnik.mqtt.FTCloudClient import FTCloudClient
from lib.Factory_Variables import *
from lib.Time import *

_code = None
_active = None
_target = None
payload = None


def publish_state_VGR(_code, _active, _target):
  """Publica el estado y el destino actual del robot VGR.

  Args:
    _code: Codigo de estado del robot.
    _active: Indicador de actividad del robot.
    _target: Nombre del destino o posicion solicitada.

  Returns:
    None. Publica un payload MQTT con timestamp si el cloud esta activo.
  """
  global payload
  logging.log(logging.TRACE_FCL, '%d, %d', _code, _active)
  if get_cloud_active():
    payload = '{{"ts":"{}","station":"vgr","code":{},"active":{},"target":"{}"}}'.format(timestamp_utcnow(), _code, _active, _target)
    logging.log(logging.DEBUG_FCL, payload)
    FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/f/i/state/vgr", payload)


