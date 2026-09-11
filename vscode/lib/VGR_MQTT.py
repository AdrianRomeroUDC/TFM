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
  global payload
  logging.log(logging.TRACE_FCL, '%d, %d', _code, _active)
  if get_cloud_active():
    payload = '{{"ts":"{}","station":"vgr","code":{},"active":{},"target":"{}"}}'.format(timestamp_utcnow(), _code, _active, _target)
    logging.log(logging.DEBUG_FCL, payload)
    FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/f/i/state/vgr", payload)


