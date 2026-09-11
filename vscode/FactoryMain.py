# Release Notes
# =============
#
# Version 2022/12/06
#   * Initial version
#
# Version 2023/01/04
#   * added: internal switch I8 to trigger loading calibration and storage file
#
# Version 2024/02/28
#   * fix: log problem in Axes2Ref
#   * fix: USB camera version RPC-6.2.1
#
# =============
import threading
from lib.Axes1Ref import *
from lib.Axes2Ref import *
from lib.controller import *
from lib.display import *
from lib.DPS import *
from lib.DPS_MQTT import *
from lib.Factory import *
from lib.Factory_Variables import *
from lib.File import *
from lib.GUI import *
from lib.HBW import *
from lib.HBW_AxesNRef import *
from lib.HBW_Display import *
from lib.HBW_MQTT import *
from lib.HBW_Storage import *
from lib.Log import *
from lib.MPO import *
from lib.MPO_Display import *
from lib.MPO_MQTT import *
from lib.MQTT import *
from lib.MQTT_Subscriber import *
from lib.Nfc import *
from lib.Nfc_MQTT import *
from lib.SLD import *
from lib.SLD_Display import *
from lib.SLD_MQTT import *
from lib.Sound import *
from lib.SSC_Lights import *
from lib.SSC_PTU import *
from lib.SSC_PTU_Axes1Ref import *
from lib.SSC_Publisher import *
from lib.SSC_Subscriber import *
from lib.Test import *
from lib.Time import *
from lib.VGR import *
from lib.VGR_Axes1Ref import *
from lib.VGR_Display import *
from lib.VGR_MQTT import *
from lib.Voice_Control import *
from lib.Digital_Twin import thread_DigitalTwin
from lib.Influx_Collector import thread_InfluxCollector

nfc_obj = None


display.set_attr("txt_label_version.text", str('Version 2024/02/28'))
display.set_attr("txt_label_message.text", str(''))
display.set_attr("txt_label_message2.text", str(''))
initlib_log(9)
threading.Thread(target=thread_lights, daemon=True).start()
initlib_Axes1Ref()
initlib_Axes2Ref()
nfc_obj = nfc_init()
init_VGRHBW()
init_config_MQTT()
threading.Thread(target=thread_ftCloud, daemon=True).start()
threading.Thread(target=thread_Local, daemon=True).start()
threading.Thread(target=thread_DigitalTwin, daemon=True).start()
threading.Thread(target=thread_InfluxCollector, daemon=True).start()
th0 = threading.Thread(target=init_SSC_PTU, args=(), daemon=True)
th1 = threading.Thread(target=thread_HBW, args=(), daemon=True)
th2 = threading.Thread(target=thread_VGR, args=(), daemon=True)
th3 = threading.Thread(target=thread_MPO, args=(), daemon=True)
th4 = threading.Thread(target=thread_DPS, args=(), daemon=True)
th5 = threading.Thread(target=thread_SLD, args=(), daemon=True)
th0.start()
th1.start()
th2.start()
th3.start()
th4.start()
th5.start()
loadFileFactoryCalib()
th0.join()
th1.join()
th2.join()
th3.join()
th4.join()
th5.join()
display.set_attr("txt_button_acknowledge.enabled", str(True).lower())
display.set_attr("txt_button_park.enabled", str(True).lower())
display.set_attr("txt_button_test.enabled", str(True).lower())
display.set_attr("txt_label_message.text", str('READY'))
while True:
    pass