using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

public class InfluxDBClient : MonoBehaviour
{
    private static InfluxDBClient instance;
    public static InfluxDBClient Instance { get { return instance; } }

    [Header("Configuración InfluxDB Cloud AWS")]
    public string serverUrl = "https://eu-central-1-1.aws.cloud2.influxdata.com";
    public string token = "tYzrHx9kwepkwm5ZwAGFbKA_aSok9i_OQBue_zAmXZY-5FxfBFxNoVLvcIzUCc0G1RDcLKY9DNtBI5Lbe0gWAg==";
    public string org = "fischertechnik";
    public string bucket = "factory_TFM";

    public struct RegistroInflux
    {
        public DateTime timestamp;
        public string topic;
        public string payloadJson;
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }
    }

    public IEnumerator DescargarYReproducirHistorico(DateTime desde, DateTime hasta, float multiplicadorVelocidad, Action<DateTime> alCambiarTiempo)
    {
        // 1. Convertir rango UI a formato UTC ISO para InfluxDB
        string isoDesde = desde.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        string isoHasta = hasta.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

        Debug.Log($"<color=cyan>[InfluxDB DEBUG] 🔍 Consultando rango UTC: {isoDesde} hasta {isoHasta}</color>");

        // 2. Consulta Flux: extrae _value (payload), topic y se ordena estrictamente por _time
        string fluxQuery = $@"
            from(bucket: ""{bucket}"")
              |> range(start: {isoDesde}, stop: {isoHasta})
              |> filter(fn: (r) => r[""_field""] == ""payload"")
              |> sort(columns: [""_time""])";

        string url = $"{serverUrl}/api/v2/query?org={Uri.EscapeDataString(org)}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(fluxQuery);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Authorization", "Token " + token);
            request.SetRequestHeader("Content-Type", "application/vnd.flux");
            request.SetRequestHeader("Accept", "text/csv");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"❌ [InfluxDB DEBUG] Error HTTP {request.responseCode}: {request.error}\n{request.downloadHandler.text}");
                yield break;
            }

            string csvRespuesta = request.downloadHandler.text;
            Debug.Log($"<color=cyan>[InfluxDB DEBUG] 📄 CSV recibido con éxito. Tamaño total: {csvRespuesta.Length} caracteres.</color>");

            // 3. Parsear CSV detallado
            List<RegistroInflux> registros = ParsearCSVConDepuracion(csvRespuesta);

            if (registros.Count == 0)
            {
                Debug.LogWarning("⚠️ [InfluxDB DEBUG] 0 registros válidos tras el parseo del CSV.");
                yield break;
            }

            Debug.Log($"<color=green>✅ [InfluxDB DEBUG] Total de registros listos para reproducir: {registros.Count}</color>");
            Debug.Log($"<color=green>⏱️ [InfluxDB DEBUG] Primer evento de la lista -> Hora: {registros[0].timestamp:yyyy-MM-dd HH:mm:ss} | Topic: {registros[0].topic}</color>");
            Debug.Log($"<color=green>⏱️ [InfluxDB DEBUG] Último evento de la lista -> Hora: {registros[registros.Count - 1].timestamp:yyyy-MM-dd HH:mm:ss} | Topic: {registros[registros.Count - 1].topic}</color>");

            // =======================================================================
            // ⏰ MOTOR DE RELOJ VIRTUAL CON TRAZA DE INYECCIÓN
            // =======================================================================
            DateTime tiempoSimulado = desde;
            int idxMensaje = 0;

            while (tiempoSimulado <= hasta)
            {
                // Actualizar reloj UI de la cabecera
                alCambiarTiempo?.Invoke(tiempoSimulado);

                // Inyectar todos los mensajes que correspondan a este instante virtual
                while (idxMensaje < registros.Count)
                {
                    DateTime tsLocal = registros[idxMensaje].timestamp.ToLocalTime();

                    if (tsLocal <= tiempoSimulado)
                    {
                        var reg = registros[idxMensaje];

                        // 🔍 TRAZA INDIVIDUAL DE CADA MENSAJE INYECTADO
                        Debug.Log($"<color=yellow>[INYECCIÓN HISTÓRICA] Reloj: {tiempoSimulado:HH:mm:ss} | Evento Time: {tsLocal:HH:mm:ss} | Topic: {reg.topic} | Payload: {reg.payloadJson}</color>");

                        InyectarMensaje(reg.topic, reg.payloadJson);
                        idxMensaje++;
                    }
                    else
                    {
                        break;
                    }
                }

                yield return null;
                float delta = Time.deltaTime * Mathf.Max(0.1f, multiplicadorVelocidad);
                tiempoSimulado = tiempoSimulado.AddSeconds(delta);
            }

            // Inyectar sobrantes al alcanzar el final
            while (idxMensaje < registros.Count)
            {
                var reg = registros[idxMensaje];
                InyectarMensaje(reg.topic, reg.payloadJson);
                idxMensaje++;
            }

            alCambiarTiempo?.Invoke(hasta);
            Debug.Log("<color=green>✅ [InfluxDB DEBUG] Reproducción histórica completada con éxito.</color>");
        }
    }

    /// <summary>
    /// Parsea el CSV imprimiendo información de diagnóstico en la consola de Unity
    /// </summary>
    private List<RegistroInflux> ParsearCSVConDepuracion(string csvData)
    {
        List<RegistroInflux> lista = new List<RegistroInflux>();
        string[] lineas = csvData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        int colTime = -1, colValue = -1, colTopic = -1, colMeasurement = -1;
        int lineasProcesadas = 0;

        foreach (string linea in lineas)
        {
            if (linea.StartsWith("#") || string.IsNullOrWhiteSpace(linea)) continue;

            List<string> columnas = ParseLineaCSV(linea);

            if (columnas.Contains("_time") || columnas.Contains("_value"))
            {
                colTime = columnas.IndexOf("_time");
                colValue = columnas.IndexOf("_value");
                colTopic = columnas.IndexOf("topic");
                colMeasurement = columnas.IndexOf("_measurement");
                continue;
            }

            if (colTime == -1 || colValue == -1) continue;

            if (columnas.Count > colTime && columnas.Count > colValue)
            {
                string timeStr = columnas[colTime];
                string payloadRaw = columnas[colValue];

                if (DateTime.TryParse(timeStr, out DateTime ts))
                {
                    string topicStr = "";
                    if (colTopic != -1 && columnas.Count > colTopic && !string.IsNullOrEmpty(columnas[colTopic]))
                        topicStr = columnas[colTopic];
                    else if (colMeasurement != -1 && columnas.Count > colMeasurement)
                        topicStr = columnas[colMeasurement];

                    lista.Add(new RegistroInflux
                    {
                        timestamp = ts,
                        topic = topicStr,
                        payloadJson = payloadRaw
                    });

                    lineasProcesadas++;
                }
            }
        }

        Debug.Log($"<color=cyan>[InfluxDB DEBUG] Líneas analizadas del CSV: {lineasProcesadas} registros extraídos correctamente.</color>");
        return lista;
    }

    private List<string> ParseLineaCSV(string linea)
    {
        List<string> resultado = new List<string>();
        bool dentroDeComillas = false;
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < linea.Length; i++)
        {
            char c = linea[i];
            if (c == '"')
            {
                if (dentroDeComillas && i + 1 < linea.Length && linea[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    dentroDeComillas = !dentroDeComillas;
                }
            }
            else if (c == ',' && !dentroDeComillas)
            {
                resultado.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        resultado.Add(sb.ToString().Trim());
        return resultado;
    }

    private void InyectarMensaje(string topic, string payloadJson)
    {
        if (string.IsNullOrEmpty(payloadJson)) return;

        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.ProcesarMensajeExterno(topic, payloadJson);
        }

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.ProcesarMensajeExterno(topic, payloadJson);
        }
    }
}