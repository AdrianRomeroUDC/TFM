"""Control de la línea de clasificación SLD (Sorting Line Device).

SLD mueve la cinta con el motor con encoder ``TXT_SLD_E5_M1`` y cuenta sus
pasos mediante ``TXT_SLD_E5_C1``. Lee el sensor de color ``I2`` y los
fototransistores ``I1``, ``I3``, ``I6``, ``I7`` e ``I8``; el compresor ``O8`` y
las válvulas ``O5``, ``O6`` y ``O7`` accionan los cilindros expulsadores
blanco, rojo y azul. ``thread_SLD`` crea un hilo daemon de actualización. No
se usa ``threading.RLock`` en este módulo.
"""

# El SLD mide color, mueve la cinta y dispara el cilindro correspondiente.
import logging
import time
from fischertechnik.controller.Motor import Motor
from lib.controller import *
from lib.display import *
from lib.Factory_Variables import *
from lib.SLD_Display import *
from lib.SLD_MQTT import *

_data = None
_code = None
_active = None
speed = None
state_code = None
state_active = None
thresh_white_red_defaults = None
thresh_red_blue_defaults = None
thresh_white_red = None
thresh_red_blue = None
_ts_state = None
detectedColorValue = None
lastColorValue = None
counter = None
lastStateCounterSwitch = None
import threading

# Thread principal del SLD
def thread_SLD():
  """Ejecuta el ciclo daemon de clasificacion de la linea SLD.

  El hilo lee el sensor de color, mueve el motor con encoder y activa la
  valvula correspondiente. Actualiza el estado global y publica errores de
  la estacion; no adquiere locks propios.

  Returns:
    None. El ciclo permanece activo mientras la aplicacion esta en ejecucion.
  """
  global _data, _code, _active, speed, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  logging.log(logging.TRACE, '-')
  # Calibrado por defecto de los umbrales de color
  thresh_white_red_defaults = 1150
  thresh_white_red = thresh_white_red_defaults
  thresh_red_blue_defaults = 1400
  thresh_red_blue = thresh_red_blue_defaults
  _set_state_SLD(1, 0)
  threading.Thread(target=thread_update_SLD, daemon=True).start()
  while True:
    # CASO 1: Error en SLD
    if (get_factory_error_state()) == 'SLD':
      _set_state_SLD(4, 1)  # Estado de error (code=4, active=1)
      display.set_attr("txt_label_message.text", str('ERROR SLD: Please confirm with ACK button!'))
    # caso 2: Una pieza a la entrada de la cinta de clasificación
    elif isColorSensorTriggered():
      logging.log(logging.DEBUG, 'start')
      _set_state_SLD(2, 1)  # Estado de clasificación (code=2, active=1)
      detectedColorValue = 3000
      setConvBeltSpeedSLD(512)  # Arranca la cinta
      # Mientras la pieza pasa bajo el sensor, busca el valor de voltaje más bajo
      # (El sensor de color fischertechnik devuelve menos voltaje cuanto más clara es la pieza)
      print("Thresh_white_red: {}\nThresh_red_blue: {}".format(thresh_white_red, thresh_red_blue))
      while not isEjectionTriggered():
        lastColorValue = readColorValue()
        if lastColorValue < detectedColorValue:
          detectedColorValue = lastColorValue
          print("Color: {}".format(detectedColorValue))
      setConvBeltSpeedSLD(0)  # Detiene la cinta
      logging.log(logging.DEBUG, 'detectedColorValue: %d', detectedColorValue)
      TXT_SLD_E5_O8_compressor.on()  # Enciende el compresor
      logging.log(logging.DEBUG, 'check counter')
      # Para el posicionamiento se usa un motor con encoder y un interruptor (mini_switch) 
      # para contar pasos y mover el expulsor a la rampa correcta.
      counter = 0
      lastStateCounterSwitch = TXT_SLD_E5_C1_mini_switch.get_state()
      TXT_SLD_E5_M1_encodermotor.set_speed(int(350), Motor.CW)
      TXT_SLD_E5_M1_encodermotor.start()
      while counter <= 29:
        # Compara el valor detectado con los umbrales de calibración
        if (TXT_SLD_E5_C1_mini_switch.get_state()) != lastStateCounterSwitch:
          counter = (counter if isinstance(counter, (int, float)) else 0) + 1
          lastStateCounterSwitch = TXT_SLD_E5_C1_mini_switch.get_state()
          logging.log(logging.DEBUG, 'counter: %d', counter)
          time.sleep(0.001)
        # Si el color de la pieza es blanco
        if detectedColorValue >= 200 and detectedColorValue < thresh_white_red:
          sld_cylinder_state["cyl_color"] = "WHITE"
          if counter > 5:
            TXT_SLD_E5_M1_encodermotor.stop()
            logging.log(logging.DEBUG, 'detectedColorValue: %d', detectedColorValue)
            ejectWhite()
            break
        # Si el color de la pieza es rojo
        elif detectedColorValue >= thresh_white_red and detectedColorValue < thresh_red_blue:
          sld_cylinder_state["cyl_color"] = "RED"
          if counter > 14:
            TXT_SLD_E5_M1_encodermotor.stop()
            logging.log(logging.DEBUG, 'detectedColorValue: %d', detectedColorValue)
            ejectRed()
            break
        # Si el color de la pieza es azul
        elif detectedColorValue >= thresh_red_blue and detectedColorValue < 2000:
          sld_cylinder_state["cyl_color"] = "BLUE"
          if counter > 25:
            TXT_SLD_E5_M1_encodermotor.stop()
            logging.log(logging.DEBUG, 'detectedColorValue: %d', detectedColorValue)
            ejectBlue()
            break
        else:
          pass
      # Si el color de la pieza no es blanco, rojo o azul, ERROR!
      if detectedColorValue < 200 or detectedColorValue >= 2000:
        set_factory_error_state('SLD')
        logging.log(logging.DEBUG, 'error: color out of bounds')
      else:
        _set_state_SLD(1, 0)  # Estado inicial (code=1, active=0)
        logging.log(logging.DEBUG, 'sorted')
    time.sleep(0.5)

# Valores de calibración cromática predeterminados de la estación.
def get_calib_data_SLD_defaults():
  """
  Devuelve los umbrales de color de fabrica de la SLD.

  Returns:
    Una lista ``[umbral_blanco_rojo, umbral_rojo_azul]`` con los valores
    originales, sin los ajustes hechos a mano.
  """
  global _data, _code, _active, speed, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  logging.log(logging.TRACE, '-')
  return [thresh_white_red_defaults, thresh_red_blue_defaults]

# Valores de calibración cromática actualmente configurados.
def get_calib_data_SLD():
  """
  Devuelve los umbrales de color que la SLD esta usando ahora mismo.

  Returns:
    Una lista ``[umbral_blanco_rojo, umbral_rojo_azul]``.
  """
  global _data, _code, _active, speed, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  logging.log(logging.TRACE, '-')
  return [thresh_white_red, thresh_red_blue]

# Actualización de la calibración cromática de la estación.
def set_calib_data_SLD(_data):
  """
  Restablece los umbrales de color de la SLD a unos valores fijos.

  Args:
    _data: Sin usar; se conserva por compatibilidad con la llamada.

  Returns:
    None.
  """
  global _code, _active, speed, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  logging.log(logging.TRACE, _data)
  # thresh_white_red = _data[0]
  # thresh_red_blue = _data[1]
  thresh_white_red = 1150
  thresh_red_blue = 1460

# Actualizar el estado de la estación, tanto en la pantalla del controlador como en la nube
def thread_update_SLD():
  """
  Publica el estado de la SLD cada 10 segundos, en su propio hilo.

  Actualiza el indicador en pantalla y avisa por MQTT del codigo de estado
  y de si la estacion esta activa.

  Returns:
    None. Es un bucle infinito, nunca termina por si solo.
  """
  global _data, _code, _active, speed, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  logging.log(logging.TRACE, '-')
  _ts_state = 0
  while True:
    if (time.time() * 1000) - _ts_state > 10000:
      update_display_SLD(state_code)
      publish_state_SLD(state_code, state_active)
      _ts_state = (time.time() * 1000)
    time.sleep(1)

# Establecer el estado de la estación
def _set_state_SLD(_code, _active):
  """
  Guarda el nuevo estado de la SLD si algo ha cambiado.

  Si el codigo o el indicador de actividad son distintos de los que ya
  estaban guardados, los actualiza y reinicia el cronometro para que
  ``thread_update_SLD`` avise cuanto antes por pantalla y MQTT.

  Args:
    _code: Codigo de estado de la SLD (por ejemplo 1=reposo,
      2=clasificando, 4=error).
    _active: Indica si la estacion esta activa haciendo ese estado.

  Returns:
    None.
  """
  global _data, speed, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  logging.log(logging.TRACE0, '-')
  if state_code != _code or state_active != _active:
    _ts_state = 0
    state_code = _code
    state_active = _active

# Obtener el código de estado de la estación
def get_state_code_SLD():
  """
  Devuelve el codigo de estado actual de la SLD.

  Returns:
    El codigo de estado guardado (por ejemplo 1=reposo, 2=clasificando,
    4=error).
  """
  global _data, _code, _active, speed, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  logging.log(logging.TRACE0, '-')
  return state_code

# Obtener el estado de la estación (activo/desactivo)
def get_state_active_SLD():
  """
  Dice si la SLD esta activa haciendo su estado actual.

  Returns:
    True si la estacion esta ocupada con la tarea de ``state_code``, False
    si esta libre.
  """
  global _data, _code, _active, speed, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  logging.log(logging.TRACE0, '-')
  return state_active

# Especificar velocidad de la cinta de salida
def setConvBeltSpeedSLD(speed):
  """
  Pone en marcha la cinta de clasificacion a la velocidad indicada.

  Args:
    speed: Velocidad del motor de la cinta; 0 la detiene.

  Returns:
    None.
  """
  global _data, _code, _active, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  logging.log(logging.TRACE, 'speed: %d', speed)
  TXT_SLD_E5_M1_encodermotor.set_speed(int(speed), Motor.CW)
  TXT_SLD_E5_M1_encodermotor.start()

# Se activa el cilindro expulsor blanco
def ejectWhite():
  """
  Empuja fuera de la cinta la pieza blanca con su cilindro.

  Activa un instante la valvula del cilindro blanco para dar el empujon,
  apaga el compresor y comprueba con el fototransistor blanco si la pieza
  ha caido realmente por esa rampa.

  Returns:
    None.
  """
  global _data, _code, _active, speed, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  global sld_cylinder_state
  logging.log(logging.TRACE, '-')
  sld_cylinder_state["active"] = True
  TXT_SLD_E5_O5_magnetic_valve.on()
  time.sleep(0.5)
  TXT_SLD_E5_O5_magnetic_valve.off()
  TXT_SLD_E5_O8_compressor.off()
  sld_cylinder_state["active"] = False
  sld_cylinder_state["cyl_color"] = None
  time.sleep(0.5)
  if isWhite():
    state_code = 1  # Bien
  else:
    state_code = 4  # Error

# Se activa el cilindro expulsor rojo
def ejectRed():
  """
  Empuja fuera de la cinta la pieza roja con su cilindro.

  Activa un instante la valvula del cilindro rojo para dar el empujon,
  apaga el compresor y comprueba con el fototransistor rojo si la pieza
  ha caido realmente por esa rampa.

  Returns:
    None.
  """
  global _data, _code, _active, speed, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  global sld_cylinder_state
  logging.log(logging.TRACE, '-')
  sld_cylinder_state["active"] = True
  TXT_SLD_E5_O6_magnetic_valve.on()
  time.sleep(0.5)
  TXT_SLD_E5_O6_magnetic_valve.off()
  TXT_SLD_E5_O8_compressor.off()
  sld_cylinder_state["active"] = False
  sld_cylinder_state["cyl_color"] = None
  time.sleep(0.5)
  if isRed():
    state_code = 1  # Bien
  else:
    state_code = 4  # Error

# Se activa el cilindro expulsor azul
def ejectBlue():
  """
  Empuja fuera de la cinta la pieza azul con su cilindro.

  Activa un instante la valvula del cilindro azul para dar el empujon,
  apaga el compresor y comprueba con el fototransistor azul si la pieza
  ha caido realmente por esa rampa.

  Returns:
    None.
  """
  global _data, _code, _active, speed, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  global sld_cylinder_state
  logging.log(logging.TRACE, '-')
  sld_cylinder_state["active"] = True
  TXT_SLD_E5_O7_magnetic_valve.on()
  time.sleep(0.5)
  TXT_SLD_E5_O7_magnetic_valve.off()
  TXT_SLD_E5_O8_compressor.off()
  sld_cylinder_state["active"] = False
  sld_cylinder_state["cyl_color"] = None
  time.sleep(0.5)
  if isBlue():
    state_code = 1  # Bien
  else:
    state_code = 4  # Error

# Pieza en la posición del sensor de color
def isColorSensorTriggered():
  """
  Pregunta si hay una pieza en el punto de lectura de color.

  Mira el fototransistor de entrada de la cinta (I1): si algo le tapa la
  luz, es que ha llegado una pieza al lector de color.

  Returns:
    True si detecta una pieza (sensor a oscuras), False si no hay nada.
  """
  global _data, _code, _active, speed, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  logging.log(logging.TRACE0, '-')
  return TXT_SLD_E5_I1_photo_transistor.is_dark()

# Pieza en la entrada a los cilindros de clasificación
def isEjectionTriggered():
  """
  Pregunta si la pieza ha llegado a la zona de los cilindros expulsores.

  Mira el fototransistor situado junto a los cilindros (I3): si algo le
  tapa la luz, la pieza ya esta lista para ser expulsada por su rampa.

  Returns:
    True si detecta una pieza (sensor a oscuras), False si no hay nada.
  """
  global _data, _code, _active, speed, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  logging.log(logging.TRACE0, '-')
  return TXT_SLD_E5_I3_photo_transistor.is_dark()

# Se activa el fototransistor del cilindro blanco
def isWhite():
  """
  Pregunta si la pieza ha caido por la rampa de piezas blancas.

  Mira el fototransistor del carril blanco: si algo le tapa la luz, la
  pieza expulsada ha caido en esa rampa.

  Returns:
    True si detecta una pieza (sensor a oscuras), False si no hay nada.
  """
  global _data, _code, _active, speed, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  logging.log(logging.TRACE0, '-')
  return TXT_SLD_E5_I6_photo_transistor.is_dark()

# Se activa el fototransistor del cilindro rojo
def isRed():
  """
  Pregunta si la pieza ha caido por la rampa de piezas rojas.

  Mira el fototransistor del carril rojo: si algo le tapa la luz, la
  pieza expulsada ha caido en esa rampa.

  Returns:
    True si detecta una pieza (sensor a oscuras), False si no hay nada.
  """
  global _data, _code, _active, speed, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  logging.log(logging.TRACE0, '-')
  return TXT_SLD_E5_I7_photo_transistor.is_dark()

# Se activa el fototransistor del cilindro azul
def isBlue():
  """
  Pregunta si la pieza ha caido por la rampa de piezas azules.

  Mira el fototransistor del carril azul: si algo le tapa la luz, la
  pieza expulsada ha caido en esa rampa.

  Returns:
    True si detecta una pieza (sensor a oscuras), False si no hay nada.
  """
  global _data, _code, _active, speed, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  logging.log(logging.TRACE0, '-')
  return TXT_SLD_E5_I8_photo_transistor.is_dark()

# Devuelve el valor de voltaje del sensor de color en función del color
def readColorValue():
  """
  Lee el voltaje que da el sensor de color en este instante.

  Cuanto mas clara es la pieza que pasa por debajo, menos voltaje
  devuelve el sensor.

  Returns:
    El voltaje leido por el sensor de color.
  """
  global _data, _code, _active, speed, state_code, state_active, thresh_white_red_defaults, thresh_red_blue_defaults, thresh_white_red, thresh_red_blue, _ts_state, detectedColorValue, lastColorValue, counter, lastStateCounterSwitch
  logging.log(logging.TRACE0, '-')
  return TXT_SLD_E5_I2_color_sensor.get_voltage()



# Estado publicado de los cilindros de clasificación SLD.
sld_cylinder_state = {"cyl_color": None, "active": False}

def get_sld_cylinder_state():
  """
  Devuelve una copia del estado actual de los cilindros de la SLD.

  Returns:
    Un diccionario con el color que se esta expulsando ahora mismo y si
    algun cilindro esta activo.
  """
  return sld_cylinder_state.copy()
