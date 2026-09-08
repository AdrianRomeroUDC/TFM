using System;
using System.IO;
using System.Collections;
using UnityEngine;

[Serializable]
public class PingPayload
{
    public long t0;
}

public class MQTTLatencyLogger : MonoBehaviour
{
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
        StartCoroutine(RutinaPingPong());
    }

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
                File.WriteAllText(filePath, "Topic,t0_Send_ms,t1_Recv_ms,t2_Exec_ms,Latency_MQTT_ms,Delay_Unity_ms,Latency_Total_ms\n");
                Debug.Log($"<color=cyan><b>[MQTT Latency Logger] Archivo CSV iniciado en:</b> {filePath}</color>");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MQTT Latency Logger] Error al crear el archivo CSV: {ex.Message}");
            }
        }
    }

    private IEnumerator RutinaPingPong()
    {
        while (true)
        {
            if (guardarLatencia && MQTTClient.Instance != null && MQTTClient.Instance.IsConnected)
            {
                long t0_Send = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                PingPayload payload = new PingPayload { t0 = t0_Send };
                string jsonStr = JsonUtility.ToJson(payload);

                MQTTClient.Instance.PublishPing(jsonStr);
            }
            yield return new WaitForSeconds(intervaloPingSec);
        }
    }

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

    private IEnumerator RegistrarFrameFinal(long t0, long t1)
    {
        yield return new WaitForEndOfFrame();

        if (!guardarLatencia) yield break;

        long t2_Exec = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        long rtt = t1 - t0;
        long latenciaMqttUnidireccional = rtt / 2;
        long retardoUnity = t2_Exec - t1;
        long latenciaTotal = latenciaMqttUnidireccional + retardoUnity;

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