"""Control del robot VGR (Vertical Gantry Robot).

El VGR es un brazo cartesiano de tres ejes en ``TXT_VGR_E2``: M1 rota, M2
sube o baja y M3 extiende o retrae. La ventosa se controla con el compresor
O7 y la válvula magnética O8. Los finales de carrera I1-I3 y los sensores DPS
I7/C4 e I8 proporcionan realimentación. El ciclo principal y el publicador de
estado usan hilos daemon; las rutinas de ejes de ``VGR_Axes1Ref`` protegen los
movimientos con ``threading.RLock``. Este módulo no crea otro lock.
"""

import logging
import time
from lib.Axes1Ref import *
from lib.controller import *
from lib.display import *
from lib.DPS import *
from lib.Factory import *
from lib.Factory_Variables import *
from lib.File import *
from lib.HBW import *
from lib.HBW_Storage import *
from lib.MQTT import *
from lib.Nfc import *
from lib.Nfc_MQTT import *
from lib.SLD import *
from lib.Sound import *
from lib.VGR_Axes1Ref import *
from lib.VGR_Display import *
from lib.VGR_MQTT import *

_tr0 = None
_tr = None
_dg = None
m = None
k = None
taguid = None
num = None
history_ts = None
w_uid = None
w_color = None
_code = None
_active = None
_target = None
name = None
color = None
mi = None
mask = None
res = None
valid = None
state_code = None
state_active = None
ts_readuid = None
_ts_state = None
count_write = None
list_nfctag_history = None
index = None
state_target = None
uid = None
i = None
map_nfctag_history = None
_state = None
bit = None
_type = None
_mask = None
_vts = None
ts_dsi = None
wp = None
wp_color = None
data = None
req = None
temp = None
ts0 = None
ts_diff = None
last_color = None
ts_cs = None
ack = None

# Thread principal del VGR
def thread_VGR():
  """Ejecuta el ciclo daemon del robot cartesiano VGR.

  Atiende comandos NFC, recoge piezas de SLD y DPS y coordina su entrega al
  HBW. Acciona motores, ventosa y lector NFC; el movimiento se protege con el
  ``RLock`` de ``VGR_Axes1Ref`` y el estado se refleja en interfaz y MQTT.

  Returns:
    None. El ciclo permanece activo durante la ejecucion de la fabrica.
  """
  global _tr0, _tr, _dg, m, k, taguid, num, history_ts, w_uid, w_color, _code, _active, _target, name, color, mi, mask, res, valid, state_code, state_active, ts_readuid, _ts_state, count_write, list_nfctag_history, index, state_target, uid, i, map_nfctag_history, _state, bit, _type, _mask, _vts, ts_dsi, wp, wp_color, data, req, temp, ts0, ts_diff, last_color, ts_cs, ack
  logging.log(logging.TRACE_VGR, '-')
  ts_readuid = 0
  init_VGR()  # Inicializa motores y posiciones
  _set_state_VGR(1, 0, '')  # Estado inicial (code=1, active=0, target='')
  # Lanza un hilo paralelo para actualizar el estado del VGR continuamente
  threading.Thread(target=thread_update_VGR, daemon=True).start()
  while True:
    # CASO 1: Error en el VGR
    if (get_factory_error_state()) == 'VGR':
      print('VGR: error')
      _set_state_VGR(4, 0, '')  # Estado de error (code=4, active=0, target='')
      display.set_attr("txt_label_message.text", str('ERROR VGR: Please confirm with ACK button!'))
    # CASO 2: Comando NFC manuales, si el usuario pide leer o borrar la etiqueta NFC desde la pantalla o la nube
    elif (getcmd_reqVGR_Nfc()) != None:
      print('VGR: check cmd NFC read, delete')
      # Lee solo el ID único de la etiqueta NFC
      if (getcmd_reqVGR_Nfc()) == 'read_uid': 
        logging.log(logging.TRACE_VGR, "read_uid")
        res = nfc_read_uid()
        if res != None:
          beep()
          display.set_attr("txt_label_message.text", str('Read UID NFC'))
          display.set_attr("txt_label_message2.text", str(''))
          publish_Nfc_Data()
      # Lee toda la información de la etiqueta NFC (ID único + datos almacenados)
      elif (getcmd_reqVGR_Nfc()) == 'read': 
        logging.log(logging.TRACE_VGR, "read")
        res = nfc_read()
        if res != None:
          beep()
          display.set_attr("txt_label_message.text", str('Read NFC'))
          display.set_attr("txt_label_message2.text", str(''))
          publish_Nfc_Data()
      # Borra la información almacenada en la etiqueta NFC (no borra el ID único)
      elif (getcmd_reqVGR_Nfc()) == 'delete':
        logging.log(logging.TRACE_VGR, "delete")
        res = nfc_delete()
        if res != None:
          beep()
          display.set_attr("txt_label_message.text", str('Delete NFC'))
          display.set_attr("txt_label_message2.text", str(''))
          publish_Nfc_Data()
      # Comando desconocido
      else:
        display.set_attr("txt_label_message.text", str('Uknown cmd'))
        display.set_attr("txt_label_message2.text", str(''))
      res = reqVGR_Nfc(None)
    # CASO 3: Resetea el estado del almacen. Comprueba si de detecta un chip maestro cada 5 segundos.
    elif time.time() - ts_readuid > 5:
      if (nfc_read_uid()) != None:
        print('VGR: check cmd NFC reset')
        # Si se detecta el chip maestro
        if is_cmd_uid():
          logging.log(logging.TRACE_VGR, "is_cmd_uid")
          beep()
          resetStorage()
          updateStorageState()
          display.set_attr("txt_label_message.text", str('Reset HBW storage'))
          display.set_attr("txt_label_message2.text", str(''))
      ts_readuid = time.time()
    # CASO 4: Si hay piezas listas en las rampas el VGR las recoge
    elif isWhite():
      print('VGR: SLD White')
      display.set_attr("txt_label_message.text", str('Pick up at the sorting line: WHITE'))
      display.set_attr("txt_label_message2.text", str(''))
      moveSLD('SLD white', 'WHITE')
    elif isRed():
      print('VGR: SLD Red')
      display.set_attr("txt_label_message.text", str('Pick up at the sorting line: RED'))
      display.set_attr("txt_label_message2.text", str(''))
      moveSLD('SLD red', 'RED')
    elif isBlue():
      print('VGR: SLD Blue')
      display.set_attr("txt_label_message.text", str('Pick up at the sorting line: BLUE'))
      display.set_attr("txt_label_message2.text", str(''))
      moveSLD('SLD blue', 'BLUE')
    # CASO 5: Si hay piezas listas en el DSI el VGR las recoge, lee su color y las almacena en el HBW
    elif is_dsi():
      print('VGR: DSI')
      set_state_dsi(1)
      ts_dsi = (time.time() * 1000)
      display.set_attr("txt_label_message.text", str('Delivery raw material'))
      display.set_attr("txt_label_message2.text", str(''))
      _set_state_VGR(2, 1, 'hbw') # Estado de entrega a HBW (code=2, active=1, target='hbw')
      valid = True
      wp = [time.time(), None, None, False]
      req = reqHBW_fetchContainer(wp) # Informa al HBW para buscar un contenedor vacío
      #move: ref
      moveRef_VGR_P123()  # Mueve el VGR a la posición de referencia
      #move: DSI
      moveAbs_VGR_P123_name('DSI')  # Se mueve a la posición del DSI para recoger la pieza
      grip()  # Coge la pieza
      #move: Color Reader (discard)
      temp = get_pos3_VGR_name('DSI') # Obtiene la posición del DSI
      temp[1] = get_pos1_discard_VGR_name('DSI')  # Guarda en la coordenada Y la posición de descarte del DSI
      moveAbs_VGR_P123_list(temp) # Eleva el brazo del VGR para evitar colisiones al moverse al Color Reader
      temp = get_pos3_VGR_name('Color Reader')  # Obtiene la posición del Color Reader
      temp[1] = get_pos1_discard_VGR_name('DSI')  # Guarda en la coordenada Y la posición de descarte del DSI para evitar colisiones al moverse al Color Reader
      moveAbs_VGR_P123_list(temp) # Se mueve hacia el color reader con la pieza cogida y elevada para evitar colisiones
      #move: Color Reader
      moveAbs_VGR_P123_name('Color Reader') # Se mueve hacia el Color Reader para leer el color de la pieza
      time.sleep(1)
      last_color = readDPSColor() # Lee el color de la pieza
      valid = last_color != None
      # Si el color es distinto de None
      if valid:
        ts_cs = (time.time() * 1000)  # Guarda el timestamp de la lectura del color
        set_color_event(last_color)
        display.set_attr("txt_label_message2.text", str('Color: {}'.format(last_color)))
      else:
        display.set_attr("txt_label_message2.text", str('Color is None'))
      valid = can_color_be_stored(last_color) # Comprueba si hay 3 piezas almacenadas en el HBW del mismo color, lo que impediría almacenar la pieza actual
      if not valid:
        display.set_attr("txt_label_message2.text", str("Color can't be stored in HBW storage"))
      # Si el color es válido para almacenar en el HBW
      if valid:
        logging.log(logging.DEBUG_VGR, 'color: %s', last_color)
        #move: Color Reader (discard)
        temp = get_pos3_VGR_name('Color Reader')
        temp[1] = get_pos1_discard_VGR_name('DSI')
        moveAbs_VGR_P123_list(temp) # Eleva el brazo del VGR para evitar colisiones al moverse al siguiente destino
        #move: NFC (discard)
        temp = get_pos3_VGR_name('NFC')
        temp[1] = get_pos1_discard_VGR_name('DSI')
        moveAbs_VGR_P123_list(temp) # Se mueve hacia el lector NFC con la pieza cogida y elevada para evitar colisiones
        #move: NFC
        moveAbs_VGR_P123_name('NFC')  # Se mueve hacia el lector NFC para escribir la información de la pieza en una etiqueta NFC
        uid = nfc_read_uid()  # Lee el ID único de la etiqueta NFC
        if uid == None:
          #move: NiO
          NiO_exit()  # Si no se detecta ningún chip NFC, tira la pieza a la cesta de descarte (NiO)
        set_nfctag_item(uid, 1, ts_dsi)  # FIXME: si no habia chip (uid es None), la pieza ya se ha tirado al cesto de descarte y este apunte no sirve de nada
        set_nfctag_item(uid, 2, ts_cs) 
        res = nfc_write_history(uid, last_color)  # Escribe el color de la pieza y los timestamps de la lectura del DSI y del Color Reader en la etiqueta NFC
        if res:
          beep_blocked()  # Pitido para indicar que se ha escrito correctamente en la etiqueta NFC
          logging.log(logging.DEBUG_VGR, 'nfc tag valid')
          valid = uid != None and uid != '' # Verifica que se ha leído un ID único válido de la etiqueta NFC
          if not valid:
            last_color = None
            display.set_attr("txt_label_message2.text", str('Color is None'))
        else: # SI la lectura falla
          #move: NiO
          NiO_exit()  # Si no hay chip, tira la pieza a la cesta de descarte (NiO)
      # Si la etiqueta es válida
      if valid:
        wp = [time.time(), uid, last_color, False]  # Se crea una 'workpiece' pieza de trabajo
        valid = wp != None
        display.set_attr("txt_label_message2.text", str('Color: {} uid: {}'.format(last_color, uid)))
        logging.log(logging.DEBUG_VGR, 'wp valid')
      if valid:
        ts0 = (time.time() * 1000)  # Guarda el timestamp del inicio del proceso de entrega al HBW
        set_state_dsi(0)
        while True:
          logging.log(logging.DEBUG_VGR, 'req fetchContainer')
          req = reqHBW_fetchContainer(wp) # Informa al HBW para buscar un contenedor vacío para almacenar la pieza
          ts_diff = (time.time() * 1000) - ts0
          # Si transcurren más de 20 segundos sin recibir una respuesta válida del HBW, se considera que ha habido un error en el proceso
          if ts_diff > 20000:
            logging.log(logging.DEBUG_VGR, 'timeout req fetchContainer %d', ts_diff)
            valid = False
            break
          ack = getstate_ackHBW_fetchContainer()  # Comprueba si el HBW ha reconocido la petición
          if ack == 1:  # ACK 1: El almacén dice "Estoy en posición, ven a dejarla"
            logging.log(logging.DEBUG_VGR, 'ackHBW=1')
            valid = True
            #move: HBW
            moveAbs_VGR_P123_name('HBW')  # El brazo se mueve hacia el HBW
            temp = get_pos3_VGR_name('HBW') # Obtiene la posición del HBW
            temp[1] = get_pos1_discard_VGR_name('HBW')  # Coordenada eje Y para dejar la pieza en el HBW con la altura de descarte para evitar colisiones
            temp[2] = get_offset_VGR_name('HBW_h')   # Coordenada eje Z para dejar la pieza a la altura del HBW
            moveAbs_VGR_P123_list(temp) # Se mueve a la posición de entrega del HBW
            break
          time.sleep(1)
      # Si se ha entregado la pieza al HBW correctamente
      if valid:
        ts0 = (time.time() * 1000)  
        while True:
          logging.log(logging.DEBUG_VGR, 'ack fetchContainer')
          ts_diff = (time.time() * 1000) - ts0
          # Se espera un máximo de 60 segundos a que el almacén confirme que el contenedor está en posición del VGR
          if ts_diff > 60000:
            logging.log(logging.DEBUG_VGR, 'timeout ack fetchContainer %d', ts_diff)
            valid = False
            break
          ack = getstate_ackHBW_fetchContainer()
          # Si el contenedor está en posición, el almacén responde con ACK 2: "Contenedor en posición, el VGR puede dejar la pieza"
          if ack == 2:
            logging.log(logging.DEBUG_VGR, 'ackHBW=2')
            valid = True
            release() # Soltar la pieza en el HBW
            #move: ref
            moveRef(3)  # Se retrae el brazo VGR
            moveRef_VGR_P123()  # Se mueve a la posición de referencia
            valid = True
            break
          time.sleep(1)
      if valid:
        set_state_dsi(0)
        set_nfctag_item(uid, 3, (time.time() * 1000))
      # Si no salió bien, se considera que ha habido un error en el proceso y se tira la pieza a la cesta de descarte (NiO)
      else:
        #move: NiO
        if False:
          #TODO: move from HBW or NFC?
          temp = get_pos3_VGR_name('HBW')
          temp[2] = get_offset_VGR_name('HBW_h')
          moveAbs_VGR_P123_list(temp)
          moveAbs_VGR_P123_name('HBW')
        moveRef(2) # Se eleva el brazo para evitar colisiones al moverse a la posición de descarte
        moveAbs_VGR_P123_name('NiO')  # Se mueve a la posición de la cesta de descarte (NiO)
        release() # Suelta la pieza en la cesta de descarte
        #move: ref
        moveRef(2)  # Se eleva el brazo para evitar colisiones al moverse a la posición de referencia
        moveRef_VGR_P123()  # Se mueve a la posición de referencia
      display.set_attr("txt_label_message.text", str(''))
      display.set_attr("txt_label_message2.text", str(''))
    # CASO 6: Si el HBW ha recogido la pieza, el VGR la entrega en la posición de producción (MPO) para su mecanizado
    elif (getwp_reqHBW_fetchWP()) != None:
      print('HBW remove')
      set_nfctag_item(uid, 4, (time.time() * 1000)) # Actualiza el chip NFC con el timestamp de inicio de la entrega a producción
      wp = getwp_reqHBW_fetchWP()
      wp_color = wp[2]
      publish_state_order('ORDERED', wp_color)  # Publica en la nube que la pieza está ordenada para producción
      display.set_attr("txt_label_message.text", str('Get raw material for production'))
      display.set_attr("txt_label_message2.text", str(''))
      _set_state_VGR(2, 1, 'mpo') # Estado de entrega a producción (code=2, active=1, target='mpo')
      #move: ref
      moveRef_VGR_P123()  # Mueve el VGR a la posición de referencia
      #move: HBW (over)
      moveAbs_VGR_P123_name('HBW')  # Se mueve hacia el HBW para recoger la pieza
      temp = get_pos3_VGR_name('HBW') # Obtiene la posición del HBW
      temp[2] = get_offset_VGR_name('HBW_h') # Coordenada eje Z para coger la pieza a la altura del HBW
      moveAbs_VGR_P123_list(temp) # Se mueve a la posición de recogida del HBW
      ts0 = (time.time() * 1000)
      while True:
        ts_diff = (time.time() * 1000) - ts0
        logging.log(logging.DEBUG_VGR, 'check ack fetchWP %.1f', ts_diff)
        if ts_diff < 50000:
          # El alamcén tiene 50 segundos para confirmar que la pieza está lista para ser recogida con un ACK 1: "Pieza lista para recoger"
          if (getstate_ackHBW_fetchWP()) == 1:  
            valid = True
            break
        else:
          logging.log(logging.DEBUG_VGR, 'timeout ack fetchWP')
          valid = False
          break
        time.sleep(1)
      # Si ha respondido que la pieza está lista para ser recogida, se procede a recogerla y entregarla en producción
      if valid:
        #move: HBW
        temp = get_pos3_VGR_name('HBW') # Obtiene la posición del HBW
        temp[1] = get_offset_VGR_name('HBW')  # Coordenada eje Y para moverse a la altura del contenedor con la pieza
        temp[2] = get_offset_VGR_name('HBW_h')  # Coordenada eje Z para coger la pieza a la altura del HBW
        moveAbs_VGR_P123_list(temp) # Se mueve a la posición de recogida del HBW
        grip()  # Coge la pieza del HBW
        time.sleep(1)
        #move: MPO
        temp = get_pos3_VGR_name('HBW') # Obtiene la posición del HBW
        temp[2] = get_offset_VGR_name('HBW_h')  # Coordenada eje Z para estirarse o retraerse a la altura del HBW
        moveAbs_VGR_P123_list(temp) # Realiza el movimiento
        moveAbs_VGR_P123_name('HBW')  # Se mueve a la posición de referencia del HBW
        moveAbs_VGR_P123_name('MPO')  # Se mueve hacia la posción de producción (MPO) para entregar la pieza 
        temp = get_pos3_VGR_name('MPO') # Obtiene la posición del MPO
        temp[1] = get_offset_VGR_name('MPO') # Coordenada eje Y para moverse a la altura de entrega en el MPO
        moveAbs_VGR_P123_list(temp) # Se mueve a la posición asignada
        release() # Suelta la pieza en la posición de producción (MPO)
        set_nfctag_item(uid, 5, (time.time() * 1000)) # Actualiza el chip NFC con el timestamp de fin de la entrega a producción
        publish_state_order('IN_PROCESS', wp_color)
        moveAbs_VGR_P123_name('MPO')  # Se mueve a la posición de referencia del MPO
        moveRef(3)  # Se retrae el brazo del VGR
      # El almacén tiene un máximo de 50 segundos para confirmar con un ACK 2 que la pieza ha sido entregada correctamente
      if valid:
        ts0 = (time.time() * 1000)
        while True:
          ts_diff = (time.time() * 1000) - ts0
          logging.log(logging.DEBUG_VGR, 'check ack fetchWP %.1f', ts_diff)
          if ts_diff < 50000:
            if (getstate_ackHBW_fetchWP()) == 2:  # El almacén confirma con un ACK 2 que la pieza ha sido entregada correctamente y que el proceso ha finalizado
              valid = True
              break
          else:
            logging.log(logging.DEBUG_VGR, 'timeout ack fetchWP')
            valid = False
            break
          time.sleep(1)
      if valid:
        #move: ref
        moveRef_VGR_P123() # El VGR se mueve a la posición de referencia
      if valid:
        #move to MPO milling:
        set_nfctag_item(uid, 6, (time.time() * 1000)) # Actualiza el chip NFC con el timestamp de inicio del mecanizado en el MPO
      else:
        set_factory_error_state('VGR')
    # CASO 7: Si el usuario pulsa el interruptor de modo manual, se resetea el estado del VGR para permitir controlar el brazo manualmente desde la pantalla o la nube
    elif TXT_SSC_M_I8_mini_switch.is_closed():
      beep_blocked()
      loadFileStorage()
      loadFileFactoryCalib()
      beep_blocked()
    _set_state_VGR(1, 0, '')
    time.sleep(1)


# Inicializa el logging específico para el VGR con los niveles TRACE0_VGR, TRACE_VGR y DEBUG_VGR
def initlog_VGR(_tr0, _tr, _dg):
  """
  Da de alta los niveles de registro propios del VGR.

  Crea las etiquetas TRACE0_VGR, TRACE_VGR y DEBUG_VGR para que los mensajes
  de este brazo se puedan filtrar en el log aparte del resto de estaciones.

  Args:
    _tr0: Nivel numérico para el trazado mas detallado.
    _tr: Nivel numérico para el trazado normal.
    _dg: Nivel numérico para los mensajes de depuración.

  Returns:
    None.
  """
  global m, k, taguid, num, history_ts, w_uid, w_color, _code, _active, _target, name, color, mi, mask, res, valid, state_code, state_active, ts_readuid, _ts_state, count_write, list_nfctag_history, index, state_target, uid, i, map_nfctag_history, _state, bit, _type, _mask, _vts, ts_dsi, wp, wp_color, data, req, temp, ts0, ts_diff, last_color, ts_cs, ack
  logging.TRACE0_VGR= _tr0
  logging.addLevelName(logging.TRACE0_VGR , 'TRACE0_VGR')
  logging.TRACE_VGR = _tr
  logging.addLevelName(logging.TRACE_VGR , 'TRACE_VGR')
  logging.DEBUG_VGR = _dg
  logging.addLevelName(logging.DEBUG_VGR, 'DEBUG_VGR')

# Movimiento del VGR hasta su estado de reposo.
def parkVGR():
  """
  Manda el brazo VGR a su posicion de reposo.

  Primero lo lleva a la posicion de referencia y despues lo deja aparcado
  en unas coordenadas fijas, fuera del paso de las demas estaciones.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, m, k, taguid, num, history_ts, w_uid, w_color, _code, _active, _target, name, color, mi, mask, res, valid, state_code, state_active, ts_readuid, _ts_state, count_write, list_nfctag_history, index, state_target, uid, i, map_nfctag_history, _state, bit, _type, _mask, _vts, ts_dsi, wp, wp_color, data, req, temp, ts0, ts_diff, last_color, ts_cs, ack
  logging.log(logging.TRACE_VGR, '-')
  moveRef_VGR_P123()
  moveAbs_VGR_P123_list([445, 865, 10])

# Inicialización del historial de etapas de proceso indexado por UID NFC.
def init_map_nfctag_history():
  """
  Vacia el historial de etapas de las piezas con chip NFC.

  Crea el diccionario donde luego se va apuntando, para cada UID de
  etiqueta NFC, en que momento paso la pieza por cada una de las 8 etapas
  del proceso (llegada, control de calidad, almacenaje...).

  Returns:
    None.
  """
  global _tr0, _tr, _dg, m, k, taguid, num, history_ts, w_uid, w_color, _code, _active, _target, name, color, mi, mask, res, valid, state_code, state_active, ts_readuid, _ts_state, count_write, list_nfctag_history, index, state_target, uid, i, map_nfctag_history, _state, bit, _type, _mask, _vts, ts_dsi, wp, wp_color, data, req, temp, ts0, ts_diff, last_color, ts_cs, ack
  logging.log(logging.TRACE_VGR, '-')
  #nfc history code:
  # 100 = "Anlieferung Rohware"
  # 200 = "Qualitätskontrolle"
  # 300 = "Einlagerung"
  # 400 = "Auslagerung"
  # 500 = "Bearbeitung Brennofen"
  # 600 = "Bearbeitung Fräse"
  # 700 = "Sortierung"
  # 800 = "Versand Ware"
  #map of items:
  #uid -> [ts, code]*8
  map_nfctag_history = {}

# 
def map_get_uid(m, k):
  """
  Busca el historial de una pieza por el UID de su etiqueta NFC.

  Args:
    m: Diccionario con el historial de todas las piezas.
    k: UID de la etiqueta NFC que se busca.

  Returns:
    La lista con las 8 marcas de tiempo de esa pieza, o ``None`` si ese
    UID no esta todavia en el historial.
  """
  global _tr0, _tr, _dg, taguid, num, history_ts, w_uid, w_color, _code, _active, _target, name, color, mi, mask, res, valid, state_code, state_active, ts_readuid, _ts_state, count_write, list_nfctag_history, index, state_target, uid, i, map_nfctag_history, _state, bit, _type, _mask, _vts, ts_dsi, wp, wp_color, data, req, temp, ts0, ts_diff, last_color, ts_cs, ack
  logging.log(logging.TRACE0_VGR, '-')
  if m != None:
    mi = m.get(k)
  return mi


def map_get_msk():
  """
  Calcula que etapas del proceso tiene ya registradas la ultima pieza.

  Recorre las 8 marcas de tiempo del historial y construye un numero donde
  cada bit dice si esa etapa ya tiene fecha guardada o no. Ese numero es el
  que luego se graba en la etiqueta NFC de la pieza.

  Returns:
    El numero (mascara de bits) con las etapas ya registradas.
  """
  global _tr0, _tr, _dg, m, k, taguid, num, history_ts, w_uid, w_color, _code, _active, _target, name, color, mi, mask, res, valid, state_code, state_active, ts_readuid, _ts_state, count_write, list_nfctag_history, index, state_target, uid, i, map_nfctag_history, _state, bit, _type, _mask, _vts, ts_dsi, wp, wp_color, data, req, temp, ts0, ts_diff, last_color, ts_cs, ack
  logging.log(logging.TRACE_VGR, '-')
  mask = 0
  if list_nfctag_history != None:
    print("list_nfctag_history: ", list_nfctag_history)
    index = 0
    for i in list_nfctag_history:
      #print("i: ", i)
      bit = bin(1 if i != None else 0)
      #print("bit: ", bit)
      if bit == bin(1):
        mask += 2 ** index
      #print("mask: ", mask)
      index = (index if isinstance(index, (int, float)) else 0) + 1
  print("map_get_msk: ", mask)
  return mask


def set_nfctag_item(taguid, num, history_ts):
  """
  Apunta en que momento paso una pieza por una etapa del proceso.

  Guarda, para el UID de la etiqueta NFC dado, la fecha y hora de la etapa
  numero ``num`` (1 a 8: llegada, control de calidad, almacenaje...) en el
  historial en memoria de esa pieza.

  Args:
    taguid: UID de la etiqueta NFC de la pieza.
    num: Numero de etapa del proceso (de 1 a 8).
    history_ts: Momento (timestamp) en el que ocurrio esa etapa.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, m, k, w_uid, w_color, _code, _active, _target, name, color, mi, mask, res, valid, state_code, state_active, ts_readuid, _ts_state, count_write, list_nfctag_history, index, state_target, uid, i, map_nfctag_history, _state, bit, _type, _mask, _vts, ts_dsi, wp, wp_color, data, req, temp, ts0, ts_diff, last_color, ts_cs, ack
  logging.log(logging.TRACE_VGR, num)
  if taguid != None:
    print("item: ", taguid, " ", num, " ", history_ts)
    if map_nfctag_history == None:
      init_map_nfctag_history()
    if map_get_uid(map_nfctag_history, taguid) == None:
      list_nfctag_history = [None] * 8
    else:
      list_nfctag_history = map_get_uid(map_nfctag_history, taguid)
    list_nfctag_history[int(num - 1)] = history_ts / 1000
    map_nfctag_history[taguid] = list_nfctag_history
  print("set_nfctag_item: ", map_nfctag_history)


def nfc_write_history(w_uid, w_color):
  """
  Graba en la etiqueta NFC de la pieza su color y su historial de etapas.

  Escribe fisicamente en el chip el estado, el color y la mascara de
  etapas completadas, y lo intenta hasta 3 veces hasta que la lectura de
  vuelta confirma que se ha grabado bien.

  Args:
    w_uid: UID de la etiqueta NFC que se actualiza.
    w_color: Color que se registra en el historial de la etiqueta.

  Returns:
    True si la escritura se confirmo correctamente, False si se agotaron
    los 3 intentos sin exito.
  """
  global _tr0, _tr, _dg, m, k, taguid, num, history_ts, _code, _active, _target, name, color, mi, mask, res, valid, state_code, state_active, ts_readuid, _ts_state, count_write, list_nfctag_history, index, state_target, uid, i, map_nfctag_history, _state, bit, _type, _mask, _vts, ts_dsi, wp, wp_color, data, req, temp, ts0, ts_diff, last_color, ts_cs, ack
  logging.log(logging.TRACE_VGR, '-')
  # nfc_data:
  #   0:nfc uid:
  #     workpiece NTAG213:  7 bytes
  #     card + blue key Mifare: 4 bytes
  #   1:byte0: state: 0:RAW, 1:PROCESSED, 2:REJECTED
  #   2:byte1: type: 0:NONE, 1:WHITE, 2:RED, 3:BLUE
  #   3:byte2: mask timestamps
  #   4:byte3: none (reserved)
  #   5:byte4...4+(8*8): vts[8]: int64_t (8 bytes)
  res = False
  count_write = 0
  while True:
    taguid = w_uid
    _state = 0
    if w_color == 'WHITE':
      _type = 1
    elif w_color == 'RED':
      _type = 2
    elif w_color == 'BLUE':
      _type = 3
    else:
      _type = 0
    _mask = map_get_msk()
    _vts = map_get_uid(map_nfctag_history, taguid)
    res = nfc_write(_state, _type, _mask, _vts)
    #HINT: res=nfc_write is always false
    data = nfc_read()
    if data != None:
      publish_Nfc_Data()
    res = (get_nfc_data_state()) == _state and (get_nfc_data_type()) == _type and (get_nfc_data_mask()) == _mask
    print("res: ", res)
    count_write = (count_write if isinstance(count_write, (int, float)) else 0) + 1
    if res or count_write >= 3:
      break
    else:
      print(data)
    time.sleep(1)
  return res


def NiO_exit():
  """
  Da por invalida la pieza actual y limpia la conversacion con el almacen.

  Se llama cuando una pieza se va a tirar a la cesta de descarte (NiO):
  marca el proceso como no valido y borra las peticiones pendientes entre
  el VGR y el HBW, para que la siguiente pieza empiece de cero.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, m, k, taguid, num, history_ts, w_uid, w_color, _code, _active, _target, name, color, mi, mask, res, valid, state_code, state_active, ts_readuid, _ts_state, count_write, list_nfctag_history, index, state_target, uid, i, map_nfctag_history, _state, bit, _type, _mask, _vts, ts_dsi, wp, wp_color, data, req, temp, ts0, ts_diff, last_color, ts_cs, ack
  valid = False
  init_VGRHBW()


def thread_update_VGR():
  """
  Publica el estado del VGR cada 10 segundos, en su propio hilo.

  Actualiza el indicador en pantalla y avisa por MQTT del codigo de estado,
  si esta activo y hacia donde se dirige el brazo.

  Returns:
    None. Es un bucle infinito, nunca termina por si solo.
  """
  global _tr0, _tr, _dg, m, k, taguid, num, history_ts, w_uid, w_color, _code, _active, _target, name, color, mi, mask, res, valid, state_code, state_active, ts_readuid, _ts_state, count_write, list_nfctag_history, index, state_target, uid, i, map_nfctag_history, _state, bit, _type, _mask, _vts, ts_dsi, wp, wp_color, data, req, temp, ts0, ts_diff, last_color, ts_cs, ack
  logging.log(logging.TRACE_VGR, '-')
  _ts_state = 0
  while True:
    if (time.time() * 1000) - _ts_state > 10000:
      update_display_VGR(state_code)
      publish_state_VGR(state_code, state_active, state_target)
      _ts_state = (time.time() * 1000)
    time.sleep(1)


def _set_state_VGR(_code, _active, _target):
  """
  Guarda el nuevo estado del VGR si algo ha cambiado.

  Si el codigo, el indicador de actividad o el destino son distintos de
  los que ya estaban guardados, los actualiza y reinicia el cronometro
  para que ``thread_update_VGR`` avise cuanto antes por pantalla y MQTT.

  Args:
    _code: Codigo de estado del VGR (por ejemplo 1=reposo, 2=en reparto,
      4=error).
    _active: Indica si el VGR esta activo haciendo ese estado.
    _target: Nombre de la estacion hacia la que se dirige (por ejemplo
      'hbw', 'mpo' o 'dso').

  Returns:
    None.
  """
  global _tr0, _tr, _dg, m, k, taguid, num, history_ts, w_uid, w_color, name, color, mi, mask, res, valid, state_code, state_active, ts_readuid, _ts_state, count_write, list_nfctag_history, index, state_target, uid, i, map_nfctag_history, _state, bit, _type, _mask, _vts, ts_dsi, wp, wp_color, data, req, temp, ts0, ts_diff, last_color, ts_cs, ack
  logging.log(logging.TRACE0_VGR, '-')
  if state_code != _code or state_active != _active or state_target != _target:
    _ts_state = 0
    state_code = _code
    state_active = _active
    state_target = _target


def moveSLD(name, color):
  """
  Recoge una pieza clasificada en la cinta SLD y la lleva a la salida DSO.

  El brazo se mueve hasta la posicion ``name`` de la cinta de clasificacion,
  coge la pieza con la ventosa, pasa por el lector NFC para grabar su
  historial y, en cuanto el punto de salida DSO queda libre, la deja alli
  para que el cliente la recoja.

  Args:
    name: Nombre de la posicion en la cinta SLD de la que se recoge la
      pieza (por ejemplo 'SLD white', 'SLD red' o 'SLD blue').
    color: Color de la pieza que se esta recogiendo.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, m, k, taguid, num, history_ts, w_uid, w_color, _code, _active, _target, mi, mask, res, valid, state_code, state_active, ts_readuid, _ts_state, count_write, list_nfctag_history, index, state_target, uid, i, map_nfctag_history, _state, bit, _type, _mask, _vts, ts_dsi, wp, wp_color, data, req, temp, ts0, ts_diff, last_color, ts_cs, ack
  _set_state_VGR(2, 1, 'dso') # Estado de entrega a DSO (code=2, active=1, target='dso')
  while is_dso(): # Espera a que el DSO esté libre para recibir la pieza
    display.set_attr("txt_label_message2.text", str('DSO: remove workpiece!'))
    time.sleep(1)
  display.set_attr("txt_label_message2.text", str(''))
  set_nfctag_item(uid, 7, (time.time() * 1000)) # Actualiza el chip NFC con el timestamp de inicio de la recogida en SLD
  moveRef_VGR_S231()  # Eleva, retrae y rota el VGR
  moveAbs_VGR_P123_name(name) # Se mueve hacia la posición de recogida en SLD
  grip()  # Coge la pieza en SLD
  moveRef(2)  # Eleva el brazo para evitar colisiones al moverse hacia el siguiente destino
  moveAbs_VGR_P123_name('NFC') # Se mueve hacia la posición de escritura en NFC
  set_nfctag_item(uid, 8, (time.time() * 1000))
  res = nfc_write_history(uid, color) # Escribe el bloque histórico en la etiqueta NFC y verifica que se ha escrito correctamente
  # Si se ha escrito correctamente en la etiqueta NFC
  if res:
    moveRef(2)  # Eleva el brazo para evitar colisiones al moverse hacia el siguiente destino
    while is_dso():
      display.set_attr("txt_label_message2.text", str('DSO: remove workpiece!'))
      time.sleep(1)
    display.set_attr("txt_label_message2.text", str(''))
    moveAbs_VGR_P123_name('DSO')  # Se mueve hacia la posición de entrega en DSO
    release() # Suelta la pieza en DSO
    beep_blocked()  # Pitido para indicar que la pieza ha sido entregada en DSO
    set_state_dso(1)  # Actualiza el estado del DSO a ocupado (1)
    publish_state_order('SHIPPED', color)
  # Si no se ha podido escribir correctamente en la etiqueta NFC
  else:
    #move: NiO
    moveAbs_VGR_P123_name('NiO')  # Descarta la pieza en la cesta de descarte (NiO)
    release()
    req = reqHBW_fetchContainer(None) # FIXME: No hace nada porque no se puede solcitar un contenedor con una pieza None
  moveRef(3)  # Se retrae el brazo
  moveRef_VGR_P123()  # Se mueve a la posición de referencia
  set_state_dso(0)  # Actualiza el estado del DSO a libre (0)
  publish_state_order('WAITING_FOR_ORDER', color)


def grip():
  """
  Cierra la ventosa del brazo para coger la pieza que tiene debajo.

  Enciende primero el compresor para generar vacio y, dos segundos
  despues, abre la valvula magnetica que conecta ese vacio con la ventosa,
  de forma que la pieza queda pegada.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, m, k, taguid, num, history_ts, w_uid, w_color, _code, _active, _target, name, color, mi, mask, res, valid, state_code, state_active, ts_readuid, _ts_state, count_write, list_nfctag_history, index, state_target, uid, i, map_nfctag_history, _state, bit, _type, _mask, _vts, ts_dsi, wp, wp_color, data, req, temp, ts0, ts_diff, last_color, ts_cs, ack
  global grip_active
  
  logging.log(logging.TRACE_VGR, '-')
  TXT_VGR_E2_O7_compressor.on()
  time.sleep(2)
  TXT_VGR_E2_O8_magnetic_valve.on()
  grip_active = True


def release():
  """
  Abre la ventosa del brazo para soltar la pieza que lleva agarrada.

  Corta primero la valvula magnetica y luego apaga el compresor, dejando
  una pequeña pausa despues para que la pieza caiga del todo antes de que
  el brazo se mueva a otro sitio.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, m, k, taguid, num, history_ts, w_uid, w_color, _code, _active, _target, name, color, mi, mask, res, valid, state_code, state_active, ts_readuid, _ts_state, count_write, list_nfctag_history, index, state_target, uid, i, map_nfctag_history, _state, bit, _type, _mask, _vts, ts_dsi, wp, wp_color, data, req, temp, ts0, ts_diff, last_color, ts_cs, ack
  global grip_active
  
  logging.log(logging.TRACE_VGR, '-')
  TXT_VGR_E2_O8_magnetic_valve.off()
  TXT_VGR_E2_O7_compressor.off()
  time.sleep(0.5)
  grip_active = False
  time.sleep(1.5) # TODO: antes era time.sleep(2) lo repartí en 0.5 y 1.5 para que el VGR pueda llegar a tiempo en la simulación


def get_state_code_VGR():
  """
  Devuelve el codigo de estado actual del VGR.

  Returns:
    El codigo de estado guardado (por ejemplo 1=reposo, 2=en reparto,
    4=error).
  """
  global _tr0, _tr, _dg, m, k, taguid, num, history_ts, w_uid, w_color, _code, _active, _target, name, color, mi, mask, res, valid, state_code, state_active, ts_readuid, _ts_state, count_write, list_nfctag_history, index, state_target, uid, i, map_nfctag_history, _state, bit, _type, _mask, _vts, ts_dsi, wp, wp_color, data, req, temp, ts0, ts_diff, last_color, ts_cs, ack
  logging.log(logging.TRACE0_VGR, '-')
  return state_code


def get_state_active_VGR():
  """
  Dice si el VGR esta activo haciendo su estado actual.

  Returns:
    True si el brazo esta ocupado con la tarea de ``state_code``, False si
    esta libre.
  """
  global _tr0, _tr, _dg, m, k, taguid, num, history_ts, w_uid, w_color, _code, _active, _target, name, color, mi, mask, res, valid, state_code, state_active, ts_readuid, _ts_state, count_write, list_nfctag_history, index, state_target, uid, i, map_nfctag_history, _state, bit, _type, _mask, _vts, ts_dsi, wp, wp_color, data, req, temp, ts0, ts_diff, last_color, ts_cs, ack
  logging.log(logging.TRACE0_VGR, '-')
  return state_active

# Estados publicados de la ventosa y del último color detectado.
grip_active = False

def get_grip_active():
  """
  Dice si la ventosa del brazo esta agarrando una pieza ahora mismo.

  Returns:
    True si la ventosa esta cerrada sujetando una pieza, False si esta
    abierta.
  """
  global grip_active
  return grip_active




_color_event = None

def set_color_event(color_value):
  """
  Guarda el ultimo color de pieza que se ha detectado.

  Args:
    color_value: Color detectado ('WHITE', 'RED' o 'BLUE').

  Returns:
    None.
  """
  global _color_event
  _color_event = color_value

def get_color_event():
  """
  Devuelve el ultimo color de pieza que se ha detectado.

  Returns:
    El ultimo color guardado ('WHITE', 'RED' o 'BLUE'), o ``None`` si
    todavia no se ha detectado ninguno o ya se ha limpiado.
  """
  global _color_event
  return _color_event

def clear_color_event():
  """
  Borra el ultimo color de pieza detectado.

  Returns:
    None.
  """
  global _color_event
  _color_event = None

