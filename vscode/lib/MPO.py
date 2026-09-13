"""Control de la estación MPO (procesado, horno y sierra).

La MPO coordina dos controladores: en ``TXT_MPOov_E3``, M1 desplaza la
plataforma del horno, M2 mueve el brazo, O5 controla el vacío de la ventosa,
O6 baja o sube el brazo, O7 acciona la puerta y O8 alimenta el compresor y la
luz O8 simula el horno. En ``TXT_MPOmi_E4``, M1 gira la mesa, M2 la sierra,
M3 la cinta, O7 expulsa la pieza y O8 alimenta el compresor. El ciclo crea
hilos daemon para movimientos simultáneos y para publicar el estado. No crea
``RLock`` directamente; los locks de ejes pertenecen a sus módulos de
referencia.
"""

# El ciclo MPO encadena horno, mesa, sierra y cinta de salida.
import logging
import time
from fischertechnik.controller.Motor import Motor
from lib.Axes2Ref import *
from lib.controller import *
from lib.display import *
from lib.Factory_Variables import *
from lib.MPO_Display import *
from lib.MPO_MQTT import *
from lib.SLD import *
import threading

_code = None
_active = None
on = None
state_code = None
state_active = None
_ts_state = None

# Posicion de reposo de la MPO
def parkMPO():
  """
  Deja la estacion MPO aparcada en su posicion de reposo.

  Abre la puerta del horno y, a la vez, retrae la plataforma del horno,
  mueve el brazo y gira la mesa hasta sus posiciones de referencia; al
  terminar, apaga el compresor.

  Returns:
    None.
  """
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE, '-')
  openDoor()
  time.sleep(0.3)
  th1 = threading.Thread(target=move2Ref, args=(3, ), daemon=True)  # Mover el brazo horno a la posición de la turntable
  th2 = threading.Thread(target=move2Ref, args=(6, ), daemon=True)  # Mover pieza plataforma horno para fuera del horno
  th3 = threading.Thread(target=move2Ref, args=(7, ), daemon=True)  # Mover la turntable a la posción del brazo horno
  th1.start() # Se inician las acciones
  th2.start()
  th3.start()
  th1.join()  # Esperar a que terminen los hilos con las acciones anteriores
  th2.join()
  th3.join()
  TXT_MPOmi_E4_O8_compressor.off()  # Cuando se terminan las acciones se apaga el compresor

def thread_MPO():
  """Ejecuta el ciclo daemon del horno, la mesa, la sierra y la cinta MPO.

  Coordina motores, sensores, valvulas y el LED que simula la coccion. Crea
  hilos daemon para movimientos simultaneos y actualiza el estado publicado;
  los locks de referencia pertenecen al modulo de ejes.

  Returns:
    None. El ciclo permanece activo mientras funciona la fabrica.
  """
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE, '-')
  moveRefMPO()  # Lleva todos los actuadores a su posición inicial de seguridad
  _set_state_MPO(1, 0)  # Estado inicial: code=1, active=0
  threading.Thread(target=thread_update_MPO, daemon=True).start()
  while True:
    # CASO 1: Si hay un error global en esta estación
    if (get_factory_error_state()) == 'MPO':
      _set_state_MPO(4, 0)  # Estado de error: code=4, active=0
      display.set_attr("txt_label_message.text", str('ERROR MPO: Please confirm with ACK button!'))
    # CASO 2: Se detecta una pieza en la entrada del horno (sensor óptico)
    elif isOvenTriggered():
      logging.debug('burn')
      _set_state_MPO(2, 1)  # Estado de burn: code=2, active=1
      openDoor()  # Abre la puerta del horno
      time.sleep(3.5)  # Espera 3.5 segundos a que la pieza entre en el horno
      move2Ref(5) # Mover la pieza del horno para dentro
      closeDoor() # Cierra la puerta del horno
      time.sleep(1)
      logging.debug('transport')
      th1 = threading.Thread(target=move2Ref, args=(4, ), daemon=True)  # Mueve el brazo a la posición del horno
      th2 = threading.Thread(target=move2Ref, args=(7, ), daemon=True)  # Mueve la turntable a la posicion del brazo
      th1.start()
      th2.start()
      for count in range(14): # Enciende y apaga el LED del horno 14 veces simulando el proceso de cocción
        setLightOven(True)
        time.sleep(0.2)
        setLightOven(False)
        time.sleep(0.2)
      openDoor()  # Abre la puerta del horno
      time.sleep(1)
      move2Ref(6) # Mueve la pieza del horno para fuera
      th1.join()  # Espera a que terminen de moverse el brazo y la turntable a la posición especificada
      th2.join()
      pickup()  # Bajar brazo, coger pieza y subir
      move2Ref(3) # Mover el brazo a la posición de la turntable
      release() # Bajar brazo, soltar pieza y subir
      logging.debug('saw')
      move2Ref(10)  # Mover la turntable a la posicion de la sierra
      time.sleep(1)
      setSawRight() # Girar sierra a derechas
      time.sleep(2.5)
      setSawLeft()  # Girar sierra a izquierdas
      time.sleep(2.5)
      setSawOff() # Apagar sierra
      time.sleep(1)
      logging.debug('belt')
      move2Ref(9) # Mover la turntable a la posicion de la cinta de salida
      eject() # Expulsar pieza de la turntable con el cilindro
      convBeltMPO(True)  # Enciende la cinta de salida
      #SLD conveyeor belt on:
      setConvBeltSpeedSLD(300)  # Establece la velocidad de la cinta SLD
      while True:
        if isEndConveyorBeltTriggered():  # Se detecta la pieza al final de la cinta de salida
          time.sleep(5)
          convBeltMPO(False)  # Apaga la cinta de salida
          break
        time.sleep(0.01)
      _set_state_MPO(1, 0)  # Estado inicial: code=1, active=0
    time.sleep(1)

# Actualiza la pantalla y el estado de la estación
def thread_update_MPO():
  """
  Publica el estado de la MPO cada 10 segundos, en su propio hilo.

  Actualiza el indicador en pantalla y avisa por MQTT del codigo de estado
  y de si la estacion esta activa.

  Returns:
    None. Es un bucle infinito, nunca termina por si solo.
  """
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE, '-')
  _ts_state = 0
  while True:
    if (time.time() * 1000) - _ts_state > 10000:
      update_display_MPO(state_code)
      publish_state_MPO(state_code, state_active)
      _ts_state = (time.time() * 1000)
    time.sleep(1)

# Establece el estado de la estación (código, activación)
def _set_state_MPO(_code, _active):
  """
  Guarda el nuevo estado de la MPO si algo ha cambiado.

  Si el codigo o el indicador de actividad son distintos de los que ya
  estaban guardados, los actualiza y reinicia el cronometro para que
  ``thread_update_MPO`` avise cuanto antes por pantalla y MQTT.

  Args:
    _code: Codigo de estado de la MPO (por ejemplo 1=reposo, 2=procesando,
      4=error).
    _active: Indica si la estacion esta activa haciendo ese estado.

  Returns:
    None.
  """
  global on, state_code, state_active, _ts_state
  logging.log(logging.TRACE0, '-')
  if state_code != _code or state_active != _active:
    _ts_state = 0
    state_code = _code
    state_active = _active

# Hacer girar la cinta de salida
def convBeltMPO(on):
  """
  Enciende o apaga la cinta de salida de la MPO.

  Args:
    on: True para poner en marcha la cinta, False para pararla.

  Returns:
    None.
  """
  global _code, _active, state_code, state_active, _ts_state
  logging.log(logging.TRACE, '-')
  if on:  # Girar cinta
    TXT_MPOmi_E4_M3_motor.set_speed(int(512), Motor.CW)
    TXT_MPOmi_E4_M3_motor.start()
  else: # Detener cinta
    TXT_MPOmi_E4_M3_motor.stop()

# Coger pieza con el brazo
def pickup():
  """
  Baja el brazo del horno, agarra la pieza con la ventosa y vuelve a subir.

  Returns:
    None.
  """
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE, '-')
  lowering(True)  # Bajar brazo
  time.sleep(1)
  vacuum(True)  # Coger la pieza con la ventosa
  time.sleep(1)
  lowering(False)  # Subir brazo

# Soltar pieza del brazo
def release():
  """
  Baja el brazo del horno, suelta la pieza y vuelve a subir.

  Al terminar, apaga tambien el compresor de aire.

  Returns:
    None.
  """
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE, '-')
  lowering(True)  # Bajar brazo
  time.sleep(0.4)
  vacuum(False)  # Soltar la pieza
  time.sleep(0.3)
  lowering(False)  # Subir brazo
  time.sleep(0.5)
  TXT_MPOmi_E4_O8_compressor.off()  # Apagar compresor

# Mover todos los actuadores a la referencia
def moveRefMPO():
  """
  Lleva todos los mecanismos de la MPO a su posicion de referencia.

  Abre la puerta del horno y, a la vez, retrae la plataforma del horno,
  mueve el brazo y gira la mesa hasta sus finales de carrera; despues
  cierra la puerta y apaga el compresor.

  Returns:
    None.
  """
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE, '-')
  openDoor()
  time.sleep(0.3)
  th1 = threading.Thread(target=move2Ref, args=(3, ), daemon=True)  # Motor plataforma entrada/salida horno
  th2 = threading.Thread(target=move2Ref, args=(6, ), daemon=True)  # Motor brazo
  th3 = threading.Thread(target=move2Ref, args=(7, ), daemon=True)  # Motor turntable
  th1.start()
  th2.start()
  th3.start()
  th1.join()
  th2.join()
  th3.join()
  closeDoor()
  TXT_MPOmi_E4_O8_compressor.off()  # Apagar compresor
  logging.debug('ref finished')

# Obtiene el código de la estación (1,2,4)
def get_state_code_MPO():
  """
  Devuelve el codigo de estado actual de la MPO.

  Returns:
    El codigo de estado guardado (por ejemplo 1=reposo, 2=procesando,
    4=error).
  """
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE0, '-')
  return state_code

# Obtiene el estado de la estación (0,1)
def get_state_active_MPO():
  """
  Dice si la MPO esta activa haciendo su estado actual.

  Returns:
    True si la estacion esta ocupada con la tarea de ``state_code``, False
    si esta libre.
  """
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE0, '-')
  return state_active

# Detener el giro de la sierra
def setSawOff():
  """
  Detiene el giro de la sierra.

  Returns:
    None.
  """
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE, '-')
  TXT_MPOmi_E4_M2_motor.stop()

# Girar sierra a la izquierda
def setSawLeft():
  """
  Pone la sierra a girar hacia la izquierda.

  Returns:
    None.
  """
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE, '-')
  TXT_MPOmi_E4_M2_motor.set_speed(int(512), Motor.CCW)
  TXT_MPOmi_E4_M2_motor.start()

# Girar sierra a la derecha
def setSawRight():
  """
  Pone la sierra a girar hacia la derecha.

  Returns:
    None.
  """
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE, '-')
  TXT_MPOmi_E4_M2_motor.set_speed(int(512), Motor.CW)
  TXT_MPOmi_E4_M2_motor.start()

# Activa el cilindro expulsor de la turntable
def eject():
  """
  Empuja la pieza fuera de la mesa giratoria con el cilindro expulsor.

  Enciende el compresor, activa un instante la valvula del cilindro para
  dar el empujon y lo apaga otra vez, junto con el compresor.

  Returns:
    None.
  """
  global _code, _active, on, state_code, state_active, _ts_state
  global turntable_state
  logging.log(logging.TRACE, '-')
  TXT_MPOmi_E4_O8_compressor.on()  # Encender compresor
  time.sleep(0.4)
  TXT_MPOmi_E4_O7_magnetic_valve.on() # Activa cilindro de la turntable
  turntable_state["eject"] = True
  time.sleep(0.1)
  TXT_MPOmi_E4_O7_magnetic_valve.off()  # Desactiva cilindro de la turntable
  TXT_MPOmi_E4_O8_compressor.off()  # Apagar compresor
  turntable_state["eject"] = False

# Hacer vacío en el brazo horno para coger la pieza
def vacuum(on):
  """
  Activa o desactiva el vacio de la ventosa del brazo del horno.

  Args:
    on: True para hacer vacio y agarrar la pieza, False para soltarla.

  Returns:
    None.
  """
  global _code, _active, state_code, state_active, _ts_state
  global arm_state
  logging.log(logging.TRACE, '-')
  if on:
    TXT_MPOov_E3_O5_magnetic_valve.on() # Vacío
  else:
    TXT_MPOov_E3_O5_magnetic_valve.off()  # No vacío
  arm_state["vacuum"] = bool(on)

# Control del movimiento vertical del brazo del horno.
def lowering(on):
  """
  Baja o sube el brazo del horno.

  Args:
    on: True para bajar el brazo, False para subirlo.

  Returns:
    None.
  """
  global _code, _active, state_code, state_active, _ts_state
  global arm_state
  logging.log(logging.TRACE, '-')
  if on:
    TXT_MPOov_E3_O6_magnetic_valve.on() # Bajar
  else:
    TXT_MPOov_E3_O6_magnetic_valve.off()  # Subir
  arm_state["lowering"] = bool(on)

# Sube puerta del horno
def openDoor():
  """
  Sube la puerta del horno para dejarlo abierto.

  Returns:
    None.
  """
  global _code, _active, on, state_code, state_active, _ts_state
  global oven_state
  logging.log(logging.TRACE, '-')
  TXT_MPOmi_E4_O8_compressor.on()
  TXT_MPOov_E3_O7_magnetic_valve.on()
  oven_state["open_door"] = True
  oven_state["close_door"] = False

# Cierra puerta del horno
def closeDoor():
  """
  Baja la puerta del horno para dejarlo cerrado.

  Returns:
    None.
  """
  global _code, _active, on, state_code, state_active, _ts_state
  global oven_state
  logging.log(logging.TRACE, '-')
  TXT_MPOov_E3_O7_magnetic_valve.off()
  oven_state["open_door"] = False
  oven_state["close_door"] = True

# Encender y apagar la luz del horno
def setLightOven(on):
  """
  Enciende o apaga el LED que simula el fuego del horno.

  Args:
    on: True para encender la luz, False para apagarla.

  Returns:
    None.
  """
  global _code, _active, state_code, state_active, _ts_state
  global oven_state
  logging.log(logging.TRACE, '-')
  if on:
    TXT_MPOov_E3_O8_led.set_brightness(int(512))  # Encender
  else:
    TXT_MPOov_E3_O8_led.set_brightness(0)  # Apagar
  oven_state["lights"] = bool(on)

# Detección de piezas en la entrada del horno.
def isOvenTriggered():
  """
  Pregunta si hay una pieza en la entrada del horno.

  Mira el fototransistor de la entrada del horno: si algo le tapa la luz,
  es que ha llegado una pieza.

  Returns:
    True si detecta una pieza (sensor a oscuras), False si no hay nada.
  """
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE0, '-')
  return TXT_MPOov_E3_I5_photo_transistor.is_dark()

# Detección final de la cinta de salida
def isEndConveyorBeltTriggered():
  """
  Pregunta si hay una pieza al final de la cinta de salida de la MPO.

  Mira el fototransistor del final de la cinta: si algo le tapa la luz, es
  que la pieza ha llegado al final.

  Returns:
    True si detecta una pieza (sensor a oscuras), False si no hay nada.
  """
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE0, '-')
  return TXT_MPOmi_E4_I4_photo_transistor.is_dark()


# Estados publicados de horno, brazo y mesa giratoria.
oven_state = {
    "close_door": False,
    "open_door": False,
    "lights": False,
    "move2Ref5": False,
    "move2Ref6": False,
}

def get_oven_state():
  """
  Devuelve una copia del estado actual del horno.

  Returns:
    Un diccionario con si la puerta esta abierta o cerrada, si la luz
    esta encendida y en que posicion estan la plataforma y el brazo.
  """
  return oven_state.copy()


arm_state = {
    "lowering": False,
    "vacuum": False,
}

def get_arm_state():
  """
  Devuelve una copia del estado actual del brazo del horno.

  Returns:
    Un diccionario con si el brazo esta bajado y si la ventosa esta
    haciendo vacio.
  """
  return arm_state.copy()


turntable_state = {"eject": False}

def get_turntable_state():
  """
  Devuelve una copia del estado actual de la mesa giratoria.

  Returns:
    Un diccionario con si el cilindro expulsor esta activado.
  """
  return turntable_state.copy()

