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
  global bme680_period, ldr_period, camera_fps, cmd, degree, keep_alive_timestamp, camera_on, msgk
  logging.log(logging.TRACE_FCL, '-')
  if (get_init_finished()) and not not len(message.payload.decode("utf-8")):
    msg = json.loads(message.payload.decode("utf-8"))
    bme680_period = msg['period']
    set_bme680_period(bme680_period)



def mqtt_callback2(message):
  global bme680_period, ldr_period, camera_fps, cmd, degree, keep_alive_timestamp, camera_on, msgk
  logging.log(logging.TRACE_FCL, '-')
  if (get_init_finished()) and not not len(message.payload.decode("utf-8")):
    msg= json.loads(message.payload.decode("utf-8"))
    ldr_period = msg['period']
    set_ldr_period(ldr_period)



def mqtt_callback3(message):
  global bme680_period, ldr_period, camera_fps, cmd, degree, keep_alive_timestamp, camera_on, msgk
  logging.log(logging.TRACE_FCL, '-')
  if (get_init_finished()) and not not len(message.payload.decode("utf-8")):
    msg= json.loads(message.payload.decode("utf-8"))
    camera_fps = msg['fps']
    camera_on = msg['on']
    set_camera_fps(camera_fps)
    set_camera_on(camera_on)



def mqtt_callback4(message):
  global bme680_period, ldr_period, camera_fps, cmd, degree, keep_alive_timestamp, camera_on, msgk
  logging.log(logging.TRACE_FCL, '-')
  if (get_init_finished()) and not not len(message.payload.decode("utf-8")):
    msg= json.loads(message.payload.decode("utf-8"))
    cmd = msg['cmd']
    if 'degree' in msg:
        degree = msg['degree']
    processCmd(cmd, degree)



def mqtt_callback5(message):
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



