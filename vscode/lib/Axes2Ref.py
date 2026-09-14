"""Primitivas de referencia y movimiento coordinado para ejes con dos referencias (Finales de carrera)."""

# Capa de movimiento para actuadores con finales de carrera.
import logging
import math
import os
import sys
import time
from fischertechnik.controller.Motor import Motor
from lib.controller import *

_tr0 = None
_tr = None
_dg = None
num = None
msg = None
_b_exit = None
SPEED = None
tsdiff = None
TIMEOUT_S = None
ts0 = None

# Inicializa los niveles de log específicos para el control de los ejes
def initlog_A2R(_tr0, _tr, _dg):
  """
  Configura los niveles de log para A2R.

  Args:
    _tr0: Nivel numérico para el trazado detallado.
    _tr: Nivel numérico para el trazado normal.
    _dg: Nivel numérico para los mensajes de depuración.

  Returns:
    None.
  """
  global num, msg, _b_exit, SPEED, tsdiff, TIMEOUT_S, ts0
  logging.TRACE0_A2R = _tr0
  logging.addLevelName(logging.TRACE0_A2R , 'TRACE0_A2R')
  logging.TRACE_A2R = _tr
  logging.addLevelName(logging.TRACE_A2R , 'TRACE_A2R')
  logging.DEBUG_A2R = _dg
  logging.addLevelName(logging.DEBUG_A2R, 'DEBUG_A2R')

# Configura los limites físicos, velocidades y tiempos de espera (timeouts) para cada eje
def initlib_Axes2Ref():
  """
  Inicializa límites, velocidades y timeouts para los ejes A2R.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, num, msg, _b_exit, SPEED, tsdiff, TIMEOUT_S, ts0
  __version__ = '2022-07-25' #Axes2Ref (A2R)
  logging.log(logging.TRACE_A2R, '-')
  logging.log(logging.DEBUG_A2R, 'Axes2Ref (A2R) %s', __version__)

  # num: #MX, IX
  #   1: HBW z rear           # HBW_E1_M3, HBW_E1_I6
  #   2: HBW z front          # HBW_E1_M3, HBW_E1_I7
  #   3: MPO suction tt     # MPO_E3_M2, MPO_E4_I5
  #   4: MPO suction ov    # MPO_E3_M2, MPO_E3_I3
  #   5: MPO kiln slid in     # MPO_E3_M1, MPO_E3_I1
  #   6: MPO kiln slid out  # MPO_E3_M1, MPO_E3_I2
  #   7: MPO turntab suc  # MPO_E4_M1, MPO_E4_I1
  #   8: MPO turntab sa1  # MPO_E4_M1, MPO_E4_I2
  #   9: MPO turntab con  # MPO_E4_M1, MPO_E4_I3
  # 10: MPO turntab sa2  # MPO_E4_M1, MPO_E4_I2

  SPEED = 512
  TIMEOUT_S = [12.8, 12.8, 46, 46, 9.8, 9.8, 7.2, 5.6, 5.6, 7.2]
  ts0 = [0] * len(TIMEOUT_S)
  tsdiff = [0] * len(TIMEOUT_S)
  _b_exit = False

# Comprueba si un eje ha excedido su tiempo de movimiento permitido y detiene el programa si es así
def _check_timeout_exit(num):
  """
  Comprueba si un movimiento lleva demasiado tiempo sin llegar a su tope.

  Si el eje indicado supera el tiempo maximo permitido sin activar su
  final de carrera (por ejemplo, porque esta atascado), detiene todo el
  programa como medida de seguridad.

  Args:
    num: Numero del eje que se esta vigilando.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, msg, _b_exit, SPEED, tsdiff, TIMEOUT_S, ts0
  logging.log(logging.TRACE0_A2R, num)
  tsdiff[int(num - 1)] = time.time() - ts0[int(num - 1)]
  if tsdiff[int(num - 1)] > TIMEOUT_S[int(num - 1)]:
    logging.warning('A2R: timeout[%d] %.1f (%.1f), exit program', num, tsdiff[num-1], TIMEOUT_S[num-1])
    _exit_Axe2Ref('timeout {}s (max: {}s)'.format(round(tsdiff[int(num - 1)], 1), TIMEOUT_S[int(num - 1)]))
  time.sleep(0.001)

# Fuerza el cierre del programa de ejes y marca las referencias como no válidas tras un error
def _exit_Axe2Ref(msg):
  """
  Para todo el programa de golpe porque ha ocurrido un error grave de movimiento.

  Se llama cuando un eje se ha quedado atascado demasiado tiempo. Marca las
  referencias como no validas y cierra el programa entero para evitar
  seguir moviendo hardware en un estado desconocido.

  Args:
    msg: Mensaje que explica que ha fallado, para dejarlo en el log.

  Returns:
    None. El programa se cierra dentro de esta funcion.
  """
  global _tr0, _tr, _dg, num, _b_exit, SPEED, tsdiff, TIMEOUT_S, ts0
  logging.log(logging.TRACE_A2R, '-')
  _b_exit = True
  msg = 'error in Axes2Ref: {}'.format(msg)
  logging.error('A2R: %s ', msg)
  os._exit(os.EX_OK)

# Mueve el eje hacia el final de carrera de referencia para calibrar el punto 0
def move2Ref(num):
  """
  Mueve el actuador seleccionado hasta su final de carrera de referencia.

  Args:
    num: Número lógico del eje o elemento que se procesa.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, msg, _b_exit, SPEED, tsdiff, TIMEOUT_S, ts0
  logging.log(logging.TRACE_A2R, '-')
  if num < 1 or num > len(TIMEOUT_S):
    logging.error('A2R: num out of bounds: %d', num)
    return
  logging.log(logging.DEBUG_A2R, 'num=%d', num)
  ts0[int(num - 1)] = time.time()
  # Retraer brazo almacén
  if num == 1:
    TXT_HBW_E1_M3_motor.set_speed(int(SPEED), Motor.CCW)
    TXT_HBW_E1_M3_motor.start()
    while not ((TXT_HBW_E1_I6_mini_switch.is_closed()) or _b_exit):
      _check_timeout_exit(num)
    TXT_HBW_E1_M3_motor.stop()
  # Extender brazo almacén
  elif num == 2:
    TXT_HBW_E1_M3_motor.set_speed(int(SPEED), Motor.CW)
    TXT_HBW_E1_M3_motor.start()
    while not ((TXT_HBW_E1_I7_mini_switch.is_closed()) or _b_exit):
      _check_timeout_exit(num)
    TXT_HBW_E1_M3_motor.stop()
  # Mover el brazo horno en posición de la turntable
  elif num == 3:
    TXT_MPOov_E3_M2_motor.set_speed(int(SPEED), Motor.CCW)
    TXT_MPOov_E3_M2_motor.start()
    move2ref_state["move2Ref3"] = True
    while not ((TXT_MPOmi_E4_I5_mini_switch.is_closed()) or _b_exit):
      _check_timeout_exit(num)
    TXT_MPOov_E3_M2_motor.stop()
    move2ref_state["move2Ref3"] = False
  # Mover el brazo horno en posción del horno
  elif num == 4:
    TXT_MPOov_E3_M2_motor.set_speed(int(SPEED), Motor.CW)
    TXT_MPOov_E3_M2_motor.start()
    move2ref_state["move2Ref4"] = True
    while not ((TXT_MPOov_E3_I3_mini_switch.is_closed()) or _b_exit):
      _check_timeout_exit(num)
    TXT_MPOov_E3_M2_motor.stop()
    move2ref_state["move2Ref4"] = False
  # Mover la pieza del horno para dentro
  elif num == 5:
    TXT_MPOov_E3_M1_motor.set_speed(int(SPEED), Motor.CW)
    TXT_MPOov_E3_M1_motor.start()
    move2ref_state["move2Ref5"] = True
    while not ((TXT_MPOov_E3_I1_mini_switch.is_closed()) or _b_exit):
      _check_timeout_exit(num)
    TXT_MPOov_E3_M1_motor.stop()
    move2ref_state["move2Ref5"] = False
  # Mover la pieza del horno para fuera
  elif num == 6:
    TXT_MPOov_E3_M1_motor.set_speed(int(SPEED), Motor.CCW)
    TXT_MPOov_E3_M1_motor.start()
    move2ref_state["move2Ref6"] = True
    while not ((TXT_MPOov_E3_I2_mini_switch.is_closed()) or _b_exit):
      _check_timeout_exit(num)
    TXT_MPOov_E3_M1_motor.stop()
    move2ref_state["move2Ref6"] = False
  # Mover turntable a la posición del brazo horno
  elif num == 7:
    TXT_MPOmi_E4_M1_motor.set_speed(int(SPEED), Motor.CCW)
    TXT_MPOmi_E4_M1_motor.start()
    move2ref_state["move2Ref7"] = True
    while not ((TXT_MPOmi_E4_I1_mini_switch.is_closed()) or _b_exit):
      _check_timeout_exit(num)
    TXT_MPOmi_E4_M1_motor.stop()
    move2ref_state["move2Ref7"] = False
  # Mover turntable a la posición de la fresa (antihorario)
  elif num == 8:
    TXT_MPOmi_E4_M1_motor.set_speed(int(SPEED), Motor.CCW)
    TXT_MPOmi_E4_M1_motor.start()
    move2ref_state["move2Ref8"] = True
    while not ((TXT_MPOmi_E4_I2_mini_switch.is_closed()) or _b_exit):
      _check_timeout_exit(num)
    TXT_MPOmi_E4_M1_motor.stop()
    move2ref_state["move2Ref8"] = False
  # Mover turntable a la posición cinta salida
  elif num == 9:
    TXT_MPOmi_E4_M1_motor.set_speed(int(SPEED), Motor.CW)
    TXT_MPOmi_E4_M1_motor.start()
    move2ref_state["move2Ref9"] = True
    while not ((TXT_MPOmi_E4_I3_mini_switch.is_closed()) or _b_exit):
      _check_timeout_exit(num)
    TXT_MPOmi_E4_M1_motor.stop()
    move2ref_state["move2Ref9"] = False
  # Mover turntable a la posición de la fresa (horario)
  elif num == 10:
    TXT_MPOmi_E4_M1_motor.set_speed(int(SPEED), Motor.CW)
    TXT_MPOmi_E4_M1_motor.start()
    move2ref_state["move2Ref10"] = True
    while not ((TXT_MPOmi_E4_I2_mini_switch.is_closed()) or _b_exit):
      _check_timeout_exit(num)
    TXT_MPOmi_E4_M1_motor.stop()
    move2ref_state["move2Ref10"] = False
  logging.log(logging.DEBUG_A2R, 'stop')



# Estado de los movimientos de referencia de la estación MPO/HBW.
move2ref_state = {
    "move2Ref3": False,
    "move2Ref4": False,
    "move2Ref5": False,
    "move2Ref6": False,
    "move2Ref7": False,
    "move2Ref8": False,
    "move2Ref9": False,
    "move2Ref10": False,
}

def get_move2ref_state():
  """
  Devuelve una copia del estado de los movimientos de referencia.

  Returns:
    Un diccionario que dice, para cada eje de MPO/HBW con dos referencias,
    si esta en ese momento moviendose hacia su final de carrera.
  """
  return move2ref_state.copy()