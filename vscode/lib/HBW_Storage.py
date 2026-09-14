"""Modelo de inventario y operaciones de almacenamiento del HBW."""

import json
import logging
import subprocess
import time
from os.path import exists

num = None
wp = None
color = None
storage_wp = None
storage_location = None
ret = None
count = None
currentNum = None
fileHBWStorage = None
x = None
nextFetchNum = None
storage_container = None
storage_wp_json = None
y = None
storage_wp_map = None
iuid = None
icolor = None
listwp_num_ts = None
listwp_num_ts_sorted = None


def initStorage():
  """
  Prepara el inventario del almacen al arrancar la fabrica.

  Da nombre a las 9 casillas del estante (A1, B1, C1... hasta C3), vacia el
  inventario en memoria y lo rellena con lo que hubiera guardado en el
  fichero de la ultima vez, corrigiendo cualquier dato incoherente.

  Returns:
    None.
  """
  global num, wp, color, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE_HBW, '-')
  currentNum = -1
  nextFetchNum = -1
  storage_location = ['A1', 'B1', 'C1', 'A2', 'B2', 'C2', 'A3', 'B3', 'C3']
  resetStorage()
  loadFileStorage()
  if checkAndCorrectStorage():
    print('Info: storage data corrected.')


def loadFileStorage():
  """
  Carga el inventario del almacen desde el fichero guardado en disco.

  Si el fichero ya existe, lee de el lo que habia guardado la ultima vez.
  Si todavia no existe (primer arranque), crea uno nuevo con el inventario
  vacio actual.

  Returns:
    None.
  """
  global num, wp, color, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE_HBW, '-')
  if exists('/opt/ft/workspaces/HBW.Storage.json'):
    readFileStorage()
  else:
    saveFileStorage()
  subprocess.Popen(['chmod', '777', '/opt/ft/workspaces/HBW.Storage.json'])


def readFileStorage():
  """
  Lee el fichero de inventario y rellena con el la tabla en memoria.

  Returns:
    None.
  """
  global num, wp, color, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE_HBW, '-')
  fileHBWStorage = open('/opt/ft/workspaces/HBW.Storage.json', 'r', encoding='utf8')
  storage_wp_json = fileHBWStorage.read()
  storage_wp_map = json.loads(storage_wp_json)
  storage_wp = [storage_wp_map['Storage']['A1'],storage_wp_map['Storage']['A2'], storage_wp_map['Storage']['A3'],storage_wp_map['Storage']['B1'],storage_wp_map['Storage']['B2'],storage_wp_map['Storage']['B3'],storage_wp_map['Storage']['C1'],storage_wp_map['Storage']['C2'],storage_wp_map['Storage']['C3']]
  print(storage_wp)
  fileHBWStorage.close()


def saveFileStorage():
  """
  Guarda el inventario actual del almacen en el fichero de disco.

  Returns:
    None.
  """
  global num, wp, color, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE_HBW, '-')
  fileHBWStorage = open('/opt/ft/workspaces/HBW.Storage.json', 'w', encoding='utf8')
  storage_wp_map = { "Storage" : {\
   "A1" : storage_wp[0] if (storage_wp!=None) else "None",\
   "A2" : storage_wp[1] if (storage_wp!=None) else "None",\
   "A3" : storage_wp[2] if (storage_wp!=None) else "None",\
   "B1" : storage_wp[3] if (storage_wp!=None) else "None",\
   "B2" : storage_wp[4] if (storage_wp!=None) else "None",\
   "B3" : storage_wp[5] if (storage_wp!=None) else "None",\
   "C1" : storage_wp[6] if (storage_wp!=None) else "None",\
   "C2" : storage_wp[7] if (storage_wp!=None) else "None",\
   "C3" : storage_wp[8] if (storage_wp!=None) else "None",\
   } }
  storage_wp_json = json.dumps(storage_wp_map)
  fileHBWStorage.write(storage_wp_json)
  fileHBWStorage.close()


def num2xy(num):
  """
  Traduce el numero de una casilla del estante (1 a 9) a fila y columna.

  Args:
    num: Numero de casilla del estante, de 1 a 9.

  Returns:
    Una lista ``[fila, columna]`` con la posicion de esa casilla en el
    estante de 3x3.
  """
  global wp, color, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE_HBW, 'num=%d', num)
  x = (num - 1) / 3
  y = (num - 1) % 3
  logging.log(logging.TRACE, 'x y=%d %d', x+1, y+1)
  return [x + 1, y + 1]


def get_currentpos_xy():
  """
  Devuelve la fila y columna de la ultima casilla usada del estante.

  Returns:
    Una lista ``[fila, columna]`` con esa posicion.
  """
  global num, wp, color, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE0_HBW, '-')
  return num2xy(currentNum)


def get_nextfetchpos_xy():
  """
  Devuelve la fila y columna de la casilla que toca coger o dejar ahora.

  Returns:
    Una lista ``[fila, columna]`` con esa posicion.
  """
  global num, wp, color, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE0_HBW, '-')
  return num2xy(nextFetchNum)


def get_list_storage():
  """
  Devuelve el inventario completo del almacen.

  Returns:
    Una lista con las 9 casillas del estante: ``None`` si esta vacia o
    ``[timestamp, uid, color, producida]`` si tiene una pieza.
  """
  global num, wp, color, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE0_HBW, '-')
  return storage_wp


def get_storage_location():
  """
  Devuelve los nombres de las 9 casillas del estante.

  Returns:
    La lista de nombres ``['A1', 'B1', 'C1', 'A2', 'B2', 'C2', 'A3',
    'B3', 'C3']``.
  """
  global num, wp, color, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE0_HBW, '-')
  return storage_location


def resetStorage():
  """
  Vacia por completo el inventario del almacen.

  Marca las 9 casillas como libres y sin ninguna pieza guardada, y deja
  lista la primera casilla para la proxima entrega.

  Returns:
    None.
  """
  global num, wp, color, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE_HBW, '-')
  storage_container = [True] * 9
  #wp list: 1:ts, 2:uid, 3:color, 4:produced
  storage_wp = [None] * 9
  logging.log(logging.DEBUG_HBW, storage_wp)
  nextFetchNum = 1


def storeContainer():
  """
  Marca como guardado el contenedor vacio que se acaba de devolver.

  Deja la casilla reservada por ``fetchContainer`` de nuevo vacia y libre.

  Returns:
    True si la casilla reservada era valida y se pudo liberar, False si
    no.
  """
  global num, wp, color, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE_HBW, '-')
  ret = False
  if isValidNum(nextFetchNum):
    storage_container[int(nextFetchNum - 1)] = True
    storage_wp[int(nextFetchNum - 1)] = None
    logging.log(logging.DEBUG_HBW, storage_wp)
    ret = True
  return ret


def storeWorkpiece(wp):
  """
  Anota en el inventario la pieza que se acaba de guardar en el estante.

  Args:
    wp: Datos de la pieza guardada: ``[timestamp, uid, color, producida]``.

  Returns:
    True si la casilla reservada era valida y se pudo anotar la pieza,
    False si no.
  """
  global num, color, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE_HBW, '-')
  ret = False
  if isValidNum(nextFetchNum):
    storage_wp[int(nextFetchNum - 1)] = wp
    logging.log(logging.DEBUG_HBW, storage_wp)
    ret = True
  return ret


def checkAndCorrectStorage():
  """
  Revisa el inventario y limpia las entradas con datos incompletos.

  Si alguna casilla dice tener una pieza pero le falta el UID o el color,
  se considera un dato corrupto y se vacia esa casilla.

  Returns:
    True si se corrigio alguna casilla, False si el inventario ya estaba
    bien.
  """
  global num, wp, color, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE_HBW, '-')
  ret = False
  for num in range(1, 10):
    if storage_wp[int(num - 1)] != None:
      iuid = storage_wp[int(num - 1)][1]
      icolor = storage_wp[int(num - 1)][2]
      if iuid == None or icolor == None or icolor == 'NONE':
        logging.log(logging.DEBUG_HBW, 'storage num %d corrected to null! (uid or color is null)', num)
        storage_wp[int(num - 1)] = None
        ret = True
  return ret


def fetchWorkpiece(color):
  """
  Reserva la pieza mas antigua del color pedido para sacarla del almacen.

  Busca entre todas las piezas guardadas las que sean de ese color y elige
  la que lleva mas tiempo esperando (la primera que entro es la primera
  que sale). Marca esa casilla como reservada mientras se recoge.

  Args:
    color: Color de pieza que se quiere sacar ('WHITE', 'RED' o 'BLUE').

  Returns:
    True si se encontro y reservo una pieza de ese color, False si no
    habia ninguna.
  """
  global num, wp, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE_HBW, color)
  ret = False
  nextFetchNum = -1
  if storage_wp != None:
    logging.log(logging.DEBUG_HBW, storage_wp)
    if color == 'WHITE' or color == 'RED' or color == 'BLUE':
      listwp_num_ts = []
      for num in range(1, 10):
        if storage_wp[int(num - 1)] != None:
          if storage_wp[int(num - 1)][2] == color:
            #list: 1:num, 2:ts
            listwp_num_ts.append([num, storage_wp[int(num - 1)][0]])
      #FIFO

      #print(listwp_num_ts)

      # take the second element for sort
      def take_second(elem):
        """
        Devuelve la fecha de una entrada, para poder ordenar por antiguedad.

        Args:
          elem: Entrada ``[numero_casilla, timestamp]``.

        Returns:
          El timestamp de esa entrada.
        """
        return elem[1]

      listwp_num_ts_sorted = sorted(listwp_num_ts, key=take_second)

      #print(listwp_num_ts_sorted)
      if len(listwp_num_ts_sorted) > 0:
        nextFetchNum = listwp_num_ts_sorted[0][0]
        print(nextFetchNum)
    if isValidNum(nextFetchNum):
      storage_container[int(nextFetchNum - 1)] = True
      storage_wp[int(nextFetchNum - 1)] = [(time.time() * 1000), '', 'NONE', False]
      logging.log(logging.DEBUG_HBW, storage_wp)
      ret = True
  return ret


def fetchContainer():
  """Reserva el primer contenedor libre del almacén HBW.

  Returns:
    ``True`` si se reservó un contenedor; en otro caso, ``False``.
  """
  global num, wp, color, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE_HBW, '-')
  ret = False
  nextFetchNum = -1
  for num in range(1, 10):
    if storage_wp[int(num - 1)] == None or storage_wp[int(num - 1)][2] == 'NONE':
      nextFetchNum = num
      break
  if isValidNum(nextFetchNum):
    storage_container[int(nextFetchNum - 1)] = True
    storage_wp[int(nextFetchNum - 1)] = [(time.time() * 1000), '', 'NONE', False]
    logging.log(logging.DEBUG_HBW, storage_wp)
    ret = True
  return ret


def isValidNum(num):
  """Comprueba si un número identifica una ubicación válida del almacén.

  Args:
    num: Número de ubicación que se valida.

  Returns:
    ``True`` para los valores de 1 a 9; en otro caso, ``False``.
  """
  global wp, color, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE_HBW, 'num=%d', num)
  return num >= 1 and num <= 9


def can_color_be_stored(color):
  """Comprueba si todavía hay capacidad para almacenar un color.

  Args:
    color: Color de la pieza que se quiere almacenar.

  Returns:
    ``True`` si el color puede almacenarse; en otro caso, ``False``.
  """
  global num, wp, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE_HBW, color)
  ret = False
  if color != None:
    count = 0
    for num in range(1, 10):
      if storage_wp[int(num - 1)] != None:
        icolor = storage_wp[int(num - 1)][2]
      else:
        icolor = 'NONE'
      if icolor != None:
        if icolor.upper() == color.upper():
          count = (count if isinstance(count, (int, float)) else 0) + 1
    logging.log(logging.DEBUG_HBW, "count=%d", count)
    if count < 3:
      ret = True
  return ret


def get_num_color_stored(color):
  """Cuenta cuántas piezas del color indicado están almacenadas.

  Args:
    color: Color que se quiere contabilizar.

  Returns:
    Número de piezas almacenadas con ese color.
  """
  global num, wp, storage_wp, storage_location, ret, count, currentNum, fileHBWStorage, x, nextFetchNum, storage_container, storage_wp_json, y, storage_wp_map, iuid, icolor, listwp_num_ts, listwp_num_ts_sorted
  logging.log(logging.TRACE0_HBW, color)
  count = 0
  if color != None:
    for num in range(1, 10):
      if storage_wp[int(num - 1)] != None:
        icolor = storage_wp[int(num - 1)][2]
      else:
        icolor = 'NONE'
      if icolor != None and icolor.upper() == color.upper():
        count = (count if isinstance(count, (int, float)) else 0) + 1
    logging.log(logging.DEBUG_HBW, count)
  return count


