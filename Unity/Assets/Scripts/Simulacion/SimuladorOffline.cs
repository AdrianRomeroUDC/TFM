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

    public static event Action<bool> OnEstadoSimulacionOfflineCambiado;

    [Header("--- Archivos JSON de InfluxDB ---")]
    [SerializeField] private TextAsset jsonPiezaBlanca;
    [SerializeField] private TextAsset jsonPiezaRoja;
    [SerializeField] private TextAsset jsonPiezaAzul;

    private readonly Dictionary<string, SecuenciaPiezaData> mapaSecuencias = new Dictionary<string, SecuenciaPiezaData>();
    private Coroutine corrutinaSimulacion;
    public bool EnEjecucion { get; private set; } = false;

    private JSON_FullStock ultimoStockConocido = null;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        CargarDatosHistoricos();
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
            Debug.LogError($"❌ [SimuladorOffline] Error al parsear JSON de {clave}: {ex.Message}");
        }
    }

    public void ComprobarYConfigurarModoOffline()
    {
        Debug.Log("🔌 [SimuladorOffline] Inicializando entorno y stock offline para simulación.");
        ResetearTurntable();
        LimpiarPlataformaDSO();
        InicializarStockPorDefecto();
    }

    public void InicializarStockPorDefecto()
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

        ultimoStockConocido = stockFake;
        Debug.Log("📦 [SimuladorOffline] Inicializando stock completo por defecto (9 piezas).");
        InyectarMensajeOffline("f/i/stock", JsonUtility.ToJson(stockFake));
    }

    public IEnumerator ResetearYRefrescarAlmacen3DCorrutina()
    {
        string tsNow = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        string payloadLimpieza = "{\"workpiece\":null,\"ts\":\"" + tsNow + "\"}";

        InyectarMensajeOffline("dt/hbw/state", payloadLimpieza);
        InyectarMensajeOffline("f/i/hbw", payloadLimpieza);

        JSON_FullStock stockVacio = new JSON_FullStock
        {
            stockItems = new JSON_StockItem[0],
            ts = tsNow
        };
        InyectarMensajeOffline("f/i/stock", JsonUtility.ToJson(stockVacio));

        yield return null;

        InicializarStockPorDefecto();
    }

    public void ResetearTurntable()
    {
        string tsNow = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        string payloadLimpieza = "{\"workpiece\":null,\"ts\":\"" + tsNow + "\"}";
        InyectarMensajeOffline("dt/mpo/state", payloadLimpieza);
        InyectarMensajeOffline("f/i/mpo", payloadLimpieza);
    }

    public void LimpiarPlataformaDSO()
    {
        string tsNow = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        string payloadLimpieza = "{\"workpiece\":null,\"ts\":\"" + tsNow + "\"}";
        InyectarMensajeOffline("dt/dso/state", payloadLimpieza);
        InyectarMensajeOffline("f/i/dso", payloadLimpieza);
    }

    private void EnviarEstadosInicialesPrepedido()
    {
        string tsNow = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        string payloadDso = "{\"ts\":\"" + tsNow + "\",\"dso_sensor\":false}";
        InyectarMensajeOffline("dt/dps/dso", payloadDso);

        string payloadTurntable = "{\"ts\":\"" + tsNow + "\",\"eject\":false,\"move2Ref7\":true,\"move2Ref8\":false,\"move2Ref9\":false,\"move2Ref10\":false,\"rotation\":0,\"saw\":0}";
        InyectarMensajeOffline("dt/mpo/turntable", payloadTurntable);
    }

    public void PedirPieza(string tipoPieza)
    {
        if (UI_ControladorMenu.Instance != null && UI_ControladorMenu.Instance.EsModoBBDDActivo)
        {
            Debug.LogWarning("⚠️ [SimuladorOffline] No se pueden pedir piezas mientras el Modo BBDD esté activo.");
            return;
        }

        if (EnEjecucion)
        {
            Debug.LogWarning("⚠️ [SimuladorOffline] Ya hay una simulación en curso. Ignorando petición.");
            return;
        }

        // 🟢 Ejecutamos SIEMPRE la reproducción local cuando se pide desde la sección Simulación
        string claveUpper = tipoPieza.ToUpper();
        string claveMap = claveUpper.Contains("WHITE") || claveUpper.Contains("BLANC") ? "WHITE" :
                         claveUpper.Contains("RED") || claveUpper.Contains("ROJ") ? "RED" :
                         claveUpper.Contains("BLUE") || claveUpper.Contains("AZUL") ? "BLUE" : claveUpper;

        if (mapaSecuencias.TryGetValue(claveMap, out SecuenciaPiezaData secuencia))
        {
            if (corrutinaSimulacion != null)
            {
                StopCoroutine(corrutinaSimulacion);
                corrutinaSimulacion = null;
            }

            Debug.Log($"🚀 [SimuladorOffline] Preparando simulación offline para: {tipoPieza}");
            StartCoroutine(SecuenciaPreparacionYArrancar(secuencia));
        }
        else
        {
            Debug.LogError($"❌ [SimuladorOffline] No se encontró secuencia de datos para la pieza: {tipoPieza}");
        }
    }

    private IEnumerator SecuenciaPreparacionYArrancar(SecuenciaPiezaData secuencia)
    {
        EnEjecucion = true;
        OnEstadoSimulacionOfflineCambiado?.Invoke(true);

        ResetearTurntable();
        LimpiarPlataformaDSO();

        yield return StartCoroutine(ResetearYRefrescarAlmacen3DCorrutina());

        EnviarEstadosInicialesPrepedido();

        corrutinaSimulacion = StartCoroutine(ReproducirSecuencia(secuencia));
    }

    private IEnumerator ReproducirSecuencia(SecuenciaPiezaData secuencia)
    {
        float tiempoAcumulado = 0f;
        Debug.Log($"<color=green>▶️ [Modo Offline] Reproduciendo {secuencia.tipoPieza} ({secuencia.eventos.Count} eventos)...</color>");

        foreach (var ev in secuencia.eventos)
        {
            if (string.IsNullOrEmpty(ev.topic) || string.IsNullOrEmpty(ev.payloadJson)) continue;

            float tiempoEspera = ev.tiempoRelativoSegundos - tiempoAcumulado;

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

                yield return null;
            }

            // Tratamiento especial de stock: la UI se actualiza, el 3D no para no vaciar cajones
            if (ev.topic == "f/i/stock")
            {
                if (MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.isActiveAndEnabled)
                {
                    MQTT_InterfaceClient.Instance.ProcesarMensajeExterno(ev.topic, ev.payloadJson);
                }
                try
                {
                    JSON_FullStock stockActualizado = JsonUtility.FromJson<JSON_FullStock>(ev.payloadJson);
                    if (stockActualizado != null) ultimoStockConocido = stockActualizado;
                }
                catch { }

                continue;
            }

            InyectarMensajeOffline(ev.topic, ev.payloadJson);
        }

        EnEjecucion = false;
        OnEstadoSimulacionOfflineCambiado?.Invoke(false);
        Debug.Log($"<color=green>✅ [Modo Offline] Finalizada reproducción de {secuencia.tipoPieza}.</color>");
    }

    public void DetenerSimulacionForzada()
    {
        if (corrutinaSimulacion != null)
        {
            StopCoroutine(corrutinaSimulacion);
            corrutinaSimulacion = null;
        }

        EnEjecucion = false;
        OnEstadoSimulacionOfflineCambiado?.Invoke(false);
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