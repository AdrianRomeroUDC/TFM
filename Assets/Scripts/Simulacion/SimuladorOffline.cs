using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

[Serializable]
public class EventoHistoricoPieza
{
    public float tiempoRelativoSegundos;
    public string topic;
    public string payloadJson;
}

[Serializable]
public class SecuenciaPiezaData
{
    public string tipoPieza;
    public List<EventoHistoricoPieza> eventos = new List<EventoHistoricoPieza>();
}

public class SimuladorOffline : MonoBehaviour
{
    private static SimuladorOffline instance;
    public static SimuladorOffline Instance => instance;

    [Header("--- Archivos JSON descargados de InfluxDB ---")]
    public TextAsset jsonPiezaBlanca;
    public TextAsset jsonPiezaRoja;
    public TextAsset jsonPiezaAzul;

    private SecuenciaPiezaData datosBlanca;
    private SecuenciaPiezaData datosRoja;
    private SecuenciaPiezaData datosAzul;

    private Coroutine corrutinaSimulacionPieza;
    private bool estaEjecutandoSimulacion = false;

    private void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        CargarDatosHistoricos();
        Invoke(nameof(ComprobarYConfigurarModoOffline), 0.5f);
    }

    public void CargarDatosHistoricos()
    {
        datosBlanca = CargarSecuencia(jsonPiezaBlanca, "datosPiezaBlanca.json", "WHITE");
        datosRoja = CargarSecuencia(jsonPiezaRoja, "datosPiezaRoja.json", "RED");
        datosAzul = CargarSecuencia(jsonPiezaAzul, "datosPiezaAzul.json", "BLUE");
    }

    private SecuenciaPiezaData CargarSecuencia(TextAsset assetAsignado, string nombreArchivoDisco, string etiqueta)
    {
        // 1. Cargar desde Inspector
        if (assetAsignado != null && !string.IsNullOrEmpty(assetAsignado.text))
        {
            try
            {
                SecuenciaPiezaData data = JsonUtility.FromJson<SecuenciaPiezaData>(assetAsignado.text);
                if (data != null && data.eventos != null && data.eventos.Count > 0)
                {
                    Debug.Log($"<color=cyan>[SimuladorOffline] 📦 Cargada secuencia {etiqueta} ({data.eventos.Count} eventos)</color>");
                    return data;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error parseando JSON {etiqueta}: {ex.Message}");
            }
        }

        // 2. Respaldo directo desde disco
        string rutaDisco = Path.Combine(Application.dataPath, "DatosSimulacion", nombreArchivoDisco);
        if (File.Exists(rutaDisco))
        {
            try
            {
                string texto = File.ReadAllText(rutaDisco);
                SecuenciaPiezaData data = JsonUtility.FromJson<SecuenciaPiezaData>(texto);
                if (data != null && data.eventos != null && data.eventos.Count > 0)
                {
                    Debug.Log($"<color=cyan>[SimuladorOffline] 📁 Cargado desde disco {nombreArchivoDisco} ({data.eventos.Count} eventos)</color>");
                    return data;
                }
            }
            catch { }
        }

        return null;
    }

    public void ComprobarYConfigurarModoOffline()
    {
        bool mqtt1Conectado = (MQTTClient.Instance != null && MQTTClient.Instance.IsConnected);
        bool mqtt2Conectado = (MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.IsConnected);

        bool ambosDesconectados = !mqtt1Conectado && !mqtt2Conectado;

        if (ambosDesconectados)
        {
            Debug.Log("<color=yellow>⚠️ [Modo Offline] Clientes MQTT desconectados. Inicializando almacén inicial...</color>");
            InicializarStockPorDefecto();
        }
    }

    private void InicializarStockPorDefecto()
    {
        JSON_FullStock stockFake = new JSON_FullStock();
        List<JSON_StockItem> listaItems = new List<JSON_StockItem>();

        string[] filas = { "A", "B", "C" };
        string[] coloresPorColumna = { "WHITE", "RED", "BLUE" };

        int idCounter = 0;

        for (int col = 1; col <= 3; col++)
        {
            string colorColumna = coloresPorColumna[col - 1];

            foreach (string fila in filas)
            {
                listaItems.Add(new JSON_StockItem
                {
                    location = $"{fila}{col}",
                    workpiece = new JSON_Workpiece
                    {
                        id = $"OFFLINE_{idCounter++}",
                        type = colorColumna,
                        state = "RAW"
                    }
                });
            }
        }

        stockFake.stockItems = listaItems.ToArray();
        stockFake.ts = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        InyectarMensajeOffline("f/i/stock", JsonUtility.ToJson(stockFake));
    }

    public void PedirPieza(string tipoPieza)
    {
        bool mqtt1Conectado = (MQTTClient.Instance != null && MQTTClient.Instance.IsConnected);
        bool mqtt2Conectado = (MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.IsConnected);

        bool ambosDesconectados = !mqtt1Conectado && !mqtt2Conectado;

        if (!ambosDesconectados)
        {
            Debug.Log($"<color=green>🌐 [Modo Online] Enviando orden MQTT para {tipoPieza}...</color>");
            if (MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.IsConnected)
            {
                MQTT_InterfaceClient.Instance.SendOrder(tipoPieza);
            }
        }
        else
        {
            if (datosBlanca == null && datosRoja == null && datosAzul == null)
            {
                CargarDatosHistoricos();
            }

            SecuenciaPiezaData datosAProcesar = null;
            string tipoUpper = tipoPieza.ToUpper();

            if (tipoUpper.Contains("WHITE") || tipoUpper.Contains("BLANC")) datosAProcesar = datosBlanca;
            else if (tipoUpper.Contains("RED") || tipoUpper.Contains("ROJ")) datosAProcesar = datosRoja;
            else if (tipoUpper.Contains("BLUE") || tipoUpper.Contains("AZUL")) datosAProcesar = datosAzul;

            if (datosAProcesar != null && datosAProcesar.eventos != null && datosAProcesar.eventos.Count > 0)
            {
                if (estaEjecutandoSimulacion && corrutinaSimulacionPieza != null)
                {
                    StopCoroutine(corrutinaSimulacionPieza);
                }

                corrutinaSimulacionPieza = StartCoroutine(ReproducirSecuencia(datosAProcesar));
            }
            else
            {
                Debug.LogError($"❌ [Modo Offline] No hay eventos guardados para '{tipoPieza}'. Vuelve a generar el JSON especificando una franja con actividad en InfluxDB.");
            }
        }
    }

    private IEnumerator ReproducirSecuencia(SecuenciaPiezaData secuencia)
    {
        estaEjecutandoSimulacion = true;
        float tiempoAcumulado = 0f;

        secuencia.eventos.Sort((a, b) => a.tiempoRelativoSegundos.CompareTo(b.tiempoRelativoSegundos));

        Debug.Log($"<color=green>▶️ [Modo Offline] Iniciando reproducción para {secuencia.tipoPieza} ({secuencia.eventos.Count} eventos)...</color>");

        foreach (var ev in secuencia.eventos)
        {
            if (string.IsNullOrEmpty(ev.topic) || string.IsNullOrEmpty(ev.payloadJson)) continue;

            float tiempoEspera = ev.tiempoRelativoSegundos - tiempoAcumulado;

            if (tiempoEspera > 0f)
            {
                yield return new WaitForSeconds(tiempoEspera);
            }

            tiempoAcumulado = ev.tiempoRelativoSegundos;

            // Inyección exacta al igual que en reproducción BBDD
            InyectarMensajeOffline(ev.topic.Trim(), ev.payloadJson);
        }

        estaEjecutandoSimulacion = false;
        Debug.Log($"<color=green>✅ [Modo Offline] Finalizada reproducción de la pieza {secuencia.tipoPieza}.</color>");
    }

    private void InyectarMensajeOffline(string topic, string payloadJson)
    {
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