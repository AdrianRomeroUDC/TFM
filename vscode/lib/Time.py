"""Conversión de timestamps y fechas UTC usadas por la telemetría."""

# Utilidades de fecha compartidas por MQTT, NFC y telemetria.
import logging
from datetime import datetime
from datetime import timezone

ts = None


def timestamp_utcnow():
  """Genera la fecha y hora UTC actual con precision de milisegundos.

  Returns:
    Cadena ISO 8601 terminada en ``Z``. Tambien actualiza la variable global
    ``ts`` para conservar el ultimo valor generado.
  """
  global ts
  logging.log(logging.TRACE0, '-')
  ts = datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3] + "Z"
  return ts


def to_datetime_utc(ts):
  """Convierte un timestamp ISO 8601 con sufijo ``Z`` a ``datetime`` UTC.

  Args:
    ts: Timestamp textual con milisegundos, o ``None`` si no hay valor.

  Returns:
    ``datetime`` consciente de zona horaria, o una cadena vacia para ``None``.
  """
  logging.log(logging.TRACE0, ts)
  if ts != None:
    ts = datetime.strptime(ts[:-1], "%Y-%m-%dT%H:%M:%S.%f").replace(tzinfo=timezone.utc)
  else:
    logging.log(logging.DEBUG, 'ts empty')
    ts = ''
  return ts


