# Gemelo Digital — Learning Factory 4.0 (TFM)

Proyecto Unity que implementa el **Gemelo Digital** de una planta industrial automatizada real (Fischertechnik Learning Factory 4.0). Unity se conecta a la fábrica física a través de **MQTT** y consulta/reproduce datos históricos almacenados en **InfluxDB**.

## Estaciones simuladas

- **HBW** (High-Bay Warehouse): almacén automatizado que guarda y saca piezas en estantes.
- **VGR** (Vacuum Gripper Robot): robot de brazo con ventosa que mueve piezas entre estaciones.
- **DPS** (Deposit & Processing Station): estación de entrada y salida de piezas.
- **MPO** (Multi-Processing Oven): estación de horneado y fresado con cinta transportadora y plato giratorio.
- **SLD** (Sorting Line with Detection): cinta clasificadora que detecta el color de la pieza (blanca, roja, azul) y la empuja con pistones.
- **SSC** (Sensor Station Camera): estación de supervisión con cámara Pan-Tilt y sensores ambientales (BME680, LDR).

## Estructura del código (`Assets/Scripts/`)

- `MovimientoDelGemelo/`: cliente MQTT central (`MQTTClient.cs`), cliente de InfluxDB (`InfluxDBClient.cs`) y los controladores/proxies que mueven cada estación del gemelo digital en 3D.
- `Interfaz/`: paneles de la interfaz de usuario (menú, calendario de históricos, monitor de sensores, control de stock, etc.) y el cliente MQTT específico de la interfaz (`MQTT_InterfaceClient.cs`).
- `Simulacion/`: generación y reproducción de simulaciones offline a partir de históricos exportados de InfluxDB.
- `Trazabilidad/`: herramientas de depuración para seguir el rastro de una pieza durante el desarrollo.

## Modos de funcionamiento

1. **En vivo (MQTT directo)**: Unity se conecta al broker MQTT de la fábrica real y refleja su estado en tiempo real.
2. **Histórico (Base de Datos)**: se descarga un rango de fechas desde InfluxDB y se reproduce en el gemelo digital respetando los tiempos reales.
3. **Simulación offline**: se reproducen secuencias de eventos pregrabadas sin necesitar conexión real a la fábrica ni a InfluxDB.

## Requisitos

- Unity (ver `ProjectSettings/ProjectVersion.txt` para la versión exacta usada).
- Conexión de red para el modo en vivo (broker MQTT en HiveMQ Cloud) y el modo histórico (InfluxDB Cloud).

## Notas

Las credenciales de conexión (broker MQTT e InfluxDB) están definidas como valores por defecto en `MQTTClient.cs` e `InfluxDBClient.cs`. En un entorno de producción real deberían gestionarse fuera del código fuente (variables de entorno, gestor de secretos, etc.).
