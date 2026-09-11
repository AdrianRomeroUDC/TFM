import logging
import math
import threading
from fischertechnik.controller.Motor import Motor
from lib.Axes1Ref import *
from lib.Axes2Ref import *
from lib.controller import *
from lib.Factory_Variables import *

_data = None
name = None
num = None
value = None
idx = None
rv1 = None
rv2 = None
poslist = None
av1 = None
av2 = None
numxy = None
lockHBW = None
p12 = None
poslist_HBW_defaults = None
poslist_HBW = None
abspos_HBW = None
OFFSET_Y = None
temp_x = None
listnameoffset_HBW = None
temp_y = None
listnameoffset_HBW_defaults = None
def get_pos2_HBW_name(name):
  global _data, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.debug('%s', name)
  p12 = None
  if name == 'Belt':
    p12 = poslist_HBW[3]
  elif name == 'Rack A1':
    p12 = poslist_HBW[0]
  elif name == 'Rack B2':
    p12 = poslist_HBW[1]
  elif name == 'Rack C3':
    p12 = poslist_HBW[2]
  else:
    pass
  return p12

def get_offset_HBW_name(name):
  global _data, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.debug('%s', name)
  value = None
  if name == 'Rack':
    value = listnameoffset_HBW[0]
  elif name == 'Belt':
    value = listnameoffset_HBW[1]
  else:
    pass
  logging.debug('%d', value)
  return value



def get_lock_HBW():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE0_HBW, '-')
  return lockHBW


def init_HBW():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, '-')
  lockHBW = threading.RLock() #https://stackoverflow.com/questions/28017535/do-i-have-to-lock-all-functions-that-calls-to-one-or-more-locked-function-for-mu
  OFFSET_Y = 40
  poslist_HBW_defaults = [[750, 70], [1360, 435], [1965, 845], [0, 710]]
  poslist_HBW = poslist_HBW_defaults
  #TODO: offsets
  listnameoffset_HBW_defaults = [180, 200]
  listnameoffset_HBW = listnameoffset_HBW_defaults
  moveRef_HBW_P12()
  set_hbw_ready(True)


def get_calib_data_HBW_defaults():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, '-')
  return [poslist_HBW_defaults]


def get_calib_data_HBW():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, '-')
  return [poslist_HBW]


def set_calib_data_HBW(_data):
  global name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, _data)
  poslist_HBW = _data[0]


def log_abspos_HBW():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, '-')
  abspos_HBW = (get_abspos())[3 : 5]
  if abspos_HBW[0] == None or abspos_HBW[1] == None:
    logging.log(logging.DEBUG_HBW, 'abspos_HBW=None')
  else:
    logging.log(logging.DEBUG_HBW, 'abspos_HBW=%d %d', abspos_HBW[0], abspos_HBW[1])


def set_pos2_HBW_name_num(name, num, value):
  global _data, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.debug('%s %d %d', name, num, value)
  if name == 'Belt':
    set_pos2_HBW_idx_num(4, num, value)
  elif name == 'Rack A1':
    set_pos2_HBW_idx_num(1, num, value)
  elif name == 'Rack B2':
    set_pos2_HBW_idx_num(2, num, value)
  elif name == 'Rack C3':
    set_pos2_HBW_idx_num(3, num, value)
  else:
    pass


def set_pos2_HBW_idx_num(idx, num, value):
  global _data, name, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  p12 = poslist_HBW[int(idx - 1)]
  p12[int(num - 1)] = value
  logging.debug(p12)
  poslist_HBW[int(idx - 1)] = p12


def set_offset_HBW_name(name, value):
  global _data, num, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.debug('%s %d', name, value)
  if name == 'Rack':
    listnameoffset_HBW[0] = value
    logging.debug(listnameoffset[0])
  elif name == 'Belt':
    listnameoffset_HBW[1] = value
    logging.debug(listnameoffset[1])
  else:
    pass


def stop_HBW():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, '-')
  lockHBW.acquire()
  TXT_HBW_E1_M2_encodermotor.stop_sync()
  TXT_HBW_E1_M4_encodermotor.stop_sync()
  lockHBW.release()


def moveRef_HBW_P12():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, '-')
  lockHBW.acquire()
  move2Ref(1)
  th1 = threading.Thread(target=moveRef, args=(4, ), daemon=True)
  th2 = threading.Thread(target=moveRef, args=(5, ), daemon=True)
  th1.start()
  th2.start()
  th1.join()
  th2.join()
  logging.log(logging.DEBUG_HBW, 'ref finished')
  lockHBW.release()


def moveRel_HBW_P12(rv1, rv2):
  global _data, name, num, value, idx, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, 'rv=%d %d', rv1, rv2)
  lockHBW.acquire()
  move2Ref(1)
  th1 = threading.Thread(target=moveRel, args=(1,rv1, ), daemon=True)
  th2 = threading.Thread(target=moveRel, args=(2,rv2, ), daemon=True)
  th1.start()
  th2.start()
  th1.join()
  th2.join()
  lockHBW.release()


def moveRel_HBW_P12_list(poslist):
  global _data, name, num, value, idx, rv1, rv2, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, 'poslist=%d %d', poslist[0], poslist[1])
  moveRel_HBW_P12(poslist[0], poslist[1])


def moveAbs_HBW_P12(av1, av2):
  global _data, name, num, value, idx, rv1, rv2, poslist, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, 'av=%d %d', av1, av2)
  lockHBW.acquire()
  move2Ref(1)
  th1 = threading.Thread(target=moveAbs, args=(4,av1, ), daemon=True)
  th2 = threading.Thread(target=moveAbs, args=(5,av2, ), daemon=True)
  th1.start()
  th2.start()
  th1.join()
  th2.join()
  lockHBW.release()


def moveAbs_HBW_P12_list(poslist):
  global _data, name, num, value, idx, rv1, rv2, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, 'poslist=%d %d', poslist[0], poslist[1])
  moveAbs_HBW_P12(poslist[0], poslist[1])


def moveConv():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, '-')
  moveAbs_HBW_P12_list(poslist_HBW[3])


def moveCR(numxy):
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, 'numxy=%d %d', numxy[0], numxy[1])
  lockHBW.acquire()
  temp_x = poslist_HBW[int(numxy[0] - 1)][0]
  temp_y = poslist_HBW[int(numxy[1] - 1)][1]
  logging.log(logging.DEBUG_HBW, 'temp_x=%d temp_y=%d', temp_x, temp_y)
  moveAbs_HBW_P12_list([temp_x, temp_y])
  lockHBW.release()


def moveGet():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, OFFSET_Y)
  lockHBW.acquire()
  move2Ref(2)
  moveRel(5, -OFFSET_Y)
  move2Ref(1)
  lockHBW.release()


def moveGet2():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, '-')
  lockHBW.acquire()
  moveRel(5, -OFFSET_Y)
  move2Ref(1)
  lockHBW.release()


def movePut():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, OFFSET_Y)
  lockHBW.acquire()
  moveRel(5, -OFFSET_Y)
  move2Ref(2)
  moveRel(5, OFFSET_Y)
  move2Ref(1)
  lockHBW.release()


def movePut1():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, OFFSET_Y)
  lockHBW.acquire()
  moveRel(5, -OFFSET_Y)
  move2Ref(2)
  moveRel(5, OFFSET_Y)
  lockHBW.release()


def get_abspos_HBW():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, '-')
  return (get_abspos())[3 : 5]


