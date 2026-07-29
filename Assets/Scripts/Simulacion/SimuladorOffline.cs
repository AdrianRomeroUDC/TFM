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

    // Evento para notificar a la UI que la simulación empezó (true) o terminó (false)
    public static event Action<bool> OnEstadoSimulacionOfflineCambiado;

    [Header("--- Archivos JSON de InfluxDB ---")]
    [SerializeField] private TextAsset jsonPiezaBlanca;
    [SerializeField] private TextAsset jsonPiezaRoja;
    [SerializeField] private TextAsset jsonPiezaAzul;

    private readonly Dictionary<string, SecuenciaPiezaData> mapaSecuencias = new Dictionary<string, SecuenciaPiezaData>();
    private Coroutine corrutinaSimulacion;
    public bool EnEjecucion { get; private set; } = false;

    // Guardamos la última foto del stock conocido
    private JSON_FullStock ultimoStockConocido = null;

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
            Debug.LogError($"❌ [SimuladorOffline] Error al parsear JSON de {clave}: {ex.Message}");
        }
    }

    public void ComprobarYConfigurarModoOffline()
    {
        bool mqtt1Conectado = MQTTClient.Instance != null && MQTTClient.Instance.IsConnected;
        bool mqtt2Conectado = MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.IsConnected;

        if (!mqtt1Conectado && !mqtt2Conectado)
        {
            Debug.Log("🔌 [SimuladorOffline] Sin conexión MQTT. Configurando modo offline inicial.");
            ResetearTurntable();
            LimpiarPlataformaDSO();
            InicializarStockPorDefecto();
        }
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
        Debug.Log("📦 [SimuladorOffline] Inicializando stock completo por defecto (9 piezas en almacén).");
        InyectarMensajeOffline("f/i/stock", JsonUtility.ToJson(stockFake));
    }

    /// <summary>
    /// Resetea el almacén de forma asíncrona para forzar el spawning 3D completo al iniciar pedido.
    /// </summary>
    public IEnumerator ResetearYRefrescarAlmacen3DCorrutina()
    {
        string tsNow = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        string payloadLimpieza = "{\"workpiece\":null,\"ts\":\"" + tsNow + "\"}";

        // 1. Limpiar estado de la grúa HBW
        InyectarMensajeOffline("dt/hbw/state", payloadLimpieza);
        InyectarMensajeOffline("f/i/hbw", payloadLimpieza);

        // 2. Enviar stock vacío para limpiar visualmente la escena 3D
        JSON_FullStock stockVacio = new JSON_FullStock
        {
            stockItems = new JSON_StockItem[0],
            ts = tsNow
        };
        InyectarMensajeOffline("f/i/stock", JsonUtility.ToJson(stockVacio));

        // 3. Esperar un frame exacto para que Unity procese el borrado
        yield return null;

        // 4. Volver a poblar el stock completo con las 9 piezas
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

        bool mqtt1Conectado = MQTTClient.Instance != null && MQTTClient.Instance.IsConnected;
        bool mqtt2Conectado = MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.IsConnected;

        if (mqtt1Conectado || mqtt2Conectado)
        {
            if (MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.IsConnected)
            {
                Debug.Log($"📡 [MQTT Directo] Enviando orden real para pieza {tipoPieza}...");
                MQTT_InterfaceClient.Instance.SendOrder(tipoPieza);
            }
            return;
        }

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

        // Rellenar y refrescar el almacén completo antes de empezar
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

            // 🟢 TRATAMIENTO ESPECIAL PARA EL STOCK DURANTE LA REPRODUCCIÓN:
            if (ev.topic == "f/i/stock")
            {
                // 1. La interfaz (UI) SÍ lee el estado para actualizar los contadores e iconos de la pantalla
                if (MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.isActiveAndEnabled)
                {
                    MQTT_InterfaceClient.Instance.ProcesarMensajeExterno(ev.topic, ev.payloadJson);
                }

                // 2. La escena 3D en Unity (MQTTClient) NO recibe este mensaje, 
                // asegurando que las piezas permanezcan inalterables y llenas en los contenedores.
                try
                {
                    JSON_FullStock stockActualizado = JsonUtility.FromJson<JSON_FullStock>(ev.payloadJson);
                    if (stockActualizado != null) ultimoStockConocido = stockActualizado;
                }
                catch { }

                continue; // Saltamos la inyección general para que el 3D no se entere
            }

            // Resto de topics normales de la simulación
            InyectarMensajeOffline(ev.topic, ev.payloadJson);
        }

        EnEjecucion = false;
        OnEstadoSimulacionOfflineCambiado?.Invoke(false);
        Debug.Log($"<color=green>✅ [Modo Offline] Finalizada reproducción de {secuencia.tipoPieza}. Botones UI habilitados.</color>");
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
        Debug.LogWarning("⚠️ [SimuladorOffline] Simulación detenida de forma forzada. Botones UI habilitados.");
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