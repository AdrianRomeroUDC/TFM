"""Actualización de la pantalla y del estado visual de la estación MPO."""

import logging
from lib.display import *

state_code = None


def update_display_MPO(state_code):
  """Actualiza los indicadores visuales de estado de la estacion MPO.

  Args:
    state_code: Codigo de estado de la estacion: 1, 2, 4 o 7.

  Returns:
    None. Modifica los atributos de los indicadores en la interfaz.
  """
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


