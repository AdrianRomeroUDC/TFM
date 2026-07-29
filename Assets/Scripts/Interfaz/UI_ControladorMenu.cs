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

    // Evento y propiedad pública para comunicar cambios de visibilidad del reloj a la cámara
    public static event Action<bool> OnRelojSimulacionVisibilidadCambiada;
    public static bool EsRelojSimulacionVisible { get; private set; } = false;

    public enum ModoOrigen { MQTT_Directo, BaseDeDatos_Historico }
    public enum EstadoSimulacion { Detenido, Reproduciendo }

    [Header("Mi Panel Desplegable Principal")]
    public GameObject panelLateral;

    [Header("Cierre al Clicar Fuera")]
    public GameObject fondoCierre;

    [Header("Reloj Digital de la Cabecera (Hora Real)")]
    public TMP_Text textoReloj; // SIEMPRE muestra la hora real del sistema

    [Header("Reloj de Simulación BBDD (Debajo del Reloj Principal)")]
    [Tooltip("Panel secundario que aparece debajo del reloj principal durante la reproducción BBDD")]
    public GameObject panelRelojSimulacion;
    [Tooltip("Componente TMP_Text donde se muestra la hora que transcurre en la simulación")]
    public TMP_Text textoRelojSimulacion;

    [Header("UI Control de Simulación (Cabecera)")]
    public Toggle toggleModoBBDD;            // El botón ON/OFF de BBDD
    public Button btnPlay;                  // Botón PLAY / PAUSE
    public Button btnReset;                 // Botón RESET

    [Header("Símbolos y Textos del Botón PLAY / PAUSE")]
    [Tooltip("Componente TextMeshProUGUI que contiene el símbolo/texto dentro del botón Play")]
    public TMP_Text textoBotonPlay;
    [Tooltip("Símbolo que se muestra cuando la simulación está detenida o en pausa")]
    public string simboloPlay = "▶";
    [Tooltip("Símbolo que se muestra mientras la simulación BBDD se está ejecutando")]
    public string simboloPause = "⏸";

    [Header("Botones Multiplicador (Ocultos y Colapsados)")]
    public GameObject contenedorMultiplicador;
    public Button btnSpeedX1;
    public Button btnSpeedX2;
    public Button btnSpeedX5;

    [Header("Fecha y Hora - INICIO")]
    public UI_CalendarPicker calendarInicio; // Componente Calendario
    public TMP_Dropdown dropdownHoraInicio;  // 00 - 23
    public TMP_Dropdown dropdownMinInicio;   // 00 - 59
    public TMP_Dropdown dropdownSegInicio;   // 00 - 59

    [Header("Fecha y Hora - FIN")]
    public UI_CalendarPicker calendarFin;    // Componente Calendario
    public TMP_Dropdown dropdownHoraFin;     // 00 - 23
    public TMP_Dropdown dropdownMinFin;      // 00 - 59
    public TMP_Dropdown dropdownSegFin;      // 00 - 59

    [Header("Ajustes de Reproducción BBDD")]
    [Tooltip("Aumenta este número para reproducir el histórico más rápido (ej: 2.0 = el doble de rápido)")]
    public float multiplicadorVelocidad = 1.0f;

    [Header("Estado Actual (Lectura)")]
    public ModoOrigen modoSeleccionado = ModoOrigen.MQTT_Directo;
    public EstadoSimulacion estadoActual = EstadoSimulacion.Detenido;

    private ModoOrigen? modoEnEjecucion = null;
    private DateTime? fechaIniEnEjecucion = null;
    private DateTime? fechaFinEnEjecucion = null;

    private RectTransform rectPanel;
    private RectTransform rectSecciones;
    private float ultimoSegundoActualizado = -1f;
    private Coroutine corrutinaReplayBBDD;

    // Control de reproducción y pausa
    private bool simulacionEnCurso = false;
    private bool esPausado = false;

    // Propiedad pública de lectura para consultar si está pausado
    public bool EsPausado => esPausado;

    // Persistencia estática tras el reset/play de escena
    private static bool autoStartPendiente = false;
    private static ModoOrigen autoStartModo = ModoOrigen.MQTT_Directo;
    private static DateTime autoStartFechaIni = DateTime.Today.AddHours(8);
    private static DateTime autoStartFechaFin = DateTime.Today.AddHours(18);

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private void Start()
    {
        simulacionEnCurso = false;
        esPausado = false;

        if (textoBotonPlay == null && btnPlay != null)
        {
            textoBotonPlay = btnPlay.GetComponentInChildren<TMP_Text>();
        }

        if (panelLateral != null)
        {
            rectPanel = panelLateral.GetComponent<RectTransform>();
            VerticalLayoutGroup layout = panelLateral.GetComponentInChildren<VerticalLayoutGroup>();
            if (layout != null) rectSecciones = layout.GetComponent<RectTransform>();
            panelLateral.SetActive(false);
        }

        if (fondoCierre != null) fondoCierre.SetActive(false);

        if (!autoStartPendiente)
        {
            SetVisibilidadRelojSimulacion(false);
        }

        InicializarControlesTiempo();
        OcultarYColapsarMultiplicadores();

        if (btnPlay != null)
        {
            btnPlay.onClick.RemoveListener(OnBotonPlayPulsado);
            btnPlay.onClick.AddListener(OnBotonPlayPulsado);
        }

        if (btnReset != null)
        {
            btnReset.onClick.RemoveListener(OnBotonResetPulsado);
            btnReset.onClick.AddListener(OnBotonResetPulsado);
        }

        if (toggleModoBBDD != null)
        {
            toggleModoBBDD.onValueChanged.RemoveListener(OnToggleModoCambiado);
            toggleModoBBDD.onValueChanged.AddListener(OnToggleModoCambiado);
        }

        Time.timeScale = 1.0f;
        VincularListenersDeCambioEnControles();

        if (autoStartPendiente)
        {
            autoStartPendiente = false;
            bool esBBDD = (autoStartModo == ModoOrigen.BaseDeDatos_Historico);

            if (toggleModoBBDD != null)
            {
                toggleModoBBDD.SetIsOnWithoutNotify(esBBDD);

                UI_ToggleSwitch switchComp = toggleModoBBDD.GetComponent<UI_ToggleSwitch>();
                if (switchComp != null)
                {
                    switchComp.ActualizarEstadoInstantaneo(esBBDD);
                }
            }

            modoSeleccionado = autoStartModo;

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
        else
        {
            if (toggleModoBBDD != null)
            {
                toggleModoBBDD.SetIsOnWithoutNotify(false);

                UI_ToggleSwitch switchComp = toggleModoBBDD.GetComponent<UI_ToggleSwitch>();
                if (switchComp != null)
                {
                    switchComp.ActualizarEstadoInstantaneo(false);
                }
            }

            OnToggleModoCambiado(false);
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
    }

    private void SetVisibilidadRelojSimulacion(bool visible)
    {
        EsRelojSimulacionVisible = visible;

        if (panelRelojSimulacion != null)
        {
            panelRelojSimulacion.SetActive(visible);
        }

        OnRelojSimulacionVisibilidadCambiada?.Invoke(visible);
    }

    private void ActualizarTextoRelojPrincipal(DateTime fechaHora)
    {
        if (textoReloj != null)
        {
            string fecha = fechaHora.ToString("dd / MM / yyyy");
            string hora = fechaHora.ToString("HH:mm:ss");
            textoReloj.text = fecha + "\n" + hora;
        }
    }

    private void ActualizarTextoRelojSimulacion(DateTime fechaHora)
    {
        if (textoRelojSimulacion != null)
        {
            string fecha = fechaHora.ToString("dd / MM / yyyy");
            string hora = fechaHora.ToString("HH:mm:ss");
            textoRelojSimulacion.text = fecha + "\n" + hora;
        }
    }

    private void OcultarYColapsarMultiplicadores()
    {
        if (btnSpeedX1 != null) { btnSpeedX1.interactable = false; btnSpeedX1.gameObject.SetActive(false); }
        if (btnSpeedX2 != null) { btnSpeedX2.interactable = false; btnSpeedX2.gameObject.SetActive(false); }
        if (btnSpeedX5 != null) { btnSpeedX5.interactable = false; btnSpeedX5.gameObject.SetActive(false); }

        if (contenedorMultiplicador != null)
        {
            contenedorMultiplicador.SetActive(false);
        }
        else if (btnSpeedX1 != null && btnSpeedX1.transform.parent != null)
        {
            Transform padre = btnSpeedX1.transform.parent;
            if (padre.name.Contains("Multiplicador") || padre.GetComponent<HorizontalLayoutGroup>() != null)
            {
                padre.gameObject.SetActive(false);
            }
        }

        if (rectSecciones != null) LayoutRebuilder.MarkLayoutForRebuild(rectSecciones);
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

    public void OnToggleModoCambiado(bool modoBBDDActivo)
    {
        modoSeleccionado = modoBBDDActivo ? ModoOrigen.BaseDeDatos_Historico : ModoOrigen.MQTT_Directo;

        if (!modoBBDDActivo)
        {
            esPausado = false;
        }

        EvaluarEstadoBotonPlay();
    }

    private bool HayCambioEnFechasEnEjecucion()
    {
        if (modoEnEjecucion != ModoOrigen.BaseDeDatos_Historico) return false;
        if (fechaIniEnEjecucion == null || fechaFinEnEjecucion == null) return false;

        if (ObtenerRangoFechas(out DateTime fIniSel, out DateTime fFinSel))
        {
            return (fIniSel != fechaIniEnEjecucion.Value) || (fFinSel != fechaFinEnEjecucion.Value);
        }

        return false;
    }

    private void EvaluarEstadoBotonPlay()
    {
        if (btnPlay == null) return;

        ActualizarVisualBotonPlay();

        if (modoSeleccionado == ModoOrigen.BaseDeDatos_Historico && simulacionEnCurso)
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
            if (ObtenerRangoFechas(out DateTime fIniSeleccionada, out DateTime fFinSeleccionada))
            {
                bool hayCambio = (fechaIniEnEjecucion == null || fechaFinEnEjecucion == null) ||
                                 (fIniSeleccionada != fechaIniEnEjecucion.Value) ||
                                 (fFinSeleccionada != fechaFinEnEjecucion.Value);

                btnPlay.interactable = hayCambio;
                return;
            }
        }

        btnPlay.interactable = false;
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

        if (dropdown.template != null)
        {
            ScrollRect scrollRect = dropdown.template.GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.scrollSensitivity = 3f;
            }
        }
    }

    private void SetDropdownValor(TMP_Dropdown dropdown, int valor)
    {
        if (dropdown != null && dropdown.options.Count > 0)
        {
            dropdown.value = Mathf.Clamp(valor, 0, dropdown.options.Count - 1);
            dropdown.RefreshShownValue();
        }
    }

    public void OnBotonPlayPulsado()
    {
        if (modoSeleccionado == ModoOrigen.BaseDeDatos_Historico && modoEnEjecucion == ModoOrigen.BaseDeDatos_Historico && simulacionEnCurso)
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
            Debug.Log(esPausado ? "<color=yellow>⏸️ UI: Solicitando PAUSA en BBDD...</color>" : "<color=green>▶️ UI: Solicitando REANUDACIÓN en BBDD...</color>");
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
        RecargarEscenaLimpia();
    }

    private void ArrancarSimulacion()
    {
        estadoActual = EstadoSimulacion.Reproduciendo;
        modoEnEjecucion = modoSeleccionado;
        esPausado = false;
        Time.timeScale = 1.0f;

        SetUIInteractables(true);

        if (modoSeleccionado == ModoOrigen.MQTT_Directo)
        {
            simulacionEnCurso = false;
            fechaIniEnEjecucion = null;
            fechaFinEnEjecucion = null;

            SetVisibilidadRelojSimulacion(false);

            if (MQTTClient.Instance != null) { MQTTClient.Instance.enabled = true; MQTTClient.Instance.Connect(); }
            if (MQTT_InterfaceClient.Instance != null) { MQTT_InterfaceClient.Instance.enabled = true; }

            Debug.Log("<color=green>▶️ EN DIRECTO: Escuchando MQTT en tiempo real...</color>");
            EvaluarEstadoBotonPlay();
        }
        else if (modoSeleccionado == ModoOrigen.BaseDeDatos_Historico)
        {
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

                Debug.Log($"<color=green>▶️ INICIANDO HISTÓRICO BBDD | Desde: {desde:dd-MM-yyyy HH:mm:ss} Hasta: {hasta:dd-MM-yyyy HH:mm:ss}</color>");
                corrutinaReplayBBDD = StartCoroutine(ProcesarHistoricoBBDD(desde, hasta));
            }
            else
            {
                Debug.LogError("❌ Error al construir las fechas desde los controles UI.");
                DetenerYResetearEstado();
            }
        }
    }

    private void RecargarEscenaLimpia()
    {
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void DetenerYResetearEstado()
    {
        estadoActual = EstadoSimulacion.Detenido;
        modoEnEjecucion = null;
        fechaIniEnEjecucion = null;
        fechaFinEnEjecucion = null;
        simulacionEnCurso = false;
        esPausado = false;
        Time.timeScale = 1.0f;

        if (corrutinaReplayBBDD != null)
        {
            StopCoroutine(corrutinaReplayBBDD);
            corrutinaReplayBBDD = null;
        }

        SetUIInteractables(true);
        EvaluarEstadoBotonPlay();
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
        Debug.Log("<color=green>✅ Fin de la reproducción BBDD.</color>");
        DetenerYResetearEstado();
    }

    private void ActualizarVisualBotonPlay()
    {
        if (textoBotonPlay == null && btnPlay != null)
        {
            textoBotonPlay = btnPlay.GetComponentInChildren<TMP_Text>();
        }

        if (textoBotonPlay != null)
        {
            bool hayCambioFechas = HayCambioEnFechasEnEjecucion();

            bool mostrarPausa = (modoSeleccionado == ModoOrigen.BaseDeDatos_Historico)
                             && simulacionEnCurso
                             && !esPausado
                             && !hayCambioFechas;

            textoBotonPlay.text = mostrarPausa ? simboloPause : simboloPlay;

            // 🟢 Margen inferior de 2.5f aplicado únicamente cuando el texto es el símbolo de pausa
            Vector4 margin = textoBotonPlay.margin;
            margin.w = (textoBotonPlay.text == simboloPause) ? 2.5f : 0f;
            textoBotonPlay.margin = margin;

            textoBotonPlay.ForceMeshUpdate();
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
        catch (Exception ex)
        {
            Debug.LogError($"Error leyendo controles de fecha/hora: {ex.Message}");
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