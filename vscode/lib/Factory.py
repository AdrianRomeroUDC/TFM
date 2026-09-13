"""Coordinación de peticiones, confirmaciones y estados globales de fábrica."""

# Coordinador de peticiones entre estaciones y de sus confirmaciones de proceso.
import logging
import os
import sys
from lib.Sound import *

_tr0 = None
_tr = None
_dg = None
wp = None
ackState = None
cmd = None
wp_reqHBW_fetchContainer = None
state_ackHBW_fetchContainer = None
wp_reqHBW_fetchWP = None
state_ackHBW_fetchWP = None
cmdNfc = None
def init_VGRHBW():
  """
  Borra la conversacion pendiente entre el VGR y el almacen.

  Se usa para dejar limpio el intercambio de peticiones y confirmaciones
  entre el brazo y el almacen antes de empezar (o despues de descartar
  una pieza), de forma que la siguiente peticion arranque de cero.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, wp, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, '')
  state_ackHBW_fetchContainer = None
  state_ackHBW_fetchWP = None
  wp_reqHBW_fetchContainer = None
  wp_reqHBW_fetchWP = None

def ackHBW_fetchContainer(ackState):
  """
  El almacen confirma al VGR el progreso de una peticion de contenedor.

  Guarda el codigo de confirmacion (por ejemplo "en posicion" o
  "entregado") y da por atendida la peticion pendiente.

  Args:
    ackState: Codigo de confirmacion del almacen.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, wp, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, ackState)
  state_ackHBW_fetchContainer = ackState
  wp_reqHBW_fetchContainer = None

def ackHBW_fetchWP(ackState):
  """
  El almacen confirma al VGR el progreso de una peticion de pieza.

  Guarda el codigo de confirmacion (por ejemplo "pieza lista" o
  "entregada") y da por atendida la peticion pendiente.

  Args:
    ackState: Codigo de confirmacion del almacen.

  Returns:
    None.
  """
  global _tr0, _tr, _dg, wp, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, ackState)
  state_ackHBW_fetchWP = ackState
  wp_reqHBW_fetchWP = None



def initlog_FSM(_tr0, _tr, _dg):
  """
  Da de alta los niveles de registro propios del coordinador de fabrica.

  Crea las etiquetas TRACE0_FSM, TRACE_FSM y DEBUG_FSM para que los
  mensajes de las peticiones entre estaciones se puedan filtrar aparte.

  Args:
    _tr0: Nivel numérico para el trazado mas detallado.
    _tr: Nivel numérico para el trazado normal.
    _dg: Nivel numérico para los mensajes de depuración.

  Returns:
    None.
  """
  global wp, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.TRACE0_FSM = _tr0
  logging.addLevelName(logging.TRACE0_FSM , 'TRACE0_FSM')
  logging.TRACE_FSM = _tr
  logging.addLevelName(logging.TRACE_FSM , 'TRACE_FSM')
  logging.DEBUG_FSM = _dg
  logging.addLevelName(logging.DEBUG_FSM, 'DEBUG_FSM')


def parkFactory():
  """Lleva HBW, VGR, MPO y SSC a sus posiciones de aparcamiento.

  Crea un hilo daemon por estacion, espera a que terminen los cuatro
  movimientos, emite un aviso acustico y finaliza el proceso de control.

  Returns:
    None. El efecto principal es fisico y termina la aplicacion mediante
    ``os._exit`` cuando todos los actuadores han finalizado.
  """
  global _tr0, _tr, _dg, wp, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, '-')
  from lib.HBW import parkHBW
  from lib.VGR import parkVGR
  from lib.MPO import parkMPO
  from lib.SSC_PTU import parkSSC

  th1 = threading.Thread(target=parkHBW, daemon=True)
  th2 = threading.Thread(target=parkVGR, daemon=True)
  th3 = threading.Thread(target=parkMPO, daemon=True)
  th4 = threading.Thread(target=parkSSC, daemon=True)
  th1.start()
  th2.start()
  th3.start()
  th4.start()
  th1.join()
  th2.join()
  th3.join()
  th4.join()
  beep()
  logging.debug('exit')
  os._exit(os.EX_OK)


def reqHBW_fetchContainer(wp):
  """
  El VGR pide al almacen que le prepare un hueco para guardar una pieza.

  Args:
    wp: Datos de la pieza para la que se pide sitio.

  Returns:
    True si la peticion se ha dejado registrada, False si ``wp`` es None.
  """
  global _tr0, _tr, _dg, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, wp)
  if wp != None:
    wp_reqHBW_fetchContainer = wp
  logging.log(logging.DEBUG_FSM, str(wp!=None))
  return wp != None


def getwp_reqHBW_fetchContainer():
  """
  El almacen consulta que pieza espera un hueco para ser guardada.

  Returns:
    Los datos de la pieza pendiente, o ``None`` si no hay ninguna
    peticion en curso.
  """
  global _tr0, _tr, _dg, wp, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  return wp_reqHBW_fetchContainer


def getstate_ackHBW_fetchContainer():
  """
  El VGR consulta si el almacen ya ha confirmado la peticion de hueco.

  Returns:
    El ultimo codigo de confirmacion del almacen, o ``None`` si todavia no
    ha respondido.
  """
  global _tr0, _tr, _dg, wp, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, '-')
  return state_ackHBW_fetchContainer


def reqHBW_fetchWP(wp):
  """
  El VGR pide al almacen que le saque de vuelta una pieza guardada.

  Args:
    wp: Datos de la pieza que se quiere recuperar.

  Returns:
    True si la peticion se ha dejado registrada, False si ``wp`` es None.
  """
  global _tr0, _tr, _dg, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, wp)
  if wp != None:
    wp_reqHBW_fetchWP = wp
  logging.log(logging.DEBUG_FSM, str(wp!=None))
  return wp != None


def getwp_reqHBW_fetchWP():
  """
  El almacen consulta que pieza le estan pidiendo que devuelva.

  Returns:
    Los datos de la pieza pendiente, o ``None`` si no hay ninguna
    peticion en curso.
  """
  global _tr0, _tr, _dg, wp, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, '-')
  return wp_reqHBW_fetchWP


def getstate_ackHBW_fetchWP():
  """
  El VGR consulta si el almacen ya ha confirmado la devolucion de la pieza.

  Returns:
    El ultimo codigo de confirmacion del almacen, o ``None`` si todavia no
    ha respondido.
  """
  global _tr0, _tr, _dg, wp, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, '-')
  logging.log(logging.DEBUG_FSM, str(state_ackHBW_fetchWP))
  return state_ackHBW_fetchWP


def reqVGR_Nfc(cmd):
  """
  Deja pedido al VGR que lea o borre una etiqueta NFC.

  Args:
    cmd: Orden a ejecutar, como ``'read'``, ``'read_uid'`` o ``'delete'``.

  Returns:
    True si la orden se ha dejado registrada, False si ``cmd`` es None.
  """
  global _tr0, _tr, _dg, wp, ackState, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, cmd)
  cmdNfc = cmd
  logging.log(logging.DEBUG_FSM, str(cmdNfc!=None))
  return cmdNfc != None


def getcmd_reqVGR_Nfc():
  """
  El VGR consulta si tiene pendiente alguna orden de leer o borrar NFC.

  Returns:
    La orden pendiente, o ``None`` si no hay ninguna.
  """
  global _tr0, _tr, _dg, wp, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  return cmdNfc


