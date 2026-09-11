import logging
from datetime import datetime
from datetime import timezone

ts = None


def timestamp_utcnow():
  global ts
  logging.log(logging.TRACE0, '-')
  ts = datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3] + "Z"
  return ts


def to_datetime_utc(ts):
  logging.log(logging.TRACE0, ts)
  if ts != None:
    ts = datetime.strptime(ts[:-1], "%Y-%m-%dT%H:%M:%S.%f").replace(tzinfo=timezone.utc)
  else:
    logging.log(logging.DEBUG, 'ts empty')
    ts = ''
  return ts


