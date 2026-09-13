"""Variables compartidas de configuración, alarmas y cliente local."""

# Variables compartidas que coordinan los distintos subsistemas.
import logging

client = None
state = None
value = None
client_local = None
factory_error_state = None
ALARM_TIMER = None
LDR_PERIOD = None
CAMERA_FPS = None
BME680_PERIOD = None
CAMERA_ON = None
INIT_FINISHED = None
KEEP_ALIVE = None
CLOUD_ACTIVE = None


def set_client_local(client):
  """Registra el cliente MQTT local compartido por los subsistemas.

  Args:
    client: Cliente MQTT local ya conectado, o ``None`` para desconectarlo.

  Returns:
    None. Actualiza el estado global usado por los publicadores y callbacks.
  """
  global state, value, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  client_local = client


def get_client_local():
  """Obtiene el cliente MQTT local registrado.

  Returns:
    El cliente MQTT local compartido, o ``None`` si no se ha creado.
  """
  global client, state, value, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  return client_local


def get_factory_error_state():
  """Consulta la alarma global que bloquea o marca una estacion.

  Returns:
    Identificador de la estacion en error, o ``None`` cuando no hay alarma.
  """
  global client, state, value, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  logging.log(logging.TRACE0, factory_error_state)
  return factory_error_state


def set_factory_error_state(state):
  """Actualiza la alarma global de la fabrica.

  Args:
    state: Identificador de la estacion en error, o ``None`` para reconocerla.

  Returns:
    None. El valor queda disponible para los hilos de control de las estaciones.
  """
  global client, value, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  logging.log(logging.TRACE, state)
  factory_error_state = state


def get_alarm_timer():
  """Obtiene el intervalo global usado para detectar alarmas.

  Returns:
    El intervalo configurado, o ``None`` si aún no se ha inicializado.
  """
  global client, state, value, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  return ALARM_TIMER


def set_alarm_timer(value):
  """Configura el intervalo global usado para detectar alarmas.

  Args:
    value: Intervalo, expresado en segundos, que se asignará al temporizador.

  Returns:
    None.
  """
  global client, state, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  ALARM_TIMER = value


def get_ldr_period():
  """Obtiene el periodo global de lectura del sensor LDR.

  Returns:
    El periodo configurado, o ``None`` si aún no se ha inicializado.
  """
  global client, state, value, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  return LDR_PERIOD


def set_ldr_period(value):
  """Configura el periodo global de lectura del sensor LDR.

  Args:
    value: Periodo de lectura, expresado en segundos.

  Returns:
    None.
  """
  global client, state, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  LDR_PERIOD = value


def get_camera_fps():
  """Obtiene la frecuencia global de captura de la cámara.

  Returns:
    La frecuencia en fotogramas por segundo, o ``None`` si no está configurada.
  """
  global client, state, value, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  return CAMERA_FPS


def set_camera_fps(value):
  """Configura la frecuencia global de captura de la cámara.

  Args:
    value: Frecuencia de captura expresada en fotogramas por segundo.

  Returns:
    None.
  """
  global client, state, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  CAMERA_FPS = value


def get_bme680_period():
  """Obtiene el periodo global de lectura del sensor BME680.

  Returns:
    El periodo configurado, o ``None`` si aún no se ha inicializado.
  """
  global client, state, value, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  return BME680_PERIOD


def set_bme680_period(value):
  """Configura el periodo global de lectura del sensor BME680.

  Args:
    value: Periodo de lectura, expresado en segundos.

  Returns:
    None.
  """
  global client, state, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  BME680_PERIOD = value


def get_camera_on():
  """Consulta si la captura de la cámara está habilitada.

  Returns:
    ``True`` si la cámara está habilitada, ``False`` si está deshabilitada o
    ``None`` si todavía no se ha configurado.
  """
  global client, state, value, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  return CAMERA_ON


def set_camera_on(value):
  """Habilita o deshabilita la captura global de la cámara.

  Args:
    value: Indicador booleano que determina si la cámara queda habilitada.

  Returns:
    None.
  """
  global client, state, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  CAMERA_ON = value


def get_init_finished():
  """Consulta si la inicialización de la fábrica ha terminado.

  Returns:
    El indicador de finalización, o ``None`` si aún no se ha establecido.
  """
  global client, state, value, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  return INIT_FINISHED


def set_init_finished(value):
  """Actualiza el indicador global de finalización de la inicialización.

  Args:
    value: Indicador booleano que señala si el arranque ha terminado.

  Returns:
    None.
  """
  global client, state, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  INIT_FINISHED = value


def set_keep_alive(value):
  """Actualiza la señal global de mantenimiento de las comunicaciones.

  Args:
    value: Indicador que controla si los servicios deben permanecer activos.

  Returns:
    None.
  """
  global client, state, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  KEEP_ALIVE = value


def get_keep_alive():
  """Consulta la señal global de mantenimiento de las comunicaciones.

  Returns:
    El indicador configurado, o ``None`` si aún no se ha establecido.
  """
  global client, state, value, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  return KEEP_ALIVE


def set_cloud_active(value):
  """Actualiza el indicador de disponibilidad de la conexión en la nube.

  Args:
    value: Indicador booleano que señala si la conexión cloud está activa.

  Returns:
    None.
  """
  global client, state, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  CLOUD_ACTIVE = value


def get_cloud_active():
  """Consulta si la conexión MQTT con la nube está activa.

  Returns:
    El indicador de conexión, o ``None`` si todavía no se ha configurado.
  """
  global client, state, value, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE
  return CLOUD_ACTIVE


# Estados de preparación de los ejes VGR y HBW.

AXES_READY = False
VGR_READY = False
HBW_READY = False

def get_axes_ready():
  """Consulta si los ejes de VGR y HBW están preparados.

  Returns:
    ``True`` cuando ambos subsistemas están listos; en otro caso, ``False``.
  """
  global client, state, value, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE, AXES_READY, VGR_READY, HBW_READY
  return AXES_READY

def set_vgr_ready(value):
  """Actualiza el estado de preparación del robot VGR.

  Args:
    value: Indicador booleano que señala si VGR está preparado.

  Returns:
    None.
  """
  global client, state, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE, AXES_READY, VGR_READY, HBW_READY
  VGR_READY = value
  if VGR_READY and HBW_READY:
    AXES_READY = True

def set_hbw_ready(value):
  """Actualiza el estado de preparación del almacén HBW.

  Args:
    value: Indicador booleano que señala si HBW está preparado.

  Returns:
    None.
  """
  global client, state, client_local, factory_error_state, ALARM_TIMER, LDR_PERIOD, CAMERA_FPS, BME680_PERIOD, CAMERA_ON, INIT_FINISHED, KEEP_ALIVE, CLOUD_ACTIVE, AXES_READY, VGR_READY, HBW_READY
  HBW_READY = value
  if VGR_READY and HBW_READY:
    AXES_READY = True



