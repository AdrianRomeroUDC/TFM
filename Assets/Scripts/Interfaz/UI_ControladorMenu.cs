using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

public class UI_ControladorMenu : MonoBehaviour
{
    private static UI_ControladorMenu instance;
    public static UI_ControladorMenu Instance => instance;

    public static event Action<bool> OnRelojSimulacionVisibilidadCambiada;
    public static bool EsRelojSimulacionVisible { get; private set; } = false;

    public static event Action<bool> OnEstadoPermisoPedidoCambiado;

    public enum ModoOrigen { MQTT_Directo, Simulacion_Offline, BaseDeDatos_Historico }
    public enum EstadoSimulacion { Detenido, Reproduciendo }

    [Header("Referencias a Subsecciones")]
    public UI_SeccionBBDD seccionBBDD;
    public UI_SeccionSimulacion seccionSimulacion;

    [Header("Mi Panel Desplegable Principal")]
    public GameObject panelLateral;

    [Header("Cierre al Clicar Fuera")]
    public GameObject fondoCierre;

    [Header("Panel Informativo de Modo (Azul)")]
    public TMP_Text txtModoTitulo;
    public TMP_Text txtModoSubtitulo;

    [Header("Icono Informativo de Cambio Pendiente (Tooltip)")]
    public GameObject iconoInfoPlay;

    [Header("Reloj Digital de la Cabecera (Hora Real)")]
    public TMP_Text textoReloj;

    [Header("Reloj de Simulación BBDD")]
    public GameObject panelRelojSimulacion;
    public TMP_Text textoRelojSimulacion;

    [Header("UI Control de Simulación (Cabecera)")]
    public Toggle toggleModoBBDD;
    public Toggle toggleModoSimulacion;
    public Button btnPlay;
    public Button btnReset;

    [Header("Símbolos y Textos del Botón PLAY / PAUSE")]
    public TMP_Text textoBotonPlay;
    public string simboloPlay = "▶";
    public string simboloPause = "⏸";

    [Header("Ajustes de Reproducción BBDD")]
    public float multiplicadorVelocidad = 1.0f;

    [Header("Estado Actual (Lectura)")]
    public ModoOrigen modoSeleccionado = ModoOrigen.MQTT_Directo;
    public EstadoSimulacion estadoActual = EstadoSimulacion.Detenido;

    public ModoOrigen? modoEnEjecucion = null;
    private DateTime? fechaIniEnEjecucion = null;
    private DateTime? fechaFinEnEjecucion = null;

    private RectTransform rectPanel;
    private RectTransform rectSecciones;
    private float ultimoSegundoActualizado = -1f;
    private Coroutine corrutinaReplayBBDD;

    private bool simulacionEnCurso = false;
    private bool simulacionOfflinePedidoEnCurso = false;
    private bool esPausado = false;

    public bool EsPausado => esPausado;
    public bool EsModoSimulacionActivo => modoSeleccionado == ModoOrigen.Simulacion_Offline;
    public bool EsModoBBDDActivo => modoSeleccionado == ModoOrigen.BaseDeDatos_Historico;

    public bool PuedePedirPieza => (modoEnEjecucion == null || modoEnEjecucion == ModoOrigen.MQTT_Directo) &&
                                   !simulacionOfflinePedidoEnCurso &&
                                   !estaDesconectadoMQTT;

    // Persistencia de Estado de Menú
    private static bool panelLateralEstabaAbierto = false;
    public static HashSet<string> seccionesAbiertasPrevias = new HashSet<string>();

    private static bool autoStartPendiente = false;
    private static ModoOrigen autoStartModo = ModoOrigen.MQTT_Directo;
    private static DateTime autoStartFechaIni = DateTime.Today.AddHours(8);
    private static DateTime autoStartFechaFin = DateTime.Today.AddHours(18);
    private static string autoStartPiezaSimulacion = null;

    // Control global y local de desconexión
    public static bool estaDesconectadoMQTT = false;
    private static bool yaSeRecargoPorDesconexion = false;
    private bool estuvoEnVivoMQTT = false;

    private float tiempoUltimoHeartbeatReal = -1f;

    [Tooltip("Tiempo en segundos sin recibir heartbeat para considerar la fábrica desconectada")]
    public float timeoutHeartbeatSegundos = 5.0f;

    private void Awake()
    {
        if (instance == null) instance = this;

        if (seccionBBDD == null)
        {
            seccionBBDD = GetComponentInChildren<UI_SeccionBBDD>();
            Debug.Log($"🔍 [UI_ControladorMenu] Búsqueda automática de UI_SeccionBBDD -> {(seccionBBDD != null ? "Encontrado" : "NO ENCONTRADO")}");
        }

        if (seccionSimulacion == null)
        {
            seccionSimulacion = GetComponentInChildren<UI_SeccionSimulacion>();
            Debug.Log($"🔍 [UI_ControladorMenu] Búsqueda automática de UI_SeccionSimulacion -> {(seccionSimulacion != null ? "Encontrado" : "NO ENCONTRADO")}");
        }
    }

    private void OnEnable()
    {
        SimuladorOffline.OnEstadoSimulacionOfflineCambiado += OnEstadoSimulacionOfflineCambiado;

        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnFactoryHeartbeatEvent += OnFactoryHeartbeatRecibido;
        }
    }

    private void OnDisable()
    {
        SimuladorOffline.OnEstadoSimulacionOfflineCambiado -= OnEstadoSimulacionOfflineCambiado;

        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnFactoryHeartbeatEvent -= OnFactoryHeartbeatRecibido;
        }
    }

    private void Start()
    {
        Debug.Log("🚀 [UI_ControladorMenu] Start() iniciado.");
        simulacionEnCurso = false;
        esPausado = false;
        estuvoEnVivoMQTT = false;
        tiempoUltimoHeartbeatReal = Time.realtimeSinceStartup;

        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnFactoryHeartbeatEvent -= OnFactoryHeartbeatRecibido;
            MQTTClient.Instance.OnFactoryHeartbeatEvent += OnFactoryHeartbeatRecibido;
        }

        if (textoBotonPlay == null && btnPlay != null)
            textoBotonPlay = btnPlay.GetComponentInChildren<TMP_Text>();

        if (panelLateral != null)
        {
            rectPanel = panelLateral.GetComponent<RectTransform>();
            VerticalLayoutGroup layout = panelLateral.GetComponentInChildren<VerticalLayoutGroup>();
            if (layout != null) rectSecciones = layout.GetComponent<RectTransform>();

            CambiarEstadoMenu(panelLateralEstabaAbierto);
        }

        if (fondoCierre != null && !panelLateralEstabaAbierto)
        {
            fondoCierre.SetActive(false);
        }

        if (!autoStartPendiente)
        {
            SetVisibilidadRelojSimulacion(false);
        }

        // Inicializar Subsecciones
        if (seccionBBDD != null)
        {
            Debug.Log("📌 [UI_ControladorMenu] Llamando a seccionBBDD.Inicializar()...");
            seccionBBDD.Inicializar(() => EvaluarEstadoBotonPlay());
        }
        else
        {
            Debug.LogError("❌ [UI_ControladorMenu] CRÍTICO: 'seccionBBDD' es NULL en UI_ControladorMenu. No se pueden inicializar los desplegables de tiempo!");
        }

        if (seccionSimulacion != null) seccionSimulacion.Inicializar();

        // Listeners Botones Principales
        if (btnPlay != null) { btnPlay.onClick.RemoveAllListeners(); btnPlay.onClick.AddListener(OnBotonPlayPulsado); }
        if (btnReset != null) { btnReset.onClick.RemoveAllListeners(); btnReset.onClick.AddListener(OnBotonResetPulsado); }

        // Listeners Toggles Excluyentes
        if (toggleModoBBDD != null)
        {
            toggleModoBBDD.onValueChanged.RemoveAllListeners();
            toggleModoBBDD.onValueChanged.AddListener(OnToggleBBDDCambiado);
        }

        if (toggleModoSimulacion != null)
        {
            toggleModoSimulacion.onValueChanged.RemoveAllListeners();
            toggleModoSimulacion.onValueChanged.AddListener(OnToggleSimulacionCambiado);
        }

        Time.timeScale = 1.0f;

        if (autoStartPendiente)
        {
            autoStartPendiente = false;
            ConfigurarEstadoPorAutoStart();
        }
        else
        {
            modoSeleccionado = ModoOrigen.MQTT_Directo;
            ActualizarTogglesVisuales(false, false);
            ArrancarSimulacion();
        }

        ActualizarPanelInformativoModo();
        NotificarEstadoPermisoPedido();
    }

    private void Update()
    {
        if (textoReloj != null && Time.time - ultimoSegundoActualizado >= 1f)
        {
            ultimoSegundoActualizado = Time.time;
            ActualizarTextoRelojPrincipal(DateTime.Now);
        }

        // Watchdog MQTT
        if (modoEnEjecucion == ModoOrigen.MQTT_Directo && !estaDesconectadoMQTT)
        {
            bool brokerDesconectado = (MQTTClient.Instance != null && !MQTTClient.Instance.IsConnected);
            float transcurrido = Time.realtimeSinceStartup - tiempoUltimoHeartbeatReal;
            bool timeoutHeartbeat = (transcurrido > timeoutHeartbeatSegundos);

            if (brokerDesconectado || timeoutHeartbeat)
            {
                ProcesarDesconexionFabrica();
            }
        }
    }

    private void OnFactoryHeartbeatRecibido(bool connected, DateTime timestamp)
    {
        if (modoEnEjecucion.HasValue && modoEnEjecucion.Value != ModoOrigen.MQTT_Directo) return;

        double desfaseSegundos = Math.Abs((DateTime.UtcNow - timestamp).TotalSeconds);
        bool esMensajeReciente = desfaseSegundos < 5.0;
        bool esMensajeRetenidoDeArranque = Time.timeSinceLevelLoad < 2.0f;

        if (connected && esMensajeReciente)
        {
            if (estaDesconectadoMQTT && esMensajeRetenidoDeArranque) return;

            tiempoUltimoHeartbeatReal = Time.realtimeSinceStartup;
            estuvoEnVivoMQTT = true;

            if (estaDesconectadoMQTT)
            {
                estaDesconectadoMQTT = false;
                yaSeRecargoPorDesconexion = false;
                Debug.Log("<color=green><b>🟢 [Fábrica MQTT] ¡Conexión Restablecida!</b></color>");
                ActualizarPanelInformativoModo();
                NotificarEstadoPermisoPedido();

                if (MQTTClient.Instance != null)
                {
                    MQTTClient.Instance.ReemitirUltimoStock();
                }
            }
        }
        else if (!connected && esMensajeReciente && !estaDesconectadoMQTT)
        {
            ProcesarDesconexionFabrica();
        }
    }

    private void OnEstadoSimulacionOfflineCambiado(bool enEjecucion)
    {
        simulacionOfflinePedidoEnCurso = enEjecucion;

        if (enEjecucion)
        {
            modoEnEjecucion = ModoOrigen.Simulacion_Offline;
            esPausado = false;
            ActualizarPanelInformativoModo();
        }
        else
        {
            esPausado = false;
            if (modoEnEjecucion == ModoOrigen.Simulacion_Offline)
            {
                modoEnEjecucion = modoSeleccionado;
            }
        }

        if (modoSeleccionado == ModoOrigen.Simulacion_Offline)
        {
            ActualizarTogglesVisuales(false, true);
        }

        EvaluarEstadoBotonPlay();
        NotificarEstadoPermisoPedido();
        ActualizarEstadoBotonesSeccionSimulacion();
    }

    private void ProcesarDesconexionFabrica()
    {
        if (!estaDesconectadoMQTT)
        {
            estaDesconectadoMQTT = true;
            ActualizarPanelInformativoModo();
            NotificarEstadoPermisoPedido();

            if (estuvoEnVivoMQTT && !yaSeRecargoPorDesconexion)
            {
                yaSeRecargoPorDesconexion = true;
                estuvoEnVivoMQTT = false;
                Debug.LogWarning($"⚠️ [Fábrica MQTT] Desconexión en vivo detectada ({timeoutHeartbeatSegundos}s sin respuesta). Recargando escena una sola vez...");
                RecargarEscenaLimpia();
            }
        }
    }

    public void OnToggleSimulacionCambiado(bool activo)
    {
        if (activo)
        {
            if (toggleModoBBDD != null && toggleModoBBDD.isOn)
            {
                toggleModoBBDD.SetIsOnWithoutNotify(false);
                UI_ToggleSwitch sw = toggleModoBBDD.GetComponent<UI_ToggleSwitch>();
                if (sw != null) sw.ActualizarEstadoInstantaneo(false);
            }
            modoSeleccionado = ModoOrigen.Simulacion_Offline;
        }
        else if (toggleModoBBDD == null || !toggleModoBBDD.isOn)
        {
            modoSeleccionado = ModoOrigen.MQTT_Directo;
        }

        ProcesarCambioDeSeleccionToggle();
    }

    public void OnToggleBBDDCambiado(bool activo)
    {
        if (activo)
        {
            if (toggleModoSimulacion != null && toggleModoSimulacion.isOn)
            {
                toggleModoSimulacion.SetIsOnWithoutNotify(false);
                UI_ToggleSwitch sw = toggleModoSimulacion.GetComponent<UI_ToggleSwitch>();
                if (sw != null) sw.ActualizarEstadoInstantaneo(false);
            }
            modoSeleccionado = ModoOrigen.BaseDeDatos_Historico;
        }
        else if (toggleModoSimulacion == null || !toggleModoSimulacion.isOn)
        {
            modoSeleccionado = ModoOrigen.MQTT_Directo;
        }

        ProcesarCambioDeSeleccionToggle();
    }

    private void ProcesarCambioDeSeleccionToggle()
    {
        if (seccionBBDD != null) seccionBBDD.SetUIInteractables(modoSeleccionado == ModoOrigen.BaseDeDatos_Historico);

        bool esBBDD = (modoSeleccionado == ModoOrigen.BaseDeDatos_Historico);
        bool esSim = (modoSeleccionado == ModoOrigen.Simulacion_Offline);
        ActualizarTogglesVisuales(esBBDD, esSim);

        ActualizarPanelInformativoModo();
        EvaluarEstadoBotonPlay();
        NotificarEstadoPermisoPedido();
        ActualizarEstadoBotonesSeccionSimulacion();
    }

    private void ActualizarPanelInformativoModo()
    {
        if (txtModoTitulo == null || txtModoSubtitulo == null) return;

        switch (modoSeleccionado)
        {
            case ModoOrigen.MQTT_Directo:
                if (estaDesconectadoMQTT)
                {
                    txtModoTitulo.text = "<color=#000000>●</color> Desconectado (Fábrica Real)";
                    txtModoSubtitulo.text = "Sin respuesta de la fábrica por MQTT.";
                }
                else
                {
                    txtModoTitulo.text = "<color=#FF4D4D>●</color> En Vivo (Fábrica Real)";
                    txtModoSubtitulo.text = "Sincronizado en tiempo real por MQTT.";
                }
                break;

            case ModoOrigen.BaseDeDatos_Historico:
                txtModoTitulo.text = "<color=#FFC107>●</color> Histórico (Base de Datos)";
                txtModoSubtitulo.text = "Reproduciendo datos de InfluxDB.";
                break;

            case ModoOrigen.Simulacion_Offline:
                txtModoTitulo.text = "<color=#00E676>●</color> Simulación Local";
                txtModoSubtitulo.text = "Ejecución de acciones offline.";
                break;
        }
    }

    private void ActualizarTogglesVisuales(bool bbddActivo, bool simulacionActiva)
    {
        if (toggleModoBBDD != null)
        {
            toggleModoBBDD.SetIsOnWithoutNotify(bbddActivo);
            UI_ToggleSwitch sw = toggleModoBBDD.GetComponent<UI_ToggleSwitch>();
            if (sw != null) sw.ActualizarEstadoInstantaneo(bbddActivo);
        }

        if (toggleModoSimulacion != null)
        {
            toggleModoSimulacion.SetIsOnWithoutNotify(simulacionActiva);
            UI_ToggleSwitch sw = toggleModoSimulacion.GetComponent<UI_ToggleSwitch>();
            if (sw != null) sw.ActualizarEstadoInstantaneo(simulacionActiva);
        }
    }

    private void ActualizarEstadoBotonesSeccionSimulacion()
    {
        bool sePuedePedirSimulacion = (modoSeleccionado == ModoOrigen.Simulacion_Offline) && !simulacionOfflinePedidoEnCurso;
        if (seccionSimulacion != null)
        {
            seccionSimulacion.ActualizarEstadoBotones(sePuedePedirSimulacion);
        }
    }

    public void PedirPiezaSimulacion(string color)
    {
        if (modoEnEjecucion != ModoOrigen.Simulacion_Offline)
        {
            autoStartPendiente = true;
            autoStartModo = ModoOrigen.Simulacion_Offline;
            autoStartPiezaSimulacion = color;
            RecargarEscenaLimpia();
            return;
        }

        if (SimuladorOffline.Instance != null && modoSeleccionado == ModoOrigen.Simulacion_Offline)
        {
            SimuladorOffline.Instance.PedirPieza(color);
        }
    }

    public void NotificarEstadoPermisoPedido()
    {
        OnEstadoPermisoPedidoCambiado?.Invoke(PuedePedirPieza);
    }

    public void OnBotonPlayPulsado()
    {
        bool hayCambioModo = (modoEnEjecucion.HasValue && modoSeleccionado != modoEnEjecucion.Value);

        if (simulacionOfflinePedidoEnCurso && !hayCambioModo)
        {
            esPausado = !esPausado;
            ActualizarVisualBotonPlay();
            return;
        }

        if (modoSeleccionado == ModoOrigen.BaseDeDatos_Historico && modoEnEjecucion == ModoOrigen.BaseDeDatos_Historico && simulacionEnCurso && !hayCambioModo)
        {
            if (seccionBBDD != null && seccionBBDD.ObtenerRangoFechas(out DateTime fIni, out DateTime fFin))
            {
                bool hayCambioFechas = (fechaIniEnEjecucion == null || fechaFinEnEjecucion == null) ||
                                       (fIni != fechaIniEnEjecucion.Value) ||
                                       (fFin != fechaFinEnEjecucion.Value);

                if (hayCambioFechas)
                {
                    autoStartPendiente = true;
                    autoStartModo = modoSeleccionado;
                    autoStartFechaIni = fIni;
                    autoStartFechaFin = fFin;
                    RecargarEscenaLimpia();
                    return;
                }
            }

            esPausado = !esPausado;
            ActualizarVisualBotonPlay();
            return;
        }

        autoStartPendiente = true;
        autoStartModo = modoSeleccionado;

        if (seccionBBDD != null && seccionBBDD.ObtenerRangoFechas(out DateTime fIniSel, out DateTime fFinSel))
        {
            autoStartFechaIni = fIniSel;
            autoStartFechaFin = fFinSel;
        }

        RecargarEscenaLimpia();
    }

    public void OnBotonResetPulsado()
    {
        if (seccionBBDD != null) seccionBBDD.ObtenerRangoFechas(out _, out _);

        autoStartPendiente = false;
        autoStartPiezaSimulacion = null;

        bool estaConectadoMQTT = (MQTTClient.Instance != null && MQTTClient.Instance.IsConnected);
        estaDesconectadoMQTT = !estaConectadoMQTT;
        yaSeRecargoPorDesconexion = false;

        panelLateralEstabaAbierto = false;
        seccionesAbiertasPrevias.Clear();

        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.enabled = true;
            MQTTClient.Instance.Connect();
        }

        RecargarEscenaLimpia();
    }

    private void ArrancarSimulacion()
    {
        estadoActual = EstadoSimulacion.Reproduciendo;
        modoEnEjecucion = modoSeleccionado;
        esPausado = false;
        Time.timeScale = 1.0f;

        if (seccionBBDD != null) seccionBBDD.SetUIInteractables(modoSeleccionado == ModoOrigen.BaseDeDatos_Historico);

        if (modoSeleccionado == ModoOrigen.MQTT_Directo)
        {
            simulacionEnCurso = false;
            fechaIniEnEjecucion = null;
            fechaFinEnEjecucion = null;

            SetVisibilidadRelojSimulacion(false);

            if (ControladorSpawnPiecesHBW_mqtt.Instance != null)
            {
                ControladorSpawnPiecesHBW_mqtt.Instance.ForzarRelecturaStock();
            }

            if (MQTTClient.Instance != null)
            {
                MQTTClient.Instance.enabled = true;
                MQTTClient.Instance.Connect();

                if (!MQTTClient.Instance.IsConnected)
                {
                    estaDesconectadoMQTT = true;
                }
            }
            else
            {
                estaDesconectadoMQTT = true;
            }

            EvaluarEstadoBotonPlay();
        }
        else if (modoSeleccionado == ModoOrigen.Simulacion_Offline)
        {
            simulacionEnCurso = false;
            fechaIniEnEjecucion = null;
            fechaFinEnEjecucion = null;

            SetVisibilidadRelojSimulacion(false);

            if (MQTTClient.Instance != null) { MQTTClient.Instance.enabled = true; MQTTClient.Instance.DesconectarRed(); }
            if (MQTT_InterfaceClient.Instance != null) { MQTT_InterfaceClient.Instance.enabled = true; MQTT_InterfaceClient.Instance.DesconectarRed(); }

            if (ControladorSpawnPiecesHBW_mqtt.Instance != null)
            {
                ControladorSpawnPiecesHBW_mqtt.Instance.LlenarAlmacenConTodasLasPiezas();
            }

            if (SimuladorOffline.Instance != null)
            {
                SimuladorOffline.Instance.ComprobarYConfigurarModoOffline();
            }

            EvaluarEstadoBotonPlay();
        }
        else if (modoSeleccionado == ModoOrigen.BaseDeDatos_Historico)
        {
            if (SimuladorOffline.Instance != null)
            {
                SimuladorOffline.Instance.DetenerSimulacionForzada();
            }

            if (MQTTClient.Instance != null) { MQTTClient.Instance.enabled = true; MQTTClient.Instance.DesconectarRed(); }
            if (MQTT_InterfaceClient.Instance != null) { MQTT_InterfaceClient.Instance.enabled = true; MQTT_InterfaceClient.Instance.DesconectarRed(); }

            if (seccionBBDD != null && seccionBBDD.ObtenerRangoFechas(out DateTime desde, out DateTime hasta))
            {
                fechaIniEnEjecucion = desde;
                fechaFinEnEjecucion = hasta;
                simulacionEnCurso = true;

                SetVisibilidadRelojSimulacion(true);
                ActualizarTextoRelojSimulacion(desde);

                EvaluarEstadoBotonPlay();
                corrutinaReplayBBDD = StartCoroutine(ProcesarHistoricoBBDD(desde, hasta));
            }
        }

        ActualizarPanelInformativoModo();
        NotificarEstadoPermisoPedido();
        ActualizarEstadoBotonesSeccionSimulacion();
    }

    private void ConfigurarEstadoPorAutoStart()
    {
        bool esBBDD = (autoStartModo == ModoOrigen.BaseDeDatos_Historico);
        bool esSim = (autoStartModo == ModoOrigen.Simulacion_Offline);

        modoSeleccionado = autoStartModo;
        ActualizarTogglesVisuales(esBBDD, esSim);

        if (seccionBBDD != null)
        {
            seccionBBDD.ConfigurarEstadoPorAutoStart(autoStartFechaIni, autoStartFechaFin);
        }

        string piezaAPedir = autoStartPiezaSimulacion;
        autoStartPiezaSimulacion = null;

        ArrancarSimulacion();

        if (!string.IsNullOrEmpty(piezaAPedir) && SimuladorOffline.Instance != null)
        {
            SimuladorOffline.Instance.PedirPieza(piezaAPedir);
        }

        ActualizarPanelInformativoModo();
    }

    public void EvaluarEstadoBotonPlay()
    {
        if (btnPlay == null) return;
        ActualizarVisualBotonPlay();
        ActualizarIconoInfoPlay();

        if (simulacionOfflinePedidoEnCurso)
        {
            btnPlay.interactable = true;
            return;
        }

        bool hayCambioModoPendiente = (modoEnEjecucion.HasValue && modoSeleccionado != modoEnEjecucion.Value);
        if (hayCambioModoPendiente)
        {
            btnPlay.interactable = true;
            return;
        }

        if (modoSeleccionado == ModoOrigen.Simulacion_Offline)
        {
            btnPlay.interactable = true;
            return;
        }

        if (modoSeleccionado == ModoOrigen.BaseDeDatos_Historico)
        {
            if (simulacionEnCurso || modoEnEjecucion != ModoOrigen.BaseDeDatos_Historico)
            {
                btnPlay.interactable = true;
                return;
            }

            if (seccionBBDD != null && seccionBBDD.ObtenerRangoFechas(out DateTime fIni, out DateTime fFin))
            {
                bool hayCambio = (fechaIniEnEjecucion == null || fechaFinEnEjecucion == null) ||
                                 (fIni != fechaIniEnEjecucion.Value) ||
                                 (fFin != fechaFinEnEjecucion.Value);

                btnPlay.interactable = hayCambio;
                return;
            }
        }

        if (modoSeleccionado == ModoOrigen.MQTT_Directo)
        {
            btnPlay.interactable = false;
            return;
        }

        btnPlay.interactable = false;
    }

    private void ActualizarIconoInfoPlay()
    {
        if (iconoInfoPlay != null)
        {
            iconoInfoPlay.SetActive(true);
        }
    }

    private void ActualizarVisualBotonPlay()
    {
        if (textoBotonPlay == null && btnPlay != null)
            textoBotonPlay = btnPlay.GetComponentInChildren<TMP_Text>();

        if (textoBotonPlay != null)
        {
            bool hayCambioFechas = HayCambioEnFechasEnEjecucion();
            bool hayCambioModo = (modoEnEjecucion.HasValue && modoSeleccionado != modoEnEjecucion.Value);

            bool mostrarPausa = ((modoSeleccionado == ModoOrigen.Simulacion_Offline && simulacionOfflinePedidoEnCurso)
                              || (modoSeleccionado == ModoOrigen.BaseDeDatos_Historico && simulacionEnCurso))
                              && !esPausado
                              && !hayCambioFechas
                              && !hayCambioModo;

            textoBotonPlay.text = mostrarPausa ? simboloPause : simboloPlay;
        }
    }

    private bool HayCambioEnFechasEnEjecucion()
    {
        if (modoEnEjecucion != ModoOrigen.BaseDeDatos_Historico) return false;
        if (fechaIniEnEjecucion == null || fechaFinEnEjecucion == null) return false;

        if (seccionBBDD != null && seccionBBDD.ObtenerRangoFechas(out DateTime fIni, out DateTime fFin))
        {
            return (fIni != fechaIniEnEjecucion.Value) || (fFin != fechaFinEnEjecucion.Value);
        }
        return false;
    }

    private void RecargarEscenaLimpia()
    {
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private IEnumerator ProcesarHistoricoBBDD(DateTime desde, DateTime hasta)
    {
        if (InfluxDBClient.Instance != null)
        {
            yield return StartCoroutine(
                InfluxDBClient.Instance.DescargarYReproducirHistorico(
                    desde,
                    hasta,
                    () => esPausado ? 0f : multiplicadorVelocidad,
                    (horaMuestra) => ActualizarTextoRelojSimulacion(horaMuestra)
                )
            );
        }

        simulacionEnCurso = false;
        esPausado = false;
        EvaluarEstadoBotonPlay();
    }

    private void SetVisibilidadRelojSimulacion(bool visible)
    {
        EsRelojSimulacionVisible = visible;
        if (panelRelojSimulacion != null) panelRelojSimulacion.SetActive(visible);
        OnRelojSimulacionVisibilidadCambiada?.Invoke(visible);
    }

    private void ActualizarTextoRelojPrincipal(DateTime fechaHora)
    {
        if (textoReloj != null)
            textoReloj.text = fechaHora.ToString("dd / MM / yyyy") + "\n" + fechaHora.ToString("HH:mm:ss");
    }

    private void ActualizarTextoRelojSimulacion(DateTime fechaHora)
    {
        if (textoRelojSimulacion != null)
            textoRelojSimulacion.text = fechaHora.ToString("dd / MM / yyyy") + "\n" + fechaHora.ToString("HH:mm:ss");
    }

    public void ToggleMenu()
    {
        if (panelLateral != null) CambiarEstadoMenu(!panelLateral.activeSelf);
    }

    public void CerrarDesdeFuera()
    {
        CambiarEstadoMenu(false);
    }

    private void CambiarEstadoMenu(bool activar)
    {
        panelLateralEstabaAbierto = activar;

        if (panelLateral != null) panelLateral.SetActive(activar);
        if (fondoCierre != null) fondoCierre.SetActive(activar);

        if (activar)
        {
            if (rectSecciones != null) LayoutRebuilder.MarkLayoutForRebuild(rectSecciones);
            if (rectPanel != null) LayoutRebuilder.MarkLayoutForRebuild(rectPanel);
        }
    }
}