import json
import logging
import time
from fischertechnik.mqtt.Constants import CONTROLLER_ID
from fischertechnik.mqtt.FTCloudClient import FTCloudClient
from lib.Factory import *
from lib.Factory_Variables import *
from lib.Nfc import *
from lib.Time import *

nfc_data_uid = None
nfc_data_type_str = None
nfc_data_state_str = None
nfc_data_mask_str = None
ts = None
res = None
cmd = None
list_tsstr = None
history = None
payload = None
i = None
workpiece = None
history_item = None


def publish_Nfc_Data():
  global nfc_data_uid, nfc_data_type_str, nfc_data_state_str, nfc_data_mask_str, ts, res, cmd, list_tsstr, history, payload, i, workpiece, history_item
  logging.log(logging.TRACE_FCL, '-')
  if get_cloud_active():
    nfc_data_uid = get_nfc_data_uid()
    nfc_data_type_str = (get_nfc_data_type_str()).upper()
    nfc_data_state_str = (get_nfc_data_state_str()).upper()
    nfc_data_mask_str = get_nfc_data_mask_str()
    workpiece =  {
         "id": nfc_data_uid if (nfc_data_uid!=None) else "" ,
         "type": nfc_data_type_str,
         "state": nfc_data_state_str
    }
    logging.log(logging.DEBUG_FCL, workpiece)
    logging.log(logging.DEBUG_FCL, nfc_data_mask_str)
    list_tsstr = get_nfc_data_tsstr_list()
    history = []
    if list_tsstr != None:
      #print("list_tsstr: ", list_tsstr)
      logging.log(logging.DEBUG_FCL, list_tsstr)
      i_end = len(list_tsstr)
      for i in (1 <= i_end) and upRange(1, i_end, 1) or downRange(1, i_end, 1):
        #print("i: ", i, nfc_data_mask_str, list_tsstr)
        logging.log(logging.DEBUG_FCL, nfc_data_mask_str)
        if nfc_data_mask_str != None and 8 - i < len(nfc_data_mask_str):
          if nfc_data_mask_str[int((9 - i) - 1)] == '1':
            if i < len(list_tsstr):
              pass
            history_item = {
               "ts": list_tsstr[i-1],
               "code": i*100
            }
            #print("ADD item: ", history_item)
            history.append(history_item)
      #print("history ", history)
    payload = '{{"ts":"{}", "workpiece":{}, "history":{} }}'.format(timestamp_utcnow(), workpiece, history)
    #print(payload)
    payload = json.dumps(eval(payload))
    #print(payload)
    logging.log(logging.DEBUG_FCL, payload)
    FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/f/i/nfc/ds", payload)


def mqtt_callback(message):
  global nfc_data_uid, nfc_data_type_str, nfc_data_state_str, nfc_data_mask_str, ts, res, cmd, list_tsstr, history, payload, i, workpiece, history_item
  logging.log(logging.TRACE_FCL, '-')
  if (get_init_finished()) and not not len(message.payload.decode("utf-8")):
    msg= json.loads(message.payload.decode("utf-8"))
    print(msg)
    tsstr = msg["ts"]
    #print(tsstr)
    ts = datetime.strptime(tsstr[:-1], "%Y-%m-%dT%H:%M:%S.%f").replace(tzinfo=timezone.utc).timestamp()
    #print(ts)
    cmd = msg["cmd"]
    #print(cmd)
    if time.time() - ts < 60:
      logging.log(logging.DEBUG_FCL, '-')
      if cmd == 'read' or cmd == 'delete':
        logging.log(logging.DEBUG_FCL, cmd)
        res = reqVGR_Nfc(cmd)



FTCloudClient.getInstance().subscribe("/j1/txt/" + str(CONTROLLER_ID) + "/f/o/nfc/ds", mqtt_callback)


def upRange(start, stop, step):
  while start <= stop:
    yield start
    start += abs(step)

def downRange(start, stop, step):
  while start >= stop:
    yield start
    start -= abs(step)


