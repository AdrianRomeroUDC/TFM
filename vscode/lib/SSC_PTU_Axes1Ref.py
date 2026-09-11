import logging
import threading
from lib.Axes1Ref import *

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
lockSSC = None
p12 = None
abspos_SSC = None
poslist_SSC_defaults = None
poslist_SSC = None


def get_lock_SSC():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.log(logging.TRACE0, '-')
  return lockSSC


def init_SSC_PTU():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.log(logging.TRACE, '-')
  lockSSC = threading.RLock() #https://stackoverflow.com/questions/28017535/do-i-have-to-lock-all-functions-that-calls-to-one-or-more-locked-function-for-mu
  poslist_SSC_defaults = [[925, 425], [1500, 290], [925, 100]]
  poslist_SSC = poslist_SSC_defaults
  moveRef_SSC_P12()
  movePosHBW_SSC()


def get_calib_data_SSC_defaults():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.log(logging.TRACE, '-')
  return [poslist_SSC_defaults]


def get_calib_data_SSC():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.log(logging.TRACE, '-')
  return [poslist_SSC]


def set_calib_data_SSC(_data):
  global name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.log(logging.TRACE, _data)
  poslist_SSC = _data[0]


def get_pos2_SSC_name(name):
  global _data, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.log(logging.TRACE, name)
  p12 = [None, None]
  if name == 'Center':
    p12 = poslist_SSC[0]
  elif name == 'HBW':
    p12 = poslist_SSC[1]
  elif name == 'Park':
    p12 = poslist_SSC[2]
  else:
    pass
  logging.log(logging.DEBUG, '%d %d', p12[0], p12[1])
  return p12


def set_pos2_SSC_name_num(name, num, value):
  global _data, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.log(logging.TRACE, '%s %d %d', name, num, value)
  if name == 'Center':
    set_pos2_SSC_idx_num(1, num, value)
  elif name == 'HBW':
    set_pos2_SSC_idx_num(2, num, value)
  elif name == 'Park':
    set_pos2_SSC_idx_num(3, num, value)
  else:
    pass


def set_pos2_SSC_idx_num(idx, num, value):
  global _data, name, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  p12 = poslist_SSC[int(idx - 1)]
  p12[int(num - 1)] = value
  logging.log(logging.DEBUG, p12)
  poslist_SSC[int(idx - 1)] = p12


def get_abspos_SSC_pan():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.log(logging.TRACE0, '-')
  return (get_abspos())[5]


def get_abspos_SSC_tilt():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.log(logging.TRACE0, '-')
  return (get_abspos())[6]


def log_abspos_SSC():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  abspos_SSC = (get_abspos())[5 : 7]
  if abspos_SSC[ : 6] == None or abspos_SSC[ : 7] == None:
    logging.debug('abspos_SSC=None')
  else:
    logging.debug('abspos_SSC=%d %d', abspos_SSC[0], abspos_SSC[1])


def movePosCenter_SSC():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  moveAbs_SSC_P12_list(poslist_SSC[0])


def movePosHBW_SSC():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  moveAbs_SSC_P12_list(poslist_SSC[1])


def movePosPark_SSC():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  moveAbs_SSC_P12_list(poslist_SSC[2])


def moveRef_SSC_P12():
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.debug('start')
  lockSSC.acquire()
  th1 = threading.Thread(target=moveRef, args=(6, ), daemon=True)
  th2 = threading.Thread(target=moveRef, args=(7, ), daemon=True)
  th1.start()
  th2.start()
  th1.join()
  th2.join()
  logging.debug('ref finished')
  lockSSC.release()


def moveRel_SSC_P12(rv1, rv2):
  global _data, name, num, value, idx, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.debug('rv=%d %d', rv1, rv2)
  lockSSC.acquire()
  th1 = threading.Thread(target=moveRel, args=(6,rv1, ), daemon=True)
  th2 = threading.Thread(target=moveRel, args=(7,rv2, ), daemon=True)
  th1.start()
  th2.start()
  th1.join()
  th2.join()
  log_abspos_SSC()
  lockSSC.release()


def moveRel_SSC_P12_list(poslist):
  global _data, name, num, value, idx, rv1, rv2, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.debug('poslist=%d %d', poslist[0], poslist[1])
  moveRel_SSC_P12(poslist[0], poslist[1])


def moveAbs_SSC_P12(av1, av2):
  global _data, name, num, value, idx, rv1, rv2, poslist, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.debug('av=%d %d', av1, av2)
  lockSSC.acquire()
  th1 = threading.Thread(target=moveAbs, args=(6,av1, ), daemon=True)
  th2 = threading.Thread(target=moveAbs, args=(7,av2, ), daemon=True)
  th1.start()
  th2.start()
  th1.join()
  th2.join()
  log_abspos_SSC()
  lockSSC.release()


def moveAbs_SSC_P12_list(poslist):
  global _data, name, num, value, idx, rv1, rv2, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.debug('poslist=%d %d', poslist[0], poslist[1])
  moveAbs_SSC_P12(poslist[0], poslist[1])


