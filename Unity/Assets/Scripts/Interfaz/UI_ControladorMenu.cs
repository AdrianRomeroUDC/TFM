using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Es el "cerebro" central de toda la interfaz de usuario del gemelo digital. Controla el menú
/// lateral desplegable, el botón PLAY/PAUSE, el reloj de cabecera y el reloj de simulación, y
/// decide en todo momento en qué "modo de origen" está trabajando la aplicación:
/// <list type="bullet">
/// <item><description><b>MQTT_Directo</b>: viendo la fábrica real en vivo por MQTT.</description></item>
/// <item><description><b>Simulacion_Offline</b>: reproduciendo una simulación local sin conexión real (ver <c>SimuladorOffline</c>).</description></item>
/// <item><description><b>BaseDeDatos_Historico</b>: reproduciendo un histórico guardado en InfluxDB entre dos fechas (ver <c>InfluxDBClient</c>).</description></item>
/// </list>
/// Como cambiar de modo implica reconectar/desconectar los clientes MQTT y reiniciar contadores,
/// este script resuelve el cambio recargando la escena completa (<see cref="RecargarEscenaLimpia"/>)
/// y usando variables estáticas para "recordar" qué modo y qué fechas había elegido el usuario antes
/// de la recarga (patrón de auto-arranque). También vigila mediante un heartbeat si la fábrica real
/// sigue conectada, y avisa a <see cref="InfluxDBClient"/> (a través de <see cref="EsPausado"/>) de
/// si debe pausar la inyección de mensajes históricos.
/// </summary>
public class UI_ControladorMenu : MonoBehaviour
{
    // Patrón Singleton: solo debe existir un UI_ControladorMenu en la escena,
    // accesible desde cualquier script como "UI_ControladorMenu.Instance".
    private static UI_ControladorMenu instance;
    public static UI_ControladorMenu Instance => instance;

    // Evento estático que avisa a otros paneles (como UI_CameraController) cuando el reloj de
    // simulación aparece o desaparece, para que puedan reubicarse en pantalla.
    public static event Action<bool> OnRelojSimulacionVisibilidadCambiada;
    public static bool EsRelojSimulacionVisible { get; private set; } = false;

    // Evento estático que avisa a los paneles de pedido (como UI_SeccionSimulacion) de si en este
    // momento está permitido pedir una pieza nueva.
    public static event Action<bool> OnEstadoPermisoPedidoCambiado;

    // Los tres orígenes posibles de los datos que mueven el gemelo digital.
    public enum ModoOrigen { MQTT_Directo, Simulacion_Offline, BaseDeDatos_Historico }
    // Si la reproducción (de simulación o histórico) está detenida o en marcha.
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
    public ModoOrigen modoSeleccionado = ModoOrigen.MQTT_Directo; // Lo que el usuario tiene elegido en los toggles ahora mismo.
    public EstadoSimulacion estadoActual = EstadoSimulacion.Detenido;

    public ModoOrigen? modoEnEjecucion = null; // El modo que realmente está corriendo (puede diferir del seleccionado si hay un cambio pendiente de aplicar con PLAY).
    private DateTime? fechaIniEnEjecucion = null;
    private DateTime? fechaFinEnEjecucion = null;

    private RectTransform rectPanel;
    private RectTransform rectSecciones;
    private float ultimoSegundoActualizado = -1f;
    private Coroutine corrutinaReplayBBDD;

    private bool simulacionEnCurso = false; // true mientras se está reproduciendo un histórico de BBDD.
    private bool simulacionOfflinePedidoEnCurso = false; // true mientras SimuladorOffline está tramitando un pedido.
    private bool esPausado = false; // true si el usuario ha pulsado PAUSE sobre una reproducción en marcha.

    // Lo consulta InfluxDBClient para saber si debe congelar la inyección de mensajes históricos.
    public bool EsPausado => esPausado;
    public bool EsModoSimulacionActivo => modoSeleccionado == ModoOrigen.Simulacion_Offline;
    public bool EsModoBBDDActivo => modoSeleccionado == ModoOrigen.BaseDeDatos_Historico;

    // Solo se puede pedir una pieza nueva si estamos en modo MQTT directo (o sin modo aún), no hay
    // ya un pedido de simulación offline en curso, y la fábrica real no está desconectada.
    public bool PuedePedirPieza => (modoEnEjecucion == null || modoEnEjecucion == ModoOrigen.MQTT_Directo) &&
                                   !simulacionOfflinePedidoEnCurso &&
                                   !estaDesconectadoMQTT;

    // Persistencia de Estado de Menú y Panel Azul
    // Estas variables son "static" a propósito: como el cambio de modo recarga la escena entera,
    // necesitamos guardar aquí si el menú lateral estaba abierto y qué secciones tenía desplegadas,
    // para restaurar el mismo aspecto justo después de la recarga.
    private static bool panelLateralEstabaAbierto = false;
    public static HashSet<string> seccionesAbiertasPrevias = new HashSet<string>();

    // Persistencia estática del texto informativo para evitar parpadeos al pulsar Reset
    private static string ultimoTituloGuardado = null;
    private static string ultimoSubtituloGuardado = null;

    // Estas variables "static" son el mecanismo de auto-arranque: antes de recargar la escena
    // (por ejemplo al pulsar PLAY), se guarda aquí qué modo y qué fechas hay que restaurar nada
    // más arrancar de nuevo, ya que una recarga de escena destruye todos los objetos y sus datos locales.
    private static bool autoStartPendiente = false;
    private static ModoOrigen autoStartModo = ModoOrigen.MQTT_Directo;
    private static DateTime autoStartFechaIni = DateTime.Today.AddHours(8);
    private static DateTime autoStartFechaFin = DateTime.Today.AddHours(18);
    private static string autoStartPiezaSimulacion = null;

    // Control global y local de desconexión
    public static bool estaDesconectadoMQTT = false;
    private static bool yaSeRecargoPorDesconexion = false; // Evita recargar la escena en bucle si la desconexión se mantiene.
    private bool estuvoEnVivoMQTT = false; // Recuerda si llegamos a tener conexión real, para distinguir "nunca conectó" de "se cortó la conexión".

    private float tiempoUltimoHeartbeatReal = -1f;

    [Tooltip("Tiempo en segundos sin recibir heartbeat para considerar la fábrica desconectada")]
    public float timeoutHeartbeatSegundos = 5.0f;

    private void Awake()
    {
        if (instance == null) instance = this;

        // Si no se han arrastrado las subsecciones a mano en el Inspector, las buscamos automáticamente
        // entre los hijos de este mismo objeto.
        if (seccionBBDD == null)
            seccionBBDD = GetComponentInChildren<UI_SeccionBBDD>();

        if (seccionSimulacion == null)
            seccionSimulacion = GetComponentInChildren<UI_SeccionSimulacion>();
    }

    private void OnEnable()
    {
        // Nos suscribimos a los avisos del simulador offline y al heartbeat de la fábrica real,
        // para saber en todo momento si siguen "vivos".
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
        simulacionEnCurso = false;
        esPausado = false;
        estuvoEnVivoMQTT = false;
        tiempoUltimoHeartbeatReal = Time.realtimeSinceStartup;

        // Restaurar inmediatamente el texto previo guardado para evitar parpadeo visual
        // (mientras se decide el modo real, ya se ve el último texto correcto en pantalla).
        RestaurarTextoModoPrevio();

        if (MQTTClient.Instance != null)
        {
            // Quitamos primero el listener por si ya estaba puesto de una ejecución anterior,
            // para no acabar suscritos dos veces al mismo evento.
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

            // Restauramos si el panel lateral estaba abierto antes de la última recarga de escena.
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
        if (seccionBBDD != null) seccionBBDD.Inicializar(() => EvaluarEstadoBotonPlay());
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
            // Venimos de una recarga de escena provocada por un cambio de modo: restauramos
            // el modo y las fechas que se guardaron justo antes de recargar.
            autoStartPendiente = false;
            ConfigurarEstadoPorAutoStart();
        }
        else
        {
            // Primer arranque "limpio" de la aplicación: empezamos siempre en modo MQTT en vivo.
            modoSeleccionado = ModoOrigen.MQTT_Directo;
            ActualizarTogglesVisuales(false, false);
            ArrancarSimulacion();
        }

        ActualizarPanelInformativoModo();
        NotificarEstadoPermisoPedido();
    }

    private void Update()
    {
        // Refrescamos el reloj de cabecera una vez por segundo (no hace falta más a menudo).
        if (textoReloj != null && Time.time - ultimoSegundoActualizado >= 1f)
        {
            ultimoSegundoActualizado = Time.time;
            ActualizarTextoRelojPrincipal(DateTime.Now);
        }

        // Watchdog MQTT: si estamos en modo en vivo y aún no hemos detectado la desconexión,
        // comprobamos dos señales de alarma: que el cliente MQTT ya no esté conectado, o que
        // haya pasado demasiado tiempo sin recibir un heartbeat de la fábrica real.
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

    // ====================================================================
    // GESTIÓN DE EVENTOS Y HEARTBEAT
    // ====================================================================

    // Se llama cada vez que llega un mensaje de heartbeat ("dt/factory") desde MQTTClient.
    // Sirve para saber, en modo MQTT en vivo, si la fábrica real sigue respondiendo a tiempo.
    private void OnFactoryHeartbeatRecibido(bool connected, DateTime timestamp)
    {
        // Si no estamos en modo MQTT directo, el heartbeat no nos interesa ahora mismo.
        if (modoEnEjecucion.HasValue && modoEnEjecucion.Value != ModoOrigen.MQTT_Directo) return;

        double desfaseSegundos = Math.Abs((DateTime.UtcNow - timestamp).TotalSeconds);
        bool esMensajeReciente = desfaseSegundos < 5.0;
        bool esMensajeRetenidoDeArranque = Time.timeSinceLevelLoad < 2.0f;

        if (connected && esMensajeReciente)
        {
            // Si acabamos de arrancar la escena y este mensaje "connected" es en realidad un mensaje
            // retenido (viejo) del broker, lo ignoramos para no dar una falsa sensación de reconexión.
            if (estaDesconectadoMQTT && esMensajeRetenidoDeArranque) return;

            tiempoUltimoHeartbeatReal = Time.realtimeSinceStartup;
            estuvoEnVivoMQTT = true;

            if (estaDesconectadoMQTT)
            {
                // Nos habíamos marcado como desconectados y ahora ha vuelto a llegar un heartbeat
                // válido: la fábrica se ha reconectado.
                estaDesconectadoMQTT = false;
                yaSeRecargoPorDesconexion = false;
                Debug.Log("<color=green><b>🟢 [Fábrica MQTT] ¡Conexión Restablecida!</b></color>");
                ActualizarPanelInformativoModo();
                NotificarEstadoPermisoPedido();

                if (MQTTClient.Instance != null)
                {
                    // Pedimos que se vuelva a emitir el último inventario del HBW conocido,
                    // para que la escena se ponga al día sin esperar al siguiente mensaje real.
                    MQTTClient.Instance.ReemitirUltimoStock();
                }
            }
        }
        else if (!connected && esMensajeReciente && !estaDesconectadoMQTT)
        {
            ProcesarDesconexionFabrica();
        }
    }

    // Se llama cuando SimuladorOffline avisa de que ha empezado o terminado de tramitar un pedido de pieza.
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

    // Marca la fábrica como desconectada, actualiza la interfaz y, si llegamos a estar en vivo antes,
    // fuerza una única recarga de escena para dejar todo en un estado limpio (evita quedarnos con
    // piezas o animaciones "a medias" cuando se corta la conexión en pleno movimiento).
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

    // ====================================================================
    // GESTIÓN DE TOGGLES Y PANELES
    // ====================================================================

    /// <summary>
    /// Se llama cuando el usuario activa o desactiva el interruptor de "Simulación Offline" de la cabecera.
    /// Los dos toggles (BBDD y Simulación) son excluyentes entre sí: activar uno apaga el otro.
    /// </summary>
    /// <param name="activo">true si el usuario acaba de activar este interruptor.</param>
    public void OnToggleSimulacionCambiado(bool activo)
    {
        if (activo)
        {
            if (toggleModoBBDD != null && toggleModoBBDD.isOn)
            {
                // Apagamos el toggle de BBDD sin disparar su propio evento (para no entrar en un bucle de eventos cruzados).
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

    /// <summary>
    /// Se llama cuando el usuario activa o desactiva el interruptor de "Base de Datos Histórico" de la cabecera.
    /// </summary>
    /// <param name="activo">true si el usuario acaba de activar este interruptor.</param>
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

    // Centraliza lo que hay que hacer cada vez que cambia "modoSeleccionado" desde los toggles:
    // bloquear/desbloquear el panel de BBDD, refrescar el panel informativo y el botón PLAY.
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

    // Actualiza el texto del panel informativo azul (arriba del menú) según el modo seleccionado
    // y, en el caso de MQTT en vivo, según si la fábrica está conectada o no en este momento.
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

        // Almacenar el texto visual para mantenerlo intacto en reinicios de escena
        // (así RestaurarTextoModoPrevio puede pintarlo de inmediato antes de recalcularlo).
        ultimoTituloGuardado = txtModoTitulo.text;
        ultimoSubtituloGuardado = txtModoSubtitulo.text;
    }

    // Pinta de inmediato, nada más arrancar Start(), el último texto informativo guardado,
    // para que no se vea un "parpadeo" en blanco mientras se decide el modo real tras la recarga.
    private void RestaurarTextoModoPrevio()
    {
        if (!string.IsNullOrEmpty(ultimoTituloGuardado) && txtModoTitulo != null)
            txtModoTitulo.text = ultimoTituloGuardado;

        if (!string.IsNullOrEmpty(ultimoSubtituloGuardado) && txtModoSubtitulo != null)
            txtModoSubtitulo.text = ultimoSubtituloGuardado;
    }

    // Sincroniza visualmente los dos toggles de la cabecera (BBDD y Simulación) sin disparar
    // sus eventos de cambio (para evitar bucles), incluyendo el interruptor visual personalizado UI_ToggleSwitch si existe.
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

    // Los botones de pedido de pieza (blanca/roja/azul) solo deben estar activos si el modo
    // seleccionado es Simulación Offline y no hay ya otro pedido en curso.
    private void ActualizarEstadoBotonesSeccionSimulacion()
    {
        bool sePuedePedirSimulacion = (modoSeleccionado == ModoOrigen.Simulacion_Offline) && !simulacionOfflinePedidoEnCurso;
        if (seccionSimulacion != null)
        {
            seccionSimulacion.ActualizarEstadoBotones(sePuedePedirSimulacion);
        }
    }

    /// <summary>
    /// Punto de entrada que usa <see cref="UI_SeccionSimulacion"/> cuando el usuario pulsa uno de
    /// los botones de pedido de pieza. Si el modo Simulación Offline no está corriendo todavía,
    /// primero programa un auto-arranque en ese modo y recarga la escena (necesario para dejar todo
    /// en un estado limpio); si ya está corriendo, reenvía el pedido directamente a <c>SimuladorOffline</c>.
    /// </summary>
    /// <param name="color">Color de la pieza pedida ("WHITE", "RED" o "BLUE").</param>
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

    /// <summary>
    /// Difunde a través de <see cref="OnEstadoPermisoPedidoCambiado"/> si en este momento está
    /// permitido pedir una pieza nueva, para que los paneles de pedido activen o desactiven sus botones.
    /// </summary>
    public void NotificarEstadoPermisoPedido()
    {
        OnEstadoPermisoPedidoCambiado?.Invoke(PuedePedirPieza);
    }

    // ====================================================================
    // CONTROL DE REPRODUCCIÓN Y ESCENA
    // ====================================================================

    /// <summary>
    /// Se ejecuta al pulsar el botón PLAY/PAUSE de la cabecera. Su comportamiento depende del
    /// contexto: si ya hay una reproducción en curso del mismo modo (sin cambios pendientes),
    /// simplemente alterna pausa/reanudación; si el usuario ha seleccionado un modo distinto al
    /// que está corriendo, o ha cambiado el rango de fechas del histórico, se programa un
    /// auto-arranque con la nueva configuración y se recarga la escena para aplicarlo desde cero.
    /// </summary>
    public void OnBotonPlayPulsado()
    {
        bool hayCambioModo = (modoEnEjecucion.HasValue && modoSeleccionado != modoEnEjecucion.Value);

        if (simulacionOfflinePedidoEnCurso && !hayCambioModo)
        {
            // Ya hay un pedido de simulación en marcha y no se ha cambiado de modo: solo pausamos/reanudamos.
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
                    // El usuario ha cambiado el rango de fechas mientras el histórico ya estaba corriendo:
                    // hace falta reiniciar la reproducción desde cero con el nuevo rango.
                    autoStartPendiente = true;
                    autoStartModo = modoSeleccionado;
                    autoStartFechaIni = fIni;
                    autoStartFechaFin = fFin;
                    RecargarEscenaLimpia();
                    return;
                }
            }

            // Mismas fechas de siempre: solo alternamos pausa/reanudación de la reproducción actual.
            esPausado = !esPausado;
            ActualizarVisualBotonPlay();
            return;
        }

        // Caso general: hay que arrancar (o cambiar a) un modo distinto del que está corriendo ahora mismo.
        autoStartPendiente = true;
        autoStartModo = modoSeleccionado;

        if (seccionBBDD != null && seccionBBDD.ObtenerRangoFechas(out DateTime fIniSel, out DateTime fFinSel))
        {
            autoStartFechaIni = fIniSel;
            autoStartFechaFin = fFinSel;
        }

        RecargarEscenaLimpia();
    }

    /// <summary>
    /// Se ejecuta al pulsar el botón RESET de la cabecera: cancela cualquier auto-arranque
    /// pendiente, olvida el estado del menú lateral, reconecta el cliente MQTT principal y
    /// recarga la escena para volver a un estado inicial limpio (equivalente a "empezar de nuevo").
    /// </summary>
    public void OnBotonResetPulsado()
    {
        if (seccionBBDD != null) seccionBBDD.ObtenerRangoFechas(out _, out _);

        autoStartPendiente = false;
        autoStartPiezaSimulacion = null;

        // Mantenemos el estado de desconexión tal cual estaba sin forzar cambios
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

    // Pone en marcha de verdad el modo actualmente seleccionado: conecta o desconecta los clientes
    // MQTT según corresponda, prepara el almacén HBW, y arranca la simulación offline o la
    // reproducción del histórico de BBDD si procede. Se llama tanto en un arranque normal de la
    // escena como después de restaurar un auto-arranque.
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
                // Pedimos releer el inventario real del HBW por si hubo cambios mientras no estábamos en este modo.
                ControladorSpawnPiecesHBW_mqtt.Instance.ForzarRelecturaStock();
            }

            if (MQTTClient.Instance != null)
            {
                MQTTClient.Instance.enabled = true;
                MQTTClient.Instance.Connect();
                // Dejamos que los heartbeats reales o el watchdog manejen la desconexión
                // sin forzar 'estaDesconectadoMQTT = true' en el primer fotograma
            }

            EvaluarEstadoBotonPlay();
        }
        else if (modoSeleccionado == ModoOrigen.Simulacion_Offline)
        {
            simulacionEnCurso = false;
            fechaIniEnEjecucion = null;
            fechaFinEnEjecucion = null;

            SetVisibilidadRelojSimulacion(false);

            // En simulación offline no queremos ningún dato real llegando por MQTT, así que
            // cortamos ambas conexiones (la del gemelo digital y la de la interfaz).
            if (MQTTClient.Instance != null) { MQTTClient.Instance.enabled = true; MQTTClient.Instance.DesconectarRed(); }
            if (MQTT_InterfaceClient.Instance != null) { MQTT_InterfaceClient.Instance.enabled = true; MQTT_InterfaceClient.Instance.DesconectarRed(); }

            if (ControladorSpawnPiecesHBW_mqtt.Instance != null)
            {
                // Llenamos el almacén virtual con piezas de sobra para poder simular pedidos sin depender del inventario real.
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

            // En modo histórico tampoco queremos datos reales en vivo: cortamos ambas conexiones MQTT.
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
                // Lanzamos la corrutina que va pidiendo a InfluxDBClient los datos históricos y los reproduce
                // como si llegaran en directo, respetando la pausa y el multiplicador de velocidad.
                corrutinaReplayBBDD = StartCoroutine(ProcesarHistoricoBBDD(desde, hasta));
            }
        }

        ActualizarPanelInformativoModo();
        NotificarEstadoPermisoPedido();
        ActualizarEstadoBotonesSeccionSimulacion();
    }

    // Restaura, justo después de una recarga de escena, el modo y las fechas que se habían guardado
    // en las variables estáticas de auto-arranque, y vuelve a llamar a ArrancarSimulacion() con esa configuración.
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
            // Si el auto-arranque venía de pulsar un botón de pedido de pieza, lo tramitamos ahora
            // que el modo Simulación Offline ya está realmente en marcha.
            SimuladorOffline.Instance.PedirPieza(piezaAPedir);
        }

        ActualizarPanelInformativoModo();
    }

    /// <summary>
    /// Recalcula si el botón PLAY debe estar activo (interactable) y actualiza su icono/símbolo
    /// (▶ o ⏸) según el modo seleccionado, el modo realmente en ejecución y si hay una reproducción
    /// en curso. Se llama cada vez que cambia cualquier cosa que pueda afectar a esta decisión
    /// (toggles, fechas, pedidos de simulación, etc.).
    /// </summary>
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

            btnPlay.interactable = true;
            return;
        }

        if (modoSeleccionado == ModoOrigen.MQTT_Directo)
        {
            // En modo MQTT en vivo no existe "pausa": el botón se desactiva porque no hay nada que reproducir manualmente.
            btnPlay.interactable = false;
            return;
        }

        btnPlay.interactable = false;
    }

    // Muestra el icono/tooltip que recuerda al usuario que debe pulsar PLAY para aplicar un cambio pendiente.
    private void ActualizarIconoInfoPlay()
    {
        if (iconoInfoPlay != null)
        {
            iconoInfoPlay.SetActive(true);
        }
    }

    // Decide si el botón PLAY debe mostrar el símbolo de "reproducir" o el de "pausa", según si
    // hay una reproducción en marcha (offline o histórico) que no esté pausada ni tenga cambios pendientes.
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

    // Comprueba si el rango de fechas actualmente seleccionado en el panel de BBDD difiere del
    // rango que realmente está reproduciéndose ahora mismo (solo tiene sentido en modo histórico).
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

    // Recarga la escena activa desde cero. Es el mecanismo elegido en este proyecto para cambiar
    // de modo de forma segura: en vez de intentar reconfigurar en caliente todos los controladores
    // (VGR, HBW, DPS, MPO, SLD, SSC) y sus conexiones MQTT, simplemente se destruye todo y se vuelve
    // a construir, restaurando el estado deseado a través de las variables estáticas de auto-arranque.
    private void RecargarEscenaLimpia()
    {
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Corrutina que delega en InfluxDBClient la descarga y reproducción del histórico entre las
    // fechas indicadas. Le pasamos como funciones: cuánto multiplicador de velocidad aplicar en
    // cada instante (0 si está en pausa) y qué hacer cada vez que avanza la "hora simulada" (actualizar el reloj de simulación).
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

        // Al terminar de reproducir todo el rango, volvemos a dejar el botón PLAY listo para una nueva reproducción.
        simulacionEnCurso = false;
        esPausado = false;
        EvaluarEstadoBotonPlay();
    }

    // Muestra u oculta el panel del reloj de simulación (usado en modo histórico) y avisa
    // mediante el evento estático a otros paneles (como la cámara) para que se reubiquen si hace falta.
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

    /// <summary>Abre el menú lateral si está cerrado, o lo cierra si está abierto.</summary>
    public void ToggleMenu()
    {
        if (panelLateral != null) CambiarEstadoMenu(!panelLateral.activeSelf);
    }

    /// <summary>Cierra el menú lateral; pensado para llamarse al hacer clic fuera del panel (sobre <see cref="fondoCierre"/>).</summary>
    public void CerrarDesdeFuera()
    {
        CambiarEstadoMenu(false);
    }

    // Activa o desactiva el panel lateral y su fondo de cierre, y fuerza un recálculo del layout
    // al abrirlo (para que el contenido interno se ajuste bien de inmediato).
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
