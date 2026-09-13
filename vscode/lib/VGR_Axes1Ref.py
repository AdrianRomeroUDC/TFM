"""Calibración y movimientos coordinados de los tres ejes del VGR."""

# Agrupa los tres ejes VGR y protege sus movimientos con lockVGR.
import logging
import threading
from fischertechnik.controller.Motor import Motor
from lib.Axes1Ref import *
from lib.controller import *
from lib.Nfc import *
from lib.Factory_Variables import *

_data = None
name = None
num = None
value = None
rv1 = None
rv2 = None
rv3 = None
poslist = None
av1 = None
av2 = None
av3 = None
lockVGR = None
p123 = None
poslist_VGR_defaults = None
listnamepos1_discard_VGR_defaults = None
listnameoffset_VGR_defaults = None
poslist_VGR = None
listnamepos1_discard_VGR = None
listnameoffset_VGR = None
i = None
abspos_VGR = None
lockNFC = None
p1234 = None
p12 = None
temp = None
def get_pos3_VGR_name(name):
  """
  Busca las coordenadas de una posicion con nombre (por ejemplo 'HBW').

  Args:
    name: Nombre de la posicion que se busca en la tabla del VGR.

  Returns:
    Una lista ``[rotacion, altura, extension]`` con los tres valores de esa
    posicion, o ``None`` si no existe ninguna posicion con ese nombre.
  """
  global _data, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.debug('%s', name)
  p123 = None
  for p1234 in poslist_VGR:
    if p1234[0] == name:
      p123 = p1234[1 : 4]
      break
  if p123 == None:
    logging.warning('%s not found', name)
  return p123

def get_pos1_discard_VGR_name(name):
  """
  Busca la altura segura de transito guardada para una posicion con nombre.

  El brazo sube a esta altura antes de desplazarse con una pieza cogida,
  para no chocar con nada de camino a su destino.

  Args:
    name: Nombre de la posicion que se busca.

  Returns:
    La altura de transito guardada, o 0 si no hay ninguna para ese nombre.
  """
  global _data, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.debug('%s', name)
  value = 0
  p12 = None
  for p12 in listnamepos1_discard_VGR:
    if p12[0] == name:
      value = p12[1]
      logging.debug('%d', value)
      break
  return value

def get_offset_VGR_name(name):
  """
  Busca el ajuste fino guardado para una posicion con nombre.

  Es la correccion que se suma a la coordenada de esa estacion para que el
  brazo llegue justo al punto exacto de recogida o entrega.

  Args:
    name: Nombre de la posicion que se busca.

  Returns:
    El ajuste guardado, o 0 si no hay ninguno para ese nombre.
  """
  global _data, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.debug('%s', name)
  value = 0
  p12 = None
  for p12 in listnameoffset_VGR:
    if p12[0] == name:
      value = p12[1]
      logging.debug('%d', value)
      break
  return value



def get_lock_VGR():
  """
  Devuelve el cerrojo que evita que dos movimientos del VGR se pisen.

  Cualquier rutina que mueva los motores del brazo debe pedir este cerrojo
  primero, para que nunca haya dos ordenes de movimiento a la vez.

  Returns:
    El cerrojo (``threading.RLock``) del VGR.
  """
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE0_VGR, '-')
  return lockVGR


def init_VGR():
  """
  Prepara el brazo VGR para empezar a trabajar.

  Crea el cerrojo de movimiento, carga las tablas de fabrica con las
  coordenadas de cada estacion (Color Reader, DSI, DSO, HBW, MPO, NFC,
  NiO, y las tres rampas de la SLD), lleva el brazo a su posicion de
  referencia de forma segura y lo marca como listo para trabajar.

  Returns:
    None.
  """
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, '-')
  lockVGR = threading.RLock() #https://stackoverflow.com/questions/28017535/do-i-have-to-lock-all-functions-that-calls-to-one-or-more-locked-function-for-mu
  lockNFC = get_lock_NFC()
  poslist_VGR_defaults = [['Color Reader', 107, 645, 40], ['DSI', 5, 752, 5], ['DSO', 247, 290, 530], ['HBW', 1395, 0, 0], ['MPO', 911, 0, 850], ['NFC', 180, 643, 230], ['NiO', 300, 400, 0], ['SLD blue', 304, 835, 579], ['SLD red', 371, 835, 418], ['SLD white', 450, 835, 360]]
  poslist_VGR = poslist_VGR_defaults
  listnamepos1_discard_VGR_defaults = [['DSI', 550], ['DSO', 50], ['HBW', 20]]
  listnamepos1_discard_VGR = listnamepos1_discard_VGR_defaults
  listnameoffset_VGR_defaults = [['DSI', 202], ['DSO', 250], ['HBW_h', 158], ['HBW', 150], ['MPO', 490]]
  listnameoffset_VGR = listnameoffset_VGR_defaults
  moveRef_VGR_S231()
  set_vgr_ready(True)


def get_calib_data_VGR_defaults():
  """
  Devuelve las tablas de posiciones del VGR tal y como vienen de fabrica.

  Returns:
    Una lista con las tres tablas originales: posiciones, alturas de
    transito y ajustes finos, sin los cambios que se hayan hecho a mano.
  """
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, '-')
  return [poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults]


def get_calib_data_VGR():
  """
  Devuelve las tablas de posiciones que el VGR esta usando ahora mismo.

  Returns:
    Una lista con las tres tablas activas: posiciones, alturas de transito
    y ajustes finos.
  """
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, '-')
  return [poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR]


def set_calib_data_VGR(_data):
  """
  Reemplaza las tres tablas de posiciones del VGR por unas nuevas.

  Args:
    _data: Lista con las tres tablas nuevas, en este orden: posiciones,
      alturas de transito y ajustes finos.

  Returns:
    None.
  """
  global name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, _data)
  poslist_VGR = _data[0]
  listnamepos1_discard_VGR = _data[1]
  listnameoffset_VGR = _data[2]


def set_pos3_VGR_name_num(name, num, value):
  """
  Cambia una de las tres coordenadas de una posicion con nombre.

  Args:
    name: Nombre de la posicion que se quiere corregir (por ejemplo 'HBW').
    num: Que eje se corrige: 1 = rotacion, 2 = altura, 3 = extension.
    value: Nuevo valor para ese eje.

  Returns:
    None.
  """
  global _data, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.debug('%s %d %d', name, num, value)
  i = 1
  for p1234 in poslist_VGR:
    if p1234[0] == name:
      p1234[int((num + 1) - 1)] = value
      logging.debug(p1234)
      poslist_VGR[int(i - 1)] = p1234
      break
    i = (i if isinstance(i, (int, float)) else 0) + 1


def set_pos1_discard_VGR_name(name, value):
  """
  Cambia la altura segura de transito de una posicion con nombre.

  Args:
    name: Nombre de la posicion que se quiere corregir.
    value: Nueva altura de transito.

  Returns:
    None.
  """
  global _data, num, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.debug('%s %d', name, value)
  i = 1
  for p12 in listnamepos1_discard_VGR:
    if p12[0] == name:
      p12[1] = value
      logging.debug(p12)
      listnamepos1_discard_VGR[int(i - 1)] = p12
      break
    i = (i if isinstance(i, (int, float)) else 0) + 1


def set_offset_VGR_name(name, value):
  """
  Cambia el ajuste fino de una posicion con nombre.

  Args:
    name: Nombre de la posicion que se quiere corregir.
    value: Nuevo ajuste.

  Returns:
    None.
  """
  global _data, num, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.debug('%s %d', name, value)
  i = 1
  for p12 in listnameoffset_VGR:
    if p12[0] == name:
      p12[1] = value
      logging.debug(p12)
      listnameoffset_VGR[int(i - 1)] = p12
      break
    i = (i if isinstance(i, (int, float)) else 0) + 1


def log_abspos_VGR():
  """
  Escribe en el log la posicion actual de los tres ejes del VGR.

  Solo sirve para depurar: no mueve nada, solo lee donde esta el brazo
  ahora mismo y lo apunta en el registro.

  Returns:
    None.
  """
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, '-')
  abspos_VGR = (get_abspos())[ : 3]
  if abspos_VGR[ : 1] == None or abspos_VGR[ : 2] == None or abspos_VGR[ : 3] == None:
    logging.log(logging.DEBUG_VGR, 'abspos_VGR=None')
  else:
    logging.log(logging.DEBUG_VGR, 'abspos_VGR=%d %d %d', abspos_VGR[0], abspos_VGR[1], abspos_VGR[2])


def stop_VGR():
  """
  Para en seco los tres motores del brazo VGR.

  Se usa como parada de emergencia: corta el giro, la subida/bajada y la
  extension del brazo de golpe, con el cerrojo puesto para que nadie mas
  intente moverlo a la vez.

  Returns:
    None.
  """
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, '-')
  lockVGR.acquire()
  TXT_VGR_E2_M1_encodermotor.stop_sync()
  TXT_VGR_E2_M2_encodermotor.stop_sync()
  TXT_VGR_E2_M3_encodermotor.stop_sync()
  lockVGR.release()


def moveRef_VGR_P123():
  """
  Lleva los tres ejes del brazo a su posicion de referencia a la vez.

  Mueve rotacion, altura y extension en paralelo (cada uno en su propio
  hilo) hasta que cada uno toca su final de carrera, y espera a que los
  tres terminen antes de continuar.

  Returns:
    None.
  """
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, '->')
  lockVGR.acquire()
  th1 = threading.Thread(target=moveRef, args=(1, ), daemon=True)
  th2 = threading.Thread(target=moveRef, args=(2, ), daemon=True)
  th3 = threading.Thread(target=moveRef, args=(3, ), daemon=True)
  th1.start()
  th2.start()
  th3.start()
  th1.join()
  th2.join()
  th3.join()
  lockVGR.release()
  logging.log(logging.TRACE_VGR, '<-')


def moveRef_VGR_S231():
  """
  Lleva los tres ejes a su posicion de referencia, en orden seguro.

  Primero sube el brazo (eje 2), luego lo retrae (eje 3) y por ultimo gira
  hasta el tope (eje 1). Hacerlo en este orden evita que el brazo choque
  con otras piezas de la fabrica al girar.

  Returns:
    None.
  """
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, '->')
  lockVGR.acquire()
  moveRef(2)
  moveRef(3)
  moveRef(1)
  lockVGR.release()
  logging.log(logging.TRACE_VGR, '<-')


def moveRef_VGR_S23():
  """
  Sube y retrae el brazo hasta sus topes, sin tocar la rotacion.

  Lleva a su posicion de referencia primero la altura (eje 2) y luego la
  extension (eje 3), dejando el giro (eje 1) como estaba.

  Returns:
    None.
  """
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, '->')
  lockVGR.acquire()
  moveRef(2)
  moveRef(3)
  lockVGR.release()
  logging.log(logging.TRACE_VGR, '<-')


def moveRel_VGR_P123(rv1, rv2, rv3):
  """
  Mueve los tres ejes del brazo a la vez, cada uno una cantidad relativa.

  Rota, sube/baja y extiende/retrae en paralelo (un hilo por eje), y
  espera a que los tres terminen antes de seguir.

  Args:
    rv1: Cuanto girar respecto a la posicion actual.
    rv2: Cuanto subir o bajar respecto a la posicion actual.
    rv3: Cuanto extender o retraer respecto a la posicion actual.

  Returns:
    None.
  """
  global _data, name, num, value, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'rv=%d %d %d', rv1, rv2, rv3)
  lockVGR.acquire()
  th1 = threading.Thread(target=moveRel, args=(1,rv1, ), daemon=True)
  th2 = threading.Thread(target=moveRel, args=(2,rv2, ), daemon=True)
  th3 = threading.Thread(target=moveRel, args=(3,rv3, ), daemon=True)
  th1.start()
  th2.start()
  th3.start()
  th1.join()
  th2.join()
  th3.join()
  log_abspos_VGR()
  lockVGR.release()


def moveRel_VGR_P123_list(poslist):
  """
  Igual que ``moveRel_VGR_P123`` pero recibiendo los tres valores en lista.

  Args:
    poslist: Lista ``[rv1, rv2, rv3]`` con lo que hay que girar, subir y
      extender respecto a la posicion actual.

  Returns:
    None.
  """
  global _data, name, num, value, rv1, rv2, rv3, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'poslist=%d %d %d', poslist[0], poslist[1], poslist[2])
  moveRel_VGR_P123(poslist[0], poslist[1], poslist[2])


def moveRel_VGR_S123(rv1, rv2, rv3):
  """
  Mueve los tres ejes del brazo uno detras de otro, cada uno relativo.

  Primero gira, luego sube o baja y por ultimo extiende o retrae, esperando
  a que cada eje termine antes de mover el siguiente.

  Args:
    rv1: Cuanto girar respecto a la posicion actual.
    rv2: Cuanto subir o bajar respecto a la posicion actual.
    rv3: Cuanto extender o retraer respecto a la posicion actual.

  Returns:
    None.
  """
  global _data, name, num, value, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'rv=%d %d %d', rv1, rv2, rv3)
  lockVGR.acquire()
  moveRel(1, rv1)
  moveRel(2, rv2)
  moveRel(3, rv3)
  log_abspos_VGR()
  lockVGR.release()


def moveRel_VGR_S123_list(poslist):
  """
  Igual que ``moveRel_VGR_S123`` pero recibiendo los tres valores en lista.

  Args:
    poslist: Lista ``[rv1, rv2, rv3]`` con lo que hay que girar, subir y
      extender respecto a la posicion actual.

  Returns:
    None.
  """
  global _data, name, num, value, rv1, rv2, rv3, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'poslist=%d %d %d', poslist[0], poslist[1], poslist[2])
  moveRel_VGR_S123(poslist[0], poslist[1], poslist[2])


def moveAbs_VGR_P123(av1, av2, av3):
  """
  Lleva los tres ejes del brazo a unas coordenadas exactas, a la vez.

  Rota, sube/baja y extiende/retrae en paralelo (un hilo por eje) hasta
  llegar a la posicion absoluta indicada en cada uno.

  Args:
    av1: Angulo de rotacion al que moverse.
    av2: Altura a la que moverse.
    av3: Extension a la que moverse.

  Returns:
    None.
  """
  global _data, name, num, value, rv1, rv2, rv3, poslist, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'av=%d %d %d', av1, av2, av3)
  lockVGR.acquire()
  th1 = threading.Thread(target=moveAbs, args=(1,av1, ), daemon=True)
  th2 = threading.Thread(target=moveAbs, args=(2,av2, ), daemon=True)
  th3 = threading.Thread(target=moveAbs, args=(3,av3, ), daemon=True)
  th1.start()
  th2.start()
  th3.start()
  th1.join()
  th2.join()
  th3.join()
  log_abspos_VGR()
  lockVGR.release()


def moveAbs_VGR_P123_list(poslist):
  """
  Igual que ``moveAbs_VGR_P123`` pero recibiendo las coordenadas en lista.

  Args:
    poslist: Lista ``[rotacion, altura, extension]`` con la posicion
      absoluta a la que moverse.

  Returns:
    None.
  """
  global _data, name, num, value, rv1, rv2, rv3, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'poslist=%d %d %d', poslist[0], poslist[1], poslist[2])
  moveAbs_VGR_P123(poslist[0], poslist[1], poslist[2])


def moveAbs_VGR_P123_name(name):
  """
  Mueve el brazo a una posicion de la fabrica llamandola por su nombre.

  Busca las coordenadas guardadas para ese nombre (por ejemplo 'HBW' o
  'NFC') y mueve los tres ejes a la vez hasta llegar alli. Si el nombre no
  existe, no mueve nada.

  Args:
    name: Nombre de la posicion a la que ir.

  Returns:
    None.
  """
  global _data, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'name=%s', name)
  if name != None:
    temp = get_pos3_VGR_name(name)
    if temp != None and len(temp) == 3:
      moveAbs_VGR_P123_list(temp)


def moveAbs_VGR_S123(av1, av2, av3):
  """
  Lleva los tres ejes del brazo a unas coordenadas exactas, uno a uno.

  Primero gira hasta el angulo indicado, luego sube o baja y por ultimo
  extiende o retrae, esperando a que cada eje termine antes del siguiente.

  Args:
    av1: Angulo de rotacion al que moverse.
    av2: Altura a la que moverse.
    av3: Extension a la que moverse.

  Returns:
    None.
  """
  global _data, name, num, value, rv1, rv2, rv3, poslist, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'av=%d %d %d', av1, av2, av3)
  lockVGR.acquire()
  moveAbs(1, av1)
  moveAbs(2, av2)
  moveAbs(3, av3)
  log_abspos_VGR()
  lockVGR.release()


def moveAbs_VGR_S123_list(poslist):
  """
  Igual que ``moveAbs_VGR_S123`` pero recibiendo las coordenadas en lista.

  Args:
    poslist: Lista ``[rotacion, altura, extension]`` con la posicion
      absoluta a la que moverse.

  Returns:
    None.
  """
  global _data, name, num, value, rv1, rv2, rv3, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  logging.log(logging.TRACE_VGR, 'poslist=%d %d %d', poslist[0], poslist[1], poslist[2])
  moveAbs_VGR_S123(poslist[0], poslist[1], poslist[2])


def get_abspos_VGR():
  """
  Devuelve la posicion exacta en la que estan ahora mismo los tres ejes.

  Returns:
    Una lista ``[rotacion, altura, extension]`` con la posicion actual del
    brazo.
  """
  global _data, name, num, value, rv1, rv2, rv3, poslist, av1, av2, av3, lockVGR, p123, poslist_VGR_defaults, listnamepos1_discard_VGR_defaults, listnameoffset_VGR_defaults, poslist_VGR, listnamepos1_discard_VGR, listnameoffset_VGR, i, abspos_VGR, lockNFC, p1234, p12, temp
  return (get_abspos())[ : 3]


