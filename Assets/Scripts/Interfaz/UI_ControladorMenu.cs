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

    [Header("Mi Panel Desplegable Principal")]
    public GameObject panelLateral;

    [Header("Cierre al Clicar Fuera")]
    public GameObject fondoCierre;

    [Header("Reloj Digital de la Cabecera (Hora Real)")]
    public TMP_Text textoReloj;

    [Header("Reloj de Simulación BBDD (Debajo del Reloj Principal)")]
    public GameObject panelRelojSimulacion;
    public TMP_Text textoRelojSimulacion;

    [Header("UI Control de Simulación (Cabecera)")]
    public Toggle toggleModoBBDD;             // Toggle de BBDD
    public Toggle toggleModoSimulacion;      // Toggle de Modo Simulación
    public Button btnPlay;                   // Botón PLAY / PAUSE
    public Button btnReset;                  // Botón RESET

    [Header("UI Sección Simulación (Nuevos Botones)")]
    public Button btnSimPedirBlanca;         // Botón pedir Blanca en panel Simulación
    public Button btnSimPedirRoja;           // Botón pedir Roja en panel Simulación
    public Button btnSimPedirAzul;           // Botón pedir Azul en panel Simulación

    [Header("Símbolos y Textos del Botón PLAY / PAUSE")]
    public TMP_Text textoBotonPlay;
    public string simboloPlay = "▶";
    public string simboloPause = "⏸";

    [Header("Botones Multiplicador")]
    public GameObject contenedorMultiplicador;
    public Button btnSpeedX1;
    public Button btnSpeedX2;
    public Button btnSpeedX5;

    [Header("Fecha y Hora - INICIO")]
    public UI_CalendarPicker calendarInicio;
    public TMP_Dropdown dropdownHoraInicio;
    public TMP_Dropdown dropdownMinInicio;
    public TMP_Dropdown dropdownSegInicio;

    [Header("Fecha y Hora - FIN")]
    public UI_CalendarPicker calendarFin;
    public TMP_Dropdown dropdownHoraFin;
    public TMP_Dropdown dropdownMinFin;
    public TMP_Dropdown dropdownSegFin;

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
    public bool EsModoBBDDActivo => modoSeleccionado == ModoOrigen.BaseDeDatos_Historico || modoEnEjecucion == ModoOrigen.BaseDeDatos_Historico;

    // Inhabilita pedir piezas si estamos desconectados del Broker o la fábrica
    public bool PuedePedirPieza => modoSeleccionado == ModoOrigen.MQTT_Directo &&
                                   (modoEnEjecucion == null || modoEnEjecucion == ModoOrigen.MQTT_Directo) &&
                                   !simulacionOfflinePedidoEnCurso &&
                                   !estaDesconectadoMQTT;

    // 🟢 Persistencia de Estado de Menú y Secciones Abiertas
    private static bool panelLateralEstabaAbierto = false;
    public static HashSet<string> seccionesAbiertasPrevias = new HashSet<string>();

    private static bool autoStartPendiente = false;
    private static ModoOrigen autoStartModo = ModoOrigen.MQTT_Directo;
    private static DateTime autoStartFechaIni = DateTime.Today.AddHours(8);
    private static DateTime autoStartFechaFin = DateTime.Today.AddHours(18);

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

    private void OnFactoryHeartbeatRecibido(bool connected, DateTime timestamp)
    {
        if (modoSeleccionado != ModoOrigen.MQTT_Directo) return;

        double desfaseSegundos = Math.Abs((DateTime.UtcNow - timestamp).TotalSeconds);
        bool esMensajeReciente = desfaseSegundos < 5.0;
        bool esMensajeRetenidoDeArranque = Time.timeSinceLevelLoad < 2.0f;

        if (connected && esMensajeReciente)
        {
            if (estaDesconectadoMQTT && esMensajeRetenidoDeArranque)
            {
                return;
            }

            tiempoUltimoHeartbeatReal = Time.realtimeSinceStartup;
            estuvoEnVivoMQTT = true;

            if (estaDesconectadoMQTT)
            {
                estaDesconectadoMQTT = false;
                yaSeRecargoPorDesconexion = false;
                Debug.Log("<color=green><b>🟢 [Fábrica MQTT] ¡Conexión Restablecida! Recargando escena para reinicializar todos los datos...</b></color>");

                if (ControladorSpawnPiecesHBW_mqtt.Instance != null)
                {
                    ControladorSpawnPiecesHBW_mqtt.Instance.ForzarRelecturaStock();
                }

                RecargarEscenaLimpia();
            }
        }
        else if (!connected && esMensajeReciente)
        {
            if (!estaDesconectadoMQTT)
            {
                ProcesarDesconexionFabrica();
            }
        }
    }

    private void OnEstadoSimulacionOfflineCambiado(bool enEjecucion)
    {
        simulacionOfflinePedidoEnCurso = enEjecucion;

        if (enEjecucion)
        {
            modoEnEjecucion = ModoOrigen.Simulacion_Offline;
            esPausado = false;
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

    private void Start()
    {
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

        InicializarControlesTiempo();
        OcultarYColapsarMultiplicadores();

        if (btnPlay != null) { btnPlay.onClick.RemoveAllListeners(); btnPlay.onClick.AddListener(OnBotonPlayPulsado); }
        if (btnReset != null) { btnReset.onClick.RemoveAllListeners(); btnReset.onClick.AddListener(OnBotonResetPulsado); }

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

        if (btnSimPedirBlanca != null) btnSimPedirBlanca.onClick.AddListener(() => PedirPiezaSimulacion("WHITE"));
        if (btnSimPedirRoja != null) btnSimPedirRoja.onClick.AddListener(() => PedirPiezaSimulacion("RED"));
        if (btnSimPedirAzul != null) btnSimPedirAzul.onClick.AddListener(() => PedirPiezaSimulacion("BLUE"));

        Time.timeScale = 1.0f;
        VincularListenersDeCambioEnControles();

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
    }

    private void Update()
    {
        if (textoReloj != null && Time.time - ultimoSegundoActualizado >= 1f)
        {
            ultimoSegundoActualizado = Time.time;
            ActualizarTextoRelojPrincipal(DateTime.Now);
        }

        if (modoSeleccionado == ModoOrigen.MQTT_Directo && (modoEnEjecucion == null || modoEnEjecucion == ModoOrigen.MQTT_Directo))
        {
            if (!estaDesconectadoMQTT)
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
    }

    private void ProcesarDesconexionFabrica()
    {
        if (!estaDesconectadoMQTT)
        {
            estaDesconectadoMQTT = true;
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

            if (SimuladorOffline.Instance != null)
            {
                SimuladorOffline.Instance.ComprobarYConfigurarModoOffline();
            }
        }
        else
        {
            if (toggleModoBBDD == null || !toggleModoBBDD.isOn)
            {
                modoSeleccionado = ModoOrigen.MQTT_Directo;
            }
        }

        ProcesarCambioDeModo();
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
        else
        {
            if (toggleModoSimulacion == null || !toggleModoSimulacion.isOn)
            {
                modoSeleccionado = ModoOrigen.MQTT_Directo;
            }
        }

        ProcesarCambioDeModo();
    }

    private void ProcesarCambioDeModo()
    {
        if (!simulacionOfflinePedidoEnCurso && !simulacionEnCurso)
        {
            esPausado = false;
        }

        if (modoSeleccionado == ModoOrigen.Simulacion_Offline || modoSeleccionado == ModoOrigen.BaseDeDatos_Historico)
        {
            if (MQTTClient.Instance != null) { MQTTClient.Instance.DesconectarRed(); }
            if (MQTT_InterfaceClient.Instance != null) { MQTT_InterfaceClient.Instance.DesconectarRed(); }
        }
        else if (modoSeleccionado == ModoOrigen.MQTT_Directo && !simulacionOfflinePedidoEnCurso && !simulacionEnCurso)
        {
            modoEnEjecucion = ModoOrigen.MQTT_Directo;
            tiempoUltimoHeartbeatReal = Time.realtimeSinceStartup;
            SetVisibilidadRelojSimulacion(false);

            if (MQTTClient.Instance != null)
            {
                MQTTClient.Instance.enabled = true;
                MQTTClient.Instance.Connect();

                if (!MQTTClient.Instance.IsConnected)
                {
                    estaDesconectadoMQTT = true;
                }
            }

            if (MQTT_InterfaceClient.Instance != null) { MQTT_InterfaceClient.Instance.enabled = true; }
        }

        SetUIInteractables(modoSeleccionado == ModoOrigen.BaseDeDatos_Historico);

        bool esBBDD = (modoSeleccionado == ModoOrigen.BaseDeDatos_Historico);
        bool esSim = (modoSeleccionado == ModoOrigen.Simulacion_Offline);
        ActualizarTogglesVisuales(esBBDD, esSim);

        EvaluarEstadoBotonPlay();
        NotificarEstadoPermisoPedido();
        ActualizarEstadoBotonesSeccionSimulacion();
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

        if (btnSimPedirBlanca != null) btnSimPedirBlanca.interactable = sePuedePedirSimulacion;
        if (btnSimPedirRoja != null) btnSimPedirRoja.interactable = sePuedePedirSimulacion;
        if (btnSimPedirAzul != null) btnSimPedirAzul.interactable = sePuedePedirSimulacion;
    }

    private void PedirPiezaSimulacion(string color)
    {
        if (SimuladorOffline.Instance != null && modoSeleccionado == ModoOrigen.Simulacion_Offline)
        {
            SimuladorOffline.Instance.PedirPieza(color);

            if (ControladorSpawnPiecesHBW_mqtt.Instance != null)
            {
                ControladorSpawnPiecesHBW_mqtt.Instance.LlenarAlmacenConTodasLasPiezas();
            }
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
            if (ObtenerRangoFechas(out DateTime fIni, out DateTime fFin))
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

        if (ObtenerRangoFechas(out DateTime fIniSel, out DateTime fFinSel))
        {
            autoStartFechaIni = fIniSel;
            autoStartFechaFin = fFinSel;
        }

        RecargarEscenaLimpia();
    }

    public void OnBotonResetPulsado()
    {
        autoStartPendiente = false;
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

        SetUIInteractables(modoSeleccionado == ModoOrigen.BaseDeDatos_Historico);

        if (modoSeleccionado == ModoOrigen.MQTT_Directo)
        {
            simulacionEnCurso = false;
            fechaIniEnEjecucion = null;
            fechaFinEnEjecucion = null;

            SetVisibilidadRelojSimulacion(false);

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

            if (ObtenerRangoFechas(out DateTime desde, out DateTime hasta))
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

        NotificarEstadoPermisoPedido();
        ActualizarEstadoBotonesSeccionSimulacion();
    }

    private void ConfigurarEstadoPorAutoStart()
    {
        bool esBBDD = (autoStartModo == ModoOrigen.BaseDeDatos_Historico);
        bool esSim = (autoStartModo == ModoOrigen.Simulacion_Offline);

        modoSeleccionado = autoStartModo;
        ActualizarTogglesVisuales(esBBDD, esSim);

        if (calendarInicio != null) calendarInicio.SetFechaInicial(autoStartFechaIni);
        if (calendarFin != null) calendarFin.SetFechaInicial(autoStartFechaFin);

        SetDropdownValor(dropdownHoraInicio, autoStartFechaIni.Hour);
        SetDropdownValor(dropdownMinInicio, autoStartFechaIni.Minute);
        SetDropdownValor(dropdownSegInicio, autoStartFechaIni.Second);

        SetDropdownValor(dropdownHoraFin, autoStartFechaFin.Hour);
        SetDropdownValor(dropdownMinFin, autoStartFechaFin.Minute);
        SetDropdownValor(dropdownSegFin, autoStartFechaFin.Second);

        ArrancarSimulacion();
    }

    private void EvaluarEstadoBotonPlay()
    {
        if (btnPlay == null) return;
        ActualizarVisualBotonPlay();

        if (simulacionOfflinePedidoEnCurso || (modoSeleccionado == ModoOrigen.BaseDeDatos_Historico && simulacionEnCurso))
        {
            btnPlay.interactable = true;
            return;
        }

        if (modoSeleccionado != modoEnEjecucion)
        {
            btnPlay.interactable = true;
            return;
        }

        if (modoSeleccionado == ModoOrigen.BaseDeDatos_Historico)
        {
            if (ObtenerRangoFechas(out DateTime fIni, out DateTime fFin))
            {
                bool hayCambio = (fechaIniEnEjecucion == null || fechaFinEnEjecucion == null) ||
                               (fIni != fechaIniEnEjecucion.Value) ||
                               (fFin != fechaFinEnEjecucion.Value);

                btnPlay.interactable = hayCambio;
                return;
            }
        }

        btnPlay.interactable = false;
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

        if (ObtenerRangoFechas(out DateTime fIni, out DateTime fFin))
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

    private void OcultarYColapsarMultiplicadores()
    {
        if (contenedorMultiplicador != null) contenedorMultiplicador.SetActive(false);
    }

    private void VincularListenersDeCambioEnControles()
    {
        if (dropdownHoraInicio != null) dropdownHoraInicio.onValueChanged.AddListener((_) => EvaluarEstadoBotonPlay());
        if (dropdownMinInicio != null) dropdownMinInicio.onValueChanged.AddListener((_) => EvaluarEstadoBotonPlay());
        if (dropdownSegInicio != null) dropdownSegInicio.onValueChanged.AddListener((_) => EvaluarEstadoBotonPlay());

        if (dropdownHoraFin != null) dropdownHoraFin.onValueChanged.AddListener((_) => EvaluarEstadoBotonPlay());
        if (dropdownMinFin != null) dropdownMinFin.onValueChanged.AddListener((_) => EvaluarEstadoBotonPlay());
        if (dropdownSegFin != null) dropdownSegFin.onValueChanged.AddListener((_) => EvaluarEstadoBotonPlay());

        if (calendarInicio != null) calendarInicio.OnFechaSeleccionada += (_) => EvaluarEstadoBotonPlay();
        if (calendarFin != null) calendarFin.OnFechaSeleccionada += (_) => EvaluarEstadoBotonPlay();
    }

    private void InicializarControlesTiempo()
    {
        List<string> horas = new List<string>();
        for (int i = 0; i < 24; i++) horas.Add(i.ToString("D2"));

        List<string> minSeg = new List<string>();
        for (int i = 0; i < 60; i++) minSeg.Add(i.ToString("D2"));

        PoblarDropdown(dropdownHoraInicio, horas, 8);
        PoblarDropdown(dropdownMinInicio, minSeg, 0);
        PoblarDropdown(dropdownSegInicio, minSeg, 0);

        PoblarDropdown(dropdownHoraFin, horas, 18);
        PoblarDropdown(dropdownMinFin, minSeg, 0);
        PoblarDropdown(dropdownSegFin, minSeg, 0);

        if (calendarInicio != null) calendarInicio.SetFechaInicial(DateTime.Today);
        if (calendarFin != null) calendarFin.SetFechaInicial(DateTime.Today);
    }

    private void PoblarDropdown(TMP_Dropdown dropdown, List<string> opciones, int indiceDefecto)
    {
        if (dropdown == null) return;
        dropdown.ClearOptions();
        dropdown.AddOptions(opciones);
        dropdown.value = Mathf.Clamp(indiceDefecto, 0, opciones.Count - 1);
        dropdown.RefreshShownValue();
    }

    private void SetDropdownValor(TMP_Dropdown dropdown, int valor)
    {
        if (dropdown != null && dropdown.options.Count > 0)
        {
            dropdown.value = Mathf.Clamp(valor, 0, dropdown.options.Count - 1);
            dropdown.RefreshShownValue();
        }
    }

    private bool ObtenerRangoFechas(out DateTime fechaInicio, out DateTime fechaFin)
    {
        fechaInicio = DateTime.Now;
        fechaFin = DateTime.Now;

        try
        {
            DateTime diaIni = (calendarInicio != null) ? calendarInicio.FechaSeleccionada : DateTime.Today;
            int hIni = ObtenerValorDropdown(dropdownHoraInicio, 8);
            int mIni = ObtenerValorDropdown(dropdownMinInicio, 0);
            int sIni = ObtenerValorDropdown(dropdownSegInicio, 0);
            fechaInicio = new DateTime(diaIni.Year, diaIni.Month, diaIni.Day, hIni, mIni, sIni);

            DateTime diaFin = (calendarFin != null) ? calendarFin.FechaSeleccionada : DateTime.Today;
            int hFin = ObtenerValorDropdown(dropdownHoraFin, 18);
            int mFin = ObtenerValorDropdown(dropdownMinFin, 0);
            int sFin = ObtenerValorDropdown(dropdownSegFin, 0);
            fechaFin = new DateTime(diaFin.Year, diaFin.Month, diaFin.Day, hFin, mFin, sFin);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private int ObtenerValorDropdown(TMP_Dropdown dropdown, int valorPorDefecto)
    {
        if (dropdown != null && dropdown.options.Count > dropdown.value)
        {
            if (int.TryParse(dropdown.options[dropdown.value].text, out int res))
                return res;
        }
        return valorPorDefecto;
    }

    private void SetUIInteractables(bool estado)
    {
        if (calendarInicio != null) calendarInicio.SetInteractable(estado);
        if (calendarFin != null) calendarFin.SetInteractable(estado);

        if (dropdownHoraInicio != null) dropdownHoraInicio.interactable = estado;
        if (dropdownMinInicio != null) dropdownMinInicio.interactable = estado;
        if (dropdownSegInicio != null) dropdownSegInicio.interactable = estado;

        if (dropdownHoraFin != null) dropdownHoraFin.interactable = estado;
        if (dropdownMinFin != null) dropdownMinFin.interactable = estado;
        if (dropdownSegFin != null) dropdownSegFin.interactable = estado;
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
        else
        {
            UI_SeccionAcordeon.CerrarCualquierSeccionAbierta();
        }
    }
}