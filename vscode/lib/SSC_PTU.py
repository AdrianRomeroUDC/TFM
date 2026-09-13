"""Control de la cámara SSC (Station Supervisory Controller).

SSC mueve la cámara PTU mediante los motores con encoder ``TXT_SSC_M_M1``
(pan/rotación) y ``TXT_SSC_M_M2`` (tilt/subida y bajada), ambos en el
controlador maestro. ``processCmd`` lanza cada orden en un hilo daemon. Las
operaciones de referencia y parada sincronizada delegan en
``SSC_PTU_Axes1Ref``, donde se usa ``threading.RLock`` para proteger los
movimientos; este módulo no crea locks propios.
"""

# Recibe comandos PTU y los ejecuta en hilos para no bloquear MQTT.
import logging
import math
from fischertechnik.controller.Motor import Motor
from lib.Axes1Ref import *
from lib.controller import *
from lib.SSC_PTU_Axes1Ref import *
from lib.SSC_Publisher import *

cmd = None
degree = None
pos_pan = None
pos_tilt = None
posnew_pan = None
posnew_tilt = None


def processCmd(cmd, degree):
  """Despacha una orden de movimiento de la camara PTU a un hilo daemon.

  Args:
    cmd: Comando PTU, como ``stop``, ``home``, ``park`` o un movimiento.
    degree: Magnitud del movimiento relativo en grados de interfaz.

  Returns:
    None. Inicia el movimiento solicitado y retorna sin bloquear al callback.
  """
  global pos_pan, pos_tilt, posnew_pan, posnew_tilt
  logging.log(logging.TRACE, '-')
  print("processCmd: ", cmd, degree)
  if cmd == 'stop':
    threading.Thread(target=stop_SSC, daemon=True).start()
  elif cmd == 'home':
    threading.Thread(target=movePosCenter_SSC, daemon=True).start()
  elif cmd == 'start_pan':
    threading.Thread(target=start_pan, daemon=True).start()
  elif cmd == 'end_pan':
    threading.Thread(target=end_pan, daemon=True).start()
  elif cmd == 'start_tilt':
    threading.Thread(target=start_tilt, daemon=True).start()
  elif cmd == 'end_tilt':
    threading.Thread(target=end_tilt, daemon=True).start()
  elif cmd == 'relmove_left':
    threading.Thread(target=move_left, args=(degree, ), daemon=True).start()
  elif cmd == 'relmove_right':
    threading.Thread(target=move_right, args=(degree, ), daemon=True).start()
  elif cmd == 'relmove_up':
    threading.Thread(target=move_up, args=(degree, ), daemon=True).start()
  elif cmd == 'relmove_down':
    threading.Thread(target=move_down, args=(degree, ), daemon=True).start()
  elif cmd == 'hbw':
    threading.Thread(target=movePosHBW_SSC, daemon=True).start()
  elif cmd == 'park':
    threading.Thread(target=movePosPark_SSC, daemon=True).start()


def parkSSC():
  """
  Lleva la camara a su posicion de aparcado y avisa de la nueva posicion.

  Returns:
    None.
  """
  global cmd, degree, pos_pan, pos_tilt, posnew_pan, posnew_tilt
  logging.log(logging.TRACE, '-')
  movePosPark_SSC()
  publish_ptu_pos()


def start_pan():
  """
  Gira la camara hasta el extremo izquierdo de su giro (pan al minimo).

  Returns:
    None.
  """
  global cmd, degree, pos_pan, pos_tilt, posnew_pan, posnew_tilt
  logging.log(logging.TRACE, '-')
  moveAbs(6, 0)
  publish_ptu_pos()


def end_pan():
  """
  Gira la camara hasta el extremo derecho de su giro (pan al maximo).

  Returns:
    None.
  """
  global cmd, degree, pos_pan, pos_tilt, posnew_pan, posnew_tilt
  logging.log(logging.TRACE, '-')
  moveAbs(6, (get_ABSLIMIT())[5])
  publish_ptu_pos()


def start_tilt():
  """
  Inclina la camara hasta el extremo inferior de su recorrido.

  Returns:
    None.
  """
  global cmd, degree, pos_pan, pos_tilt, posnew_pan, posnew_tilt
  logging.log(logging.TRACE, '-')
  moveAbs(7, 0)
  publish_ptu_pos()


def end_tilt():
  """
  Inclina la camara hasta el extremo superior de su recorrido.

  Returns:
    None.
  """
  global cmd, degree, pos_pan, pos_tilt, posnew_pan, posnew_tilt
  logging.log(logging.TRACE, '-')
  moveAbs(7, (get_ABSLIMIT())[6])
  publish_ptu_pos()


def stop_SSC():
  """
  Para en seco los motores de giro e inclinacion de la camara.

  Returns:
    None.
  """
  global cmd, degree, pos_pan, pos_tilt, posnew_pan, posnew_tilt
  logging.log(logging.TRACE, '-')
  TXT_SSC_M_M1_encodermotor.stop_sync()
  TXT_SSC_M_M2_encodermotor.stop_sync()
  publish_ptu_pos()


def move_left(degree):
  """
  Gira la camara un poco hacia la izquierda.

  Resta los grados indicados a la posicion actual de giro, sin pasarse de
  los limites mecanicos, y mueve la camara hasta ese nuevo angulo.

  Args:
    degree: Cuantos grados girar hacia la izquierda.

  Returns:
    None.
  """
  global cmd, pos_pan, pos_tilt, posnew_pan, posnew_tilt
  logging.log(logging.TRACE, 'degree: %f', degree)
  pos_pan = get_abspos_SSC_pan()
  logging.log(logging.DEBUG, pos_pan)
  if pos_pan != None:
    posnew_pan = pos_pan + -(degree * 10)
    logging.log(logging.DEBUG, posnew_pan)
    posnew_pan = min(max(posnew_pan, 0), (get_ABSLIMIT())[5])
    logging.log(logging.DEBUG, posnew_pan)
    moveAbs(6, posnew_pan)
    publish_ptu_pos()


def move_right(degree):
  """
  Gira la camara un poco hacia la derecha.

  Suma los grados indicados a la posicion actual de giro, sin pasarse de
  los limites mecanicos, y mueve la camara hasta ese nuevo angulo.

  Args:
    degree: Cuantos grados girar hacia la derecha.

  Returns:
    None.
  """
  global cmd, pos_pan, pos_tilt, posnew_pan, posnew_tilt
  logging.log(logging.TRACE, 'degree: %f', degree)
  pos_pan = get_abspos_SSC_pan()
  logging.log(logging.DEBUG, pos_pan)
  if pos_pan != None:
    posnew_pan = pos_pan + degree * 10
    logging.log(logging.DEBUG, posnew_pan)
    posnew_pan = min(max(posnew_pan, 0), (get_ABSLIMIT())[5])
    logging.log(logging.DEBUG, posnew_pan)
    moveAbs(6, posnew_pan)
    publish_ptu_pos()


def move_down(degree):
  """
  Inclina la camara un poco hacia abajo.

  Resta los grados indicados a la inclinacion actual, sin pasarse de los
  limites mecanicos, y mueve la camara hasta ese nuevo angulo.

  Args:
    degree: Cuantos grados inclinar hacia abajo.

  Returns:
    None.
  """
  global cmd, pos_pan, pos_tilt, posnew_pan, posnew_tilt
  logging.log(logging.TRACE, 'degree: %f', degree)
  pos_tilt = get_abspos_SSC_tilt()
  logging.log(logging.DEBUG, pos_tilt)
  if pos_tilt != None:
    posnew_tilt = pos_tilt + -(degree * 10)
    logging.log(logging.DEBUG, posnew_tilt)
    posnew_tilt = min(max(posnew_tilt, 0), (get_ABSLIMIT())[6])
    logging.log(logging.DEBUG, posnew_tilt)
    moveAbs(7, posnew_tilt)
    publish_ptu_pos()


def move_up(degree):
  """
  Inclina la camara un poco hacia arriba.

  Suma los grados indicados a la inclinacion actual, sin pasarse de los
  limites mecanicos, y mueve la camara hasta ese nuevo angulo.

  Args:
    degree: Cuantos grados inclinar hacia arriba.

  Returns:
    None.
  """
  global cmd, pos_pan, pos_tilt, posnew_pan, posnew_tilt
  logging.log(logging.TRACE, 'degree: %f', degree)
  pos_tilt = get_abspos_SSC_tilt()
  logging.log(logging.DEBUG, pos_tilt)
  if pos_tilt != None:
    posnew_tilt = pos_tilt + degree * 10
    logging.log(logging.DEBUG, posnew_tilt)
    posnew_tilt = min(max(posnew_tilt, 0), (get_ABSLIMIT())[6])
    logging.log(logging.DEBUG, posnew_tilt)
    moveAbs(7, posnew_tilt)
    publish_ptu_pos()


