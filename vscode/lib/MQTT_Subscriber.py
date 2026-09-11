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
  global res, keep_alive_timestamp, cmd, degree, color, msgk
  logging.log(logging.TRACE_FCL, '-')
  msg = json.loads(message.payload.decode("utf-8"))
  color = msg["type"]
  print(color)
  if reqHBW_fetchWP([time.time(), '', color, True]):
    pass



def mqtt_callback_hbw_reset(message):
  global res, keep_alive_timestamp, cmd, degree, color, msgk
  logging.log(logging.TRACE_FCL, '-')
  resetStorage()
  updateStorageState()
  beep()



def mqtt_callback_park(message):
  global res, keep_alive_timestamp, cmd, degree, color, msgk
  logging.log(logging.TRACE_FCL, '-')
  parkFactory()



def mqtt_callback_ack(message):
  global res, keep_alive_timestamp, cmd, degree, color, msgk
  logging.log(logging.TRACE_FCL, '-')
  set_factory_error_state(None)



def mqtt_callback_nfc(message):
  global res, keep_alive_timestamp, cmd, degree, color, msgk
  logging.log(logging.TRACE_FCL, '-')
  msg = json.loads(message.payload.decode("utf-8"))
  cmd = msg["cmd"]
  print(cmd)
  res = reqVGR_Nfc(cmd)



def mqtt_callback_ptu(message):
  global res, keep_alive_timestamp, cmd, degree, color, msgk
  logging.log(logging.TRACE_FCL, '-')
  if not not len(message.payload.decode("utf-8")):
    msg= json.loads(message.payload.decode("utf-8"))
    cmd = msg['cmd']
    if 'degree' in msg:
        degree = msg['degree']
    processCmd(cmd, degree)



