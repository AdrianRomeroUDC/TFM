"""Primitivas de referencia, posición absoluta y movimiento de ejes con encoder y una referencia (Final de carrera)."""

# Capa comun de movimiento: encoders y finales de carrera para VGR, HBW y SSC.
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
rv = None
av = None
abspos = None
ABSLIMIT = None
_b_exit = None
tsdiff = None
_ref_valid = None
SPEED = None
ts0 = None
temp = None
_ref_last = None
SPEED_REF = None
TIMEOUT_S = None

# Estado de seguimiento en vivo de los siete ejes físicos.
live_abspos = [0, 0, 0, 0, 0, 0, 0]
live_base_enc = [0, 0, 0, 0, 0, 0, 0]
live_motion_dir = [0, 0, 0, 0, 0, 0, 0]
live_offset_abs = [0, 0, 0, 0, 0, 0, 0]
live_last_delta = [0, 0, 0, 0, 0, 0, 0]
first_live_delta = [False, False, False, False, False, False, False]
live_ref_mode = [False, False, False, False, False, False, False]


def begin_live_motion(axis, direction, ref_mode=False):
  """Inicia el seguimiento de la posición de un eje mediante su encoder.

  Args:
    axis: Número lógico del eje, entre 1 y 7.
    direction: Sentido de movimiento usado para actualizar la posición.
    ref_mode: ``True`` para interpretar el movimiento como búsqueda de referencia.

  Returns:
    None.
  """
  global live_base_enc, live_motion_dir, first_live_delta, live_ref_mode
  # Ejes lógicos: VGR (1-3), HBW (4-5) y SSC (6-7).
  i = axis - 1
  if i == 0:
    live_base_enc[i] = TXT_VGR_E2_C1_motor_step_counter.get_count()
  elif i == 1:
    live_base_enc[i] = TXT_VGR_E2_C2_motor_step_counter.get_count()
  elif i == 2:
    live_base_enc[i] = TXT_VGR_E2_C3_motor_step_counter.get_count()
  elif i == 3:
    live_base_enc[i] = TXT_HBW_E1_C2_motor_step_counter.get_count()
  elif i == 4:
    live_base_enc[i] = TXT_HBW_E1_C4_motor_step_counter.get_count()
  elif i == 5:
    live_base_enc[i] = TXT_SSC_M_C1_motor_step_counter.get_count()
  elif i == 6:
    live_base_enc[i] = TXT_SSC_M_C2_motor_step_counter.get_count()
  live_motion_dir[i] = direction
  live_ref_mode[i] = ref_mode
  first_live_delta[i] = True


def end_live_motion(axis):
  """Detiene el seguimiento en vivo del eje indicado.

  Args:
    axis: Número lógico del eje cuyo movimiento debe finalizar.

  Returns:
    None.
  """
  global live_motion_dir, live_ref_mode
  live_motion_dir[axis - 1] = 0
  live_ref_mode[axis - 1] = False


def update_live():
  """Actualiza las posiciones en vivo a partir de los contadores de encoder.

  Returns:
    None.
  """
  global live_abspos, first_live_delta, live_last_delta
  # Se consulta una vez cada encoder para calcular el desplazamiento del ciclo.
  current_encoders = [
    TXT_VGR_E2_C1_motor_step_counter.get_count(),
    TXT_VGR_E2_C2_motor_step_counter.get_count(),
    TXT_VGR_E2_C3_motor_step_counter.get_count(),
    TXT_HBW_E1_C2_motor_step_counter.get_count(),
    TXT_HBW_E1_C4_motor_step_counter.get_count(),
    TXT_SSC_M_C1_motor_step_counter.get_count(),
    TXT_SSC_M_C2_motor_step_counter.get_count()
  ]

  for i in range(7):
    if live_motion_dir[i] != 0:
      if live_abspos[i] is None:
        live_abspos[i] = 0
      if live_offset_abs[i] is None:
        live_offset_abs[i] = 0
      if live_last_delta[i] is None:
        live_last_delta[i] = 0

      delta = current_encoders[i] - live_base_enc[i]
      if delta == 0 and first_live_delta[i]:
        continue

      # En referencia se corrige la posición; en movimiento normal se conserva
      # el extremo más cercano para evitar retrocesos de lectura.
      if live_ref_mode[i]:
        if first_live_delta[i]:
          live_last_delta[i] = delta
          first_live_delta[i] = False
          continue

        step = delta - live_last_delta[i]
        live_last_delta[i] = delta
        live_abspos[i] = live_abspos[i] - step
        new_abs = live_abspos[i]
      else:
        first_live_delta[i] = False
        new_abs = live_offset_abs[i] + (live_motion_dir[i] * delta)
        if live_motion_dir[i] == 1:
          live_abspos[i] = max(live_abspos[i], new_abs)
        elif live_motion_dir[i] == -1:
          live_abspos[i] = min(live_abspos[i], new_abs)
        else:
          live_abspos[i] = new_abs


def get_live_abspos(axis):
  """Devuelve la posición absoluta seguida para un eje.

  Args:
    axis: Número lógico del eje consultado.

  Returns:
    Posición absoluta en pasos, o ``0`` si todavía no hay una posición válida.
  """
  if live_abspos[axis - 1] == None:
    return 0
  return live_abspos[axis - 1]


def _get_axis_step_count(num):
  """Lee el contador de pasos asociado al número lógico de eje.

  Args:
    num: Número lógico del eje, entre 1 y 7.

  Returns:
    Conteo actual del encoder; devuelve ``0`` para un eje no reconocido.
  """
  if num == 1:
    return TXT_VGR_E2_C1_motor_step_counter.get_count()
  elif num == 2:
    return TXT_VGR_E2_C2_motor_step_counter.get_count()
  elif num == 3:
    return TXT_VGR_E2_C3_motor_step_counter.get_count()
  elif num == 4:
    return TXT_HBW_E1_C2_motor_step_counter.get_count()
  elif num == 5:
    return TXT_HBW_E1_C4_motor_step_counter.get_count()
  elif num == 6:
    return TXT_SSC_M_C1_motor_step_counter.get_count()
  elif num == 7:
    return TXT_SSC_M_C2_motor_step_counter.get_count()
  return 0

def _set_abspos_live_from_counter(num, abs_start, rv):
  """Calcula la posición en vivo desde una posición inicial y el encoder.

  Args:
    num: Número lógico del eje que se actualiza.
    abs_start: Posición absoluta al comienzo del movimiento.
    rv: Sentido del movimiento relativo; positivo incrementa y negativo decrementa.

  Returns:
    None.
  """
  count = _get_axis_step_count(num)
  if rv > 0:
    live_abspos[int(num - 1)] = min(abs_start + count, ABSLIMIT[int(num - 1)])
  elif rv < 0:
    live_abspos[int(num - 1)] = max(abs_start - count, 0)
# Actualiza la posición absoluta después de un movimiento relativo.
def _update_abspos(num, rv):
  """Actualiza la posición absoluta usando el desplazamiento del encoder.

  Args:
    num: Número lógico del eje cuyo movimiento se procesa.
    rv: Sentido del movimiento relativo; positivo avanza y negativo retrocede.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, msg, av, abspos, ABSLIMIT, _b_exit, tsdiff, _ref_valid, SPEED, ts0, _ref_last, SPEED_REF, TIMEOUT_S
  logging.log(logging.TRACE0_A1R, num)
  if num < 1 or num > len(ABSLIMIT):
    logging.error('A1R: num out of bounds: %d', num)
    return
  if abspos[int(num - 1)] == None:
    logging.error('A1R: abspos[num]=None')
    return
  logging.log(logging.DEBUG_A1R, 'num=%d, rv=%d abspos[num]=%d' , num, rv, abspos[num-1])
  encoder_count = 0
# Selección del contador de pasos (encoder) según del eje (1-3: VGR, 4-5: HBW, 6-7: SSC)
  if num == 1:
    encoder_count = TXT_VGR_E2_C1_motor_step_counter.get_count()
  elif num == 2:
    encoder_count = TXT_VGR_E2_C2_motor_step_counter.get_count()
  elif num == 3:
    encoder_count = TXT_VGR_E2_C3_motor_step_counter.get_count()
  elif num == 4:
    encoder_count = TXT_HBW_E1_C2_motor_step_counter.get_count()
  elif num == 5:
    encoder_count = TXT_HBW_E1_C4_motor_step_counter.get_count()
  elif num == 6:
    encoder_count = TXT_SSC_M_C1_motor_step_counter.get_count()
  elif num == 7:
    encoder_count = TXT_SSC_M_C2_motor_step_counter.get_count()
# Si el valor relativo (rv) es positivo, se suma a la posición absoluta; si es negativo, se resta. 
# Esto permite mantener un registro preciso de la posición del eje en pasos de motor.
  if rv > 0:
    abspos[int(num - 1)] = abspos[int(num - 1)] + encoder_count
    logging.log(logging.DEBUG_A1R, 'set abspos[%d]+=%d', num, abspos[num-1])
  elif rv < 0:
    abspos[int(num - 1)] = abspos[int(num - 1)] - encoder_count
    logging.log(logging.DEBUG_A1R, 'set abspos[%d]-=%d', num, abspos[num-1])


# Inicializa los niveles de log específicos para el control de los ejes
def initlog_A1R(_tr0, _tr, _dg):
  """Registra los niveles de trazado usados por el módulo de ejes.

  Args:
    _tr0: Nivel numérico para los mensajes de trazado detallado.
    _tr: Nivel numérico para los mensajes de trazado normal.
    _dg: Nivel numérico para los mensajes de depuración.

  Returns:
    None.
  """
  global num, msg, rv, av, abspos, ABSLIMIT, _b_exit, tsdiff, _ref_valid, SPEED, ts0, temp, _ref_last, SPEED_REF, TIMEOUT_S
  logging.TRACE0_A1R = _tr0
  logging.addLevelName(logging.TRACE0_A1R , 'TRACE0_A1R')
  logging.TRACE_A1R = _tr
  logging.addLevelName(logging.TRACE_A1R , 'TRACE_A1R')
  logging.DEBUG_A1R = _dg
  logging.addLevelName(logging.DEBUG_A1R, 'DEBUG_A1R')

# Configura los limites físicos, velocidades y tiempos de espera (timeouts) para cada eje
def initlib_Axes1Ref():
  """Inicializa límites, velocidades y temporizadores de los siete ejes.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, num, msg, rv, av, abspos, ABSLIMIT, _b_exit, tsdiff, _ref_valid, SPEED, ts0, temp, _ref_last, SPEED_REF, TIMEOUT_S
  __version__ = '2022-07-25' #Axes1Ref (A1R)
  logging.log(logging.TRACE_A1R, '-')
  logging.log(logging.DEBUG_A1R, 'Axes1Ref (A1R) %s', __version__)

  # Correspondencia entre eje lógico, motor, final de carrera y encoder.
  #num: #MX, IX, CX
  # 1: VGR x  # VGR_E2_M1, VGR_E2_I1, VGR_E2_C1
  # 2: VGR y  # VGR_E2_M2, VGR_E2_I2, VGR_E2_C2
  # 3: VGR z  # VGR_E2_M3, VGR_E2_I3, VGR_E2_C3
  # 4: HBW x # HBW_E1_M2, HBW_E1_I5, HBW_E1_C2
  # 5: HBW y # HBW_E1_M4, HBW_E1_I8, HBW_E1_C4
  # 6: SSC pan # SSC_M_M1, SSC_M_I1, SSC_M_C1
  # 7: SSC tilt # SSC_M_M2, SSC_M_I2, SSC_M_C2

  # Límites máximos de pasos para cada eje, a partir de los cuales se considera que el movimiento ha llegado al final del recorrido físico permitido
  ABSLIMIT = [1500, 900, 950, 2050, 850, 1550, 700]
  abspos = [None] * len(ABSLIMIT) # Posición absoluta actual de cada eje
  SPEED = 512
  SPEED_REF = 200
  # Segundos máximos que puede tardar cada eje en moverse antes de dar error de bloqueo
  TIMEOUT_S = [10.9, 7, 7.3, 14.6, 7.1, 10.5, 6.6]
  ts0 = [0] * len(ABSLIMIT) # Tiempo de inicio del movimiento para cada eje
  tsdiff = [0] * len(ABSLIMIT)  # Tiempo transcurrido desde el inicio del movimiento para cada eje
  _b_exit = False # Flag que indica que se ha producido un error
  _ref_valid = [False] * len(ABSLIMIT)  # Flag de cada eje que indica que el eje está referenciado, se pone a True al hacer moveRef. Si pierde la referencia se pone a False y no permite hacer moveRel/moveAbs hasta que se vuelva a referenciar con moveRef
  _ref_last = [False] * len(ABSLIMIT) # Flag que indica si el último movimiento de cada eje ha sido un moveRef (True) o un moveRel/moveAbs (False)

# Devuelve la lista con las posiciones absolutas actuales de todos los ejes
def get_abspos():
  """Obtiene la lista de posiciones absolutas de los ejes.

  Returns:
    Lista global con una posición por eje.
  """
  global _tr0, _tr, _dg, num, msg, rv, av, abspos, ABSLIMIT, _b_exit, tsdiff, _ref_valid, SPEED, ts0, temp, _ref_last, SPEED_REF, TIMEOUT_S
  logging.log(logging.TRACE_A1R, abspos)
  return abspos

# Devuelve los límites máximos permitidos para cada eje
def get_ABSLIMIT():
  """Obtiene los límites absolutos configurados para los ejes.

  Returns:
    Lista de límites máximos por eje.
  """
  global _tr0, _tr, _dg, num, msg, rv, av, abspos, ABSLIMIT, _b_exit, tsdiff, _ref_valid, SPEED, ts0, temp, _ref_last, SPEED_REF, TIMEOUT_S
  logging.log(logging.TRACE_A1R, abspos)
  return ABSLIMIT

# Comprueba si un eje ha excedido su tiempo de movimiento permitido y detiene el programa si es así
def _check_timeout_exit(num):
  """Comprueba si la búsqueda de referencia de un eje ha superado su tiempo límite.

  Args:
    num: Número lógico del eje que se está referenciando.

  Returns:
    ``True`` cuando debe abortarse el movimiento; en otro caso, ``False``.
  """
  global _tr0, _tr, _dg, msg, rv, av, abspos, ABSLIMIT, _b_exit, tsdiff, _ref_valid, SPEED, ts0, temp, _ref_last, SPEED_REF, TIMEOUT_S
  logging.log(logging.TRACE0_A1R, num)
  tsdiff[int(num - 1)] = time.time() - ts0[int(num - 1)]
  if tsdiff[int(num - 1)] > TIMEOUT_S[int(num - 1)]:
    logging.warning('A1R: timeout[%d] %.1f (%.1f), exit program', num, tsdiff[num-1], TIMEOUT_S[num-1])
    _exit_Axes1Ref('timeout {}s (max: {}s)'.format(round(tsdiff[int(num - 1)], 1), TIMEOUT_S[int(num - 1)]))
  time.sleep(0.001)

# Fuerza el cierre del programa de ejes y marca las referencias como no válidas tras un error
def _exit_Axes1Ref(msg):
  """Marca la salida del control de referencia de ejes y registra el motivo.

  Args:
    msg: Mensaje que explica la causa de la salida.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, num, rv, av, abspos, ABSLIMIT, _b_exit, tsdiff, _ref_valid, SPEED, ts0, temp, _ref_last, SPEED_REF, TIMEOUT_S
  logging.log(logging.TRACE_A1R, '-')
  _b_exit = True  
  _ref_valid = [False] * len(ABSLIMIT) 
  msg = 'error in Axes1Ref: {}'.format(msg)
  logging.error('A1R: %s', msg)
  os._exit(os.EX_OK)  # Cierra el programa de python inmediatamente

# Mueve el eje hacia el final de carrera de referencia para calibrar el punto 0
def moveRef(num):
  """Mueve un eje hasta su posición de referencia.

  Args:
    num: Número lógico del eje que se debe referenciar.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, msg, rv, av, abspos, ABSLIMIT, _b_exit, tsdiff, _ref_valid, SPEED, ts0, temp, _ref_last, SPEED_REF, TIMEOUT_S
  logging.log(logging.TRACE_A1R, num)
  if num < 1 or num > len(ABSLIMIT):
    logging.error('A1R: num out of bounds: %d', num)
    return
  if _ref_last[int(num - 1)]:
    logging.log(logging.DEBUG_A1R, 'ref already done. ignore command')
    return
  ts0[int(num - 1)] = time.time()

  live_offset_abs[num - 1] = abspos[num - 1]
  live_abspos[num - 1] = abspos[num - 1]


  try:
    # CCW: Counterclockwise, CW: Clockwise
    if num == 1:
      # Mueve rotacionalmente el brazo central VGR hasta alcanzar el final de carrera de referencia, luego se separa un ddpoco para marcar la posición de referencia
      # El motor se mueve a la velocidad (SPEED) hasta que el final de carrera de referencia se cierre
      begin_live_motion(1, -1, True)  # Indica que el eje 1 (VGR rotacional) está en movimiento en dirección antihoraria (CCW)
      TXT_VGR_E2_M1_encodermotor.set_speed(int(SPEED), Motor.CCW)
      TXT_VGR_E2_M1_encodermotor.start_sync()
      while not ((TXT_VGR_E2_I1_mini_switch.is_closed()) or _b_exit):
        _check_timeout_exit(num)
      TXT_VGR_E2_M1_encodermotor.stop_sync()

      end_live_motion(1)  # Indica que el eje 1 (VGR rotacional) ha terminado su movimiento

      begin_live_motion(1, 1, True)
      TXT_VGR_E2_M1_encodermotor.set_speed(int(SPEED), Motor.CW)
      TXT_VGR_E2_M1_encodermotor.start_sync()
      while not ((TXT_VGR_E2_I1_mini_switch.is_open()) or _b_exit):
        _check_timeout_exit(num)
      TXT_VGR_E2_M1_encodermotor.stop_sync()

    # Mueve el brazo central VGR verticalmente hasta alcanzar el final de carrera de referencia, luego baja un poco para marcar el punto de referencia
    elif num == 2:
      begin_live_motion(2, -1, True)
      TXT_VGR_E2_M2_encodermotor.set_speed(int(SPEED), Motor.CCW)
      TXT_VGR_E2_M2_encodermotor.start_sync()
      while not ((TXT_VGR_E2_I2_mini_switch.is_closed()) or _b_exit):
        _check_timeout_exit(num)
      TXT_VGR_E2_M2_encodermotor.stop_sync()
      end_live_motion(2)

      begin_live_motion(2, 1, True)
      TXT_VGR_E2_M2_encodermotor.set_speed(int(SPEED), Motor.CW)
      TXT_VGR_E2_M2_encodermotor.start_sync()
      while not ((TXT_VGR_E2_I2_mini_switch.is_open()) or _b_exit):
        _check_timeout_exit(num)
      TXT_VGR_E2_M2_encodermotor.stop_sync()

    # Se retrae el brazo central VGR hasta alcanzar el final de carrera de referencia, luego se avanza un poco para marcar la posición de referencia
    elif num == 3:
      begin_live_motion(3, -1, True)
      TXT_VGR_E2_M3_encodermotor.set_speed(int(SPEED), Motor.CCW)
      TXT_VGR_E2_M3_encodermotor.start_sync()
      while not ((TXT_VGR_E2_I3_mini_switch.is_closed()) or _b_exit):
        _check_timeout_exit(num)
      TXT_VGR_E2_M3_encodermotor.stop_sync()
      end_live_motion(3)

      begin_live_motion(3, 1, True)
      TXT_VGR_E2_M3_encodermotor.set_speed(int(SPEED), Motor.CW)
      TXT_VGR_E2_M3_encodermotor.start_sync()
      while not ((TXT_VGR_E2_I3_mini_switch.is_open()) or _b_exit):
        _check_timeout_exit(num)
      TXT_VGR_E2_M3_encodermotor.stop_sync()

    # Mueve horizontalmente el brazo del almacén hasta alcanzar el final de carrera de referencia, luego se separa un poco para quedar en la posición de referencia
    elif num == 4:
      begin_live_motion(4, -1, True)
      TXT_HBW_E1_M2_encodermotor.set_speed(int(SPEED), Motor.CCW)
      TXT_HBW_E1_M2_encodermotor.start_sync()
      while not ((TXT_HBW_E1_I5_mini_switch.is_closed()) or _b_exit):
        _check_timeout_exit(num)
      TXT_HBW_E1_M2_encodermotor.stop_sync()
      end_live_motion(4)

      begin_live_motion(4, 1, True)
      TXT_HBW_E1_M2_encodermotor.set_speed(int(SPEED_REF), Motor.CW)
      TXT_HBW_E1_M2_encodermotor.start_sync()
      while not ((TXT_HBW_E1_I5_mini_switch.is_open()) or _b_exit):
        _check_timeout_exit(num)
      TXT_HBW_E1_M2_encodermotor.stop_sync()

    # Sube el brazo del almacén hasta alcanzar el final de carrera de referencia, luego baja un poco para marcar el punto de referencia
    elif num == 5:
      begin_live_motion(5, -1, True)
      TXT_HBW_E1_M4_encodermotor.set_speed(int(SPEED), Motor.CCW)
      TXT_HBW_E1_M4_encodermotor.start_sync()
      while not ((TXT_HBW_E1_I8_mini_switch.is_closed()) or _b_exit):
        _check_timeout_exit(num)
      TXT_HBW_E1_M4_encodermotor.stop_sync()
      end_live_motion(5)

      begin_live_motion(5, 1, True)
      TXT_HBW_E1_M4_encodermotor.set_speed(int(SPEED_REF), Motor.CW)
      TXT_HBW_E1_M4_encodermotor.start_sync()
      while not ((TXT_HBW_E1_I8_mini_switch.is_open()) or _b_exit):
        _check_timeout_exit(num)
      TXT_HBW_E1_M4_encodermotor.stop_sync()

    # Mueve la cámara rotacionalmente hasta alcanzar el final de carrera de referencia
    elif num == 6:
      begin_live_motion(6, -1, True)
      TXT_SSC_M_M1_encodermotor.set_speed(int(SPEED), Motor.CCW)
      TXT_SSC_M_M1_encodermotor.start_sync()
      while not ((TXT_SSC_M_I1_mini_switch.is_closed()) or _b_exit):
        _check_timeout_exit(num)
      TXT_SSC_M_M1_encodermotor.stop_sync()

    # Mueve la cámara verticalmente hasta alcanzar el final de carrera de referencia
    elif num == 7:
      begin_live_motion(7, -1, True)
      TXT_SSC_M_M2_encodermotor.set_speed(int(SPEED), Motor.CCW)
      TXT_SSC_M_M2_encodermotor.start_sync()
      while not ((TXT_SSC_M_I2_mini_switch.is_closed()) or _b_exit):
        _check_timeout_exit(num)
      TXT_SSC_M_M2_encodermotor.stop_sync()

  finally:

    end_live_motion(num)
    live_offset_abs[int(num - 1)] = 0 # live_abspos_vgr[int(num - 1)]
    live_abspos[int(num - 1)] = 0
    abspos[int(num - 1)] = 0
    _ref_valid[int(num - 1)] = True
    _ref_last[int(num - 1)] = True
    logging.log(logging.DEBUG_A1R, 'stop')

# Mueve el eje a una distancia relativa a su posición actual
def moveRel(num, rv):
  """Mueve un eje una distancia relativa y actualiza su posición.

  Args:
    num: Número lógico del eje que se debe mover.
    rv: Desplazamiento relativo expresado en pasos de motor.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, msg, av, abspos, ABSLIMIT, _b_exit, tsdiff, _ref_valid, SPEED, ts0, temp, _ref_last, SPEED_REF, TIMEOUT_S
  logging.log(logging.TRACE_A1R, num)

  if num < 1 or num > len(ABSLIMIT):
    logging.error('A1R: num out of bounds: %d', num)
    return
  if not _ref_valid[int(num - 1)]:
    logging.error('A1R: ref not valid')
    return
  if abspos[int(num - 1)] == None:
    logging.error('A1R: abspos[num]=None')
    return

  logging.log(logging.DEBUG_A1R, 'num=%d, rv=%d, abspos[num]=%d, ABSLIMIT[num]=%d', num, rv, abspos[num-1], ABSLIMIT[num-1])
  ts0[int(num - 1)] = time.time()
  abs_start = abspos[int(num - 1)]
  temp = abs_start + rv

  live_offset_abs[num - 1] = abs_start
  live_abspos[num - 1] = abs_start
  live_last_delta[num - 1] = 0

  if rv > 0:
    if temp <= ABSLIMIT[int(num - 1)]:
      begin_live_motion(num, 1)
      if num == 1:
        TXT_VGR_E2_M1_encodermotor.set_speed(int(SPEED), Motor.CW)
        TXT_VGR_E2_M1_encodermotor.set_distance(int(math.fabs(rv)))
      elif num == 2:
        TXT_VGR_E2_M2_encodermotor.set_speed(int(SPEED), Motor.CW)
        TXT_VGR_E2_M2_encodermotor.set_distance(int(math.fabs(rv)))
      elif num == 3:
        TXT_VGR_E2_M3_encodermotor.set_speed(int(SPEED), Motor.CW)
        TXT_VGR_E2_M3_encodermotor.set_distance(int(math.fabs(rv)))
      elif num == 4:
        TXT_HBW_E1_M2_encodermotor.set_speed(int(SPEED), Motor.CW)
        TXT_HBW_E1_M2_encodermotor.set_distance(int(math.fabs(rv)))
      elif num == 5:
        TXT_HBW_E1_M4_encodermotor.set_speed(int(SPEED), Motor.CW)
        TXT_HBW_E1_M4_encodermotor.set_distance(int(math.fabs(rv)))
      elif num == 6:
        TXT_SSC_M_M1_encodermotor.set_speed(int(SPEED), Motor.CW)
        TXT_SSC_M_M1_encodermotor.set_distance(int(math.fabs(rv)))
      elif num == 7:
        TXT_SSC_M_M2_encodermotor.set_speed(int(SPEED), Motor.CW)
        TXT_SSC_M_M2_encodermotor.set_distance(int(math.fabs(rv)))
    else:
      _exit_Axes1Ref('num {} out of bounds {} > {}'.format(num, temp, ABSLIMIT[int(num - 1)]))
      return
  elif rv < 0:
    if temp >= 0:
      begin_live_motion(num, -1)
      if num == 1:
        TXT_VGR_E2_M1_encodermotor.set_speed(int(SPEED), Motor.CCW)
        TXT_VGR_E2_M1_encodermotor.set_distance(int(math.fabs(rv)))
      elif num == 2:
        TXT_VGR_E2_M2_encodermotor.set_speed(int(SPEED), Motor.CCW)
        TXT_VGR_E2_M2_encodermotor.set_distance(int(math.fabs(rv)))
      elif num == 3:
        TXT_VGR_E2_M3_encodermotor.set_speed(int(SPEED), Motor.CCW)
        TXT_VGR_E2_M3_encodermotor.set_distance(int(math.fabs(rv)))
      elif num == 4:
        TXT_HBW_E1_M2_encodermotor.set_speed(int(SPEED), Motor.CCW)
        TXT_HBW_E1_M2_encodermotor.set_distance(int(math.fabs(rv)))
      elif num == 5:
        TXT_HBW_E1_M4_encodermotor.set_speed(int(SPEED), Motor.CCW)
        TXT_HBW_E1_M4_encodermotor.set_distance(int(math.fabs(rv)))
      elif num == 6:
        TXT_SSC_M_M1_encodermotor.set_speed(int(SPEED), Motor.CCW)
        TXT_SSC_M_M1_encodermotor.set_distance(int(math.fabs(rv)))
      elif num == 7:
        TXT_SSC_M_M2_encodermotor.set_speed(int(SPEED), Motor.CCW)
        TXT_SSC_M_M2_encodermotor.set_distance(int(math.fabs(rv)))
    elif temp == 0:
      logging.log(logging.DEBUG_A1R, 'temp==0')
    else:
      _exit_Axes1Ref('num {} out of bounds {} > {}'.format(num, temp, ABSLIMIT[int(num - 1)]))
      return
  elif rv == 0:
    logging.log(logging.DEBUG_A1R, 'rv==0')
    live_abspos[num - 1] = live_offset_abs[num - 1]

  try:
    while True:
      if num == 1:
        running = TXT_VGR_E2_M1_encodermotor.is_running()
      elif num == 2:
        running = TXT_VGR_E2_M2_encodermotor.is_running()
      elif num == 3:
        running = TXT_VGR_E2_M3_encodermotor.is_running()
      elif num == 4:
        running = TXT_HBW_E1_M2_encodermotor.is_running()
      elif num == 5:
        running = TXT_HBW_E1_M4_encodermotor.is_running()
      elif num == 6:
        running = TXT_SSC_M_M1_encodermotor.is_running()
      elif num == 7:
        running = TXT_SSC_M_M2_encodermotor.is_running()

      if not running or _b_exit:
        break

      _set_abspos_live_from_counter(num, abs_start, rv)
      _check_timeout_exit(num)

    abspos[num - 1] = abs_start + rv
    live_abspos[num - 1] = abspos[num - 1]

  finally:
    end_live_motion(num)

  _ref_last[int(num - 1)] = False
# Mueve el eje a una posición absoluta (av)
def moveAbs(num, av):
  """Mueve un eje hasta una posición absoluta.

  Args:
    num: Número lógico del eje que se debe mover.
    av: Posición absoluta de destino en pasos de motor.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, msg, rv, abspos, ABSLIMIT, _b_exit, tsdiff, _ref_valid, SPEED, ts0, temp, _ref_last, SPEED_REF, TIMEOUT_S
  if num < 1 or num > len(ABSLIMIT):
    logging.error('A1R: num out of bounds: %d', num)
    return
  if not _ref_valid[int(num - 1)]:
    logging.error('A1R: ref not valid')
    return
  logging.log(logging.DEBUG_A1R, 'num=%d, av=%d, abspos[num]=%d, ABSLIMIT[num]=%d', num, av, abspos[num-1], ABSLIMIT[num-1])
  if abspos[int(num - 1)] == None:
    _exit_Axes1Ref('num {} no ref, abspos not valid {}'.format(num, abspos[int(num - 1)]))
    return
  else:
    # Calcula la diferencia entre la posición absoluta actual y la posición absoluta de destino (av) y llama a moveRel para mover esa distancia relativa
    temp = av - abspos[int(num - 1)]
    moveRel(num, temp)


