using UnityEngine;
using System;
using System.IO;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;

// Esta clase es simplemente "una ficha de configuración" que rellenas desde el Inspector de Unity:
// para cada color de pieza, indicas qué franja de tiempo real quieres descargar de InfluxDB
// (la base de datos donde queda guardado el histórico de la fábrica) y con qué nombre de
// archivo se guardará el resultado.
[Serializable]
public class RangoFechaPieza
{
    public string tipoPieza = "WHITE";
    [Tooltip("Formato obligatorio: dd/MM/yyyy HH:mm:ss (Escribe la hora LOCAL de tu PC, ej: 27/07/2026 14:45:25)")]
    public string fechaHoraInicio = "27/07/2026 14:45:25";
    [Tooltip("Formato obligatorio: dd/MM/yyyy HH:mm:ss (Escribe la hora LOCAL de tu PC, ej: 27/07/2026 14:47:45)")]
    public string fechaHoraFin = "27/07/2026 14:47:45";
    public string nombreArchivoJson = "datosPiezaBlanca.json";
}

/// <summary>
/// Herramienta de "grabación": va a InfluxDB (el histórico de la fábrica real), descarga todos
/// los mensajes MQTT que se registraron durante un instante en el que una pieza concreta (blanca,
/// roja o azul) recorrió la planta, y los guarda en un archivo JSON dentro del proyecto.
/// Ese archivo JSON es justo lo que luego lee <see cref="SimuladorOffline"/> para poder reproducir
/// esa misma secuencia sin necesidad de tener la fábrica física encendida ni conexión a InfluxDB:
/// es como grabar un vídeo de la fábrica trabajando para poder verlo luego offline.
/// Todo se dispara a mano desde el menú contextual del componente en el Inspector (clic derecho),
/// no ocurre automáticamente mientras juegas.
/// </summary>
public class GenerarJSONSimulacion : MonoBehaviour
{
    [Header("--- Carpeta de Guardado ---")]
    public string carpetaGuardado = "Assets/DatosSimulacion";

    // Configuración por defecto de cada pieza: en qué ventana de tiempo (hora de tu PC) se movió
    // esa pieza por la fábrica real, y en qué archivo se guardará su "grabación".
    [Header("--- Configuración Rango de Fechas por Pieza (Hora Local PC) ---")]
    public RangoFechaPieza piezaBlanca = new RangoFechaPieza
    {
        tipoPieza = "WHITE",
        fechaHoraInicio = "27/07/2026 14:45:25",
        fechaHoraFin = "27/07/2026 14:47:45",
        nombreArchivoJson = "datosPiezaBlanca.json"
    };

    public RangoFechaPieza piezaRoja = new RangoFechaPieza
    {
        tipoPieza = "RED",
        fechaHoraInicio = "29/07/2026 12:35:05",
        fechaHoraFin = "29/07/2026 12:37:20",
        nombreArchivoJson = "datosPiezaRoja.json"
    };

    public RangoFechaPieza piezaAzul = new RangoFechaPieza
    {
        tipoPieza = "BLUE",
        fechaHoraInicio = "29/07/2026 14:26:40",
        fechaHoraFin = "29/07/2026 14:29:05",
        nombreArchivoJson = "datosPiezaAzul.json"
    };

    // =========================================================================
    // MENÚ CONTEXTUAL (Haz clic derecho sobre este componente en el Inspector)
    // =========================================================================

    // Genera los 3 archivos JSON (blanca, roja y azul) uno detrás de otro.
    [ContextMenu("▶ 1. Generar TODOS los JSONs desde InfluxDB")]
    public void GenerarTodosLosArchivos()
    {
        StartCoroutine(ProcesarTodos());
    }

    // Genera solo el JSON de la pieza blanca, usando la configuración de "piezaBlanca".
    [ContextMenu("▶ 2. Generar Solo Pieza BLANCA")]
    public void GenerarJSONBlanca() => StartCoroutine(ProcesarYGuardarPieza(piezaBlanca));

    // Genera solo el JSON de la pieza roja, usando la configuración de "piezaRoja".
    [ContextMenu("▶ 3. Generar Solo Pieza ROJA")]
    public void GenerarJSONRoja() => StartCoroutine(ProcesarYGuardarPieza(piezaRoja));

    // Genera solo el JSON de la pieza azul, usando la configuración de "piezaAzul".
    [ContextMenu("▶ 4. Generar Solo Pieza AZUL")]
    public void GenerarJSONAzul() => StartCoroutine(ProcesarYGuardarPieza(piezaAzul));

    // Corrutina "envoltorio" que simplemente lanza la descarga de las 3 piezas, una después de
    // otra (espera a que termine cada una antes de empezar la siguiente).
    private IEnumerator ProcesarTodos()
    {
        yield return StartCoroutine(ProcesarYGuardarPieza(piezaBlanca));
        yield return StartCoroutine(ProcesarYGuardarPieza(piezaRoja));
        yield return StartCoroutine(ProcesarYGuardarPieza(piezaAzul));
        Debug.Log("<color=green><b>🎉 ¡Proceso finalizado! Todos los JSONs fueron descargados y guardados.</b></color>");
    }

    /// <summary>
    /// Descarga de InfluxDB el histórico de mensajes MQTT correspondiente a la ventana de tiempo
    /// indicada en <paramref name="config"/>, y lo vuelca a un archivo JSON en disco. No reproduce
    /// nada en la escena de Unity: solo genera el archivo para poder usarlo más tarde en modo offline.
    /// </summary>
    /// <param name="config">Qué pieza, qué rango de fechas y en qué archivo guardar el resultado.</param>
    private IEnumerator ProcesarYGuardarPieza(RangoFechaPieza config)
    {
        // 🟢 Interpreta la hora introducida como Hora Local de tu PC y la convierte a UTC para InfluxDB
        DateTime inicioUTC = ParsearFecha(config.fechaHoraInicio);
        DateTime finUTC = ParsearFecha(config.fechaHoraFin);

        // Si el texto de alguna de las dos fechas no tiene el formato correcto, avisamos y abortamos.
        if (inicioUTC == DateTime.MinValue || finUTC == DateTime.MinValue)
        {
            Debug.LogError($"❌ Error en formato de fecha para [{config.tipoPieza}]. Usa 'dd/MM/yyyy HH:mm:ss'");
            yield break;
        }

        // Buscamos el cliente de InfluxDB que ya existe en la escena (o cualquier otro si no hay
        // ninguno marcado como "el" singleton). Sin él no podemos consultar el histórico.
        InfluxDBClient influxClient = InfluxDBClient.Instance != null ? InfluxDBClient.Instance : FindFirstObjectByType<InfluxDBClient>();
        if (influxClient == null)
        {
            Debug.LogError("❌ InfluxDBClient no está activo en la escena.");
            yield break;
        }

        Debug.Log($"<color=yellow>⏳ Descargando InfluxDB para [{config.tipoPieza}]\n• Hora PC introducida: {config.fechaHoraInicio} a {config.fechaHoraFin}\n• Consulta convertida a UTC: {inicioUTC:dd/MM/yyyy HH:mm:ss} a {finUTC:dd/MM/yyyy HH:mm:ss}</color>");

        // Aquí iremos guardando los eventos ya convertidos al formato que usa SimuladorOffline.
        SecuenciaPiezaData secuenciaData = new SecuenciaPiezaData { tipoPieza = config.tipoPieza };
        bool descargaCompletada = false;
        List<InfluxDBClient.RegistroInflux> eventosObtenidos = null;

        // Pedimos a InfluxDBClient que descargue todos los mensajes MQTT guardados en ese rango de
        // fechas, PERO sin reproducirlos en la escena (por eso "SinReproducir"): solo queremos los
        // datos en bruto para guardarlos en un archivo. Cuando termine, nos avisa con este callback.
        yield return StartCoroutine(
            influxClient.DescargarHistoricoSinReproducir(
                inicioUTC,
                finUTC,
                (registros) =>
                {
                    eventosObtenidos = registros;
                    descargaCompletada = true;
                }
            )
        );

        // Esperamos, fotograma a fotograma, a que la descarga anterior termine de verdad.
        while (!descargaCompletada)
        {
            yield return null;
        }

        if (eventosObtenidos != null && eventosObtenidos.Count > 0)
        {
            // Ordenar eventos por fecha
            eventosObtenidos.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));
            // Tomamos el instante del primer evento como "segundo 0" de la grabación.
            DateTime tiempoBase = eventosObtenidos[0].timestamp.ToUniversalTime();

            foreach (var reg in eventosObtenidos)
            {
                // Convertimos cada fecha absoluta en "segundos transcurridos desde el primer evento".
                // Así, al reproducir luego, no importa qué día ni hora sea: solo importa el ritmo
                // relativo entre un mensaje y el siguiente.
                float segundosRelativos = (float)(reg.timestamp.ToUniversalTime() - tiempoBase).TotalSeconds;

                secuenciaData.eventos.Add(new EventoHistoricoPieza
                {
                    tiempoRelativoSegundos = (float)Math.Round(segundosRelativos, 3),
                    topic = reg.topic,
                    payloadJson = reg.payloadJson
                });
            }
        }

        // Nos aseguramos de que la carpeta de destino existe antes de intentar escribir en ella.
        if (!Directory.Exists(carpetaGuardado))
        {
            Directory.CreateDirectory(carpetaGuardado);
        }

        // Convertimos la lista de eventos a texto JSON y la escribimos en el archivo final.
        string rutaCompleta = Path.Combine(carpetaGuardado, config.nombreArchivoJson);
        string jsonTexto = JsonUtility.ToJson(secuenciaData, true);

        File.WriteAllText(rutaCompleta, jsonTexto);

        Debug.Log($"<color=green>✅ Archivo guardado: <b>{rutaCompleta}</b> ({secuenciaData.eventos.Count} eventos guardados)</color>");

#if UNITY_EDITOR
        // Refrescamos la ventana "Project" del editor para que el nuevo archivo aparezca al momento.
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    /// <summary>
    /// Interpreta la fecha introducida en el Inspector como HORA LOCAL de tu PC
    /// y la convierte a UTC para consultar a InfluxDB.
    /// </summary>
    private DateTime ParsearFecha(string fechaTexto)
    {
        // Formatos de fecha que aceptamos como válidos escritos en el Inspector.
        string[] formatos = { "dd/MM/yyyy HH:mm:ss", "dd-MM-yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss" };

        // 1. Interpretar la fecha escrita como Hora Local del sistema operativo (AssumeLocal)
        if (DateTime.TryParseExact(fechaTexto.Trim(), formatos, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime dtLocal))
        {
            // 2. Convertir automáticamente a UTC para la consulta de InfluxDB
            return dtLocal.ToUniversalTime();
        }

        // Si ninguno de los formatos exactos encajó, probamos con un parseo más flexible antes de rendirnos.
        if (DateTime.TryParse(fechaTexto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dtLocal))
        {
            return dtLocal.ToUniversalTime();
        }

        // Si nada funcionó, devolvemos un valor "centinela" que el llamador interpreta como fecha inválida.
        return DateTime.MinValue;
    }
}
