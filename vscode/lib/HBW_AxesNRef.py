"""Calibración y movimiento de los ejes cartesianos del almacén HBW."""

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
  """
  Busca las coordenadas guardadas de un punto con nombre del almacen.

  Args:
    name: Nombre del punto: 'Belt' (la cinta) o una de las estanterias
      'Rack A1', 'Rack B2', 'Rack C3'.

  Returns:
    Una lista ``[horizontal, vertical]`` con la posicion de ese punto, o
    ``None`` si el nombre no se reconoce.
  """
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
  """
  Busca el ajuste fino guardado para el estante o para la cinta.

  Args:
    name: 'Rack' para el ajuste del estante o 'Belt' para el de la cinta.

  Returns:
    El ajuste guardado, o ``None`` si el nombre no se reconoce.
  """
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
  """
  Devuelve el cerrojo que evita que dos movimientos del almacen se pisen.

  Cualquier rutina que mueva el brazo del almacen debe pedir este cerrojo
  primero, para que nunca haya dos ordenes de movimiento a la vez.

  Returns:
    El cerrojo (``threading.RLock``) del almacen.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE0_HBW, '-')
  return lockHBW


def init_HBW():
  """
  Prepara el brazo del almacen para empezar a trabajar.

  Crea el cerrojo de movimiento, carga las coordenadas de fabrica de la
  cinta y de las tres estanterias, lleva el brazo a su posicion de
  referencia y lo marca como listo para trabajar.

  Returns:
    None.
  """
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
  """
  Devuelve las coordenadas del almacen tal y como vienen de fabrica.

  Returns:
    Una lista con la tabla original de coordenadas de la cinta y de las
    tres estanterias, sin los ajustes hechos a mano.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, '-')
  return [poslist_HBW_defaults]


def get_calib_data_HBW():
  """
  Devuelve las coordenadas del almacen que se estan usando ahora mismo.

  Returns:
    Una lista con la tabla activa de coordenadas de la cinta y de las tres
    estanterias.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, '-')
  return [poslist_HBW]


def set_calib_data_HBW(_data):
  """
  Reemplaza la tabla de coordenadas del almacen por una nueva.

  Args:
    _data: Lista cuyo primer elemento es la nueva tabla de coordenadas de
      la cinta y de las tres estanterias.

  Returns:
    None.
  """
  global name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, _data)
  poslist_HBW = _data[0]


def log_abspos_HBW():
  """
  Escribe en el log la posicion actual del brazo del almacen.

  Solo sirve para depurar: no mueve nada, solo lee donde esta el brazo
  (horizontal y vertical) y lo apunta en el registro.

  Returns:
    None.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, '-')
  abspos_HBW = (get_abspos())[3 : 5]
  if abspos_HBW[0] == None or abspos_HBW[1] == None:
    logging.log(logging.DEBUG_HBW, 'abspos_HBW=None')
  else:
    logging.log(logging.DEBUG_HBW, 'abspos_HBW=%d %d', abspos_HBW[0], abspos_HBW[1])


def set_pos2_HBW_name_num(name, num, value):
  """
  Cambia una coordenada de un punto con nombre del almacen.

  Args:
    name: Nombre del punto a corregir: 'Belt', 'Rack A1', 'Rack B2' o
      'Rack C3'.
    num: Que eje se corrige: 1 = horizontal, 2 = vertical.
    value: Nuevo valor para ese eje.

  Returns:
    None.
  """
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
  """
  Cambia una coordenada de un punto del almacen por su indice de tabla.

  Es la versión interna de ``set_pos2_HBW_name_num`` que ya conoce en que
  fila de la tabla esta cada punto.

  Args:
    idx: Fila de la tabla de coordenadas a corregir (1 a 4).
    num: Que eje se corrige: 1 = horizontal, 2 = vertical.
    value: Nuevo valor para ese eje.

  Returns:
    None.
  """
  global _data, name, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  p12 = poslist_HBW[int(idx - 1)]
  p12[int(num - 1)] = value
  logging.debug(p12)
  poslist_HBW[int(idx - 1)] = p12


def set_offset_HBW_name(name, value):
  """
  Cambia el ajuste fino del estante o de la cinta.

  Args:
    name: 'Rack' para el ajuste del estante o 'Belt' para el de la cinta.
    value: Nuevo ajuste.

  Returns:
    None.
  """
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
  """
  Para en seco los motores horizontal y vertical del brazo del almacen.

  Se usa como parada de emergencia, con el cerrojo puesto para que nadie
  mas intente moverlo a la vez.

  Returns:
    None.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, '-')
  lockHBW.acquire()
  TXT_HBW_E1_M2_encodermotor.stop_sync()
  TXT_HBW_E1_M4_encodermotor.stop_sync()
  lockHBW.release()


def moveRef_HBW_P12():
  """
  Lleva el brazo del almacen a su posicion de referencia.

  Primero retrae el brazo del todo y despues mueve a la vez el eje
  horizontal y el vertical hasta sus finales de carrera.

  Returns:
    None.
  """
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
  """
  Mueve el brazo del almacen una cantidad relativa en horizontal y vertical.

  Retrae primero el brazo por seguridad y despues mueve a la vez el eje
  horizontal y el vertical la distancia indicada.

  Args:
    rv1: Cuanto mover en horizontal respecto a la posicion actual.
    rv2: Cuanto mover en vertical respecto a la posicion actual.

  Returns:
    None.
  """
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
  """
  Igual que ``moveRel_HBW_P12`` pero recibiendo los dos valores en lista.

  Args:
    poslist: Lista ``[rv1, rv2]`` con lo que hay que mover en horizontal y
      en vertical respecto a la posicion actual.

  Returns:
    None.
  """
  global _data, name, num, value, idx, rv1, rv2, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, 'poslist=%d %d', poslist[0], poslist[1])
  moveRel_HBW_P12(poslist[0], poslist[1])


def moveAbs_HBW_P12(av1, av2):
  """
  Lleva el brazo del almacen a unas coordenadas exactas.

  Retrae primero el brazo por seguridad y despues mueve a la vez el eje
  horizontal y el vertical hasta la posicion indicada.

  Args:
    av1: Posicion horizontal a la que moverse.
    av2: Posicion vertical a la que moverse.

  Returns:
    None.
  """
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
  """
  Igual que ``moveAbs_HBW_P12`` pero recibiendo las coordenadas en lista.

  Args:
    poslist: Lista ``[horizontal, vertical]`` con la posicion absoluta a
      la que moverse.

  Returns:
    None.
  """
  global _data, name, num, value, idx, rv1, rv2, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, 'poslist=%d %d', poslist[0], poslist[1])
  moveAbs_HBW_P12(poslist[0], poslist[1])


def moveConv():
  """
  Mueve el brazo del almacen hasta la posicion de la cinta.

  Returns:
    None.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, '-')
  moveAbs_HBW_P12_list(poslist_HBW[3])


def moveCR(numxy):
  """
  Mueve el brazo a una celda del estante combinando fila y columna.

  Toma la coordenada horizontal de un punto guardado y la vertical de
  otro, para poder alcanzar cualquier hueco del estante aunque no tenga
  su propio nombre guardado.

  Args:
    numxy: Lista ``[fila_x, fila_y]`` con el numero de fila de la tabla de
      la que se toma la coordenada horizontal y de la que se toma la
      vertical.

  Returns:
    None.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, 'numxy=%d %d', numxy[0], numxy[1])
  lockHBW.acquire()
  temp_x = poslist_HBW[int(numxy[0] - 1)][0]
  temp_y = poslist_HBW[int(numxy[1] - 1)][1]
  logging.log(logging.DEBUG_HBW, 'temp_x=%d temp_y=%d', temp_x, temp_y)
  moveAbs_HBW_P12_list([temp_x, temp_y])
  lockHBW.release()


def moveGet():
  """
  Extiende el brazo hasta el estante y coge la pieza que hay alli.

  Estira el brazo hacia el estante, lo baja un poco para engancharse a la
  pieza y lo retrae de nuevo, trayendola consigo.

  Returns:
    None.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, OFFSET_Y)
  lockHBW.acquire()
  move2Ref(2)
  moveRel(5, -OFFSET_Y)
  move2Ref(1)
  lockHBW.release()


def moveGet2():
  """
  Coge la pieza que hay en la cinta, sin tener que extender el brazo.

  Igual que ``moveGet`` pero partiendo de un punto donde el brazo ya esta
  extendido: solo baja un poco para enganchar la pieza y retrae.

  Returns:
    None.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, '-')
  lockHBW.acquire()
  moveRel(5, -OFFSET_Y)
  move2Ref(1)
  lockHBW.release()


def movePut():
  """
  Deja la pieza en el estante y retrae el brazo vacio.

  Baja un poco, extiende el brazo hasta el estante, sube para soltar la
  pieza en su hueco y retrae el brazo ya sin ella.

  Returns:
    None.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, OFFSET_Y)
  lockHBW.acquire()
  moveRel(5, -OFFSET_Y)
  move2Ref(2)
  moveRel(5, OFFSET_Y)
  move2Ref(1)
  lockHBW.release()


def movePut1():
  """
  Deja la pieza en la cinta, dejando el brazo extendido.

  Baja un poco, extiende el brazo hasta la cinta y sube para soltar la
  pieza, pero sin retraer el brazo despues.

  Returns:
    None.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, OFFSET_Y)
  lockHBW.acquire()
  moveRel(5, -OFFSET_Y)
  move2Ref(2)
  moveRel(5, OFFSET_Y)
  lockHBW.release()


def get_abspos_HBW():
  """
  Devuelve la posicion exacta en la que esta ahora el brazo del almacen.

  Returns:
    Una lista ``[horizontal, vertical]`` con la posicion actual del brazo.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, numxy, lockHBW, p12, poslist_HBW_defaults, poslist_HBW, abspos_HBW, OFFSET_Y, temp_x, listnameoffset_HBW, temp_y, listnameoffset_HBW_defaults
  logging.log(logging.TRACE_HBW, '-')
  return (get_abspos())[3 : 5]


