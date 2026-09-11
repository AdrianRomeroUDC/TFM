import logging
import time
from lib.display import *
from lib.Factory import *
from lib.Factory_Variables import *
from lib.HBW import *
from lib.HBW_Storage import *
from lib.Sound import *
from lib.Test import *

_tr0 = None
_tr = None
_dg = None
res = None


def initlog_GUI(_tr0, _tr, _dg):
  global res
  logging.TRACE0_GUI = _tr0
  logging.addLevelName(logging.TRACE0_GUI , 'TRACE0_GUI')
  logging.TRACE_GUI = _tr
  logging.addLevelName(logging.TRACE_GUI , 'TRACE_GUI')
  logging.DEBUG_GUI = _dg
  logging.addLevelName(logging.DEBUG_GUI, 'DEBUG_GUI')


def on_txt_button_white_clicked(event):
  global _tr0, _tr, _dg, res
  logging.log(logging.TRACE_GUI, '-')
  if reqHBW_fetchWP([time.time(), '', 'WHITE', True]):
    pass


def on_txt_button_red_clicked(event):
  global _tr0, _tr, _dg, res
  logging.log(logging.TRACE_GUI, '-')
  if reqHBW_fetchWP([time.time(), '', 'RED', True]):
    pass


def on_txt_button_blue_clicked(event):
  global _tr0, _tr, _dg, res
  logging.log(logging.TRACE_GUI, '-')
  if reqHBW_fetchWP([time.time(), '', 'BLUE', True]):
    pass


def on_txt_button_acknowledge_clicked(event):
  global _tr0, _tr, _dg, res
  logging.log(logging.TRACE_GUI, '-')
  set_factory_error_state(None)


def on_txt_button_nfc_read_clicked(event):
  global _tr0, _tr, _dg, res
  logging.log(logging.TRACE_GUI, '-')
  res = reqVGR_Nfc('read')
  beep()


def on_txt_button_nfc_delete_clicked(event):
  global _tr0, _tr, _dg, res
  logging.log(logging.TRACE_GUI, '-')
  res = reqVGR_Nfc('delete')
  beep()


def on_txt_button_nfc_reset_clicked(event):
  global _tr0, _tr, _dg, res
  logging.log(logging.TRACE_GUI, '-')
  beep()
  resetStorage()
  updateStorageState()


def on_txt_button_park_clicked(event):
  global _tr0, _tr, _dg, res
  logging.log(logging.TRACE_GUI, '-')
  parkFactory()


def on_txt_button_test_clicked(event):
  global _tr0, _tr, _dg, res
  logging.log(logging.TRACE_GUI, '-')
  test_move_Axes1Ref()
  test_move_Axes2Ref()
  test_HBW_posall()
  logging.debug('exit')
  os._exit(os.EX_OK)


display.button_clicked("txt_button_white", on_txt_button_white_clicked)
display.button_clicked("txt_button_red", on_txt_button_red_clicked)
display.button_clicked("txt_button_blue", on_txt_button_blue_clicked)
display.button_clicked("txt_button_acknowledge", on_txt_button_acknowledge_clicked)
display.button_clicked("txt_button_nfc_read", on_txt_button_nfc_read_clicked)
display.button_clicked("txt_button_nfc_delete", on_txt_button_nfc_delete_clicked)
display.button_clicked("txt_button_nfc_reset", on_txt_button_nfc_reset_clicked)
display.button_clicked("txt_button_park", on_txt_button_park_clicked)
display.button_clicked("txt_button_test", on_txt_button_test_clicked)



