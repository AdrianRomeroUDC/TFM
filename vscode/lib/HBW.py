import logging
import time
from fischertechnik.controller.Motor import Motor
from lib.controller import *
from lib.display import *
from lib.Factory import *
from lib.Factory_Variables import *
from lib.HBW_AxesNRef import *
from lib.HBW_Display import *
from lib.HBW_MQTT import *
from lib.HBW_Storage import *

_tr0 = None
_tr = None
_dg = None
_code = None
_active = None
wp = None
state_code = None
state_active = None
res = None
thread_OutIn = None
_ts_state = None
thread_InOut = None
wp_color = None
ts0_OutIn = None
ts0_InOut = None
tsdiff_OutIn = None
tsdiff_InOut = None
valid = None
ts0 = None
wp_uid = None
tsdiff = None
def thread_HBW():
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  thread_OutIn = None
  thread_InOut = None
  init_HBW()
  initStorage()
  updateStorageState()
  _set_state_HBW(1, 0)
  threading.Thread(target=thread_update_HBW, daemon=True).start()
  while True:
    if (get_factory_error_state()) == 'HBW':
      print('HBW: error')
      _set_state_HBW(4, 1)
      display.set_attr("txt_label_message.text", str('ERROR HBW: Please confirm with ACK button!'))
    elif (getwp_reqHBW_fetchContainer()) != None:
      print('HBW: fetch container')
      if checkAndCorrectStorage():
        updateStorageState()
      _set_state_HBW(2, 1)
      ackHBW_fetchContainer(1)
      valid = True
      if valid:
        ts0 = (time.time() * 1000)
        while True:
          wp = getwp_reqHBW_fetchContainer()
          #wp: ts, uid, color, state
          if wp != None:
            wp_uid = wp[1]
            wp_color = wp[2]
            valid = True
            break
          else:
            wp_uid = ''
            wp_color = None
          tsdiff = (time.time() * 1000) - ts0
          if tsdiff > 60000:
            logging.log(logging.DEBUG_HBW, 'timeout %d', tsdiff)
            valid = wp_color != None and wp_uid != ''
            break
          time.sleep(1)
      if True:
        #TODO: error empty container -> new
        if valid:
          if can_color_be_stored(wp_color):
            valid = fetchContainerHBW()
      if False:
        #TODO: error empty container -> new
        if valid:
          if can_color_be_stored(wp_color):
            ts0 = (time.time() * 1000)
            while True:
              valid = fetchContainerHBW()
              if valid:
                if (getstate_ackHBW_fetchContainer()) == 2:
                  print(getstate_ackHBW_fetchContainer())
                  valid = False
                  break
              tsdiff = (time.time() * 1000) - ts0
              if tsdiff > 50000:
                logging.log(logging.DEBUG_HBW, 'timeout %d', tsdiff)
                valid = False
                break
              time.sleep(1)
      if valid:
        logging.log(logging.DEBUG_HBW, 'wait WP Out')
        thread_InOut.join()
      if valid:
        ts0 = (time.time() * 1000)
        while True:
          if wp != None:
            logging.log(logging.DEBUG_HBW, wp)
            ackHBW_fetchContainer(2)
            time.sleep(1)
            #wp list: 1:ts, 2:uid, 3:color, 4:produced
            valid = storeHBW([time.time(), wp_uid, wp_color, False])
            break
          tsdiff = (time.time() * 1000) - ts0
          if tsdiff > 20000:
            logging.log(logging.DEBUG_HBW, 'timeout %d', tsdiff)
            valid = wp_color != None and wp_uid != ''
            break
          time.sleep(1)
      if not valid:
        valid = storeContainerHBW()
      if valid:
        ackHBW_fetchContainer(3)
        moveRef_HBW_P12()
        if checkAndCorrectStorage():
          updateStorageState()
    elif (getwp_reqHBW_fetchWP()) != None:
      print('HBW: fetch WP')
      _set_state_HBW(2, 1)
      wp = getwp_reqHBW_fetchWP()
      logging.log(logging.DEBUG_HBW, wp)
      valid = wp != None
      if valid:
        #wp: ts, uid, color, state
        wp_color = wp[2]
        logging.log(logging.DEBUG_HBW, wp_color)
        valid = wp_color != None
      if valid:
        valid = (get_num_color_stored(wp_color)) > 0
      if valid:
        valid = fetchHBW(wp)
      if valid:
        ts0 = (time.time() * 1000)
        while not (getstate_ackHBW_fetchWP()) == 1:
          tsdiff = (time.time() * 1000) - ts0
          logging.log(logging.DEBUG_HBW, 'check ack fetchWP %.1f', tsdiff)
          if valid and tsdiff < 50000:
            time.sleep(1) # TODO: modificado por Lustres
            ackHBW_fetchWP(1)
            valid = True
            break
          else:
            logging.log(logging.DEBUG_VGR, 'timeout ack fetchWP')
            valid = False
          time.sleep(1)
      if valid:
        logging.log(logging.DEBUG_HBW, 'wait WP Out')
        moveRef_HBW_P12()
        ackHBW_fetchWP(2)
        moveConv()
        movePut1()
        valid = storeContainerHBW()
      if valid:
        moveRef_HBW_P12()
    elif isTriggeredIn() or isTriggeredOut():
      print('HBW: In Out blocked')
      set_factory_error_state('HBW')
      display.set_attr("txt_label_message2.text", str('In Out blocked'))
    _set_state_HBW(1, 0)
    time.sleep(1)



def initlog_HBW(_tr0, _tr, _dg):
  global _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.TRACE0_HBW = _tr0
  logging.addLevelName(logging.TRACE0_HBW , 'TRACE0_HBW')
  logging.TRACE_HBW = _tr
  logging.addLevelName(logging.TRACE_HBW , 'TRACE_HBW')
  logging.DEBUG_HBW = _dg
  logging.addLevelName(logging.DEBUG_HBW, 'DEBUG_HBW')


def parkHBW():
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  stop_HBW()
  moveRef_HBW_P12()
  moveAbs_HBW_P12_list([170, 850])


def thread_update_HBW():
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  _ts_state = 0
  while True:
    if (time.time() * 1000) - _ts_state > 10000:
      update_display_HBW(state_code)
      publish_state_HBW(state_code, state_active)
      _ts_state = (time.time() * 1000)
    time.sleep(1)


def _set_state_HBW(_code, _active):
  global _tr0, _tr, _dg, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE0_HBW, '-')
  if state_code != _code or state_active != _active:
    _ts_state = 0
    state_code = _code
    state_active = _active


def get_state_code_HBW():
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE0_HBW, '-')
  return state_code


def get_state_active_HBW():
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE0_HBW, '-')
  return state_active


def updateStorageState():
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  saveFileStorage()
  update_display_WP(get_num_color_stored('WHITE'), get_num_color_stored('RED'), get_num_color_stored('BLUE'))
  publish_state_Storage(get_list_storage())


def storeHBW(wp):
  global _tr0, _tr, _dg, _code, _active, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, 'wp=%d %s %s %d', wp[0], wp[1], wp[2], wp[3])
  #wp list: 1:ts, 2:uid, 3:color, 4:produced
  res = storeWorkpiece(wp)
  if res:
    logging.log(logging.DEBUG_HBW, 'true')
    #already in conv pos
    thread_OutIn = threading.Thread(target=thread_ConvOutIn, daemon=True)
    thread_OutIn.start()
    thread_OutIn.join()
    moveGet2()
    moveCR(get_nextfetchpos_xy())
    movePut()
    updateStorageState()
  return res


def storeContainerHBW():
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  res = storeContainer()
  if res:
    logging.log(logging.DEBUG_HBW, 'true')
    thread_OutIn = threading.Thread(target=thread_ConvOutIn, daemon=True)
    thread_OutIn.start()
    thread_OutIn.join()
    moveGet2()
    moveCR(get_nextfetchpos_xy())
    movePut()
    updateStorageState()
  return res


def fetchHBW(wp):
  global _tr0, _tr, _dg, _code, _active, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, wp)
  #wp list: 1:ts, 2:uid, 3:color, 4:produced
  wp_color = wp[2]
  res = fetchWorkpiece(wp_color)
  if res:
    logging.log(logging.DEBUG_HBW, 'true')
    moveCR(get_nextfetchpos_xy())
    moveGet()
    moveConv()
    thread_InOut = threading.Thread(target=thread_ConvInOut, daemon=True)
    thread_InOut.start()
    movePut1()
    updateStorageState()
  return res


def fetchContainerHBW():
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  res = fetchContainer()
  if res:
    logging.log(logging.DEBUG_HBW, 'true')
    moveCR(get_nextfetchpos_xy())
    moveGet()
    moveConv()
    thread_InOut = threading.Thread(target=thread_ConvInOut, daemon=True)
    thread_InOut.start()
    movePut1()
    updateStorageState()
  return res


def thread_ConvOutIn():
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  TXT_HBW_E1_M1_motor.set_speed(int(400), Motor.CCW)
  TXT_HBW_E1_M1_motor.start()
  ts0_OutIn = (time.time() * 1000)
  while not isTriggeredIn():
    tsdiff_OutIn = (time.time() * 1000) - ts0_OutIn
    if tsdiff_OutIn > 20000:
      logging.log(logging.DEBUG_HBW, 'timeout %d', tsdiff_OutIn)
      break
    time.sleep(0.05)
  stopConv()


def thread_ConvInOut():
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  TXT_HBW_E1_M1_motor.set_speed(int(400), Motor.CW)
  TXT_HBW_E1_M1_motor.start()
  ts0_InOut = (time.time() * 1000)
  while not isTriggeredOut():
    tsdiff_InOut = (time.time() * 1000) - ts0_InOut
    if tsdiff_InOut > 20000:
      logging.log(logging.DEBUG_HBW, 'timeout %d', tsdiff_InOut)
      break
    time.sleep(0.05)
  time.sleep(0.5)
  stopConv()


def stopConv():
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  TXT_HBW_E1_M1_motor.stop()


def isTriggeredOut():
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE0_HBW, '-')
  return TXT_HBW_E1_I1_photo_transistor.is_dark()


def isTriggeredIn():
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE0_HBW, '-')
  return TXT_HBW_E1_I4_photo_transistor.is_dark()


