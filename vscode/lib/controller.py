"""El plano de cableado de la fabrica, escrito en Python.

Cada TXT 4.0 es una cajita controladora atornillada a una estacion fisica
(la camara, el almacen, el brazo, el horno, la mesa giratoria o la cinta de
clasificacion). A cada una le hemos enchufado sensores, motores y luces en
unos conectores numerados (I1, I2... para entradas; O1, O2... para salidas;
M1, M2... para motores). Este archivo le pone nombre a cada cable para que,
en el resto del programa, en vez de escribir "enciende la salida 7 del
controlador 3" podamos escribir "abre la puerta del horno".

Todas las estaciones toman sus sensores y motores de aqui, asi que este
archivo se ejecuta el primero de todos: antes de mover nada hay que saber
que hay enchufado y donde.
"""

import fischertechnik.factories as txt_factory

# Arranca el firmware de la placa y lo prepara para reconocer sensores, motores y camara.
txt_factory.init()
txt_factory.init_input_factory()
txt_factory.init_output_factory()
txt_factory.init_motor_factory()
txt_factory.init_counter_factory()
txt_factory.init_i2c_factory()
txt_factory.init_usb_factory()
txt_factory.init_camera_factory()


# only internal use, loads all data from file

# TXT4.0 (0) - MASTER
# Estacion camara
TXT_SSC_M = txt_factory.controller_factory.create_graphical_controller()
# Interruptor que avisa cuando la camara termina de girar hasta el tope
TXT_SSC_M_I1_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_SSC_M, 1)   
# Interruptor que avisa cuando la camara termina de subir o bajar hasta el tope
TXT_SSC_M_I2_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_SSC_M, 2)   
# LDR: mide cuanta luz hay alrededor de la fabrica
TXT_SSC_M_I3_photo_resistor = txt_factory.input_factory.create_photo_resistor(TXT_SSC_M, 3) 
# FIXME: en I8 no hay nada conectado en la planta real
TXT_SSC_M_I8_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_SSC_M, 8)   
# LED que indica que la estacion esta conectada y funcionando
TXT_SSC_M_O5_led = txt_factory.output_factory.create_led(TXT_SSC_M, 5)
# LED rojo del semaforo de alarmas
TXT_SSC_M_O6_led = txt_factory.output_factory.create_led(TXT_SSC_M, 6)
# LED amarillo del semaforo de alarmas
TXT_SSC_M_O7_led = txt_factory.output_factory.create_led(TXT_SSC_M, 7)
# LED verde del semaforo de alarmas
TXT_SSC_M_O8_led = txt_factory.output_factory.create_led(TXT_SSC_M, 8)
# Motor que gira la camara a izquierda y derecha
TXT_SSC_M_M1_encodermotor = txt_factory.motor_factory.create_encodermotor(TXT_SSC_M, 1)
# Motor que sube y baja la inclinacion de la camara
TXT_SSC_M_M2_encodermotor = txt_factory.motor_factory.create_encodermotor(TXT_SSC_M, 2)
# Cuenta cuanto ha girado el motor de rotacion, para saber en que angulo esta la camara
TXT_SSC_M_C1_motor_step_counter = txt_factory.counter_factory.create_encodermotor_counter(TXT_SSC_M, 1)
TXT_SSC_M_C1_motor_step_counter.set_motor(TXT_SSC_M_M1_encodermotor)
# Cuenta cuanto ha subido o bajado la camara
TXT_SSC_M_C2_motor_step_counter = txt_factory.counter_factory.create_encodermotor_counter(TXT_SSC_M, 2)
TXT_SSC_M_C2_motor_step_counter.set_motor(TXT_SSC_M_M2_encodermotor)
# Sensor que mide temperatura, humedad, presion y calidad del aire de la fabrica
TXT_SSC_M_I2C_1_environment_sensor = txt_factory.i2c_factory.create_environment_sensor(TXT_SSC_M, 1)
# Camara que graba y transmite la imagen de la fabrica
TXT_SSC_M_USB1_1_camera = txt_factory.usb_factory.create_camera(TXT_SSC_M, 1)

# TXT4.0 (1) - SLAVE
# Almacen
TXT_HBW_E1 = txt_factory.controller_factory.create_graphical_controller(1)
# Fototransistor que detecta una pieza llegando por fuera del almacen
TXT_HBW_E1_I1_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_HBW_E1, 1)
# Fototransistor que detecta una pieza dentro del almacen
TXT_HBW_E1_I4_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_HBW_E1, 4)
# Interruptor que avisa cuando el brazo del almacen llega a la posicion de coger la pieza
TXT_HBW_E1_I5_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_HBW_E1, 5)
# Interruptor que avisa cuando el brazo del almacen termina de retraerse
TXT_HBW_E1_I6_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_HBW_E1, 6)
# Interruptor que avisa cuando el brazo del almacen termina de extenderse
TXT_HBW_E1_I7_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_HBW_E1, 7)
# Interruptor que avisa cuando el brazo del almacen termina de subir
TXT_HBW_E1_I8_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_HBW_E1, 8)
# Motor de la cinta que mete y saca piezas del almacen
TXT_HBW_E1_M1_motor = txt_factory.motor_factory.create_motor(TXT_HBW_E1, 1)
# Motor que mueve el brazo del almacen de lado a lado, para elegir la columna del estante
TXT_HBW_E1_M2_encodermotor = txt_factory.motor_factory.create_encodermotor(TXT_HBW_E1, 2) 
# Motor que estira y retrae el brazo del almacen para coger o dejar la pieza
TXT_HBW_E1_M3_motor = txt_factory.motor_factory.create_motor(TXT_HBW_E1, 3)
# Motor que sube y baja el brazo del almacen, para elegir la fila del estante
TXT_HBW_E1_M4_encodermotor = txt_factory.motor_factory.create_encodermotor(TXT_HBW_E1, 4)
# Cuenta el movimiento lateral del brazo, para saber en que columna esta
TXT_HBW_E1_C2_motor_step_counter = txt_factory.counter_factory.create_encodermotor_counter(TXT_HBW_E1, 2)
TXT_HBW_E1_C2_motor_step_counter.set_motor(TXT_HBW_E1_M2_encodermotor)
# Cuenta la subida y bajada del brazo, para saber en que fila esta
TXT_HBW_E1_C4_motor_step_counter = txt_factory.counter_factory.create_encodermotor_counter(TXT_HBW_E1, 4)
TXT_HBW_E1_C4_motor_step_counter.set_motor(TXT_HBW_E1_M4_encodermotor)

# TXT4.0 (2) - SLAVE
# Brazo robotizado central (VGR)
TXT_VGR_E2 = txt_factory.controller_factory.create_graphical_controller(2)
# Interruptor que avisa cuando el brazo termina de girar hasta el tope
TXT_VGR_E2_I1_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_VGR_E2, 1)
# Interruptor que avisa cuando el brazo termina de subir
TXT_VGR_E2_I2_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_VGR_E2, 2)
# Interruptor que avisa cuando el brazo termina de retraerse
TXT_VGR_E2_I3_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_VGR_E2, 3)
# Fototransistor que detecta la pieza en la salida de la estacion DPS
TXT_VGR_E2_I7_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_VGR_E2, 7)
# Sensor que lee el color de la pieza en la entrada de la estacion DPS
TXT_VGR_E2_I8_color_sensor = txt_factory.input_factory.create_color_sensor(TXT_VGR_E2, 8)
# Bomba que genera el vacio para que la ventosa agarre la pieza
TXT_VGR_E2_O7_compressor = txt_factory.output_factory.create_compressor(TXT_VGR_E2, 7)
# Valvula que abre o cierra el paso de vacio hacia la ventosa
TXT_VGR_E2_O8_magnetic_valve = txt_factory.output_factory.create_magnetic_valve(TXT_VGR_E2, 8)
# Motor que gira el brazo hacia cada estacion
TXT_VGR_E2_M1_encodermotor = txt_factory.motor_factory.create_encodermotor(TXT_VGR_E2, 1)
# Motor que sube y baja el brazo
TXT_VGR_E2_M2_encodermotor = txt_factory.motor_factory.create_encodermotor(TXT_VGR_E2, 2)
# Motor que estira y retrae el brazo para alcanzar la pieza
TXT_VGR_E2_M3_encodermotor = txt_factory.motor_factory.create_encodermotor(TXT_VGR_E2, 3)
# Cuenta el giro del brazo, para saber hacia que estacion esta orientado
TXT_VGR_E2_C1_motor_step_counter = txt_factory.counter_factory.create_encodermotor_counter(TXT_VGR_E2, 1)
TXT_VGR_E2_C1_motor_step_counter.set_motor(TXT_VGR_E2_M1_encodermotor)
# Cuenta la subida y bajada del brazo
TXT_VGR_E2_C2_motor_step_counter = txt_factory.counter_factory.create_encodermotor_counter(TXT_VGR_E2, 2)
TXT_VGR_E2_C2_motor_step_counter.set_motor(TXT_VGR_E2_M2_encodermotor)
# Cuenta cuanto se ha estirado o retraido el brazo
TXT_VGR_E2_C3_motor_step_counter = txt_factory.counter_factory.create_encodermotor_counter(TXT_VGR_E2, 3)
TXT_VGR_E2_C3_motor_step_counter.set_motor(TXT_VGR_E2_M3_encodermotor)
# Cuenta las piezas que salen por la estacion DPS
TXT_VGR_E2_C4_photo_transistor = txt_factory.counter_factory.create_photo_transistor_counter(TXT_VGR_E2, 4)

# TXT4.0 (3) - SLAVE (EXTENSION)
# Estacion de procesado (horno, MPO)
TXT_MPOov_E3 = txt_factory.controller_factory.create_graphical_controller(3)
# Interruptor que avisa cuando la pieza esta dentro del horno
TXT_MPOov_E3_I1_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_MPOov_E3, 1)
# Interruptor que avisa cuando la pieza vuelve a salir del horno
TXT_MPOov_E3_I2_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_MPOov_E3, 2)
# Interruptor que avisa cuando el brazo llega a la posicion del horno
TXT_MPOov_E3_I3_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_MPOov_E3, 3)
# Fototransistor que detecta la pieza en la entrada del horno
TXT_MPOov_E3_I5_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_MPOov_E3, 5)
# Valvula de vacio de la ventosa del brazo del horno
TXT_MPOov_E3_O5_magnetic_valve = txt_factory.output_factory.create_magnetic_valve(TXT_MPOov_E3, 5)
# Valvula que hace bajar el brazo del horno
TXT_MPOov_E3_O6_magnetic_valve = txt_factory.output_factory.create_magnetic_valve(TXT_MPOov_E3, 6)
# Valvula que sube la puerta del horno
TXT_MPOov_E3_O7_magnetic_valve = txt_factory.output_factory.create_magnetic_valve(TXT_MPOov_E3, 7)
# LED que simula el horno encendido (se enciende mientras "cuece" la pieza)
TXT_MPOov_E3_O8_led = txt_factory.output_factory.create_led(TXT_MPOov_E3, 8)
# Motor que estira o retrae la plataforma que mete la pieza en el horno
TXT_MPOov_E3_M1_motor = txt_factory.motor_factory.create_motor(TXT_MPOov_E3, 1)
# Motor que mueve el brazo del horno
TXT_MPOov_E3_M2_motor = txt_factory.motor_factory.create_motor(TXT_MPOov_E3, 2)

# TXT4.0 (4) - SLAVE (MASTER)
# Estacion de procesado 1 (mesa giratoria, sierra y cinta de salida)
TXT_MPOmi_E4 = txt_factory.controller_factory.create_graphical_controller(4)
# Interruptor que marca cuando la mesa giratoria queda frente al brazo
TXT_MPOmi_E4_I1_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_MPOmi_E4, 1)
# Interruptor que marca cuando la mesa giratoria queda frente a la fresa
TXT_MPOmi_E4_I2_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_MPOmi_E4, 2)
# Interruptor que marca cuando la mesa giratoria queda frente a la cinta
TXT_MPOmi_E4_I3_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_MPOmi_E4, 3)
# Fototransistor que detecta la pieza al final de la cinta
TXT_MPOmi_E4_I4_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_MPOmi_E4, 4)
# Interruptor que avisa de la posicion del brazo sobre la mesa giratoria
TXT_MPOmi_E4_I5_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_MPOmi_E4, 5)
# Cilindro neumatico que empuja la pieza fuera de la mesa giratoria
TXT_MPOmi_E4_O7_magnetic_valve = txt_factory.output_factory.create_magnetic_valve(TXT_MPOmi_E4, 7)
# Compresor que da presion de aire a la ventosa del brazo, al brazo del horno, a la puerta del horno y al cilindro de la mesa giratoria
TXT_MPOmi_E4_O8_compressor = txt_factory.output_factory.create_compressor(TXT_MPOmi_E4, 8)
# Motor que gira la mesa giratoria
TXT_MPOmi_E4_M1_motor = txt_factory.motor_factory.create_motor(TXT_MPOmi_E4, 1)
# Motor de la fresa que mecaniza la pieza
TXT_MPOmi_E4_M2_motor = txt_factory.motor_factory.create_motor(TXT_MPOmi_E4, 2)
# Motor de la cinta que saca la pieza de esta estacion
TXT_MPOmi_E4_M3_motor = txt_factory.motor_factory.create_motor(TXT_MPOmi_E4, 3)

# TXT4.0 (5) - SLAVE
# Cinta de clasificacion (SLD)
TXT_SLD_E5 = txt_factory.controller_factory.create_graphical_controller(5)
# Fototransistor que marca cuando la pieza llega al punto de lectura de color
TXT_SLD_E5_I1_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_SLD_E5, 1)
# Sensor que lee el color de la pieza (blanco, rojo o azul)
TXT_SLD_E5_I2_color_sensor = txt_factory.input_factory.create_color_sensor(TXT_SLD_E5, 2)
# Fototransistor situado junto a los cilindros de expulsion
TXT_SLD_E5_I3_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_SLD_E5, 3)
# Fototransistor del carril de piezas blancas
TXT_SLD_E5_I6_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_SLD_E5, 6)
# Fototransistor del carril de piezas rojas
TXT_SLD_E5_I7_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_SLD_E5, 7)
# Fototransistor del carril de piezas azules
TXT_SLD_E5_I8_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_SLD_E5, 8)
# Cilindro que empuja fuera de la cinta las piezas blancas
TXT_SLD_E5_O5_magnetic_valve = txt_factory.output_factory.create_magnetic_valve(TXT_SLD_E5, 5)
# Cilindro que empuja fuera de la cinta las piezas rojas
TXT_SLD_E5_O6_magnetic_valve = txt_factory.output_factory.create_magnetic_valve(TXT_SLD_E5, 6)
# Cilindro que empuja fuera de la cinta las piezas azules
TXT_SLD_E5_O7_magnetic_valve = txt_factory.output_factory.create_magnetic_valve(TXT_SLD_E5, 7)
# Compresor que da presion de aire a los tres cilindros de expulsion
TXT_SLD_E5_O8_compressor = txt_factory.output_factory.create_compressor(TXT_SLD_E5, 8)
# Motor de la cinta de clasificacion
TXT_SLD_E5_M1_encodermotor = txt_factory.motor_factory.create_encodermotor(TXT_SLD_E5, 1)
# Mini switch contador para detectar la posicion de la pieza en la cinta de clasificacion
TXT_SLD_E5_C1_mini_switch = txt_factory.counter_factory.create_mini_switch_counter(TXT_SLD_E5, 1)

# Congela la configuracion hardware, para que no se pueda modificar en otro lado del programa
txt_factory.initialized()