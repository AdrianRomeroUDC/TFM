# Desarrollo de un Gemelo Digital de una Estación de Manufactura

> **Trabajo Fin de Máster (TFM)**  
> **Máster Universitario en Informática Industrial y Robótica** — *Universidade da Coruña*  
> **Autor:** Adrián Romero Lustres

---

## 🏭 Planta Física y Gemelo Digital

<p align="center">
  <img width="914" alt="FabricaReal-GemeloDigital" src="https://github.com/user-attachments/assets/53d177aa-2018-4187-8f87-4e44a783c140" />
</p>

---

## 🎬 Vídeos de Funcionamiento

### 🔴 1. Modo En Vivo
Sincronización en tiempo real mediante telemetría MQTT con la planta física real.

<!-- Pega aquí el vídeo del modo En Vivo -->


### 🟡 2. Modo Reproducción de Históricos
Descarga y reproducción de datos almacenados en la base de datos InfluxDB.

<!-- Pega aquí el vídeo del modo Histórico -->


### 🟢 3. Modo Simulación
Ejecución offline y prueba de secuencias operativas sin requerir la planta física.

<!-- Pega aquí el vídeo del modo Simulación -->


---

> [!NOTE]
> **Nota sobre la latencia en las grabaciones:**  
> La ligera latencia observable en las grabaciones comparativas entre la planta física y el Gemelo Digital se debe a que la ejecución se realizó directamente desde el entorno de desarrollo de Unity (*Editor Play Mode*), el cual introduce un *overhead* de procesamiento por la depuración en tiempo real y renderizado de la interfaz. En la versión ejecutable final del sistema (*Standalone Build*), compilada sin la carga del entorno de desarrollo, dicha latencia no existe y la sincronización por MQTT se ejecuta en tiempo real estricto.
