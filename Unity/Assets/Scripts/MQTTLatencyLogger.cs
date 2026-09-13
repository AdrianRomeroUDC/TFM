using System;
using System.IO;
using System.Collections;
using UnityEngine;

// Molde del mensaje "ping" que Unity envía a la fábrica: solo lleva la hora exacta de envío (t0).
[Serializable]
public class PingPayload
{
    public long t0;
}

/// <summary>
/// Este script mide la latencia (el retraso) de la comunicación MQTT entre Unity y la fábrica real,
/// y guarda los resultados en un archivo CSV para poder analizarlos después (por ejemplo, en el TFM).
/// Funciona con el clásico sistema de "ping-pong": Unity envía un ping con la hora exacta de salida
/// (a través de <see cref="MQTTClient.PublishPing"/>), la fábrica (o el puente MQTT) lo reenvía como
/// "pong" por el topic dt/pong, y cuando <see cref="MQTTClient"/> recibe ese pong llama a
/// <see cref="ProcesarPong"/> aquí para calcular cuánto ha tardado el viaje de ida y vuelta.
/// </summary>
public class MQTTLatencyLogger : MonoBehaviour
{
    // Patrón Singleton: accesible desde cualquier script como MQTTLatencyLogger.Instance.
    public static MQTTLatencyLogger Instance { get; private set; }

    [Header("Opciones de Registro")]
    [Tooltip("Activa o desactiva la medición y el guardado de datos de latencia")]
    public bool guardarLatencia = false;

    [Tooltip("Nombre del archivo CSV")]
    public string nombreArchivoCSV = "MQTT_Latency_Data.csv";

    [Tooltip("Por defecto 'Descargas'")]
    public string rutaArchivoCSV = "";

    [Header("Configuración Ping-Pong")]
    [Tooltip("Intervalo en segundos entre cada envío de Ping desde Unity")]
    public float intervaloPingSec = 1.0f;

    private string filePath;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        PrepararRutaArchivo();
        // Lanzamos la rutina que envía pings periódicamente mientras la escena esté activa.
        StartCoroutine(RutinaPingPong());
    }

    /// <summary>
    /// Calcula dónde se va a guardar el CSV de latencia (por defecto, en la carpeta "Descargas"
    /// del usuario) y crea el archivo con la cabecera de columnas si todavía no existe.
    /// </summary>
    public void PrepararRutaArchivo()
    {
        string carpeta;

        // Detección automática y limpia de la carpeta Descargas del usuario activo
        if (string.IsNullOrWhiteSpace(rutaArchivoCSV))
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string downloads = !string.IsNullOrEmpty(userProfile) ? Path.Combine(userProfile, "Downloads") : "";

            if (!string.IsNullOrEmpty(downloads) && Directory.Exists(downloads))
            {
                carpeta = downloads;
            }
            else
            {
                // Si no encontramos la carpeta Descargas, usamos la carpeta del proyecto como alternativa segura.
                carpeta = Application.dataPath;
            }
        }
        else
        {
            carpeta = rutaArchivoCSV.Trim();
        }

        if (!Directory.Exists(carpeta))
        {
            try
            {
                Directory.CreateDirectory(carpeta);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MQTT Latency Logger] No se pudo crear la carpeta indicada: {ex.Message}. Se guardará en Application.dataPath");
                carpeta = Application.dataPath;
            }
        }

        filePath = Path.Combine(carpeta, nombreArchivoCSV);

        if (guardarLatencia && !File.Exists(filePath))
        {
            try
            {
                // Cabecera del CSV: cada fila registrada más adelante tendrá estas mismas columnas.
                File.WriteAllText(filePath, "Topic,t0_Send_ms,t1_Recv_ms,t2_Exec_ms,Latency_MQTT_ms,Delay_Unity_ms,Latency_Total_ms\n");
                Debug.Log($"<color=cyan><b>[MQTT Latency Logger] Archivo CSV iniciado en:</b> {filePath}</color>");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MQTT Latency Logger] Error al crear el archivo CSV: {ex.Message}");
            }
        }
    }

    // Corrutina que, mientras dure la escena, envía un ping cada "intervaloPingSec" segundos
    // (solo si la medición está activada y hay conexión MQTT real con la fábrica).
    private IEnumerator RutinaPingPong()
    {
        while (true)
        {
            if (guardarLatencia && MQTTClient.Instance != null && MQTTClient.Instance.IsConnected)
            {
                // Guardamos la hora exacta de envío (t0) dentro del propio mensaje, para poder
                // compararla más tarde con la hora de recepción del pong.
                long t0_Send = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                PingPayload payload = new PingPayload { t0 = t0_Send };
                string jsonStr = JsonUtility.ToJson(payload);

                MQTTClient.Instance.PublishPing(jsonStr);
            }
            yield return new WaitForSeconds(intervaloPingSec);
        }
    }

    /// <summary>
    /// Se llama desde <see cref="MQTTClient"/> cuando llega la respuesta "pong" de la fábrica.
    /// Recupera la hora de envío original (t0) del propio mensaje y lanza el cálculo final de latencia.
    /// </summary>
    /// <param name="jsonPayload">Contenido del pong, que en realidad es el mismo ping que enviamos, devuelto tal cual.</param>
    /// <param name="t1_Recv">Instante exacto (en milisegundos) en el que MQTTClient recibió el pong.</param>
    public void ProcesarPong(string jsonPayload, long t1_Recv)
    {
        if (!guardarLatencia) return;

        try
        {
            PingPayload data = JsonUtility.FromJson<PingPayload>(jsonPayload);
            if (data != null && data.t0 > 0)
            {
                StartCoroutine(RegistrarFrameFinal(data.t0, t1_Recv));
            }
        }
        catch { }
    }

    // Espera al final del frame actual (para incluir el tiempo que tarda Unity en terminar de
    // procesar ese frame) antes de calcular y guardar la latencia total en el CSV.
    private IEnumerator RegistrarFrameFinal(long t0, long t1)
    {
        yield return new WaitForEndOfFrame();

        if (!guardarLatencia) yield break;

        long t2_Exec = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // rtt = tiempo total de ida y vuelta del mensaje (Round Trip Time).
        // Se divide entre 2 para estimar la latencia de un solo trayecto (ida O vuelta, no las dos).
        long rtt = t1 - t0;
        long latenciaMqttUnidireccional = rtt / 2;
        // Tiempo extra que tarda Unity en terminar de procesar el mensaje dentro del motor (retardo interno).
        long retardoUnity = t2_Exec - t1;
        long latenciaTotal = latenciaMqttUnidireccional + retardoUnity;

        // Descartamos medidas absurdas (tiempo negativo o más de 10 segundos), que normalmente
        // indican un problema puntual de red y ensuciarían la estadística.
        if (rtt >= 0 && rtt < 10000)
        {
            try
            {
                string linea = $"dt/ping_pong,{t0},{t1},{t2_Exec},{latenciaMqttUnidireccional},{retardoUnity},{latenciaTotal}\n";
                File.AppendAllText(filePath, linea);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MQTT Latency Logger] Error al escribir en el CSV: {ex.Message}");
            }
        }
    }
}
