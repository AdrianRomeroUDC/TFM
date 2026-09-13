"""Callbacks de la interfaz gráfica para botones y controles de la fábrica."""

# La interfaz convierte las pulsaciones del panel en peticiones de la fabrica.
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
  """
  Da de alta los niveles de registro propios de la interfaz grafica.

  Crea las etiquetas TRACE0_GUI, TRACE_GUI y DEBUG_GUI para que los
  mensajes de los botones de pantalla se puedan filtrar en el log aparte
  del resto de estaciones.

  Args:
    _tr0: Nivel numérico para el trazado mas detallado.
    _tr: Nivel numérico para el trazado normal.
    _dg: Nivel numérico para los mensajes de depuración.

  Returns:
    None.
  """
  global res
  logging.TRACE0_GUI = _tr0
  logging.addLevelName(logging.TRACE0_GUI , 'TRACE0_GUI')
  logging.TRACE_GUI = _tr
  logging.addLevelName(logging.TRACE_GUI , 'TRACE_GUI')
  logging.DEBUG_GUI = _dg
  logging.addLevelName(logging.DEBUG_GUI, 'DEBUG_GUI')


def on_txt_button_white_clicked(event):
  """
  Pide desde la pantalla que se saque del almacen una pieza blanca.

  Args:
    event: Informacion del boton pulsado en pantalla (sin usar).

  Returns:
    None.
  """
  global _tr0, _tr, _dg, res
  logging.log(logging.TRACE_GUI, '-')
  if reqHBW_fetchWP([time.time(), '', 'WHITE', True]):
    pass


def on_txt_button_red_clicked(event):
  """
  Pide desde la pantalla que se saque del almacen una pieza roja.

  Args:
    event: Informacion del boton pulsado en pantalla (sin usar).

  Returns:
    None.
  """
  global _tr0, _tr, _dg, res
  logging.log(logging.TRACE_GUI, '-')
  if reqHBW_fetchWP([time.time(), '', 'RED', True]):
    pass


def on_txt_button_blue_clicked(event):
  """
  Pide desde la pantalla que se saque del almacen una pieza azul.

  Args:
    event: Informacion del boton pulsado en pantalla (sin usar).

  Returns:
    None.
  """
  global _tr0, _tr, _dg, res
  logging.log(logging.TRACE_GUI, '-')
  if reqHBW_fetchWP([time.time(), '', 'BLUE', True]):
    pass


def on_txt_button_acknowledge_clicked(event):
  """
  Confirma la alarma pulsando el boton ACK de la pantalla.

  Borra el error activo de la fabrica, para que las estaciones puedan
  volver a funcionar con normalidad.

  Args:
    event: Informacion del boton pulsado en pantalla (sin usar).

  Returns:
    None.
  """
  global _tr0, _tr, _dg, res
  logging.log(logging.TRACE_GUI, '-')
  set_factory_error_state(None)


def on_txt_button_nfc_read_clicked(event):
  """
  Pide al VGR que lea la etiqueta NFC de la pieza mas cercana.

  Al terminar, hace sonar un pitido para confirmar que se ha lanzado la
  lectura.

  Args:
    event: Informacion del boton pulsado en pantalla (sin usar).

  Returns:
    None.
  """
  global _tr0, _tr, _dg, res
  logging.log(logging.TRACE_GUI, '-')
  res = reqVGR_Nfc('read')
  beep()


def on_txt_button_nfc_delete_clicked(event):
  """
  Pide al VGR que borre los datos del chip NFC de la pieza mas cercana.

  Al terminar, hace sonar un pitido para confirmar que se ha lanzado el
  borrado.

  Args:
    event: Informacion del boton pulsado en pantalla (sin usar).

  Returns:
    None.
  """
  global _tr0, _tr, _dg, res
  logging.log(logging.TRACE_GUI, '-')
  res = reqVGR_Nfc('delete')
  beep()


def on_txt_button_nfc_reset_clicked(event):
  """
  Vacia el inventario del almacen desde el boton de reset de pantalla.

  Hace sonar un pitido, borra lo que hay guardado en el almacen y
  actualiza el contador de piezas en pantalla, igual que si se hubiera
  pasado la tarjeta maestra por el lector NFC.

  Args:
    event: Informacion del boton pulsado en pantalla (sin usar).

  Returns:
    None.
  """
  global _tr0, _tr, _dg, res
  logging.log(logging.TRACE_GUI, '-')
  beep()
  resetStorage()
  updateStorageState()


def on_txt_button_park_clicked(event):
  """
  Manda aparcar toda la fabrica desde el boton de pantalla.

  Args:
    event: Informacion del boton pulsado en pantalla (sin usar).

  Returns:
    None.
  """
  global _tr0, _tr, _dg, res
  logging.log(logging.TRACE_GUI, '-')
  parkFactory()


def on_txt_button_test_clicked(event):
  """
  Lanza las pruebas de movimiento de ejes y del almacen, y cierra el programa.

  Pensado solo para pruebas: mueve los ejes de referencia sencilla y de
  dos posiciones, recorre todas las casillas del almacen y, al terminar,
  cierra el programa entero.

  Args:
    event: Informacion del boton pulsado en pantalla (sin usar).

  Returns:
    None. El programa se cierra dentro de esta funcion.
  """
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



