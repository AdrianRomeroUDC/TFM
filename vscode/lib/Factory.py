# functions for the entire factory
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
  global _tr0, _tr, _dg, wp, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, '')
  state_ackHBW_fetchContainer = None
  state_ackHBW_fetchWP = None
  wp_reqHBW_fetchContainer = None
  wp_reqHBW_fetchWP = None

def ackHBW_fetchContainer(ackState):
  global _tr0, _tr, _dg, wp, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, ackState)
  state_ackHBW_fetchContainer = ackState
  wp_reqHBW_fetchContainer = None

def ackHBW_fetchWP(ackState):
  global _tr0, _tr, _dg, wp, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, ackState)
  state_ackHBW_fetchWP = ackState
  wp_reqHBW_fetchWP = None



def initlog_FSM(_tr0, _tr, _dg):
  global wp, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.TRACE0_FSM = _tr0
  logging.addLevelName(logging.TRACE0_FSM , 'TRACE0_FSM')
  logging.TRACE_FSM = _tr
  logging.addLevelName(logging.TRACE_FSM , 'TRACE_FSM')
  logging.DEBUG_FSM = _dg
  logging.addLevelName(logging.DEBUG_FSM, 'DEBUG_FSM')


def parkFactory():
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
  global _tr0, _tr, _dg, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, wp)
  if wp != None:
    wp_reqHBW_fetchContainer = wp
  logging.log(logging.DEBUG_FSM, str(wp!=None))
  return wp != None


def getwp_reqHBW_fetchContainer():
  global _tr0, _tr, _dg, wp, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  # logging.log(logging.TRACE_FSM, '-')
  # logging.log(logging.DEBUG_FSM, str(wp_reqHBW_fetchContainer))
  return wp_reqHBW_fetchContainer


def getstate_ackHBW_fetchContainer():
  global _tr0, _tr, _dg, wp, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, '-')
  return state_ackHBW_fetchContainer


def reqHBW_fetchWP(wp):
  global _tr0, _tr, _dg, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, wp)
  if wp != None:
    wp_reqHBW_fetchWP = wp
  logging.log(logging.DEBUG_FSM, str(wp!=None))
  return wp != None


def getwp_reqHBW_fetchWP():
  global _tr0, _tr, _dg, wp, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, '-')
  return wp_reqHBW_fetchWP


def getstate_ackHBW_fetchWP():
  global _tr0, _tr, _dg, wp, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, '-')
  logging.log(logging.DEBUG_FSM, str(state_ackHBW_fetchWP))
  return state_ackHBW_fetchWP


def reqVGR_Nfc(cmd):
  global _tr0, _tr, _dg, wp, ackState, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  logging.log(logging.TRACE_FSM, cmd)
  cmdNfc = cmd
  logging.log(logging.DEBUG_FSM, str(cmdNfc!=None))
  return cmdNfc != None


def getcmd_reqVGR_Nfc():
  global _tr0, _tr, _dg, wp, ackState, cmd, wp_reqHBW_fetchContainer, state_ackHBW_fetchContainer, wp_reqHBW_fetchWP, state_ackHBW_fetchWP, cmdNfc
  # logging.log(logging.TRACE_FSM, cmdNfc)
  # logging.log(logging.DEBUG_FSM, str(cmdNfc))
  return cmdNfc


