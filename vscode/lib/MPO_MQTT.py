import logging
from fischertechnik.mqtt.Constants import CONTROLLER_ID
from fischertechnik.mqtt.FTCloudClient import FTCloudClient
from lib.Factory_Variables import *
from lib.Time import *

_code = None
_active = None
payload = None


def publish_state_MPO(_code, _active):
  global payload
  logging.log(logging.TRACE_FCL, '%d, %d', _code, _active)
  if get_cloud_active():
    payload = '{{"ts":"{}","station":"mpo","code":{},"active":{}}}'.format(timestamp_utcnow(), _code, _active)
    logging.log(logging.DEBUG_FCL, payload)
    FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/f/i/state/mpo", payload)


