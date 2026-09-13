using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Este script es el "puente" entre Unity y la base de datos histórica InfluxDB, donde queda
/// guardado (en la nube, en AWS) todo lo que ha ido pasando en la fábrica real a lo largo del tiempo
/// (cada mensaje MQTT que envían las estaciones se archiva ahí). Sirve para dos cosas:
/// 1) Descargar un rango de fechas y reproducirlo en Unity como si fuera en directo (modo "reproducción
///    histórica"), inyectando cada mensaje antiguo en <see cref="MQTTClient"/> y <see cref="MQTT_InterfaceClient"/>
///    en el momento simulado correcto.
/// 2) Descargar un histórico sin reproducirlo, solo para generar un archivo JSON de exportación
///    (usado por <c>GenerarJSONSimulacion</c>).
/// </summary>
public class InfluxDBClient : MonoBehaviour
{
    // Patrón Singleton: solo debe existir un InfluxDBClient, accesible desde cualquier script como InfluxDBClient.Instance.
    private static InfluxDBClient instance;
    public static InfluxDBClient Instance { get { return instance; } }

    [Header("Configuración InfluxDB Cloud AWS")]
    public string serverUrl = "https://eu-central-1-1.aws.cloud2.influxdata.com";
    public string token = "tYzrHx9kwepkwm5ZwAGFbKA_aSok9i_OQBue_zAmXZY-5FxfBFxNoVLvcIzUCc0G1RDcLKY9DNtBI5Lbe0gWAg==";
    public string org = "fischertechnik";
    public string bucket = "factory_TFM";

    // Representa una única fila del histórico: en qué instante (timestamp) se recibió qué mensaje
    // (topic + JSON) de la fábrica real.
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

    /// <summary>
    /// Descarga los datos de InfluxDB y los devuelve directamente en una lista sin reproducirlos en la escena.
    /// Ideal para la generación de archivos JSON offline.
    /// </summary>
    public IEnumerator DescargarHistoricoSinReproducir(DateTime desde, DateTime hasta, Action<List<RegistroInflux>> alFinalizar)
    {
        // InfluxDB exige las fechas en formato UTC ISO-8601, así que convertimos antes de construir la consulta.
        string isoDesde = desde.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        string isoHasta = hasta.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

        string url = $"{serverUrl}/api/v2/query?org={Uri.EscapeDataString(org)}";

        // Consulta en lenguaje Flux (el lenguaje de consultas de InfluxDB): pide todos los mensajes
        // guardados en el rango de fechas indicado, ordenados por tiempo.
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
            request.SetRequestHeader("Accept", "text/csv"); // InfluxDB devuelve los resultados en formato CSV.

            yield return request.SendWebRequest(); // Esperamos la respuesta del servidor sin congelar Unity.

            List<RegistroInflux> registros = new List<RegistroInflux>();

            if (request.result == UnityWebRequest.Result.Success)
            {
                registros = ParsearCSVDirecto(request.downloadHandler.text);
                registros.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));
            }
            else
            {
                Debug.LogError($"❌ [InfluxDB] Error HTTP {request.responseCode}: {request.error}\n{request.downloadHandler.text}");
            }

            // Avisamos a quien nos llamó (normalmente GenerarJSONSimulacion) con la lista ya lista.
            alFinalizar?.Invoke(registros);
        }
    }

    /// <summary>
    /// Descarga un rango de histórico de InfluxDB y lo va "reproduciendo" en Unity mensaje a mensaje,
    /// como si la fábrica estuviera enviándolos en directo, respetando el tiempo real transcurrido entre
    /// ellos (multiplicado por la velocidad de reproducción que indique <paramref name="obtenerVelocidad"/>).
    /// </summary>
    /// <param name="desde">Instante inicial del histórico a reproducir.</param>
    /// <param name="hasta">Instante final del histórico a reproducir.</param>
    /// <param name="obtenerVelocidad">Función que devuelve la velocidad actual de reproducción (1x, 2x, pausa=0, etc.), controlada normalmente desde la interfaz.</param>
    /// <param name="alCambiarTiempo">Callback que se llama cada frame con el "reloj simulado" actual, para poder mostrarlo en la interfaz.</param>
    public IEnumerator DescargarYReproducirHistorico(DateTime desde, DateTime hasta, Func<float> obtenerVelocidad, Action<DateTime> alCambiarTiempo)
    {
        string isoDesde = desde.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        string isoHasta = hasta.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

        Debug.Log($"<color=cyan>[InfluxDB] 🔍 Consulta de histórico UTC: {isoDesde} hasta {isoHasta}</color>");

        string url = $"{serverUrl}/api/v2/query?org={Uri.EscapeDataString(org)}";

        // 1. CARGAR ÚLTIMO ESTADO PREVIO
        // Antes de reproducir el rango pedido, necesitamos saber en qué estado estaba la fábrica
        // justo antes (por ejemplo, con qué piezas en el HBW), si no, el gemelo digital arrancaría "vacío".
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
                    // Inyectamos cada estado previo directamente, sin esperar tiempo simulado,
                    // para que la escena arranque ya con las piezas y posiciones correctas. Este es
                    // el único momento en que el almacén físico debe pintarse en modo Histórico.
                    InyectarMensaje(estado.topic, estado.payloadJson);
                }
            }
        }

        // 2. DESCARGAR Y REPRODUCIR EL RANGO SELECCIONADO
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

            // Bucle principal de reproducción: avanza un "reloj simulado" según la velocidad elegida
            // por el usuario, y va inyectando los mensajes cuyo instante ya ha sido alcanzado.
            while (segundosSimuladosTranscurridos < totalSegundosRango)
            {
                float velActual = (obtenerVelocidad != null) ? obtenerVelocidad() : 1.0f;

                if (velActual <= 0f)
                {
                    // Velocidad 0 = reproducción en pausa: no avanzamos el reloj ni inyectamos mensajes.
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

                // Avanzamos el reloj simulado según el tiempo real pasado (Time.deltaTime) multiplicado
                // por la velocidad elegida (x1, x2, x10...), permitiendo "avance rápido" del histórico.
                float deltaReal = Time.deltaTime;
                segundosSimuladosTranscurridos += deltaReal * velActual;

                DateTime tiempoSimuladoLocal = desde.AddSeconds(segundosSimuladosTranscurridos);
                if (tiempoSimuladoLocal > hasta) tiempoSimuladoLocal = hasta;

                alCambiarTiempo?.Invoke(tiempoSimuladoLocal);

                // Inyectamos todos los mensajes cuyo instante ya haya sido "alcanzado" por el reloj simulado.
                while (idxMensaje < registros.Count)
                {
                    DateTime tsMensajeLocal = registros[idxMensaje].timestamp.ToLocalTime();

                    if (tsMensajeLocal <= tiempoSimuladoLocal)
                    {
                        var reg = registros[idxMensaje];
                        InyectarMensaje(reg.topic, reg.payloadJson, permitirActualizarAlmacenFisico: false);
                        idxMensaje++;
                    }
                    else
                    {
                        break;
                    }
                }

                yield return null; // Esperamos al siguiente frame antes de seguir avanzando el reloj.
            }

            // Si sobrara algún mensaje por redondeos de tiempo, lo inyectamos igualmente al terminar,
            // para no perder ningún evento del histórico.
            while (idxMensaje < registros.Count)
            {
                var reg = registros[idxMensaje];
                InyectarMensaje(reg.topic, reg.payloadJson, permitirActualizarAlmacenFisico: false);
                idxMensaje++;
            }

            alCambiarTiempo?.Invoke(hasta);
            Debug.Log("<color=green>✅ [InfluxDB] Reproducción histórica finalizada.</color>");
        }
    }

    // Convierte la respuesta CSV cruda que devuelve InfluxDB en una lista de RegistroInflux fáciles de usar.
    // InfluxDB devuelve varias "tablas" seguidas en el mismo CSV, cada una con su propia fila de cabecera,
    // por eso hay que ir detectando dónde están las columnas "_time", "_value", "topic", etc. en cada bloque.
    private List<RegistroInflux> ParsearCSVDirecto(string csvData)
    {
        List<RegistroInflux> lista = new List<RegistroInflux>();
        string[] lineas = csvData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        int colTime = -1, colValue = -1, colTopic = -1, colMeasurement = -1;

        foreach (string linea in lineas)
        {
            // Las líneas que empiezan por "#" son metadatos internos de InfluxDB, no datos útiles.
            if (linea.StartsWith("#") || string.IsNullOrWhiteSpace(linea)) continue;

            List<string> columnas = ParseLineaCSV(linea);

            if (columnas.Contains("_time") || columnas.Contains("_value"))
            {
                // Esta línea es una cabecera nueva: recalculamos en qué posición está cada columna.
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
                        // Si el registro no trae el topic explícito, lo deducimos a partir del nombre
                        // de la "measurement" (la tabla de InfluxDB donde se guardó el dato).
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

    // Separa una línea CSV en sus columnas, respetando los valores que van entre comillas
    // (necesario porque el JSON de cada mensaje puede contener comas dentro de las comillas).
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
                    // Dos comillas seguidas dentro de un valor representan una comilla "escapada".
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

    // Reenvía un mensaje histórico (topic + JSON) a los clientes MQTT de Unity, exactamente igual
    // que si hubiera llegado en directo desde la fábrica real por la red.
    // <param name="permitirActualizarAlmacenFisico">El almacén físico (piezas 3D del HBW) solo debe
    // pintarse una vez, con el estado previo cargado al principio de la reproducción; los mensajes
    // "f/i/stock" que llegan durante la reproducción cronometrada del rango deben actualizar
    // únicamente el panel de la interfaz, igual que ya hace SimuladorOffline.ReproducirSecuencia.</param>
    private void InyectarMensaje(string topic, string payloadJson, bool permitirActualizarAlmacenFisico = true)
    {
        if (string.IsNullOrEmpty(payloadJson)) return;

        // Si el usuario ha pausado la reproducción desde el menú, no inyectamos nada.
        if (UI_ControladorMenu.Instance != null && UI_ControladorMenu.Instance.EsPausado)
        {
            return;
        }

        // Normalizamos distintas variantes de nombre de topic que pueden venir guardadas en InfluxDB
        // a los nombres de topic "oficiales" que esperan MQTTClient y MQTT_InterfaceClient.
        if (topic == "stock" || topic == "f_i_stock") topic = "f/i/stock";
        else if (topic == "ldr" || topic == "i_ldr") topic = "i/ldr";
        else if (topic == "bme680" || topic == "bm680" || topic == "i_bme680" || topic == "i/bm680") topic = "i/bme680";

        Debug.Log($"<color=white>[InfluxDB] 📩 Evento Inyectado -> Topic: <b>{topic}</b></color>");

        // Tratamiento especial de stock: la UI se actualiza siempre, el almacén 3D solo en la carga
        // del estado previo (una vez al principio), para no vaciar/rellenar los cajones con cada
        // actualización de stock reproducida del histórico.
        if (topic == "f/i/stock" && !permitirActualizarAlmacenFisico)
        {
            if (MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.isActiveAndEnabled)
            {
                MQTT_InterfaceClient.Instance.ProcesarMensajeExterno(topic, payloadJson);
            }
            return;
        }

        // Reenviamos el mensaje tanto al cliente MQTT "principal" (que mueve el gemelo digital 3D)
        // como al cliente MQTT de la interfaz (que actualiza paneles y gráficas en pantalla).
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
