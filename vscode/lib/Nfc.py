import ctypes
import logging
import pynfc
import sys
import threading
import time
import traceback
from datetime import datetime

_tr0 = None
_tr = None
_dg = None
epoch = None
_inner_func = None
state = None
type2 = None
mask = None
vts = None
target = None
lockNFC = None
__version__ = None
state_str = None
type_str = None
r = None
tsstr = None
list_tsstr = None
nfc_obj = None
is_cmd = None
nfc_data = None
target_uid = None
_internal_func_ntag21x = None
list_temp = None
ts = None
def get_nfc_data_ts_list():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '-')
  r = None
  if nfc_data != None:
    r = nfc_data[4]
  logging.log(logging.DEBUG_NFC, r)
  return r

def nfc_read_uid():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '-')
  lockNFC.acquire()
  nfc_data = [None, None, None, None, [None] * 8]
  target_uid = None
  try:
    for target in nfc_obj.poll(modulations=((pynfc.nfc.NMT_ISO14443A, pynfc.nfc.NBR_424),), times=0x01, delay=1):

      target_uid = target.uid.decode('utf-8')

      #_print_device_target(target)

      if target_uid != None:
        break
  except Exception as e:
      target_uid = None
      logging.warning(e)
  nfc_data[0] = target_uid
  lockNFC.release()
  return target_uid

def _wrapper_ntag21x(_inner_func):
  global _tr0, _tr, _dg, epoch, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE0_NFC, '->')
  global tag
  def _internal_func_ntag21x():
      global tag
      target_uid = None
      try:
        for target in nfc_obj.poll(modulations=((pynfc.nfc.NMT_ISO14443A, pynfc.nfc.NBR_424),), times=0x01, delay=1):

          target_uid = target.uid.decode('utf-8')
          nfc_data[0] = target_uid
          _print_device_target(target)

          logging.log(logging.DEBUG_NFC, "freefare_get_tags")
          tags = freefare_get_tags(nfc_obj.pdevice)
          logging.log(logging.DEBUG_NFC, "tags")

          for tag in tags:
              logging.log(logging.DEBUG_NFC, "*")
              logging.log(logging.DEBUG_NFC, "tag")
              logging.log(logging.DEBUG_NFC, "type(tag)")

              logging.log(logging.DEBUG_NFC, 'freefare_get_tag_type')
              r_type = freefare_get_tag_type(tag)
              logging.log(logging.DEBUG_NFC, r_type)

              # read only NTAG21x tags
              if  r_type != 7:
                  logging.log(logging.DEBUG_NFC, "No NTAG21x tag! ", r_type)
                  friendly_name = ctypes.string_at(freefare_get_tag_friendly_name(tag))
                  logging.log(logging.DEBUG_NFC, friendly_name)
                  break

              logging.log(logging.DEBUG_NFC, 'ntag21x_connect')
              r = ntag21x_connect(tag)
              logging.log(logging.DEBUG_NFC, r)

              if (r >= 0):
                  logging.log(logging.DEBUG_NFC, 'ntag21x_get_info')
                  r = ntag21x_get_info(tag)
                  logging.log(logging.DEBUG_NFC, r)
                  if (r >= 0):
                      logging.log(logging.DEBUG_NFC, 'ntag21x_get_subtype')
                      r_subtype = ntag21x_get_subtype(tag)
                      logging.log(logging.DEBUG_NFC, r_subtype)

                      if r_subtype != NTAG_213:
                          logging.log(logging.DEBUG_NFC, "Wrong subtype, NTAG_213 expected, ",r_subtype )
                          break

                      logging.log(logging.DEBUG_NFC, '_inner_func ->')

                      r = _inner_func()
                      logging.log(logging.DEBUG_NFC, r)
                      logging.log(logging.DEBUG_NFC, '_inner_func <-')


              logging.log(logging.DEBUG_NFC, 'ntag21x_disconnect')
              ntag21x_disconnect(tag)

              #read only first tag in tags:
              break

          logging.log(logging.DEBUG_NFC, "freefare_free_tags")
          freefare_free_tags(tags)

          if target_uid != None:
            break
      except Exception as e:
          target_uid = None
          #ntag21x_disconnect(tag)
          #freefare_free_tags(tags)
          #print_nfc_data()
          #traceback.print_exc(file=sys.stdout)
          #logging.warning(e)
          logging.log(logging.DEBUG_NFC, e)
  logging.log(logging.TRACE0_NFC, '<-')
  return _internal_func_ntag21x

def init_freefare_ntag21x():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '-')
  # libs
  global _libraries
  _libraries = {}
  _libraries['libfreefare.so'] = ctypes.CDLL('libfreefare.so')
  _libraries['libnfc.so'] = ctypes.CDLL('libnfc.so')
  class struct_freefare_tag(ctypes.Structure):
      pass

  global FreefareTag
  FreefareTag = ctypes.POINTER(struct_freefare_tag)

  global freefare_get_tag_type
  freefare_get_tag_type = _libraries['libfreefare.so'].freefare_get_tag_type
  freefare_get_tag_type.restype = ctypes.c_int32
  freefare_get_tag_type.argtypes = [FreefareTag ]

  global freefare_get_tags
  freefare_get_tags = _libraries['libfreefare.so'].freefare_get_tags
  freefare_get_tags.restype = ctypes.POINTER(ctypes.POINTER(struct_freefare_tag))
  freefare_get_tags.argtypes = [ctypes.POINTER(pynfc.nfc.struct_nfc_device)]
  FreefareTag = ctypes.POINTER(struct_freefare_tag)

  global freefare_get_tag_friendly_name
  freefare_get_tag_friendly_name = _libraries['libfreefare.so'].freefare_get_tag_friendly_name
  freefare_get_tag_friendly_name.restype = ctypes.POINTER(ctypes.c_char)
  freefare_get_tag_friendly_name.argtypes = [FreefareTag]

  global freefare_free_tags
  freefare_free_tags = _libraries['libfreefare.so'].freefare_free_tags
  freefare_free_tags.restype = None
  freefare_free_tags.argtypes = [ctypes.POINTER(ctypes.POINTER(struct_freefare_tag))]

  global freefare_version
  #print("const char * freefare_version")
  freefare_version = _libraries['libfreefare.so'].freefare_version

  global nfc_version
  #print("const char * nfc_version")
  nfc_version = _libraries['libnfc.so'].nfc_version

  global NTAG_UNKNOWN, NTAG_213, NTAG_215, NTAG_216
  # values for enumeration 'c__EA_ntag_tag_subtype'
  c__EA_ntag_tag_subtype__enumvalues = {
      0: 'NTAG_UNKNOWN',
      1: 'NTAG_213',
      2: 'NTAG_215',
      2: 'NTAG_216',
  }
  NTAG_UNKNOWN= 0
  NTAG_213= 1
  NTAG_215= 2
  NTAG_216= 3
  c__EA_ntag_tag_subtype = ctypes.c_int # enum
  ntag_tag_subtype = c__EA_ntag_tag_subtype
  ntag_tag_subtype__enumvalues = c__EA_ntag_tag_subtype__enumvalues

  global ntag21x_connect
  #print("int ntag21x_connect(FreefareTag tag)")
  ntag21x_connect = _libraries['libfreefare.so'].ntag21x_connect
  ntag21x_connect.restype = ctypes.c_int32
  ntag21x_connect.argtypes = [FreefareTag ]

  global ntag21x_disconnect
  #print("int ntag21x_disconnect(FreefareTag tag)")
  ntag21x_disconnect = _libraries['libfreefare.so'].ntag21x_disconnect
  ntag21x_disconnect.restype = ctypes.c_int32
  ntag21x_disconnect.argtypes = [FreefareTag]

  global ntag21x_get_info
  #print("int ntag21x_get_info(FreefareTag tag)")
  ntag21x_get_info = _libraries['libfreefare.so'].ntag21x_get_info
  ntag21x_get_info.restype = ctypes.c_int32
  ntag21x_get_info.argtypes = [FreefareTag]

  global ntag21x_get_subtype
  #print("enum ntag_tag_subtype ntag21x_get_subtype(FreefareTag tag)")
  ntag21x_get_subtype = _libraries['libfreefare.so'].ntag21x_get_subtype
  ntag21x_get_subtype.restype = ntag_tag_subtype
  ntag21x_get_subtype.argtypes = [FreefareTag]

  global ntag21x_fast_read
  #print("int ntag21x_fast_read(FreefareTag tag, uint8_t start_page, uint8_t end_page, uint8_t *data)")
  ntag21x_fast_read = _libraries['libfreefare.so'].ntag21x_fast_read
  ntag21x_fast_read.restype = ctypes.c_int32
  ntag21x_fast_read.argtypes = [FreefareTag, ctypes.c_ubyte, ctypes.c_ubyte, ctypes.POINTER(ctypes.c_ubyte * 144)]

  global ntag21x_write
  #print("int ntag21x_write(FreefareTag tag, uint8_t page, uint8_t data[4])")
  ntag21x_write = _libraries['libfreefare.so'].ntag21x_write
  ntag21x_write.restype = ctypes.c_int32
  ntag21x_write.argtypes = [FreefareTag, ctypes.c_ubyte, ctypes.POINTER(ctypes.c_ubyte*4)]
  return r



def initlog_NFC(_tr0, _tr, _dg):
  global epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.TRACE0_NFC = _tr0
  logging.addLevelName(logging.TRACE0_NFC , 'TRACE0_NFC')
  logging.TRACE_NFC = _tr
  logging.addLevelName(logging.TRACE_NFC , 'TRACE_NFC')
  logging.DEBUG_NFC = _dg
  logging.addLevelName(logging.DEBUG_NFC, 'DEBUG_NFC')


def get_lock_NFC():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE0_NFC, '-')
  return lockNFC


def get_nfc_version():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  __version__ = '2022-07-13'
  logging.log(logging.DEBUG_NFC, __version__)
  return __version__


def get_nfc_data_uid():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '-')
  return nfc_data[0]


def get_nfc_data_state():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '-')
  return nfc_data[1]


def get_nfc_data_state_str():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '-')
  state_str = 'None'
  if get_nfc_data_state() == 0:
    state_str = 'Raw'
  elif get_nfc_data_state() == 1:
    state_str = 'Processed'
  elif get_nfc_data_state() == 2:
    state_str = 'Rejected'
  return state_str


def get_nfc_data_type():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '-')
  return nfc_data[2]


def get_nfc_data_type_str():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '-')
  type_str = 'None'
  if get_nfc_data_type() == 1:
    type_str = 'White'
  elif get_nfc_data_type() == 2:
    type_str = 'Red'
  elif get_nfc_data_type() == 3:
    type_str = 'Blue'
  return type_str


def get_nfc_data_mask():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '-')
  return nfc_data[3]


def get_nfc_data_mask_str():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '-')
  return '{:08b}'.format(get_nfc_data_mask()) if get_nfc_data_mask() != None else None


def epoch2tsstr(epoch):
  global _tr0, _tr, _dg, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE0_NFC, '-')
  try:
    tsstr = datetime.utcfromtimestamp(epoch).strftime('%Y-%m-%dT%H:%M:%S.%f')[:-3]+'Z'
  except OverflowError as e:
    tsstr = ""
    print(e)
  return tsstr


def get_nfc_data_tsstr_list():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '-')
  list_tsstr = []
  list_temp = get_nfc_data_ts_list()
  if list_temp != None:
    for ts in list_temp:
      if ts != None:
        if list_tsstr != None:
          list_tsstr.append(epoch2tsstr(ts))
    logging.log(logging.DEBUG_NFC, list_tsstr)
  return list_tsstr


def nfc_init():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '-')
  lockNFC = threading.RLock() #https://stackoverflow.com/questions/28017535/do-i-have-to-lock-all-functions-that-calls-to-one-or-more-locked-function-for-mu
  # nfc_data:
  #   0:nfc uid:
  #     workpiece NTAG213:  7 bytes
  #     card + blue key Mifare: 4 bytes
  #   1:byte0: state: 0:RAW, 1:PROCESSED, 2:REJECTED
  #   2:byte1: type: 0:NONE, 1:WHITE, 2:RED, 3:BLUE
  #   3:byte2: mask timestamps
  #   4:byte3: none (reserved)
  #   5:byte4...4+(8*8): vts[8]: int64_t (8 bytes)
  nfc_data = [0, 0, 0, 0, [0] * 8]
  nfc_obj = pynfc.Nfc(log_level=2) #default
  #nfc_obj = pynfc.Nfc(device="pn532_i2c:/dev/i2c-3", log_level=3)
  r = init_freefare_ntag21x()
  print('nfc {}'.format(get_nfc_version()))
  print('libnfc {}'.format(nfc_version_text()))
  print('libfreefare {}'.format(nfc_freefare_version_text()))
  return nfc_obj


def is_cmd_uid():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, target_uid)
  is_cmd = False
  if target_uid != None:
    logging.debug(len(target_uid))
    is_cmd = len(target_uid) == 8
  return is_cmd


def get_last_nfc_data():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '-')
  return nfc_data


def _inner_ntag21x_read():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE0_NFC, '->')
  global tag
  #print(nfc_data[4])
  bytes144 = (ctypes.c_ubyte * 144)()
  logging.log(logging.DEBUG_NFC, 'ntag21x_fast_read')
  r = ntag21x_fast_read(tag, 0x4, 0x27, bytes144)
  #print(r)
  if (r >= 0):
      #print("bytes144", bytes144)
      #print("bytes144[:]", bytes144[:])
      nfc_data[1] = bytes144[0]
      nfc_data[2] = bytes144[1]
      nfc_data[3] = bytes144[2]
      nfc_data[4] = [0] * 8
      for i in range(8):
          bytes8 = [0] * 8
          #print("bytes8", bytes8)
          for j in range(8):
              bytes8[j] = bytes144[4+i*8 +j]
              #print("bytes144[4+i*8 +j]", bytes144[4+i*8 +j])
          #print("bytes8[:]", bytes8[:])
          epoch = int.from_bytes(bytes8, byteorder='little', signed=True) / 1000000000
          #print("epoch ", epoch)
          #s = datetime.utcfromtimestamp(epoch).strftime('%Y%m%d %H:%M:%S.%f')
          #print(s)
          nfc_data[4][i] = epoch
      print("nfc_data[4]", nfc_data[4])
  logging.log(logging.TRACE0_NFC, '<-')
  return r


def nfc_read():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '->')
  lockNFC.acquire()
  nfc_data = [None, None, None, None, [None] * 8]
  ntag21x_read = _wrapper_ntag21x(_inner_ntag21x_read)
  r = ntag21x_read()
  logging.log(logging.TRACE_NFC, '<-')
  lockNFC.release()
  return nfc_data


def _inner_ntag21x_write():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE0_NFC, '->')
  global tag
  print_nfc_data()
  bytes4 = (ctypes.c_ubyte * 4)()
  bytes4[0] = nfc_data[1]
  bytes4[1] = nfc_data[2]
  bytes4[2] = nfc_data[3]
  bytes4[3] = 0 #reserved
  logging.log(logging.DEBUG_NFC, 'ntag21x_write')
  r = ntag21x_write(tag, 0x4, bytes4)
  if r< 0:
      logging.log(logging.DEBUG_NFC, "write error r 0x4: %s", str(r))
      return r
  if nfc_data[4] != None:
      for i in range(8):
          epochs = nfc_data[4][i]
          if epochs == None:
              continue
          logging.log(logging.DEBUG_NFC, "epochs %s", str(epochs))
          s = datetime.utcfromtimestamp(epochs).strftime('%Y%m%d %H:%M:%S.%f')
          logging.log(logging.DEBUG_NFC, "%s", str(s))
          bytes8str = int(epochs*1000000000).to_bytes(8, byteorder='little', signed=True)
          logging.log(logging.DEBUG_NFC, "bytes8str %s", str(bytes8str))
          bytes8 = (ctypes.c_ubyte * 8).from_buffer_copy(bytes8str)
          logging.log(logging.DEBUG_NFC, "bytes8[:] %s", str(bytes8[:]))

          bytes4a = (ctypes.c_ubyte * 4)()
          bytes4a[0] = bytes8[0]
          bytes4a[1] = bytes8[1]
          bytes4a[2] = bytes8[2]
          bytes4a[3] = bytes8[3]
          logging.log(logging.DEBUG_NFC, "bytes4a[:] %s", str(bytes4a[:]))
          #writing to tag 4 bytes on page 0x05
          logging.log(logging.DEBUG_NFC, 'ntag21x_write')
          r = ntag21x_write(tag, 0x5+i*2, bytes4a)
          if r< 0:
              logging.log(logging.DEBUG_NFC, 'write error r 0x5: %s', str(r))
              return r

          bytes4b = (ctypes.c_ubyte * 4)()
          bytes4b[0] = bytes8[4]
          bytes4b[1] = bytes8[5]
          bytes4b[2] = bytes8[6]
          bytes4b[3] = bytes8[7]
          logging.log(logging.DEBUG_NFC, "bytes4b[:] %s", str(bytes4b[:]))
          #writing to tag 4 bytes on page 0x06.
          logging.log(logging.DEBUG_NFC, 'ntag21x_write')
          r = ntag21x_write(tag, 0x5+i*2+1, bytes4b)
          if r< 0:
              logging.log(logging.DEBUG_NFC, 'write error r 0x6:', r)
              return r
      print_nfc_data()
  logging.log(logging.TRACE0_NFC, '<-')
  return r


def print_nfc_data():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '-')
  lockNFC.acquire()
  if nfc_data != None:
      print('nfc_data[0]', nfc_data[0])
      print('nfc_data[1]', nfc_data[1])
      print('nfc_data[2]', nfc_data[2])
      print('nfc_data[3]', nfc_data[3])
      if (nfc_data[4] != None):
          print('nfc_data[4][:]', nfc_data[4][:])
  else:
      print('nfc_data', None)
  lockNFC.release()


def nfc_write(state, type2, mask, vts):
  global _tr0, _tr, _dg, epoch, _inner_func, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '->')
  lockNFC.acquire()
  nfc_data = [None, state, type2, mask, vts]
  ntag21x_write = _wrapper_ntag21x(_inner_ntag21x_write)
  r = ntag21x_write()
  logging.log(logging.TRACE_NFC, r)
  nfc_data[0] = target_uid
  logging.log(logging.TRACE_NFC, '<-')
  lockNFC.release()
  return r == 0


def nfc_delete():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '->')
  r = nfc_write(0, 0, 0, [0] * 8)
  logging.log(logging.TRACE_NFC, '<-')
  return r == 0


def nfc_freefare_version_text():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '-')
  r_fv = freefare_version()
  r = ctypes.c_char_p(r_fv).value.decode('utf-8')
  return r


def nfc_version_text():
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, target, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '-')
  r_fv = nfc_version()
  r = ctypes.c_char_p(r_fv).value.decode('utf-8')
  return r


def _print_device_target(target):
  global _tr0, _tr, _dg, epoch, _inner_func, state, type2, mask, vts, lockNFC, __version__, state_str, type_str, r, tsstr, list_tsstr, nfc_obj, is_cmd, nfc_data, target_uid, _internal_func_ntag21x, list_temp, ts
  logging.log(logging.TRACE_NFC, '-')
  #todo: Application level error with multiple threads
  DESFIRE_DEFAULT_KEY = b'\x00' * 8
  MIFARE_BLANK_TOKEN = b'\xFF' * 1024 * 4

  #nfc_obj.pdevice
  #nfc_obj.pctx

  logging.log(logging.DEBUG_NFC, "%d %s", target.type, target.uid)

  if type(target) == pynfc.Desfire:
      logging.log(logging.DEBUG_NFC, "Desfire",target.auth(DESFIRE_DEFAULT_KEY))
  elif type(target) == pynfc.Mifare:
      logging.log(logging.DEBUG_NFC, "Mifare", target.auth(MIFARE_BLANK_TOKEN))
  elif target.type == 7:
      logging.log(logging.DEBUG_NFC, "NTAG_21x")
  else:
      logging.log(logging.DEBUG_NFC, "Unknown type", type(target), target.type)

  r = target.type
  return r


