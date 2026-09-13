"""Control de los sensores DPS (Delivery & Pickup Station).

DPS no acciona motores: lee el fototransistor de entrada ``TXT_VGR_E2_I7`` y
el contador/fototransistor de salida ``TXT_VGR_E2_C4`` para detectar piezas, y
el sensor de color ``TXT_VGR_E2_I8`` para clasificar blanco, rojo o azul.
``thread_DPS`` crea dos hilos daemon de publicación de estado. No se usa
``threading.RLock`` ni otro lock en este módulo.
"""

import logging
import time
from lib.controller import *
from lib.display import *
from lib.DPS_MQTT import *

_data = None
_active_dsi = None
_active_dso = None
color_str = None
thresh_white_red_defaults = None
thresh_red_blue_defaults = None
thresh_white_red = None
thresh_red_blue = None
_ts_state_dsi = None
_ts_state_dso = None
list_colorValue = None
is_dsi_last = None
is_dso_last = None
colorValue = None
state_active_dsi_last = None
state_active_dso_last = None
state_active_dsi = None
state_active_dso = None
_dsi = None
_dso = None
def thread_update_dsi():
  """
  Vigila el sensor de entrada de la DPS sin parar, en su propio hilo.

  Cada medio segundo comprueba el fototransistor de entrada (I7 del brazo
  VGR). En cuanto pasan mas de 10 segundos sin avisar o el sensor cambia de
  estado, actualiza el piloto en pantalla y publica la novedad por MQTT.

  Returns:
    None. Es un bucle infinito, nunca termina por si solo.
  """
  global _data, _active_dsi, _active_dso, color_str, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state_dsi, _ts_state_dso, list_colorValue, is_dsi_last, is_dso_last, colorValue, state_active_dsi_last, state_active_dso_last, state_active_dsi, state_active_dso, _dsi, _dso
  logging.log(logging.TRACE, '-')
  _ts_state_dsi = 0
  is_dsi_last = None
  state_active_dsi_last = None
  while True:
    if (time.time() * 1000) - _ts_state_dsi > 10000 or is_dsi() != is_dsi_last or state_active_dsi != state_active_dsi_last:
      update_display_dsi()
      publish_state_DSI(0 if is_dsi() else 1, state_active_dsi)
      _ts_state_dsi = (time.time() * 1000)
      is_dsi_last = is_dsi()
      state_active_dsi_last = state_active_dsi
    time.sleep(0.5)

def thread_update_dso():
  """
  Vigila el sensor de salida de la DPS sin parar, en su propio hilo.

  Igual que ``thread_update_dsi`` pero mirando el fototransistor de salida
  (C4 del brazo VGR): revisa cada medio segundo y avisa por pantalla y MQTT
  cuando pasan mas de 10 segundos o el sensor cambia de estado.

  Returns:
    None. Es un bucle infinito, nunca termina por si solo.
  """
  global _data, _active_dsi, _active_dso, color_str, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state_dsi, _ts_state_dso, list_colorValue, is_dsi_last, is_dso_last, colorValue, state_active_dsi_last, state_active_dso_last, state_active_dsi, state_active_dso, _dsi, _dso
  logging.log(logging.TRACE, '-')
  _ts_state_dso = 0
  is_dso_last = None
  state_active_dso_last = None
  while True:
    if (time.time() * 1000) - _ts_state_dso > 10000 or is_dso() != is_dso_last or state_active_dso != state_active_dso_last:
      update_display_dso()
      publish_state_DSO(0 if is_dso() else 1, state_active_dso)
      _ts_state_dso = (time.time() * 1000)
      is_dso_last = is_dso()
      state_active_dso_last = state_active_dso
    time.sleep(0.5)

def readDPSColor():
  """
  Decide de que color es la pieza que hay en la DPS.

  Compara el voltaje medido por el sensor de color contra los dos umbrales
  calibrados para separar blanco de rojo y rojo de azul.

  Returns:
    'WHITE', 'RED' o 'BLUE' segun el color detectado, o ``None`` si la
    lectura no encaja en ningun rango conocido.
  """
  global _data, _active_dsi, _active_dso, color_str, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state_dsi, _ts_state_dso, list_colorValue, is_dsi_last, is_dso_last, colorValue, state_active_dsi_last, state_active_dso_last, state_active_dsi, state_active_dso, _dsi, _dso
  logging.log(logging.TRACE, '-')
  color_str = None
  colorValue = readDPSColorValue()
  logging.log(logging.DEBUG, 'colorValue: %d', colorValue)
  if colorValue >= 200 and colorValue < thresh_white_red:
    color_str = 'WHITE'
  elif colorValue >= thresh_white_red and colorValue < thresh_red_blue:
    color_str = 'RED'
  elif colorValue >= thresh_red_blue and colorValue < 2000:
    color_str = 'BLUE'
  else:
    pass
  logging.log(logging.DEBUG, 'color: %s', color_str)
  return color_str



def thread_DPS():
  """
  Pone en marcha la estacion DPS.

  Fija los umbrales de color de fabrica, resetea el estado de los puntos de
  entrada y salida, y lanza los dos hilos vigilantes (``thread_update_dsi``
  y ``thread_update_dso``) que corren mientras la fabrica este encendida.

  Returns:
    None. Se queda esperando para siempre una vez lanzados los hilos.
  """
  global _data, _active_dsi, _active_dso, color_str, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state_dsi, _ts_state_dso, list_colorValue, is_dsi_last, is_dso_last, colorValue, state_active_dsi_last, state_active_dso_last, state_active_dsi, state_active_dso, _dsi, _dso
  logging.log(logging.TRACE, '-')
  thresh_white_red_defaults = 1150
  thresh_white_red = thresh_white_red_defaults
  thresh_red_blue_defaults = 1300
  thresh_red_blue = thresh_red_blue_defaults
  state_active_dsi = 0
  state_active_dso = 0
  _dsi = 0
  _dso = 0
  set_state_dsi(0)
  set_state_dso(0)
  threading.Thread(target=thread_update_dsi, daemon=True).start()
  threading.Thread(target=thread_update_dso, daemon=True).start()
  while True:
    pass


def get_calib_data_DPS_defaults():
  """
  Devuelve los umbrales de color de fabrica de la DPS.

  Son los valores originales, sin los ajustes que se hayan hecho a mano
  despues.

  Returns:
    Una lista ``[umbral_blanco_rojo, umbral_rojo_azul]``.
  """
  global _data, _active_dsi, _active_dso, color_str, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state_dsi, _ts_state_dso, list_colorValue, is_dsi_last, is_dso_last, colorValue, state_active_dsi_last, state_active_dso_last, state_active_dsi, state_active_dso, _dsi, _dso
  logging.log(logging.TRACE, '-')
  return [thresh_white_red_defaults, thresh_red_blue_defaults]


def get_calib_data_DPS():
  """
  Devuelve los umbrales de color que la DPS esta usando ahora mismo.

  Returns:
    Una lista ``[umbral_blanco_rojo, umbral_rojo_azul]``.
  """
  global _data, _active_dsi, _active_dso, color_str, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state_dsi, _ts_state_dso, list_colorValue, is_dsi_last, is_dso_last, colorValue, state_active_dsi_last, state_active_dso_last, state_active_dsi, state_active_dso, _dsi, _dso
  logging.log(logging.TRACE, '-')
  return [thresh_white_red, thresh_red_blue]


def set_calib_data_DPS(_data):
  """
  Guarda unos nuevos umbrales de color para la DPS.

  A partir de ahora ``readDPSColor`` usara estos valores para decidir si
  una pieza es blanca, roja o azul.

  Args:
    _data: Lista ``[umbral_blanco_rojo, umbral_rojo_azul]`` con los nuevos
      umbrales.

  Returns:
    None.
  """
  global _active_dsi, _active_dso, color_str, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state_dsi, _ts_state_dso, list_colorValue, is_dsi_last, is_dso_last, colorValue, state_active_dsi_last, state_active_dso_last, state_active_dsi, state_active_dso, _dsi, _dso
  logging.log(logging.TRACE, _data)
  thresh_white_red = _data[0]
  thresh_red_blue = _data[1]


def set_state_dsi(_active_dsi):
  """
  Marca si el punto de entrada de la DPS esta activo en el proceso.

  Si cambia el estado de activacion o el sensor de entrada acaba de
  cambiar, reinicia el cronometro para que ``thread_update_dsi`` avise
  cuanto antes de la novedad.

  Args:
    _active_dsi: Nuevo estado de activacion del punto de entrada.

  Returns:
    None.
  """
  global _data, _active_dso, color_str, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state_dsi, _ts_state_dso, list_colorValue, is_dsi_last, is_dso_last, colorValue, state_active_dsi_last, state_active_dso_last, state_active_dsi, state_active_dso, _dsi, _dso
  logging.log(logging.TRACE0, '-')
  if state_active_dsi != _active_dsi:
    _ts_state_dsi = 0
    state_active_dsi = _active_dsi
  if _dsi != is_dsi():
    _ts_state_dsi = 0
    _dsi = is_dsi()


def set_state_dso(_active_dso):
  """
  Marca si el punto de salida de la DPS esta activo en el proceso.

  Igual que ``set_state_dsi`` pero para el punto de salida: reinicia el
  cronometro cuando cambia la activacion o el sensor de salida, para que
  ``thread_update_dso`` avise cuanto antes.

  Args:
    _active_dso: Nuevo estado de activacion del punto de salida.

  Returns:
    None.
  """
  global _data, _active_dsi, color_str, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state_dsi, _ts_state_dso, list_colorValue, is_dsi_last, is_dso_last, colorValue, state_active_dsi_last, state_active_dso_last, state_active_dsi, state_active_dso, _dsi, _dso
  logging.log(logging.TRACE0, '-')
  if state_active_dso != _active_dso:
    _ts_state_dso = 0
    state_active_dso = _active_dso
  if _dso != is_dso():
    _ts_state_dso = 0
    _dso = is_dso()


def update_display_dsi():
  """
  Enciende o apaga en pantalla el piloto de entrada de la DPS.

  Se enciende cuando el fototransistor de entrada detecta una pieza
  (queda a oscuras) y se apaga cuando no hay nada delante.

  Returns:
    None.
  """
  global _data, _active_dsi, _active_dso, color_str, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state_dsi, _ts_state_dso, list_colorValue, is_dsi_last, is_dso_last, colorValue, state_active_dsi_last, state_active_dso_last, state_active_dsi, state_active_dso, _dsi, _dso
  logging.log(logging.TRACE0_GUI, '-')
  if is_dsi():
    display.set_attr("txt_status_indicator_DPS_in.active", str(True).lower())
  else:
    display.set_attr("txt_status_indicator_DPS_in.active", str(False).lower())


def update_display_dso():
  """
  Enciende o apaga en pantalla el piloto de salida de la DPS.

  Igual que ``update_display_dsi`` pero para el punto de salida: se
  enciende si el sensor detecta una pieza y se apaga si no.

  Returns:
    None.
  """
  global _data, _active_dsi, _active_dso, color_str, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state_dsi, _ts_state_dso, list_colorValue, is_dsi_last, is_dso_last, colorValue, state_active_dsi_last, state_active_dso_last, state_active_dsi, state_active_dso, _dsi, _dso
  logging.log(logging.TRACE0_GUI, '-')
  if is_dso():
    display.set_attr("txt_status_indicator_DPS_out.active", str(True).lower())
  else:
    display.set_attr("txt_status_indicator_DPS_out.active", str(False).lower())


def is_dsi():
  """
  Pregunta al sensor de entrada de la DPS si hay una pieza delante.

  Mira el fototransistor de entrada (I7 del brazo VGR): si algo le tapa la
  luz, es que hay una pieza esperando en el punto de entrada.

  Returns:
    True si detecta una pieza (sensor a oscuras), False si no hay nada.
  """
  global _data, _active_dsi, _active_dso, color_str, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state_dsi, _ts_state_dso, list_colorValue, is_dsi_last, is_dso_last, colorValue, state_active_dsi_last, state_active_dso_last, state_active_dsi, state_active_dso, _dsi, _dso
  logging.log(logging.TRACE0, '-')
  return TXT_VGR_E2_I7_photo_transistor.is_dark()


def is_dso():
  """
  Pregunta al sensor de salida de la DPS si hay una pieza delante.

  Mira el fototransistor/contador de salida (C4 del brazo VGR): si algo le
  tapa la luz, es que hay una pieza en el punto de salida.

  Returns:
    True si detecta una pieza (sensor a oscuras), False si no hay nada.
  """
  global _data, _active_dsi, _active_dso, color_str, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state_dsi, _ts_state_dso, list_colorValue, is_dsi_last, is_dso_last, colorValue, state_active_dsi_last, state_active_dso_last, state_active_dsi, state_active_dso, _dsi, _dso
  logging.log(logging.TRACE0, '-')
  return TXT_VGR_E2_C4_photo_transistor.is_dark()


def readDPSColorValue():
  """
  Mide el voltaje del sensor de color de la DPS.

  Toma diez lecturas seguidas y calcula la media, para que una lectura
  suelta con ruido no estropee la deteccion del color.

  Returns:
    El voltaje medio leido por el sensor de color.
  """
  global _data, _active_dsi, _active_dso, color_str, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state_dsi, _ts_state_dso, list_colorValue, is_dsi_last, is_dso_last, colorValue, state_active_dsi_last, state_active_dso_last, state_active_dsi, state_active_dso, _dsi, _dso
  logging.log(logging.TRACE0, '-')
  list_colorValue = []
  for count in range(10):
    list_colorValue.append(TXT_VGR_E2_I8_color_sensor.get_voltage())
  return math_mean(list_colorValue)


def math_mean(myList):
  """
  Calcula la media de una lista de numeros.

  Ignora cualquier elemento de la lista que no sea un numero.

  Args:
    myList: Lista de valores, normalmente lecturas de un sensor.

  Returns:
    La media como numero decimal, o ``None`` si no queda ningun numero
    valido en la lista.
  """
  localList = [e for e in myList if isinstance(e, (float, int))]
  if not localList: return
  return float(sum(localList)) / len(localList)


