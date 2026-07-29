using UnityEngine;
using System;
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
    public static SimuladorOffline Instance { get; private set; }

    // 🟢 Evento para notificar a la UI que la simulación empezó (true) o terminó (false)
    public static event Action<bool> OnEstadoSimulacionOfflineCambiado;

    [Header("--- Archivos JSON de InfluxDB ---")]
    [SerializeField] private TextAsset jsonPiezaBlanca;
    [SerializeField] private TextAsset jsonPiezaRoja;
    [SerializeField] private TextAsset jsonPiezaAzul;

    private readonly Dictionary<string, SecuenciaPiezaData> mapaSecuencias = new Dictionary<string, SecuenciaPiezaData>();
    private Coroutine corrutinaSimulacion;
    public bool EnEjecucion { get; private set; } = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        CargarDatosHistoricos();
        Invoke(nameof(ComprobarYConfigurarModoOffline), 0.5f);
    }

    public void CargarDatosHistoricos()
    {
        mapaSecuencias.Clear();
        CargarSecuencia("WHITE", jsonPiezaBlanca);
        CargarSecuencia("RED", jsonPiezaRoja);
        CargarSecuencia("BLUE", jsonPiezaAzul);
    }

    private void CargarSecuencia(string clave, TextAsset asset)
    {
        if (asset == null || string.IsNullOrEmpty(asset.text)) return;

        try
        {
            SecuenciaPiezaData data = JsonUtility.FromJson<SecuenciaPiezaData>(asset.text);
            if (data != null && data.eventos != null && data.eventos.Count > 0)
            {
                data.eventos.Sort((a, b) => a.tiempoRelativoSegundos.CompareTo(b.tiempoRelativoSegundos));
                mapaSecuencias[clave] = data;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Error al parsear JSON de {clave}: {ex.Message}");
        }
    }

    public void ComprobarYConfigurarModoOffline()
    {
        bool mqtt1Conectado = MQTTClient.Instance != null && MQTTClient.Instance.IsConnected;
        bool mqtt2Conectado = MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.IsConnected;

        if (!mqtt1Conectado && !mqtt2Conectado)
        {
            InicializarStockPorDefecto();
        }
    }

    private void InicializarStockPorDefecto()
    {
        JSON_FullStock stockFake = new JSON_FullStock();
        List<JSON_StockItem> listaItems = new List<JSON_StockItem>();

        string[] filas = { "A", "B", "C" };
        string[] colores = { "WHITE", "RED", "BLUE" };
        int idCounter = 0;

        for (int col = 1; col <= 3; col++)
        {
            string colorColumna = colores[col - 1];
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
        bool mqtt1Conectado = MQTTClient.Instance != null && MQTTClient.Instance.IsConnected;
        bool mqtt2Conectado = MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.IsConnected;

        if (mqtt1Conectado || mqtt2Conectado)
        {
            if (MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.IsConnected)
            {
                MQTT_InterfaceClient.Instance.SendOrder(tipoPieza);
            }
            return;
        }

        // Si ya hay una simulación en curso, se ignora la petición para proteger el estado
        if (EnEjecucion) return;

        string claveUpper = tipoPieza.ToUpper();
        string claveMap = claveUpper.Contains("WHITE") || claveUpper.Contains("BLANC") ? "WHITE" :
                         claveUpper.Contains("RED") || claveUpper.Contains("ROJ") ? "RED" :
                         claveUpper.Contains("BLUE") || claveUpper.Contains("AZUL") ? "BLUE" : claveUpper;

        if (mapaSecuencias.TryGetValue(claveMap, out SecuenciaPiezaData secuencia))
        {
            if (corrutinaSimulacion != null) StopCoroutine(corrutinaSimulacion);
            corrutinaSimulacion = StartCoroutine(ReproducirSecuencia(secuencia));
        }
    }

    private IEnumerator ReproducirSecuencia(SecuenciaPiezaData secuencia)
    {
        EnEjecucion = true;
        OnEstadoSimulacionOfflineCambiado?.Invoke(true); // 🔒 Notificar inicio -> Deshabilitar botones UI

        // 🟢 1. Eliminar o limpiar la pieza de plataformaDSO al comenzar una nueva simulación
        LimpiarPlataformaDSO();

        float tiempoAcumulado = 0f;
        Debug.Log($"<color=green>▶️ [Modo Offline] Reproduciendo {secuencia.tipoPieza} ({secuencia.eventos.Count} eventos)...</color>");

        foreach (var ev in secuencia.eventos)
        {
            if (string.IsNullOrEmpty(ev.topic) || string.IsNullOrEmpty(ev.payloadJson)) continue;

            float tiempoEspera = ev.tiempoRelativoSegundos - tiempoAcumulado;

            // 🟢 2. Soporte para PAUSA (exactamente igual que en reproducción BBDD)
            while (tiempoEspera > 0f)
            {
                float multiplicador = (UI_ControladorMenu.Instance != null && UI_ControladorMenu.Instance.EsPausado) ? 0f :
                                     (UI_ControladorMenu.Instance != null ? UI_ControladorMenu.Instance.multiplicadorVelocidad : 1.0f);

                if (multiplicador > 0f)
                {
                    float delta = Time.deltaTime * multiplicador;
                    tiempoEspera -= delta;
                    tiempoAcumulado += delta;
                }

                yield return null; // Esperar al siguiente frame si está pausado
            }

            InyectarMensajeOffline(ev.topic, ev.payloadJson);
        }

        EnEjecucion = false;
        OnEstadoSimulacionOfflineCambiado?.Invoke(false); // 🔓 Notificar fin -> Reagrupar e interactuar botones UI
        Debug.Log($"<color=green>✅ [Modo Offline] Finalizada reproducción de {secuencia.tipoPieza}.</color>");
    }

    /// <summary>
    /// Limpia la plataforma DSO o estación de salida enviando un estado nulo o retirando el workpiece.
    /// </summary>
    private void LimpiarPlataformaDSO()
    {
        // Notificar eliminación enviando un payload con workpiece en null o estado retirado
        string payloadLimpieza = "{\"workpiece\":null,\"ts\":\"" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") + "\"}";
        InyectarMensajeOffline("dt/dso/state", payloadLimpieza);
        InyectarMensajeOffline("f/i/dso", payloadLimpieza);
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