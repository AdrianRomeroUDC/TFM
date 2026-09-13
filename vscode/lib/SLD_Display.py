"""Actualización de indicadores gráficos de la línea SLD."""

# Indicadores locales del estado de clasificación de la estación SLD.
import logging
from lib.display import *

state_code = None


def update_display_SLD(state_code):
  """Actualiza los tres indicadores de estado de la linea SLD.

  Args:
    state_code: Codigo de estado de la estacion: 1, 2, 4 o 7.

  Returns:
    None. Escribe el estado de los indicadores en la interfaz ``display``.
  """
  logging.log(logging.TRACE0_GUI, state_code)
  if state_code == 1:
    display.set_attr("txt_status_indicator_SLD_green.active", str(True).lower())
    display.set_attr("txt_status_indicator_SLD_yellow.active", str(False).lower())
    display.set_attr("txt_status_indicator_SLD_red.active", str(False).lower())
  elif state_code == 2:
    display.set_attr("txt_status_indicator_SLD_green.active", str(False).lower())
    display.set_attr("txt_status_indicator_SLD_yellow.active", str(True).lower())
    display.set_attr("txt_status_indicator_SLD_red.active", str(False).lower())
  elif state_code == 4:
    display.set_attr("txt_status_indicator_SLD_green.active", str(False).lower())
    display.set_attr("txt_status_indicator_SLD_yellow.active", str(False).lower())
    display.set_attr("txt_status_indicator_SLD_red.active", str(True).lower())
  elif state_code == 7:
    display.set_attr("txt_status_indicator_SLD_green.active", str(True).lower())
    display.set_attr("txt_status_indicator_SLD_yellow.active", str(True).lower())
    display.set_attr("txt_status_indicator_SLD_red.active", str(True).lower())
  else:
    display.set_attr("txt_status_indicator_SLD_green.active", str(False).lower())
    display.set_attr("txt_status_indicator_SLD_yellow.active", str(False).lower())
    display.set_attr("txt_status_indicator_SLD_red.active", str(False).lower())


