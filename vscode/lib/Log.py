"""Configuración de niveles y formato de logging de la aplicación."""

# Todos los subsistemas registran trazas con niveles propios.
import logging
from lib.Axes1Ref import *
from lib.Axes2Ref import *
from lib.Factory import *
from lib.GUI import *
from lib.HBW import *
from lib.MQTT import *
from lib.Nfc import *
from lib.VGR import *

lev = None


def initlib_log(lev):
  """
  Da de alta los niveles de registro de todos los modulos de la fabrica.

  Registra los niveles propios de cada estacion (ejes, coordinador,
  conexiones MQTT, interfaz, VGR, HBW y lector NFC) y configura el
  formato con el que se escribe cada linea del log.

  Args:
    lev: Nivel minimo de mensajes que se van a mostrar.

  Returns:
    None.
  """
  #TRACE0_A1R, TRACE_A1R, DEBUG_A1R
  initlog_A1R(1, 0, 0)
  #TRACE0_A2R, TRACE_A2R, DEBUG_A2R
  initlog_A2R(2, 0, 0)
  #TRACE0_FSM, TRACE_FSM, DEBUG_FSM
  initlog_FSM(3, 0, 23)
  #TRACE0_FCL, TRACE_FCL, DEBUG_FCL
  initlog_FCL(4, 0, 0)
  #TRACE0_GUI, TRACE_GUI, DEBUG_GUI
  initlog_GUI(5, 0, 0)
  #TRACE0_VGR, TRACE_VGR, DEBUG_VGR
  initlog_VGR(6, 16, 26)
  #TRACE0_HBW, TRACE_HBW, DEBUG_HBW
  initlog_HBW(7, 17, 27)
  #TRACE0_NFC, TRACE_NFC, DEBUG_NFC
  initlog_NFC(8, 0, 0)
  # Logging levels already reserved:
  #  CRITICAL 50
  #  ERROR 40
  #  WARNING 30
  #  INFO 20
  #  DEBUG 10 (Debug all)
  #  #TRACE 9 (no loops)
  #  #TRACE0 1 (with loops)
  #  NOTSET 0

  logging.TRACE0 = 1
  logging.addLevelName(logging.TRACE0 , 'TRACE0')
  logging.TRACE = 9
  logging.addLevelName(logging.TRACE , 'TRACE')

  logging.basicConfig(\
  #filename='/opt/ft/workspaces/FactoryMain.log',\
  #filemode='w',\
  level=lev,\
  format="%(asctime)s [%(threadName)s] %(levelname)-10s %(funcName)3s %(message)s   #%(filename)3s:%(lineno)d"\
  )


