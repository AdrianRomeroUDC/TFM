"""Control de la estación HBW (High Bay Warehouse).

La estación almacena y recupera piezas mediante una cinta y un brazo cartesiano.
Los motores M1, M2, M3 y M4 están definidos en :mod:`lib.controller`: M1 mueve
la cinta, M2 el eje horizontal, M3 extiende o retrae el brazo y M4 mueve el eje
vertical. Las funciones de movimiento de :mod:`lib.HBW_AxesNRef` usan un
``threading.RLock`` para serializar el acceso a los ejes; este módulo también
crea hilos daemon para la actualización de estado y para la cinta de entrada o
salida. No crea locks adicionales.
"""

# El ciclo HBW coordina cinta, brazo cartesiano, inventario y confirmaciones.
import logging
import time
from fischertechnik.controller.Motor import Motor
from lib.controller import *
from lib.display import *
from lib.Factory import *
from lib.Factory_Variables import *
from lib.HBW_AxesNRef import *
from lib.HBW_Display import *
from lib.HBW_MQTT import *
from lib.HBW_Storage import *

_tr0 = None
_tr = None
_dg = None
_code = None
_active = None
wp = None
state_code = None
state_active = None
res = None
thread_OutIn = None
_ts_state = None
thread_InOut = None
wp_color = None
ts0_OutIn = None
ts0_InOut = None
tsdiff_OutIn = None
tsdiff_InOut = None
valid = None
ts0 = None
wp_uid = None
tsdiff = None
def thread_HBW():
  """Ejecuta el ciclo de trabajo de thread_HBW y mantiene su servicio activo cuando corresponde.

  Coordina las peticiones del VGR, el inventario persistente, la cinta y el
  brazo cartesiano. Las operaciones de ejes se serializan con el ``RLock`` de
  ``HBW_AxesNRef`` y el hilo publica estados en la interfaz y MQTT.

  Returns:
    None. El ciclo permanece activo durante toda la vida de la aplicacion.
  """
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  thread_OutIn = None
  thread_InOut = None
  init_HBW()
  initStorage()
  updateStorageState()
  _set_state_HBW(1, 0)
  threading.Thread(target=thread_update_HBW, daemon=True).start()
  while True:
    if (get_factory_error_state()) == 'HBW':
      print('HBW: error')
      _set_state_HBW(4, 1)
      display.set_attr("txt_label_message.text", str('ERROR HBW: Please confirm with ACK button!'))
    elif (getwp_reqHBW_fetchContainer()) != None:
      print('HBW: fetch container')
      if checkAndCorrectStorage():
        updateStorageState()
      _set_state_HBW(2, 1)
      ackHBW_fetchContainer(1)
      valid = True
      if valid:
        ts0 = (time.time() * 1000)
        while True:
          wp = getwp_reqHBW_fetchContainer()
          #wp: ts, uid, color, state
          if wp != None:
            wp_uid = wp[1]
            wp_color = wp[2]
            valid = True
            break
          else:
            wp_uid = ''
            wp_color = None
          tsdiff = (time.time() * 1000) - ts0
          if tsdiff > 60000:
            logging.log(logging.DEBUG_HBW, 'timeout %d', tsdiff)
            valid = wp_color != None and wp_uid != ''
            break
          time.sleep(1)
      if True:
        #TODO: error empty container -> new
        if valid:
          if can_color_be_stored(wp_color):
            valid = fetchContainerHBW()
      if False:
        #TODO: error empty container -> new
        if valid:
          if can_color_be_stored(wp_color):
            ts0 = (time.time() * 1000)
            while True:
              valid = fetchContainerHBW()
              if valid:
                if (getstate_ackHBW_fetchContainer()) == 2:
                  print(getstate_ackHBW_fetchContainer())
                  valid = False
                  break
              tsdiff = (time.time() * 1000) - ts0
              if tsdiff > 50000:
                logging.log(logging.DEBUG_HBW, 'timeout %d', tsdiff)
                valid = False
                break
              time.sleep(1)
      if valid:
        logging.log(logging.DEBUG_HBW, 'wait WP Out')
        thread_InOut.join()
      if valid:
        ts0 = (time.time() * 1000)
        while True:
          if wp != None:
            logging.log(logging.DEBUG_HBW, wp)
            ackHBW_fetchContainer(2)
            time.sleep(1)
            #wp list: 1:ts, 2:uid, 3:color, 4:produced
            valid = storeHBW([time.time(), wp_uid, wp_color, False])
            break
          tsdiff = (time.time() * 1000) - ts0
          if tsdiff > 20000:
            logging.log(logging.DEBUG_HBW, 'timeout %d', tsdiff)
            valid = wp_color != None and wp_uid != ''
            break
          time.sleep(1)
      if not valid:
        valid = storeContainerHBW()
      if valid:
        ackHBW_fetchContainer(3)
        moveRef_HBW_P12()
        if checkAndCorrectStorage():
          updateStorageState()
    elif (getwp_reqHBW_fetchWP()) != None:
      print('HBW: fetch WP')
      _set_state_HBW(2, 1)
      wp = getwp_reqHBW_fetchWP()
      logging.log(logging.DEBUG_HBW, wp)
      valid = wp != None
      if valid:
        #wp: ts, uid, color, state
        wp_color = wp[2]
        logging.log(logging.DEBUG_HBW, wp_color)
        valid = wp_color != None
      if valid:
        valid = (get_num_color_stored(wp_color)) > 0
      if valid:
        valid = fetchHBW(wp)
      if valid:
        ts0 = (time.time() * 1000)
        while not (getstate_ackHBW_fetchWP()) == 1:
          tsdiff = (time.time() * 1000) - ts0
          logging.log(logging.DEBUG_HBW, 'check ack fetchWP %.1f', tsdiff)
          if valid and tsdiff < 50000:
            time.sleep(1) # TODO: modificado por Lustres
            ackHBW_fetchWP(1)
            valid = True
            break
          else:
            logging.log(logging.DEBUG_VGR, 'timeout ack fetchWP')
            valid = False
          time.sleep(1)
      if valid:
        logging.log(logging.DEBUG_HBW, 'wait WP Out')
        moveRef_HBW_P12()
        ackHBW_fetchWP(2)
        moveConv()
        movePut1()
        valid = storeContainerHBW()
      if valid:
        moveRef_HBW_P12()
    elif isTriggeredIn() or isTriggeredOut():
      print('HBW: In Out blocked')
      set_factory_error_state('HBW')
      display.set_attr("txt_label_message2.text", str('In Out blocked'))
    _set_state_HBW(1, 0)
    time.sleep(1)



def initlog_HBW(_tr0, _tr, _dg):
  """
  Da de alta los niveles de registro propios del almacen (HBW).

  Crea las etiquetas TRACE0_HBW, TRACE_HBW y DEBUG_HBW para que los mensajes
  de esta estacion se puedan filtrar en el log aparte del resto.

  Args:
    _tr0: Nivel numérico para el trazado mas detallado.
    _tr: Nivel numérico para el trazado normal.
    _dg: Nivel numérico para los mensajes de depuración.

  Returns:
    None.
  """
  global _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.TRACE0_HBW = _tr0
  logging.addLevelName(logging.TRACE0_HBW , 'TRACE0_HBW')
  logging.TRACE_HBW = _tr
  logging.addLevelName(logging.TRACE_HBW , 'TRACE_HBW')
  logging.DEBUG_HBW = _dg
  logging.addLevelName(logging.DEBUG_HBW, 'DEBUG_HBW')


def parkHBW():
  """
  Detiene la cinta del almacen y aparca el brazo en reposo.

  Para el motor de la cinta, lleva los dos ejes del brazo a su posicion de
  referencia y despues lo deja en unas coordenadas fijas de aparcado.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  stop_HBW()
  moveRef_HBW_P12()
  moveAbs_HBW_P12_list([170, 850])


def thread_update_HBW():
  """
  Publica el estado del almacen cada 10 segundos, en su propio hilo.

  Actualiza el indicador en pantalla y avisa por MQTT del codigo de estado
  y de si el almacen esta activo.

  Returns:
    None. Es un bucle infinito, nunca termina por si solo.
  """
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  _ts_state = 0
  while True:
    if (time.time() * 1000) - _ts_state > 10000:
      update_display_HBW(state_code)
      publish_state_HBW(state_code, state_active)
      _ts_state = (time.time() * 1000)
    time.sleep(1)


def _set_state_HBW(_code, _active):
  """
  Guarda el nuevo estado del almacen si algo ha cambiado.

  Si el codigo o el indicador de actividad son distintos de los que ya
  estaban guardados, los actualiza y reinicia el cronometro para que
  ``thread_update_HBW`` avise cuanto antes por pantalla y MQTT.

  Args:
    _code: Codigo de estado del almacen (por ejemplo 1=reposo, 2=en
      movimiento, 4=error).
    _active: Indica si el almacen esta activo haciendo ese estado.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE0_HBW, '-')
  if state_code != _code or state_active != _active:
    _ts_state = 0
    state_code = _code
    state_active = _active


def get_state_code_HBW():
  """
  Devuelve el codigo de estado actual del almacen.

  Returns:
    El codigo de estado guardado (por ejemplo 1=reposo, 2=en movimiento,
    4=error).
  """
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE0_HBW, '-')
  return state_code


def get_state_active_HBW():
  """
  Dice si el almacen esta activo haciendo su estado actual.

  Returns:
    True si el almacen esta ocupado con la tarea de ``state_code``, False
    si esta libre.
  """
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE0_HBW, '-')
  return state_active


def updateStorageState():
  """
  Guarda el inventario en disco y avisa a pantalla y MQTT de su contenido.

  Escribe el estado actual del almacen en el fichero de inventario,
  actualiza en pantalla cuantas piezas blancas, rojas y azules hay
  guardadas, y publica la lista completa del almacen por MQTT.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  saveFileStorage()
  update_display_WP(get_num_color_stored('WHITE'), get_num_color_stored('RED'), get_num_color_stored('BLUE'))
  publish_state_Storage(get_list_storage())


def storeHBW(wp):
  """
  Mete una pieza nueva en el almacen y la coloca en su estante.

  Apunta la pieza en el inventario, hace avanzar la cinta hasta que el
  contenedor llega a la posicion del brazo, y mueve el brazo cartesiano
  para cogerlo y dejarlo en el primer hueco libre del estante.

  Args:
    wp: Datos de la pieza a guardar: ``[timestamp, uid, color, producida]``.

  Returns:
    True si la pieza quedo guardada correctamente, False si algo fallo.
  """
  global _tr0, _tr, _dg, _code, _active, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, 'wp=%d %s %s %d', wp[0], wp[1], wp[2], wp[3])
  #wp list: 1:ts, 2:uid, 3:color, 4:produced
  res = storeWorkpiece(wp)
  if res:
    logging.log(logging.DEBUG_HBW, 'true')
    #already in conv pos
    thread_OutIn = threading.Thread(target=thread_ConvOutIn, daemon=True)
    thread_OutIn.start()
    thread_OutIn.join()
    moveGet2()
    moveCR(get_nextfetchpos_xy())
    movePut()
    updateStorageState()
  return res


def storeContainerHBW():
  """
  Guarda un contenedor vacio de vuelta en el almacen.

  Igual que ``storeHBW`` pero para un contenedor sin pieza dentro: hace
  avanzar la cinta y mueve el brazo para dejarlo en el estante.

  Returns:
    True si el contenedor quedo guardado correctamente, False si algo
    fallo.
  """
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  res = storeContainer()
  if res:
    logging.log(logging.DEBUG_HBW, 'true')
    thread_OutIn = threading.Thread(target=thread_ConvOutIn, daemon=True)
    thread_OutIn.start()
    thread_OutIn.join()
    moveGet2()
    moveCR(get_nextfetchpos_xy())
    movePut()
    updateStorageState()
  return res


def fetchHBW(wp):
  """
  Saca del almacen una pieza del color pedido.

  Busca en el estante un hueco con una pieza de ese color, mueve el brazo
  hasta alli, la coge y la deja en la cinta; mientras tanto pone en marcha
  la cinta para que la lleve hacia fuera, hacia el VGR.

  Args:
    wp: Datos de la pieza pedida; solo se usa su color.

  Returns:
    True si se encontro y se saco una pieza de ese color, False si no.
  """
  global _tr0, _tr, _dg, _code, _active, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, wp)
  #wp list: 1:ts, 2:uid, 3:color, 4:produced
  wp_color = wp[2]
  res = fetchWorkpiece(wp_color)
  if res:
    logging.log(logging.DEBUG_HBW, 'true')
    moveCR(get_nextfetchpos_xy())
    moveGet()
    moveConv()
    thread_InOut = threading.Thread(target=thread_ConvInOut, daemon=True)
    thread_InOut.start()
    movePut1()
    updateStorageState()
  return res


def fetchContainerHBW():
  """
  Saca del almacen un contenedor vacio cualquiera.

  Igual que ``fetchHBW`` pero sin importar el color: coge un contenedor
  vacio del estante y lo deja en la cinta hacia fuera.

  Returns:
    True si se encontro y se saco un contenedor vacio, False si no.
  """
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  res = fetchContainer()
  if res:
    logging.log(logging.DEBUG_HBW, 'true')
    moveCR(get_nextfetchpos_xy())
    moveGet()
    moveConv()
    thread_InOut = threading.Thread(target=thread_ConvInOut, daemon=True)
    thread_InOut.start()
    movePut1()
    updateStorageState()
  return res


def thread_ConvOutIn():
  """
  Mueve la cinta hacia dentro, del lado del VGR hacia el brazo del almacen.

  Enciende el motor de la cinta en un sentido y lo deja correr hasta que
  el fototransistor de dentro (I4) detecta el contenedor o pasan 20
  segundos sin que llegue; en ese momento para la cinta.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  TXT_HBW_E1_M1_motor.set_speed(int(400), Motor.CCW)
  TXT_HBW_E1_M1_motor.start()
  ts0_OutIn = (time.time() * 1000)
  while not isTriggeredIn():
    tsdiff_OutIn = (time.time() * 1000) - ts0_OutIn
    if tsdiff_OutIn > 20000:
      logging.log(logging.DEBUG_HBW, 'timeout %d', tsdiff_OutIn)
      break
    time.sleep(0.05)
  stopConv()


def thread_ConvInOut():
  """
  Mueve la cinta hacia fuera, del brazo del almacen hacia el VGR.

  Enciende el motor de la cinta en el sentido contrario y lo deja correr
  hasta que el fototransistor de fuera (I1) detecta el contenedor o pasan
  20 segundos sin que llegue; entonces para la cinta.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  TXT_HBW_E1_M1_motor.set_speed(int(400), Motor.CW)
  TXT_HBW_E1_M1_motor.start()
  ts0_InOut = (time.time() * 1000)
  while not isTriggeredOut():
    tsdiff_InOut = (time.time() * 1000) - ts0_InOut
    if tsdiff_InOut > 20000:
      logging.log(logging.DEBUG_HBW, 'timeout %d', tsdiff_InOut)
      break
    time.sleep(0.05)
  time.sleep(0.5)
  stopConv()


def stopConv():
  """
  Detiene el motor de la cinta de entrada y salida del almacen.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE_HBW, '-')
  TXT_HBW_E1_M1_motor.stop()


def isTriggeredOut():
  """
  Pregunta si hay un contenedor en el extremo exterior de la cinta.

  Mira el fototransistor de fuera del almacen (I1): si algo le tapa la
  luz, es que un contenedor ha llegado al lado del VGR.

  Returns:
    True si detecta un contenedor (sensor a oscuras), False si no.
  """
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE0_HBW, '-')
  return TXT_HBW_E1_I1_photo_transistor.is_dark()


def isTriggeredIn():
  """
  Pregunta si hay un contenedor en el extremo interior de la cinta.

  Mira el fototransistor de dentro del almacen (I4): si algo le tapa la
  luz, es que un contenedor ha llegado al lado del brazo.

  Returns:
    True si detecta un contenedor (sensor a oscuras), False si no.
  """
  global _tr0, _tr, _dg, _code, _active, wp, state_code, state_active, res, thread_OutIn, _ts_state, thread_InOut, wp_color, ts0_OutIn, ts0_InOut, tsdiff_OutIn, tsdiff_InOut, valid, ts0, wp_uid, tsdiff
  logging.log(logging.TRACE0_HBW, '-')
  return TXT_HBW_E1_I4_photo_transistor.is_dark()


