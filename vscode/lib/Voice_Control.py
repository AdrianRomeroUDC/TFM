"""Callback de control por voz para órdenes de la fábrica."""

# Traduce eventos de voz a las mismas peticiones usadas por la GUI.
import logging
from fischertechnik.control.VoiceControl import VoiceControl

voice_control = VoiceControl()


def command_callback(event):
  """Traduce una orden de voz a su representación visible en consola.

  Args:
    event: Texto de la orden reconocida por ``VoiceControl``.

  Returns:
    None. Imprime la orden normalizada o un aviso para comandos desconocidos.
  """
  logging.log(logging.TRACE, '-')
  if (event) == 'Parking' or (event) == 'Parken':
    print('Parking')
  elif (event) == 'White' or (event) == 'Weiß':
    print('White')
  elif (event) == 'Red' or (event) == 'Rot':
    print('Red')
  elif (event) == 'Blue' or (event) == 'Blau':
    print('Blue')
  elif (event) == 'Reset' or (event) == 'Zurücksetzen':
    print('Reset')
  else:
    print('Unknown Command: {}'.format(event))


voice_control.add_command_listener(command_callback)



