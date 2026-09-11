import logging
from fischertechnik.control.VoiceControl import VoiceControl

voice_control = VoiceControl()


def command_callback(event):
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



