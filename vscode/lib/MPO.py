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
  # # Se mueve la turntable a una posición de seguridad
  # TXT_MPOmi_E4_M1_motor.set_speed(int(512), Motor.CW)
  # TXT_MPOmi_E4_M1_motor.start()
  # time.sleep(1.1)
  # TXT_MPOmi_E4_M1_motor.stop()
  # # Se mueve el brazo horno a una posición de seguridad
  # TXT_MPOov_E3_M2_motor.set_speed(int(512), Motor.CW)
  # TXT_MPOov_E3_M2_motor.start()
  # time.sleep(4.5)
  # TXT_MPOov_E3_M2_motor.stop()
  # logging.debug('ref finished')

# Función principal que planifica todo el proceso de la estación de procesamiento (horno, sierra, cinta)
def thread_MPO():
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
  global on, state_code, state_active, _ts_state
  logging.log(logging.TRACE0, '-')
  if state_code != _code or state_active != _active:
    _ts_state = 0
    state_code = _code
    state_active = _active

# Hacer girar la cinta de salida
def convBeltMPO(on):
  global _code, _active, state_code, state_active, _ts_state
  logging.log(logging.TRACE, '-')
  if on:  # Girar cinta
    TXT_MPOmi_E4_M3_motor.set_speed(int(512), Motor.CW)
    TXT_MPOmi_E4_M3_motor.start()
  else: # Detener cinta
    TXT_MPOmi_E4_M3_motor.stop()

# Coger pieza con el brazo
def pickup():
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE, '-')
  lowering(True)  # Bajar brazo
  time.sleep(1)
  vacuum(True)  # Coger la pieza con la ventosa
  time.sleep(1)
  lowering(False)  # Subir brazo

# Soltar pieza del brazo
def release():
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
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE0, '-')
  return state_code

# Obtiene el estado de la estación (0,1)
def get_state_active_MPO():
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE0, '-')
  return state_active

# Detener el giro de la sierra
def setSawOff():
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE, '-')
  TXT_MPOmi_E4_M2_motor.stop()

# Girar sierra a la izquierda
def setSawLeft():
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE, '-')
  TXT_MPOmi_E4_M2_motor.set_speed(int(512), Motor.CCW)
  TXT_MPOmi_E4_M2_motor.start()

# Girar sierra a la derecha
def setSawRight():
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE, '-')
  TXT_MPOmi_E4_M2_motor.set_speed(int(512), Motor.CW)
  TXT_MPOmi_E4_M2_motor.start()

# Activa el cilindro expulsor de la turntable
def eject():
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
  global _code, _active, state_code, state_active, _ts_state
  global arm_state
  logging.log(logging.TRACE, '-')
  if on:
    TXT_MPOov_E3_O5_magnetic_valve.on() # Vacío
  else:
    TXT_MPOov_E3_O5_magnetic_valve.off()  # No vacío
  arm_state["vacuum"] = bool(on)

# Función para bajar y subir el brazo del horno
def lowering(on):
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
  global _code, _active, on, state_code, state_active, _ts_state
  global oven_state
  logging.log(logging.TRACE, '-')
  TXT_MPOmi_E4_O8_compressor.on()
  TXT_MPOov_E3_O7_magnetic_valve.on()
  oven_state["open_door"] = True
  oven_state["close_door"] = False

# Cierra puerta del horno
def closeDoor():
  global _code, _active, on, state_code, state_active, _ts_state
  global oven_state
  logging.log(logging.TRACE, '-')
  TXT_MPOov_E3_O7_magnetic_valve.off()
  oven_state["open_door"] = False
  oven_state["close_door"] = True

# Encender y apagar la luz del horno
def setLightOven(on):
  global _code, _active, state_code, state_active, _ts_state
  global oven_state
  logging.log(logging.TRACE, '-')
  if on:
    TXT_MPOov_E3_O8_led.set_brightness(int(512))  # Encender
  else:
    TXT_MPOov_E3_O8_led.set_brightness(0)  # Apagar
  oven_state["lights"] = bool(on)

# Función para detectar una pieza a la entrada del horno
def isOvenTriggered():
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE0, '-')
  return TXT_MPOov_E3_I5_photo_transistor.is_dark()

# Detección final de la cinta de salida
def isEndConveyorBeltTriggered():
  global _code, _active, on, state_code, state_active, _ts_state
  logging.log(logging.TRACE0, '-')
  return TXT_MPOmi_E4_I4_photo_transistor.is_dark()


###########################################################################################
# TODO:
###########################################################################################
oven_state = {
    "close_door": False,
    "open_door": False,
    "lights": False,
    "move2Ref5": False,
    "move2Ref6": False,
}

def get_oven_state():
    return oven_state.copy()


arm_state = {
    "lowering": False,
    "vacuum": False,
}

def get_arm_state():
    return arm_state.copy()


turntable_state = {"eject": False}

def get_turntable_state():
  return turntable_state.copy()
###########################################################################################
###########################################################################################

