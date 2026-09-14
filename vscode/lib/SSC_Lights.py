"""Control de los pilotos LED de estado del controlador SSC."""

# Mapea estados de la fabrica a los LEDs del controlador maestro.
import logging
import time
from lib.controller import *
from lib.HBW import *
from lib.MPO import *
from lib.SLD import *
from lib.VGR import *

_mode = None
bits = None
lights_mode = None
lights_mode_last = None
b1 = None
b2 = None
b3 = None


def set_lights_mode(_mode):
  """
  Fuerza el semaforo de la camara a un modo concreto de encendido.

  Args:
    _mode: Modo de luces a mostrar (1=verde, 2=amarillo, 4=rojo, 7=parpadeo
      de las tres luces a la vez).

  Returns:
    None.
  """
  global bits, lights_mode, lights_mode_last, b1, b2, b3
  logging.log(logging.TRACE, _mode)
  lights_mode = _mode


def thread_lights():
  """
  Enciende el semaforo de la camara segun como va la fabrica, sin parar.

  Mira el estado del almacen, el VGR, la MPO y la SLD: si alguno esta en
  error pone la luz roja, si alguno esta trabajando pone la amarilla, si
  todos estan listos y en reposo pone la verde, y en cualquier otro caso
  deja el modo por defecto (amarillo y verde a la vez).

  Returns:
    None. Es un bucle infinito, nunca termina por si solo.
  """
  global _mode, bits, lights_mode, lights_mode_last, b1, b2, b3
  logging.log(logging.TRACE, '-')
  lights_mode = 3
  lights_mode_last = 0
  while True:
    if (get_state_code_HBW()) == 4 or (get_state_code_VGR()) == 4 or (get_state_code_MPO()) == 4 or (get_state_code_SLD()) == 4:
      lights_mode = 4
    elif (get_state_code_HBW()) == 2 or (get_state_code_VGR()) == 2 or (get_state_code_MPO()) == 2 or (get_state_code_SLD()) == 2:
      lights_mode = 2
    elif (get_state_code_HBW()) == 1 and (get_state_code_VGR()) == 1 and (get_state_code_MPO()) == 1 and (get_state_code_SLD()) == 1:
      lights_mode = 1
    else:
      lights_mode = 3
    if lights_mode == 7:
      set_LEDs(7)
      time.sleep(0.2)
      set_LEDs(0)
    else:
      if lights_mode != lights_mode_last:
        logging.log(logging.DEBUG, lights_mode)
        set_LEDs(lights_mode)
        lights_mode_last = lights_mode
    time.sleep(0.2)


def set_LEDs(bits):
  """
  Enciende o apaga las tres luces del semaforo segun sus bits.

  Args:
    bits: Numero de 3 bits; el bit 2 controla la luz roja, el bit 1 la
      amarilla y el bit 0 la verde.

  Returns:
    None.
  """
  global _mode, lights_mode, lights_mode_last, b1, b2, b3
  logging.log(logging.TRACE, bits)
  b1 = bits & (1<<2)
  b2 = bits & (1<<1)
  b3 = bits & (1<<0)
  if b1:
    TXT_SSC_M_O6_led.set_brightness(512)
  else:
    TXT_SSC_M_O6_led.set_brightness(0)
  if b2:
    TXT_SSC_M_O7_led.set_brightness(512)
  else:
    TXT_SSC_M_O7_led.set_brightness(0)
  if b3:
    TXT_SSC_M_O8_led.set_brightness(512)
  else:
    TXT_SSC_M_O8_led.set_brightness(0)



# Estado de los LEDs y modo de iluminación SSC.
def get_lights_mode():
  """
  Devuelve el modo de luces del semaforo que esta activo ahora mismo.

  Returns:
    El modo actual, o 0 si todavia no se ha establecido ninguno.
  """
  return int(lights_mode) if lights_mode is not None else 0
