import logging
from lib.display import *

state_code = None
w = None
r = None
b = None


def update_display_HBW(state_code):
  global w, r, b
  logging.log(logging.TRACE0_GUI, state_code)
  if state_code == 1:
    display.set_attr("txt_status_indicator_HBW_green.active", str(True).lower())
    display.set_attr("txt_status_indicator_HBW_yellow.active", str(False).lower())
    display.set_attr("txt_status_indicator_HBW_red.active", str(False).lower())
  elif state_code == 2:
    display.set_attr("txt_status_indicator_HBW_green.active", str(False).lower())
    display.set_attr("txt_status_indicator_HBW_yellow.active", str(True).lower())
    display.set_attr("txt_status_indicator_HBW_red.active", str(False).lower())
  elif state_code == 4:
    display.set_attr("txt_status_indicator_HBW_green.active", str(False).lower())
    display.set_attr("txt_status_indicator_HBW_yellow.active", str(False).lower())
    display.set_attr("txt_status_indicator_HBW_red.active", str(True).lower())
  elif state_code == 7:
    display.set_attr("txt_status_indicator_HBW_green.active", str(True).lower())
    display.set_attr("txt_status_indicator_HBW_yellow.active", str(True).lower())
    display.set_attr("txt_status_indicator_HBW_red.active", str(True).lower())
  else:
    display.set_attr("txt_status_indicator_HBW_green.active", str(False).lower())
    display.set_attr("txt_status_indicator_HBW_yellow.active", str(False).lower())
    display.set_attr("txt_status_indicator_HBW_red.active", str(False).lower())


def update_display_WP(w, r, b):
  global state_code
  logging.log(logging.TRACE_GUI, "%d %d %d",  w, r, b)
  display.set_attr("txt_label_white_in_stock.text", str('<h1>{}</h1>'.format(w)))
  display.set_attr("txt_button_white.enabled", str(w > 0).lower())
  display.set_attr("txt_status_indicator_white.active", str(w > 0).lower())
  display.set_attr("txt_label_red_in_stock.text", str('<h1>{}</h1>'.format(r)))
  display.set_attr("txt_button_red.enabled", str(r > 0).lower())
  display.set_attr("txt_status_indicator_red.active", str(r > 0).lower())
  display.set_attr("txt_label_blue_in_stock.text", str('<h1>{}</h1>'.format(b)))
  display.set_attr("txt_button_blue.enabled", str(b > 0).lower())
  display.set_attr("txt_status_indicator_blue.active", str(b > 0).lower())


