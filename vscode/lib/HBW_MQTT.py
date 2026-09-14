import json
"""Publicación MQTT del estado del almacén HBW y de su inventario."""

import logging
from fischertechnik.mqtt.Constants import CONTROLLER_ID
from fischertechnik.mqtt.FTCloudClient import FTCloudClient
from fischertechnik.mqtt.MqttClient import MqttClient
from lib.Factory_Variables import *
from lib.HBW_Storage import *
from lib.Time import *

_code = None
_active = None
list_storage = None
loc = None
list_item = None
b_type = None
jsonitem = None
s = None
storage_location = None
payload = None
stockItems = None
i = None
payload_storage = None


def publish_state_HBW(_code, _active):
  """
  Envia a la nube el estado actual del almacen.

  Args:
    _code: Codigo de estado del almacen (por ejemplo 1=reposo,
      2=en movimiento, 4=error).
    _active: Indica si el almacen esta activo haciendo ese estado.

  Returns:
    None.
  """
  global list_storage, loc, list_item, b_type, jsonitem, s, storage_location, payload, stockItems, i, payload_storage
  logging.log(logging.TRACE_FCL, '%d, %d', _code, _active)
  if get_cloud_active():
    payload = '{{"ts":"{}","station":"hbw","code":{},"active":{}}}'.format(timestamp_utcnow(), _code, _active)
    logging.log(logging.DEBUG_FCL, payload)
    #print(payload)
    FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/f/i/state/hbw", payload)


def publish_state_Storage(list_storage):
  """
  Envia el inventario completo del almacen a la nube y a la red local.

  Convierte cada una de las 9 casillas del estante en un elemento de
  inventario (con su ubicacion y, si tiene pieza, su color y estado) y
  publica la lista resultante por MQTT.

  Args:
    list_storage: Lista con las 9 casillas del almacen, tal como la
      devuelve ``get_list_storage``.

  Returns:
    None.
  """
  global _code, _active, loc, list_item, b_type, jsonitem, s, storage_location, payload, stockItems, i, payload_storage
  logging.log(logging.TRACE_FCL, list_storage)
  storage_location = get_storage_location()
  stockItems = []
  #wp list: 1:ts, 2:uid, 3:color, 4:produced
  if list_storage != None and len(list_storage) == 9:
    for i in range(1, 10):
      stockItems.append(storage_wp_jsonitem(storage_location[int(i - 1)], list_storage[int(i - 1)]))
    payload_storage = '{{"ts":"{}", "stockItems":{} }}'.format(timestamp_utcnow(), stockItems)
    logging.log(logging.DEBUG_FCL, payload_storage)
    #print("TEXT: ", payload)
    payload_storage = json.dumps(eval(payload_storage))
    logging.log(logging.DEBUG_FCL, payload_storage)
    #print("JSON: ", payload_storage)
    if get_cloud_active():
      FTCloudClient.getInstance().publish("/j1/txt/" + str(CONTROLLER_ID) +"/f/i/stock", payload_storage)
    if (get_client_local()) != None:
      get_client_local().publish(topic='f/i/stock', payload=payload_storage, qos=2, retain=True)


def storage_wp_jsonitem(loc, list_item):
  """
  Convierte una casilla del almacen en el formato que espera la nube.

  Args:
    loc: Nombre de la casilla (por ejemplo 'A1').
    list_item: Datos de la pieza guardada en esa casilla, o ``None`` si
      esta vacia.

  Returns:
    Un diccionario con la ubicacion y, si hay pieza, su UID, color y
    estado.
  """
  global _code, _active, list_storage, b_type, jsonitem, s, storage_location, payload, stockItems, i, payload_storage
  logging.log(logging.TRACE0_FCL, loc)
  #print(list_item)
  if ((list_item!=None) and (list_item[1]!=None)):
    jsonitem = {
      "workpiece": {
       "id": list_item[1],
       "type": list_item[2],
       "state": type2str(list_item[3])
      },
      "location": loc
    }
  else:
    jsonitem = {
      "workpiece": None,
      "location": loc
    }
  return jsonitem


def type2str(b_type):
  """
  Traduce si una pieza esta mecanizada o en bruto a un texto para la nube.

  Args:
    b_type: True si la pieza ya esta procesada, False si esta en bruto.

  Returns:
    'PROCESSED' o 'RAW'.
  """
  global _code, _active, list_storage, loc, list_item, jsonitem, s, storage_location, payload, stockItems, i, payload_storage
  logging.log(logging.TRACE0_FCL, b_type)
  if b_type:
    s = 'PROCESSED'
  else:
    s = 'RAW'
  return s


