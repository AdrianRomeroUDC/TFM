import fischertechnik.factories as txt_factory

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
# Final de carrera rotacion
TXT_SSC_M_I1_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_SSC_M, 1)   
# Final de carrera subir/bajar
TXT_SSC_M_I2_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_SSC_M, 2)   
# LDR (deteccion de luz ambiente)
TXT_SSC_M_I3_photo_resistor = txt_factory.input_factory.create_photo_resistor(TXT_SSC_M, 3) 
# FIXME: en I8 no hay nada conectado en la planta real
TXT_SSC_M_I8_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_SSC_M, 8)   
# LED rojo estado online
TXT_SSC_M_O5_led = txt_factory.output_factory.create_led(TXT_SSC_M, 5)
# LED rojo
TXT_SSC_M_O6_led = txt_factory.output_factory.create_led(TXT_SSC_M, 6)
# LED amarillo
TXT_SSC_M_O7_led = txt_factory.output_factory.create_led(TXT_SSC_M, 7)
# LED verde
TXT_SSC_M_O8_led = txt_factory.output_factory.create_led(TXT_SSC_M, 8)
# M1 motor rotar
TXT_SSC_M_M1_encodermotor = txt_factory.motor_factory.create_encodermotor(TXT_SSC_M, 1)
# M2 motor subir/bajar
TXT_SSC_M_M2_encodermotor = txt_factory.motor_factory.create_encodermotor(TXT_SSC_M, 2)
# Enconder rotacion
TXT_SSC_M_C1_motor_step_counter = txt_factory.counter_factory.create_encodermotor_counter(TXT_SSC_M, 1)
TXT_SSC_M_C1_motor_step_counter.set_motor(TXT_SSC_M_M1_encodermotor)
# Enconder subir/bajar
TXT_SSC_M_C2_motor_step_counter = txt_factory.counter_factory.create_encodermotor_counter(TXT_SSC_M, 2)
TXT_SSC_M_C2_motor_step_counter.set_motor(TXT_SSC_M_M2_encodermotor)
# Sensor ambiental I2C
TXT_SSC_M_I2C_1_environment_sensor = txt_factory.i2c_factory.create_environment_sensor(TXT_SSC_M, 1)
# Camara USB
TXT_SSC_M_USB1_1_camera = txt_factory.usb_factory.create_camera(TXT_SSC_M, 1)

# TXT4.0 (1) - SLAVE
# Almacen
TXT_HBW_E1 = txt_factory.controller_factory.create_graphical_controller(1)
# Fototransistor fuera del almacen
TXT_HBW_E1_I1_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_HBW_E1, 1)
# Fototransistor dentro del almacen
TXT_HBW_E1_I4_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_HBW_E1, 4)
# Final de carrera horizontal brazo almacen coger pieza
TXT_HBW_E1_I5_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_HBW_E1, 5)
# Final de carrera brazo retraerse
TXT_HBW_E1_I6_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_HBW_E1, 6)
# Final de carrera brazo extenderse
TXT_HBW_E1_I7_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_HBW_E1, 7)
# Final de carrera vertical brazo almacen subir
TXT_HBW_E1_I8_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_HBW_E1, 8)
# Motor cinta de entrada/salida del almacen
TXT_HBW_E1_M1_motor = txt_factory.motor_factory.create_motor(TXT_HBW_E1, 1)
# Motor con encoder del eje horizontal del brazo del almacen
TXT_HBW_E1_M2_encodermotor = txt_factory.motor_factory.create_encodermotor(TXT_HBW_E1, 2) 
# Motor estirar/retraer brazo del almacen
TXT_HBW_E1_M3_motor = txt_factory.motor_factory.create_motor(TXT_HBW_E1, 3)
# Motor con encoder del eje vertical del brazo del almacen
TXT_HBW_E1_M4_encodermotor = txt_factory.motor_factory.create_encodermotor(TXT_HBW_E1, 4)
# C2 - Encoder para el motor 2 del eje horizontal
TXT_HBW_E1_C2_motor_step_counter = txt_factory.counter_factory.create_encodermotor_counter(TXT_HBW_E1, 2)
TXT_HBW_E1_C2_motor_step_counter.set_motor(TXT_HBW_E1_M2_encodermotor)
# C4 - Encoder para el motor 4 del eje vertical
TXT_HBW_E1_C4_motor_step_counter = txt_factory.counter_factory.create_encodermotor_counter(TXT_HBW_E1, 4)
TXT_HBW_E1_C4_motor_step_counter.set_motor(TXT_HBW_E1_M4_encodermotor)

# TXT4.0 (2) - SLAVE
# Brazo robotizado central
TXT_VGR_E2 = txt_factory.controller_factory.create_graphical_controller(2)
# Final de carrera rotación brazo
TXT_VGR_E2_I1_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_VGR_E2, 1)
# Final de carrera subir brazo
TXT_VGR_E2_I2_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_VGR_E2, 2)
# Final de carrera retraer brazo
TXT_VGR_E2_I3_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_VGR_E2, 3)
# Fototransistor pieza salida DPS
TXT_VGR_E2_I7_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_VGR_E2, 7)
# Sensor de color pieza entrada DPS
TXT_VGR_E2_I8_color_sensor = txt_factory.input_factory.create_color_sensor(TXT_VGR_E2, 8)
# Compresor ventosa
TXT_VGR_E2_O7_compressor = txt_factory.output_factory.create_compressor(TXT_VGR_E2, 7)
# Valvula magnetica ventosa
TXT_VGR_E2_O8_magnetic_valve = txt_factory.output_factory.create_magnetic_valve(TXT_VGR_E2, 8)
# Motor con encoder rotación brazo
TXT_VGR_E2_M1_encodermotor = txt_factory.motor_factory.create_encodermotor(TXT_VGR_E2, 1)
# Motor con encoder subir/bajar brazo
TXT_VGR_E2_M2_encodermotor = txt_factory.motor_factory.create_encodermotor(TXT_VGR_E2, 2)
# Motor estirar/retraer brazo
TXT_VGR_E2_M3_encodermotor = txt_factory.motor_factory.create_encodermotor(TXT_VGR_E2, 3)
# C1 - Encoder para el motor 1 del eje rotación
TXT_VGR_E2_C1_motor_step_counter = txt_factory.counter_factory.create_encodermotor_counter(TXT_VGR_E2, 1)
TXT_VGR_E2_C1_motor_step_counter.set_motor(TXT_VGR_E2_M1_encodermotor)
# C2 - Encoder para el motor 2 del eje subir/bajar
TXT_VGR_E2_C2_motor_step_counter = txt_factory.counter_factory.create_encodermotor_counter(TXT_VGR_E2, 2)
TXT_VGR_E2_C2_motor_step_counter.set_motor(TXT_VGR_E2_M2_encodermotor)
# C3 - Encoder para el motor 3 del eje estirar/retraer
TXT_VGR_E2_C3_motor_step_counter = txt_factory.counter_factory.create_encodermotor_counter(TXT_VGR_E2, 3)
TXT_VGR_E2_C3_motor_step_counter.set_motor(TXT_VGR_E2_M3_encodermotor)
# Fotrotransistor pieza salida DPS
TXT_VGR_E2_C4_photo_transistor = txt_factory.counter_factory.create_photo_transistor_counter(TXT_VGR_E2, 4)

# TXT4.0 (3) - SLAVE (EXTENSION)
# Estacion de procesado
TXT_MPOov_E3 = txt_factory.controller_factory.create_graphical_controller(3)
# Final de carrera pieza horno dentro
TXT_MPOov_E3_I1_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_MPOov_E3, 1)
# Final de carrera pieza horno fuera
TXT_MPOov_E3_I2_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_MPOov_E3, 2)
# Final de carrera brazo posicion horno
TXT_MPOov_E3_I3_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_MPOov_E3, 3)
# Fototransistor (deteccion de piezas entrada horno)
TXT_MPOov_E3_I5_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_MPOov_E3, 5)
# Valvula vacio ventosa brazo
TXT_MPOov_E3_O5_magnetic_valve = txt_factory.output_factory.create_magnetic_valve(TXT_MPOov_E3, 5)
# Valvula bajar brazo
TXT_MPOov_E3_O6_magnetic_valve = txt_factory.output_factory.create_magnetic_valve(TXT_MPOov_E3, 6)
# Valvula subir puerta horno
TXT_MPOov_E3_O7_magnetic_valve = txt_factory.output_factory.create_magnetic_valve(TXT_MPOov_E3, 7)
# LED que simula el horno (enciende cuando el horno esta encendido)
TXT_MPOov_E3_O8_led = txt_factory.output_factory.create_led(TXT_MPOov_E3, 8)
# Motor plataforma pieza horno estirar/retraer
TXT_MPOov_E3_M1_motor = txt_factory.motor_factory.create_motor(TXT_MPOov_E3, 1)
# Motor mover brazo
TXT_MPOov_E3_M2_motor = txt_factory.motor_factory.create_motor(TXT_MPOov_E3, 2)

# TXT4.0 (4) - SLAVE (MASTER)
# Estacion de procesado 1
TXT_MPOmi_E4 = txt_factory.controller_factory.create_graphical_controller(4)
# Final de carrera posicion mesa giratoria - brazo
TXT_MPOmi_E4_I1_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_MPOmi_E4, 1)
# Final de carrera posicion mesa giratorio - fresa
TXT_MPOmi_E4_I2_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_MPOmi_E4, 2)
# Final de carrera posicion mesa giratoria - cinta
TXT_MPOmi_E4_I3_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_MPOmi_E4, 3)
# Fototransistor final de la cinta
TXT_MPOmi_E4_I4_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_MPOmi_E4, 4)
# Final de carrera posicion del brazo en la mesa giratoria
TXT_MPOmi_E4_I5_mini_switch = txt_factory.input_factory.create_mini_switch(TXT_MPOmi_E4, 5)
# Cilindro mesa giratoria
TXT_MPOmi_E4_O7_magnetic_valve = txt_factory.output_factory.create_magnetic_valve(TXT_MPOmi_E4, 7)
# Compresor ventosa brazo, bajar brazo, subir puerta horno, cilindro mesa giratoria
TXT_MPOmi_E4_O8_compressor = txt_factory.output_factory.create_compressor(TXT_MPOmi_E4, 8)
# Motor mesa giratoria
TXT_MPOmi_E4_M1_motor = txt_factory.motor_factory.create_motor(TXT_MPOmi_E4, 1)
# Motor fresa
TXT_MPOmi_E4_M2_motor = txt_factory.motor_factory.create_motor(TXT_MPOmi_E4, 2)
# Motor cinta
TXT_MPOmi_E4_M3_motor = txt_factory.motor_factory.create_motor(TXT_MPOmi_E4, 3)

# TXT4.0 (5) - SLAVE
# Cinta de clasificacion
TXT_SLD_E5 = txt_factory.controller_factory.create_graphical_controller(5)
# Fototransistor pieza posicion deteccion color
TXT_SLD_E5_I1_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_SLD_E5, 1)
# Sensor de color pieza
TXT_SLD_E5_I2_color_sensor = txt_factory.input_factory.create_color_sensor(TXT_SLD_E5, 2)
# Fototransitor cilindros de clasificacion
TXT_SLD_E5_I3_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_SLD_E5, 3)
# Fototransistor blanco
TXT_SLD_E5_I6_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_SLD_E5, 6)
# Fototransistor rojo
TXT_SLD_E5_I7_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_SLD_E5, 7)
# Fototransistor azul
TXT_SLD_E5_I8_photo_transistor = txt_factory.input_factory.create_photo_transistor(TXT_SLD_E5, 8)
# Cilindro blanco
TXT_SLD_E5_O5_magnetic_valve = txt_factory.output_factory.create_magnetic_valve(TXT_SLD_E5, 5)
# Cilindro rojo
TXT_SLD_E5_O6_magnetic_valve = txt_factory.output_factory.create_magnetic_valve(TXT_SLD_E5, 6)
# Cilindro azul
TXT_SLD_E5_O7_magnetic_valve = txt_factory.output_factory.create_magnetic_valve(TXT_SLD_E5, 7)
# Compresor
TXT_SLD_E5_O8_compressor = txt_factory.output_factory.create_compressor(TXT_SLD_E5, 8)
# Motor cinta SLD
TXT_SLD_E5_M1_encodermotor = txt_factory.motor_factory.create_encodermotor(TXT_SLD_E5, 1)
# Mini swith contador para detectar la posicion de la pieza en la cinta de clasificacion
TXT_SLD_E5_C1_mini_switch = txt_factory.counter_factory.create_mini_switch_counter(TXT_SLD_E5, 1)

# Congela la configuracion hardware, para que no se pueda modificar en otro lado del programa
txt_factory.initialized()