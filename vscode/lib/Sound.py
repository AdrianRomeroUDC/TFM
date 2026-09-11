import logging
import threading
import time
from lib.controller import *

def beep():
  logging.log(logging.TRACE, '-')
  threading.Thread(target=thread_sound, daemon=True).start()


def beep_blocked():
  logging.log(logging.TRACE, '-')
  thread_sound()


def thread_sound():
  TXT_SSC_M.get_loudspeaker().play("06_Car_horn_short.wav", False)
  time.sleep(0.2)
  while True:
    if (not (TXT_SSC_M.get_loudspeaker().is_playing())):
      break
    time.sleep(0.010)


