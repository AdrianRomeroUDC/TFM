import logging
import time
from lib.Axes1Ref import *
from lib.Axes2Ref import *
from lib.display import *
from lib.HBW_AxesNRef import *
from lib.MPO import *

i = None
absmax = None
j = None
t0 = None
t_diff = None


def test_move_Axes1Ref():
  global i, absmax, j, t0, t_diff
  display.set_attr("txt_label_message.text", str('TEST: Axes1Ref'))
  # num:          MX, IX, CX
  # 1: VGR x  # VGR_E2_M1, VGR_E2_I1, VGR_E2_C1
  # 2: VGR y  # VGR_E2_M2, VGR_E2_I2, VGR_E2_C2
  # 3: VGR z  # VGR_E2_M3, VGR_E2_I3, VGR_E2_C3
  # 4: HBW x # HBW_E1_M2, HBW_E1_I5, HBW_E1_C2
  # 5: HBW y # HBW_E1_M4, HBW_E1_I8, HBW_E1_C4
  # 6: SSC pan # SSC_M_M1, SSC_M_I1, SSC_M_C1
  # 7: SSC tilt # SSC_M_M2, SSC_M_I2, SSC_M_C2
  absmax = [1500, 900, 950, 2050, 850, 1550, 700]
  for i in range(1, 8):
    print('num: {}'.format(i))
    moveRef(i)
    t0 = time.time()
    moveAbs(i, absmax[int(i - 1)])
    t_diff = time.time() - t0
    print('time: {:.3f}'.format(t_diff))
    moveRef(i)
    time.sleep(1)


def test_move_Axes2Ref():
  global i, absmax, j, t0, t_diff
  display.set_attr("txt_label_message.text", str('TEST: Axes2Ref'))
  # num:                              MX, IX
  #   1: HBW z rear           # HBW_E1_M3, HBW_E1_I6
  #   2: HBW z front          # HBW_E1_M3, HBW_E1_I7
  #   3: MPO suction tt     # MPO_E3_M2, MPO_E4_I5
  #   4: MPO suction ov    # MPO_E3_M2, MPO_E3_I3
  #   5: MPO kiln slid in     # MPO_E3_M1, MPO_E3_I1
  #   6: MPO kiln slid out  # MPO_E3_M1, MPO_E3_I2
  #   7: MPO turntab suc  # MPO_E4_M1, MPO_E4_I1
  #   8: MPO turntab sa1  # MPO_E4_M1, MPO_E4_I2
  #   9: MPO turntab con  # MPO_E4_M1, MPO_E4_I3
  # 10: MPO turntab sa2  # MPO_E4_M1, MPO_E4_I2
  openDoor()
  # reverse order, execute ref last
  for i in range(10, 0, -1):
    print('num: {}'.format(i))
    t0 = time.time()
    move2Ref(i)
    t_diff = time.time() - t0
    print('time: {:.3f}'.format(t_diff))
    time.sleep(1)


def test_HBW_posall():
  global i, absmax, j, t0, t_diff
  moveConv()
  for i in range(1, 4):
    for j in range(1, 4):
      moveCR([i, j])


