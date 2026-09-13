"""Calibración y movimiento sincronizado de los ejes pan y tilt del SSC."""

# Agrupa los ejes de la camara y protege sus movimientos con lockSSC.
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
  """
  Devuelve el cerrojo que evita que dos movimientos de la camara se pisen.

  Cualquier rutina que mueva el pan o el tilt de la camara debe pedir este
  cerrojo primero, para que nunca haya dos ordenes de movimiento a la vez.

  Returns:
    El cerrojo (``threading.RLock``) de la camara.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.log(logging.TRACE0, '-')
  return lockSSC


def init_SSC_PTU():
  """
  Prepara la camara PTU para empezar a funcionar.

  Crea el cerrojo de movimiento, carga las coordenadas de fabrica de las
  tres posiciones fijas (Centro, HBW y Park), lleva la camara a su
  posicion de referencia y la deja mirando hacia el almacen.

  Returns:
    None.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.log(logging.TRACE, '-')
  lockSSC = threading.RLock() #https://stackoverflow.com/questions/28017535/do-i-have-to-lock-all-functions-that-calls-to-one-or-more-locked-function-for-mu
  poslist_SSC_defaults = [[925, 425], [1500, 290], [925, 100]]
  poslist_SSC = poslist_SSC_defaults
  moveRef_SSC_P12()
  movePosHBW_SSC()


def get_calib_data_SSC_defaults():
  """
  Devuelve las posiciones de la camara tal y como vienen de fabrica.

  Returns:
    Una lista con la tabla original de las tres posiciones (Centro, HBW y
    Park), sin los ajustes hechos a mano.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.log(logging.TRACE, '-')
  return [poslist_SSC_defaults]


def get_calib_data_SSC():
  """
  Devuelve las posiciones de la camara que se estan usando ahora mismo.

  Returns:
    Una lista con la tabla activa de las tres posiciones (Centro, HBW y
    Park).
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.log(logging.TRACE, '-')
  return [poslist_SSC]


def set_calib_data_SSC(_data):
  """
  Reemplaza la tabla de posiciones de la camara por una nueva.

  Args:
    _data: Lista cuyo primer elemento es la nueva tabla con las tres
      posiciones (Centro, HBW y Park).

  Returns:
    None.
  """
  global name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.log(logging.TRACE, _data)
  poslist_SSC = _data[0]


def get_pos2_SSC_name(name):
  """
  Busca las coordenadas de una posicion con nombre de la camara.

  Args:
    name: Nombre de la posicion: 'Center', 'HBW' o 'Park'.

  Returns:
    Una lista ``[pan, tilt]`` con la posicion de esa camara, o
    ``[None, None]`` si el nombre no se reconoce.
  """
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
  """
  Cambia una coordenada de una posicion con nombre de la camara.

  Args:
    name: Nombre de la posicion a corregir: 'Center', 'HBW' o 'Park'.
    num: Que eje se corrige: 1 = pan, 2 = tilt.
    value: Nuevo valor para ese eje.

  Returns:
    None.
  """
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
  """
  Cambia una coordenada de una posicion de la camara por su indice de tabla.

  Es la version interna de ``set_pos2_SSC_name_num`` que ya conoce en que
  fila de la tabla esta cada posicion.

  Args:
    idx: Fila de la tabla de posiciones a corregir (1 a 3).
    num: Que eje se corrige: 1 = pan, 2 = tilt.
    value: Nuevo valor para ese eje.

  Returns:
    None.
  """
  global _data, name, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  p12 = poslist_SSC[int(idx - 1)]
  p12[int(num - 1)] = value
  logging.log(logging.DEBUG, p12)
  poslist_SSC[int(idx - 1)] = p12


def get_abspos_SSC_pan():
  """
  Devuelve el angulo de giro (pan) en el que esta la camara ahora mismo.

  Returns:
    La posicion actual del eje de giro.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.log(logging.TRACE0, '-')
  return (get_abspos())[5]


def get_abspos_SSC_tilt():
  """
  Devuelve el angulo de inclinacion (tilt) en el que esta la camara ahora.

  Returns:
    La posicion actual del eje de inclinacion.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.log(logging.TRACE0, '-')
  return (get_abspos())[6]


def log_abspos_SSC():
  """
  Escribe en el log la posicion actual de la camara (pan y tilt).

  Solo sirve para depurar: no mueve nada, solo lee donde esta apuntando
  la camara y lo apunta en el registro.

  Returns:
    None.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  abspos_SSC = (get_abspos())[5 : 7]
  if abspos_SSC[ : 6] == None or abspos_SSC[ : 7] == None:
    logging.debug('abspos_SSC=None')
  else:
    logging.debug('abspos_SSC=%d %d', abspos_SSC[0], abspos_SSC[1])


def movePosCenter_SSC():
  """
  Mueve la camara a su posicion central, mirando de frente a la fabrica.

  Returns:
    None.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  moveAbs_SSC_P12_list(poslist_SSC[0])


def movePosHBW_SSC():
  """
  Mueve la camara para que apunte hacia el almacen (HBW).

  Returns:
    None.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  moveAbs_SSC_P12_list(poslist_SSC[1])


def movePosPark_SSC():
  """
  Mueve la camara a su posicion de aparcado.

  Returns:
    None.
  """
  global _data, name, num, value, idx, rv1, rv2, poslist, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  moveAbs_SSC_P12_list(poslist_SSC[2])


def moveRef_SSC_P12():
  """
  Lleva la camara a su posicion de referencia (pan y tilt a la vez).

  Mueve el giro y la inclinacion en paralelo, cada uno en su propio hilo,
  hasta que llegan a su final de carrera.

  Returns:
    None.
  """
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
  """
  Mueve pan y tilt de la camara a la vez, cada uno una cantidad relativa.

  Args:
    rv1: Cuanto girar (pan) respecto a la posicion actual.
    rv2: Cuanto inclinar (tilt) respecto a la posicion actual.

  Returns:
    None.
  """
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
  """
  Igual que ``moveRel_SSC_P12`` pero recibiendo los dos valores en lista.

  Args:
    poslist: Lista ``[rv1, rv2]`` con lo que hay que girar e inclinar
      respecto a la posicion actual.

  Returns:
    None.
  """
  global _data, name, num, value, idx, rv1, rv2, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.debug('poslist=%d %d', poslist[0], poslist[1])
  moveRel_SSC_P12(poslist[0], poslist[1])


def moveAbs_SSC_P12(av1, av2):
  """
  Lleva pan y tilt de la camara a unas coordenadas exactas, a la vez.

  Args:
    av1: Angulo de giro (pan) al que moverse.
    av2: Angulo de inclinacion (tilt) al que moverse.

  Returns:
    None.
  """
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
  """
  Igual que ``moveAbs_SSC_P12`` pero recibiendo las coordenadas en lista.

  Args:
    poslist: Lista ``[pan, tilt]`` con la posicion absoluta a la que
      moverse.

  Returns:
    None.
  """
  global _data, name, num, value, idx, rv1, rv2, av1, av2, lockSSC, p12, abspos_SSC, poslist_SSC_defaults, poslist_SSC
  logging.debug('poslist=%d %d', poslist[0], poslist[1])
  moveAbs_SSC_P12(poslist[0], poslist[1])


