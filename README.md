# Desarrollo de un Gemelo Digital de una Estación de Manufactura

> **Trabajo Fin de Máster (TFM)**  
> **Máster Universitario en Informática Industrial y Robótica** — *Universidade da Coruña*  
> **Autor:** Adrián Romero Lustres

---

## Planta Física y Gemelo Digital

<p align="center">
  <img width="620" alt="PortadaGithub" src="https://github.com/user-attachments/assets/d41f1048-31ca-4344-96ab-6352cfc3fafe" />
</p>

---

## Vídeos de Funcionamiento

> [!TIP]
> **¿Cómo ver los vídeos?** Haz clic sobre cualquiera de las tarjetas o miniaturas para abrir la reproducción completa en YouTube.

### 🔴 1. Modo En Vivo
Sincronización en tiempo real mediante telemetría MQTT con la planta física real.

[![Modo En Vivo](https://ytcards.demolab.com/?id=QrAoBLRd5Wo&lang=es)](https://www.youtube.com/watch?v=QrAoBLRd5Wo)

---

### 🟡 2. Modo Reproducción de Históricos
Descarga y reproducción de datos almacenados en la base de datos InfluxDB.

[![Modo Reproducción de Históricos](https://ytcards.demolab.com/?id=y0397x4YoD0&lang=es)](https://www.youtube.com/watch?v=y0397x4YoD0)

---

### 🟢 3. Modo Simulación
Ejecución offline y prueba de secuencias operativas sin requerir la planta física.

[![Modo Simulación](https://ytcards.demolab.com/?id=MANPuxbMLLU&lang=es)](https://www.youtube.com/watch?v=MANPuxbMLLU)

---

## ⚙️ Configuración

Por seguridad, este repositorio no incluye ninguna credencial real de conexión (broker MQTT, InfluxDB Cloud). Antes de ejecutar el proyecto hay que rellenar los siguientes valores, marcados como `ChangeMe` en el código:

### Controlador TXT (ROBO Pro Coding)

Abre `ROBO Pro Coding/FactoryMain-DigitalTwin.ft` con el software ROBO Pro Coding y, en su editor de código, rellena:

- **`lib/MQTT.py`** (función `connectLocal()`): host, puerto, usuario y contraseña del broker MQTT local.
- **`lib/Influx_Collector.py`**: los mismos datos del broker MQTT, además de `INFLUX_BASE_URL`, `INFLUX_ORG`, `INFLUX_BUCKET` y `INFLUX_TOKEN` de tu instancia de InfluxDB Cloud.

### Lado Unity

- **`MQTTClient`** (componente en la escena `GemeloDigital_LearningFactory`): rellenar en el Inspector los campos `Broker Host`, `Puerto`, `Usuario` y `Contrasena`.
- **`InfluxDBClient`** (componente en la misma escena): rellenar en el Inspector los campos `Server Url`, `Token`, `Org` y `Bucket`.

---

*Nota: la carpeta `vscode/` contiene una copia del código Python pensada por si en el futuro se programa desde ese entorno (VS Code). El código que realmente se ejecuta en el controlador es el empaquetado en `ROBO Pro Coding/FactoryMain-DigitalTwin.ft`.*

