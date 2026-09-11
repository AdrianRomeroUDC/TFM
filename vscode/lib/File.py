import json
import logging
import subprocess
from lib.DPS import *
from lib.HBW_AxesNRef import *
from lib.SLD import *
from lib.SSC_PTU_Axes1Ref import *
from lib.VGR_Axes1Ref import *
from os.path import exists

_ssc = None
_hbw = None
_vgr = None
_dps = None
_sld = None
fileCalib = None
calib_data_SSC = None
calib_json = None
calib_data_HBW = None
calib_data_VGR = None
calib_map = None
calib_data_DPS = None
calib_data_SLD = None


def loadFileFactoryCalib():
  global _ssc, _hbw, _vgr, _dps, _sld, fileCalib, calib_data_SSC, calib_json, calib_data_HBW, calib_data_VGR, calib_map, calib_data_DPS, calib_data_SLD
  logging.log(logging.TRACE, '-')
  if exists('/opt/ft/workspaces/FactoryCalib.json'):
    logging.log(logging.DEBUG, 'load calibration values')
    readFileFactoryCalib()
  else:
    logging.log(logging.DEBUG, 'use default calibration values')
    writeFileFactoryCalib_defaults()
  #subprocess.Popen(['chown', 'ftgui:ftgui', '/opt/ft/workspaces/FactoryCalib.json'])
  subprocess.Popen(['chmod', '777', '/opt/ft/workspaces/FactoryCalib.json'])


def readFileFactoryCalib():
  global _ssc, _hbw, _vgr, _dps, _sld, fileCalib, calib_data_SSC, calib_json, calib_data_HBW, calib_data_VGR, calib_map, calib_data_DPS, calib_data_SLD
  logging.log(logging.TRACE, '-')
  fileCalib = open('/opt/ft/workspaces/FactoryCalib.json', 'r', encoding='utf8')
  calib_json = fileCalib.read()
  fileCalib.close()
  calib_map = json.loads(calib_json)
  #print(calib_map)
  calib_data_SSC = [calib_map['SSC']['poslist']]
  calib_data_HBW = [calib_map['HBW']['poslist']]
  calib_data_VGR = [calib_map['VGR']['poslist'], calib_map['VGR']['discard'], calib_map['VGR']['offset']]
  calib_data_DPS = [calib_map['DPS']['thresh_white_red'], calib_map['DPS']['thresh_red_blue']]
  calib_data_SLD = [calib_map['SLD']['thresh_white_red'], calib_map['SLD']['thresh_red_blue']]
  printData()
  set_calib_data_SSC(calib_data_SSC)
  set_calib_data_HBW(calib_data_HBW)
  set_calib_data_VGR(calib_data_VGR)
  set_calib_data_DPS(calib_data_DPS)
  set_calib_data_SLD(calib_data_SLD)


def writeFileFactoryCalib(_ssc, _hbw, _vgr, _dps, _sld):
  global fileCalib, calib_data_SSC, calib_json, calib_data_HBW, calib_data_VGR, calib_map, calib_data_DPS, calib_data_SLD
  logging.log(logging.TRACE, '-')
  calib_data_SSC = _ssc
  calib_data_HBW = _hbw
  calib_data_VGR = _vgr
  calib_data_DPS = _dps
  calib_data_SLD = _sld
  printData()
  calib_map = {\
  "SSC" : {\
  "poslist" : calib_data_SSC[0]\
  },\
  "HBW" : {\
  "poslist" : calib_data_HBW[0]\
  },\
  "VGR" : {\
  "poslist" : calib_data_VGR[0],\
  "discard" : calib_data_VGR[1],\
  "offset" : calib_data_VGR[2]\
  },\
  "DPS" : {\
  "thresh_white_red" : calib_data_DPS[0],\
  "thresh_red_blue" : calib_data_DPS[1]\
  },\
  "SLD" : {\
  "thresh_white_red" : calib_data_SLD[0],\
  "thresh_red_blue" : calib_data_SLD[1]\
  }\
  }
  calib_json = json.dumps(calib_map)
  fileCalib = open('/opt/ft/workspaces/FactoryCalib.json', 'w', encoding='utf8')
  fileCalib.write(calib_json)
  fileCalib.close()


def printData():
  global _ssc, _hbw, _vgr, _dps, _sld, fileCalib, calib_data_SSC, calib_json, calib_data_HBW, calib_data_VGR, calib_map, calib_data_DPS, calib_data_SLD
  logging.log(logging.TRACE, '-')
  print("SSC: ", calib_data_SSC)
  print("HBW: ", calib_data_HBW)
  print("VGR: ", calib_data_VGR)
  print("DPS: ", calib_data_DPS)
  print("SLD: ", calib_data_SLD)


def writeFileFactoryCalib_current():
  global _ssc, _hbw, _vgr, _dps, _sld, fileCalib, calib_data_SSC, calib_json, calib_data_HBW, calib_data_VGR, calib_map, calib_data_DPS, calib_data_SLD
  logging.log(logging.TRACE, '-')
  writeFileFactoryCalib(get_calib_data_SSC(), get_calib_data_HBW(), get_calib_data_VGR(), get_calib_data_DPS(), get_calib_data_SLD())


def writeFileFactoryCalib_defaults():
  global _ssc, _hbw, _vgr, _dps, _sld, fileCalib, calib_data_SSC, calib_json, calib_data_HBW, calib_data_VGR, calib_map, calib_data_DPS, calib_data_SLD
  logging.log(logging.TRACE, '-')
  if exists('/opt/ft/workspaces/FactoryCalib.json'):
    fileCalib = open('/opt/ft/workspaces/FactoryCalib.json', 'r', encoding='utf8')
    calib_json = fileCalib.read()
    fileCalib.close()
    if False:
      #TODO: backup
      #save and write to USB Stick -> new buttons in GUI?
      fileCalib = open('/opt/ft/workspaces/FactoryCalib_backup.json', 'w', encoding='utf8')
      fileCalib.write(calib_json)
      fileCalib.close()
  writeFileFactoryCalib(get_calib_data_SSC_defaults(), get_calib_data_HBW_defaults(), get_calib_data_VGR_defaults(), get_calib_data_DPS_defaults(), get_calib_data_SLD_defaults())


