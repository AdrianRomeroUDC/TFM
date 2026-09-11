import logging
import threading
from fischertechnik.controller.Motor import Motor
from lib.Axes1Ref import *
from lib.controller import *
from lib.Nfc import *
from lib.Factory_Variables import *

_data = None
name = None
num = None
value = None
rv1 = None
rv2 = None
rv3 = None
poslist = None
av1 = None
av2 = None
av3 = None
lockVGR = None
p123 = None
poslist_VGR_defaults = None
listnamepos1_discard_VGR_defaults = None
listnameoffset_VGR_defaults = None
poslist_VGR = None
listnamepos1_discard_VGR = None
listnameoffset_VGR = None
i = None
abspos_VGR = None
lockNFC = None
p1234 = None
p12 = None
temp = None
def get_pos3_VGR_name(name):
  global _data, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.debug('%s', name)
  p123 = None
  for p1234 in poslist_VGR:
    if p1234[0] == name:
      p123 = p1234[1 : 4]
      break
  if p123 == None:
    logging.warning('%s not found', name)
  return p123

def get_pos1_discard_VGR_name(name):
  global _data, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.debug('%s', name)
  value = 0
  p12 = None
  for p12 in listnamepos1_discard_VGR:
    if p12[0] == name:
      value = p12[1]
      logging.debug('%d', value)
      break
  return value

def get_offset_VGR_name(name):
  global _data, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.debug('%s', name)
  value = 0
  p12 = None
  for p12 in listnameoffset_VGR:
    if p12[0] == name:
      value = p12[1]
      logging.debug('%d', value)
      break
  return value



def get_lock_VGR():
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE0_VGR, '-')
  return lockVGR


def init_VGR():
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, '-')
  lockVGR = threading.RLock() #https://stackoverflow.com/questions/28017535/do-i-have-to-lock-all-functions-that-calls-to-one-or-more-locked-function-for-mu
  lockNFC = get_lock_NFC()
  poslist_VGR_defaults = [['Color Reader', 107, 645, 40], ['DSI', 5, 752, 5], ['DSO', 247, 290, 530], ['HBW', 1395, 0, 0], ['MPO', 911, 0, 850], ['NFC', 180, 643, 230], ['NiO', 300, 400, 0], ['SLD blue', 304, 835, 579], ['SLD red', 371, 835, 418], ['SLD white', 450, 835, 360]]
  poslist_VGR = poslist_VGR_defaults
  listnamepos1_discard_VGR_defaults = [['DSI', 550], ['DSO', 50], ['HBW', 20]]
  listnamepos1_discard_VGR = listnamepos1_discard_VGR_defaults
  listnameoffset_VGR_defaults = [['DSI', 202], ['DSO', 250], ['HBW_h', 158], ['HBW', 150], ['MPO', 490]]
  listnameoffset_VGR = listnameoffset_VGR_defaults
  moveRef_VGR_S231()
  set_vgr_ready(True)


def get_calib_data_VGR_defaults():
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, '-')
  return [poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults]


def get_calib_data_VGR():
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, '-')
  return [poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR]


def set_calib_data_VGR(_data):
  global name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, _data)
  poslist_VGR = _data[0]
  listnamepos1_discard_VGR = _data[1]
  listnameoffset_VGR = _data[2]


def set_pos3_VGR_name_num(name, num, value):
  global _data, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.debug('%s %d %d', name, num, value)
  i = 1
  for p1234 in poslist_VGR:
    if p1234[0] == name:
      p1234[int((num + 1) - 1)] = value
      logging.debug(p1234)
      poslist_VGR[int(i - 1)] = p1234
      break
    i = (i if isinstance(i, (int, float)) else 0) + 1


def set_pos1_discard_VGR_name(name, value):
  global _data, num, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.debug('%s %d', name, value)
  i = 1
  for p12 in listnamepos1_discard_VGR:
    if p12[0] == name:
      p12[1] = value
      logging.debug(p12)
      listnamepos1_discard_VGR[int(i - 1)] = p12
      break
    i = (i if isinstance(i, (int, float)) else 0) + 1


def set_offset_VGR_name(name, value):
  global _data, num, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.debug('%s %d', name, value)
  i = 1
  for p12 in listnameoffset_VGR:
    if p12[0] == name:
      p12[1] = value
      logging.debug(p12)
      listnameoffset_VGR[int(i - 1)] = p12
      break
    i = (i if isinstance(i, (int, float)) else 0) + 1


def log_abspos_VGR():
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, '-')
  abspos_VGR = (get_abspos())[ : 3]
  if abspos_VGR[ : 1] == None or abspos_VGR[ : 2] == None or abspos_VGR[ : 3] == None:
    logging.log(logging.DEBUG_VGR, 'abspos_VGR=None')
  else:
    logging.log(logging.DEBUG_VGR, 'abspos_VGR=%d %d %d', abspos_VGR[0], abspos_VGR[1], abspos_VGR[2])


def stop_VGR():
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, '-')
  lockVGR.acquire()
  TXT_VGR_E2_M1_encodermotor.stop_sync()
  TXT_VGR_E2_M2_encodermotor.stop_sync()
  TXT_VGR_E2_M3_encodermotor.stop_sync()
  lockVGR.release()


def moveRef_VGR_P123():
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, '->')
  lockVGR.acquire()
  th1 = threading.Thread(target=moveRef, args=(1, ), daemon=True)
  th2 = threading.Thread(target=moveRef, args=(2, ), daemon=True)
  th3 = threading.Thread(target=moveRef, args=(3, ), daemon=True)
  th1.start()
  th2.start()
  th3.start()
  th1.join()
  th2.join()
  th3.join()
  lockVGR.release()
  logging.log(logging.TRACE_VGR, '<-')


def moveRef_VGR_S231():
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, '->')
  lockVGR.acquire()
  moveRef(2)
  moveRef(3)
  moveRef(1)
  lockVGR.release()
  logging.log(logging.TRACE_VGR, '<-')


def moveRef_VGR_S23():
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, '->')
  lockVGR.acquire()
  moveRef(2)
  moveRef(3)
  lockVGR.release()
  logging.log(logging.TRACE_VGR, '<-')


def moveRel_VGR_P123(rv1, rv2, rv3):
  global _data, name, num, value, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'rv=%d %d %d', rv1, rv2, rv3)
  lockVGR.acquire()
  th1 = threading.Thread(target=moveRel, args=(1,rv1, ), daemon=True)
  th2 = threading.Thread(target=moveRel, args=(2,rv2, ), daemon=True)
  th3 = threading.Thread(target=moveRel, args=(3,rv3, ), daemon=True)
  th1.start()
  th2.start()
  th3.start()
  th1.join()
  th2.join()
  th3.join()
  log_abspos_VGR()
  lockVGR.release()


def moveRel_VGR_P123_list(poslist):
  global _data, name, num, value, rv1, rv2, rv3, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'poslist=%d %d %d', poslist[0], poslist[1], poslist[2])
  moveRel_VGR_P123(poslist[0], poslist[1], poslist[2])


def moveRel_VGR_S123(rv1, rv2, rv3):
  global _data, name, num, value, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'rv=%d %d %d', rv1, rv2, rv3)
  lockVGR.acquire()
  moveRel(1, rv1)
  moveRel(2, rv2)
  moveRel(3, rv3)
  log_abspos_VGR()
  lockVGR.release()


def moveRel_VGR_S123_list(poslist):
  global _data, name, num, value, rv1, rv2, rv3, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'poslist=%d %d %d', poslist[0], poslist[1], poslist[2])
  moveRel_VGR_S123(poslist[0], poslist[1], poslist[2])


def moveAbs_VGR_P123(av1, av2, av3):
  global _data, name, num, value, rv1, rv2, rv3, poslist, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'av=%d %d %d', av1, av2, av3)
  lockVGR.acquire()
  th1 = threading.Thread(target=moveAbs, args=(1,av1, ), daemon=True)
  th2 = threading.Thread(target=moveAbs, args=(2,av2, ), daemon=True)
  th3 = threading.Thread(target=moveAbs, args=(3,av3, ), daemon=True)
  th1.start()
  th2.start()
  th3.start()
  th1.join()
  th2.join()
  th3.join()
  log_abspos_VGR()
  lockVGR.release()


def moveAbs_VGR_P123_list(poslist):
  global _data, name, num, value, rv1, rv2, rv3, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'poslist=%d %d %d', poslist[0], poslist[1], poslist[2])
  moveAbs_VGR_P123(poslist[0], poslist[1], poslist[2])


def moveAbs_VGR_P123_name(name):
  global _data, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'name=%s', name)
  if name != None:
    temp = get_pos3_VGR_name(name)
    if temp != None and len(temp) == 3:
      moveAbs_VGR_P123_list(temp)


def moveAbs_VGR_S123(av1, av2, av3):
  global _data, name, num, value, rv1, rv2, rv3, poslist, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'av=%d %d %d', av1, av2, av3)
  lockVGR.acquire()
  moveAbs(1, av1)
  moveAbs(2, av2)
  moveAbs(3, av3)
  log_abspos_VGR()
  lockVGR.release()


def moveAbs_VGR_S123_list(poslist):
  global _data, name, num, value, rv1, rv2, rv3, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'poslist=%d %d %d', poslist[0], poslist[1], poslist[2])
  moveAbs_VGR_S123(poslist[0], poslist[1], poslist[2])


def get_abspos_VGR():
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  return (get_abspos())[ : 3]


