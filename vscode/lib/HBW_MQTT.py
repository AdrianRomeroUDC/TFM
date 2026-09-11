import json
import logging
from fischertechnik.mqtt.Constants import CONTROLLER_ID
from fischertechnik.mqtt.FTCloudClient import FTCloudClient
from fischertechnik.mqtt.MqttClient import MqttClient
from lib.Factory_Variables import *
from lib.HBW_Storage import *
from lib.Time import *

_code = None
_active = None
list_storage = None
loc = None
list_item = None
b_type = None
jsonitem = None
s = None
storage_location = None
payload = None
stockItems = None
i = None
payload_storage = None


def publish_state_HBW(_code, _active):
  global list_storage, loc, list_item, b_type, jsonitem, s, storage_location, payload, stockItems, i, payload_storage
  logging.log(logging.TRACE_FCL, '%d, %d', _code, _active)
  if get_cloud_active():
    payload = '{{"ts":"{}","station":"hbw","code":{},"active":{}}}'.format(timestamp_utcnow(), _code, _active)
    logging.log(logging.DEBUG_FCL, payload)
    #print(payload)
    FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/f/i/state/hbw", payload)


def publish_state_Storage(list_storage):
  global _code, _active, loc, list_item, b_type, jsonitem, s, storage_location, payload, stockItems, i, payload_storage
  logging.log(logging.TRACE_FCL, list_storage)
  storage_location = get_storage_location()
  stockItems = []
  #wp list: 1:ts, 2:uid, 3:color, 4:produced
  if list_storage != None and len(list_storage) == 9:
    for i in range(1, 10):
      stockItems.append(storage_wp_jsonitem(storage_location[int(i - 1)], list_storage[int(i - 1)]))
    payload_storage = '{{"ts":"{}", "stockItems":{} }}'.format(timestamp_utcnow(), stockItems)
    logging.log(logging.DEBUG_FCL, payload_storage)
    #print("TEXT: ", payload)
    payload_storage = json.dumps(eval(payload_storage))
    logging.log(logging.DEBUG_FCL, payload_storage)
    #print("JSON: ", payload_storage)
    if get_cloud_active():
      FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/f/i/stock", payload_storage)
    if (get_client_local()) != None:
      get_client_local().publish(topic='f/i/stock', payload=payload_storage, qos=2, retain=True)


def storage_wp_jsonitem(loc, list_item):
  global _code, _active, list_storage, b_type, jsonitem, s, storage_location, payload, stockItems, i, payload_storage
  logging.log(logging.TRACE0_FCL, loc)
  #print(list_item)
  if ((list_item!=None) and (list_item[1]!=None)):
    jsonitem = {
      "workpiece": {
       "id": list_item[1],
       "type": list_item[2],
       "state": type2str(list_item[3])
      },
      "location": loc
    }
  else:
    jsonitem = {
      "workpiece": None,
      "location": loc
    }
  return jsonitem


def type2str(b_type):
  global _code, _active, list_storage, loc, list_item, jsonitem, s, storage_location, payload, stockItems, i, payload_storage
  logging.log(logging.TRACE0_FCL, b_type)
  if b_type:
    s = 'PROCESSED'
  else:
    s = 'RAW'
  return s


