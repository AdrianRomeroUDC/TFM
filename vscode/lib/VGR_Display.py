"""Actualización de la pantalla y del estado visual del robot VGR."""

# Indicadores locales del estado y destino del brazo VGR.
import logging
from lib.display import *

state_code = None


def update_display_VGR(state_code):
  """Actualiza los indicadores visuales de estado del robot VGR.

  Args:
    state_code: Codigo de estado del robot: 1, 2, 4 o 7.

  Returns:
    None. Modifica los indicadores de la interfaz mediante ``display``.
  """
  logging.log(logging.TRACE0_GUI, state_code)
  if state_code == 1:
    display.set_attr("txt_status_indicator_VGR_green.active", str(True).lower())
    display.set_attr("txt_status_indicator_VGR_yellow.active", str(False).lower())
    display.set_attr("txt_status_indicator_VGR_red.active", str(False).lower())
  elif state_code == 2:
    display.set_attr("txt_status_indicator_VGR_green.active", str(False).lower())
    display.set_attr("txt_status_indicator_VGR_yellow.active", str(True).lower())
    display.set_attr("txt_status_indicator_VGR_red.active", str(False).lower())
  elif state_code == 4:
    display.set_attr("txt_status_indicator_VGR_green.active", str(False).lower())
    display.set_attr("txt_status_indicator_VGR_yellow.active", str(False).lower())
    display.set_attr("txt_status_indicator_VGR_red.active", str(True).lower())
  elif state_code == 7:
    display.set_attr("txt_status_indicator_VGR_green.active", str(True).lower())
    display.set_attr("txt_status_indicator_VGR_yellow.active", str(True).lower())
    display.set_attr("txt_status_indicator_VGR_red.active", str(True).lower())
  else:
    display.set_attr("txt_status_indicator_VGR_green.active", str(False).lower())
    display.set_attr("txt_status_indicator_VGR_yellow.active", str(False).lower())
    display.set_attr("txt_status_indicator_VGR_red.active", str(False).lower())


