import logging
from lib.display import *

state_code = None


def update_display_MPO(state_code):
  logging.log(logging.TRACE0_GUI, state_code)
  if state_code == 1:
    display.set_attr("txt_status_indicator_MPO_green.active", str(True).lower())
    display.set_attr("txt_status_indicator_MPO_yellow.active", str(False).lower())
    display.set_attr("txt_status_indicator_MPO_red.active", str(False).lower())
  elif state_code == 2:
    display.set_attr("txt_status_indicator_MPO_green.active", str(False).lower())
    display.set_attr("txt_status_indicator_MPO_yellow.active", str(True).lower())
    display.set_attr("txt_status_indicator_MPO_red.active", str(False).lower())
  elif state_code == 4:
    display.set_attr("txt_status_indicator_MPO_green.active", str(False).lower())
    display.set_attr("txt_status_indicator_MPO_yellow.active", str(False).lower())
    display.set_attr("txt_status_indicator_MPO_red.active", str(True).lower())
  elif state_code == 7:
    display.set_attr("txt_status_indicator_MPO_green.active", str(True).lower())
    display.set_attr("txt_status_indicator_MPO_yellow.active", str(True).lower())
    display.set_attr("txt_status_indicator_MPO_red.active", str(True).lower())
  else:
    display.set_attr("txt_status_indicator_MPO_green.active", str(False).lower())
    display.set_attr("txt_status_indicator_MPO_yellow.active", str(False).lower())
    display.set_attr("txt_status_indicator_MPO_red.active", str(False).lower())


