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

    public IEnumerator DescargarYReproducirHistorico(DateTime desde, DateTime hasta, Func<float> obtenerVelocidad, Action<DateTime> alCambiarTiempo)
    {
        string isoDesde = desde.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        string isoHasta = hasta.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

        Debug.Log($"<color=cyan>[InfluxDB] 🔍 Consulta de histórico UTC: {isoDesde} hasta {isoHasta}</color>");

        string url = $"{serverUrl}/api/v2/query?org={Uri.EscapeDataString(org)}";

        // =======================================================================
        // 1. CARGAR ÚLTIMO ESTADO PREVIO
        // =======================================================================
        string fluxQueryPrevio = $@"
            from(bucket: ""{bucket}"")
              |> range(start: 1970-01-01T00:00:00Z, stop: {isoDesde})
              |> filter(fn: (r) => r[""_field""] == ""payload"")
              |> last()";

        using (UnityWebRequest reqPrevio = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(fluxQueryPrevio);
            reqPrevio.uploadHandler = new UploadHandlerRaw(bodyRaw);
            reqPrevio.downloadHandler = new DownloadHandlerBuffer();

            reqPrevio.SetRequestHeader("Authorization", "Token " + token);
            reqPrevio.SetRequestHeader("Content-Type", "application/vnd.flux");
            reqPrevio.SetRequestHeader("Accept", "text/csv");

            yield return reqPrevio.SendWebRequest();

            if (reqPrevio.result == UnityWebRequest.Result.Success)
            {
                List<RegistroInflux> estadosPrevios = ParsearCSVDirecto(reqPrevio.downloadHandler.text);
                Debug.Log($"<color=cyan>[InfluxDB] 📦 Cargando {estadosPrevios.Count} estados previos para inicializar al inicio ({desde:HH:mm:ss})...</color>");
                foreach (var estado in estadosPrevios)
                {
                    InyectarMensaje(estado.topic, estado.payloadJson);
                }
            }
        }

        // =======================================================================
        // 2. DESCARGAR Y REPRODUCIR EL RANGO SELECCIONADO
        // =======================================================================
        string fluxQueryRango = $@"
            from(bucket: ""{bucket}"")
              |> range(start: {isoDesde}, stop: {isoHasta})
              |> filter(fn: (r) => r[""_field""] == ""payload"")
              |> sort(columns: [""_time""])";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(fluxQueryRango);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Authorization", "Token " + token);
            request.SetRequestHeader("Content-Type", "application/vnd.flux");
            request.SetRequestHeader("Accept", "text/csv");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"❌ [InfluxDB] Error HTTP {request.responseCode}: {request.error}\n{request.downloadHandler.text}");
                yield break;
            }

            string csvRespuesta = request.downloadHandler.text;
            List<RegistroInflux> registros = ParsearCSVDirecto(csvRespuesta);

            if (registros.Count == 0)
            {
                Debug.LogWarning("⚠️ [InfluxDB] No se encontraron registros dentro del rango seleccionado.");
            }
            else
            {
                Debug.Log($"<color=green>✅ [InfluxDB] {registros.Count} eventos listos para reproducción.</color>");
            }

            double totalSegundosRango = (hasta - desde).TotalSeconds;
            double segundosSimuladosTranscurridos = 0;
            int idxMensaje = 0;

            registros.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));

            bool registradoLogPausa = false;

            while (segundosSimuladosTranscurridos < totalSegundosRango)
            {
                float velActual = (obtenerVelocidad != null) ? obtenerVelocidad() : 1.0f;

                // 🛑 SI ESTÁ EN PAUSA (velActual <= 0), CONGELAMOS TOTALMENTE LA EJECUCIÓN
                if (velActual <= 0f)
                {
                    if (!registradoLogPausa)
                    {
                        registradoLogPausa = true;
                        DateTime horaCongelada = desde.AddSeconds(segundosSimuladosTranscurridos);
                        Debug.Log($"<color=yellow>[InfluxDB] ⏸️ PAUSADO: Reloj congelado en {horaCongelada:HH:mm:ss}. No se inyectará ningún mensaje.</color>");
                    }
                    yield return null;
                    continue;
                }

                if (registradoLogPausa)
                {
                    registradoLogPausa = false;
                    Debug.Log($"<color=green>[InfluxDB] ▶️ REANUDADO: Continuando reproducción a velocidad x{velActual}.</color>");
                }

                float deltaReal = Time.deltaTime;
                segundosSimuladosTranscurridos += deltaReal * velActual;

                DateTime tiempoSimuladoLocal = desde.AddSeconds(segundosSimuladosTranscurridos);
                if (tiempoSimuladoLocal > hasta) tiempoSimuladoLocal = hasta;

                alCambiarTiempo?.Invoke(tiempoSimuladoLocal);

                while (idxMensaje < registros.Count)
                {
                    DateTime tsMensajeLocal = registros[idxMensaje].timestamp.ToLocalTime();

                    if (tsMensajeLocal <= tiempoSimuladoLocal)
                    {
                        var reg = registros[idxMensaje];
                        InyectarMensaje(reg.topic, reg.payloadJson);
                        idxMensaje++;
                    }
                    else
                    {
                        break;
                    }
                }

                yield return null;
            }

            while (idxMensaje < registros.Count)
            {
                var reg = registros[idxMensaje];
                InyectarMensaje(reg.topic, reg.payloadJson);
                idxMensaje++;
            }

            alCambiarTiempo?.Invoke(hasta);
            Debug.Log("<color=green>✅ [InfluxDB] Reproducción histórica finalizada.</color>");
        }
    }

    private List<RegistroInflux> ParsearCSVDirecto(string csvData)
    {
        List<RegistroInflux> lista = new List<RegistroInflux>();
        string[] lineas = csvData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        int colTime = -1, colValue = -1, colTopic = -1, colMeasurement = -1;

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
                    {
                        topicStr = columnas[colTopic];
                    }
                    else if (colMeasurement != -1 && columnas.Count > colMeasurement)
                    {
                        string m = columnas[colMeasurement];
                        if (m == "bme680" || m == "bm680") topicStr = "i/bme680";
                        else if (m == "ldr") topicStr = "i/ldr";
                        else if (m == "stock") topicStr = "f/i/stock";
                        else topicStr = m;
                    }

                    if (!string.IsNullOrEmpty(payloadRaw))
                    {
                        lista.Add(new RegistroInflux
                        {
                            timestamp = ts,
                            topic = topicStr,
                            payloadJson = payloadRaw
                        });
                    }
                }
            }
        }
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

        // 🛑 BLOQUEO DE SEGURIDAD: Si el menú está en estado de pausa, aborta la inyección inmediatamente.
        if (UI_ControladorMenu.Instance != null && UI_ControladorMenu.Instance.EsPausado)
        {
            return;
        }

        if (topic == "stock" || topic == "f_i_stock") topic = "f/i/stock";
        else if (topic == "ldr" || topic == "i_ldr") topic = "i/ldr";
        else if (topic == "bme680" || topic == "bm680" || topic == "i_bme680" || topic == "i/bm680") topic = "i/bme680";

        Debug.Log($"<color=white>[InfluxDB] 📩 Evento Inyectado -> Topic: <b>{topic}</b></color>");

        if (MQTTClient.Instance != null && MQTTClient.Instance.isActiveAndEnabled)
        {
            MQTTClient.Instance.ProcesarMensajeExterno(topic, payloadJson);
        }

        if (MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.isActiveAndEnabled)
        {
            MQTT_InterfaceClient.Instance.ProcesarMensajeExterno(topic, payloadJson);
        }
    }
}