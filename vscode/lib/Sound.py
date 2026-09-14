"""Señales acústicas y avisos sonoros de la interfaz de la fábrica."""

# Cola sencilla de avisos acusticos para interfaz y errores.
import logging
import threading
import time
from lib.controller import *

def beep():
  """Inicia un aviso acústico sin bloquear el hilo.
      Args:
        No requiere argumentos.
  
      Returns:
        None.
  """
  logging.log(logging.TRACE, '-')
  threading.Thread(target=thread_sound, daemon=True).start()


def beep_blocked():
  """Reproduce un aviso acústico y espera a que termine.
      Args:
        No requiere argumentos.
  
      Returns:
        None.
  """
  logging.log(logging.TRACE, '-')
  thread_sound()


def thread_sound():
  """
  Hace sonar el pitido del altavoz y espera a que termine de reproducirse.

  Returns:
    None.
  """
  TXT_SSC_M.get_loudspeaker().play("06_Car_horn_short.wav", False)
  time.sleep(0.2)
  while True:
    if (not (TXT_SSC_M.get_loudspeaker().is_playing())):
      break
    time.sleep(0.010)


