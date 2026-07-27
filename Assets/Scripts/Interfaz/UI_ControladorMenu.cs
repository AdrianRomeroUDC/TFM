using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

public class UI_ControladorMenu : MonoBehaviour
{
    public enum ModoOrigen { MQTT_Directo, BaseDeDatos_Historico }
    public enum EstadoSimulacion { Detenido, Reproduciendo }

    [Header("Mi Panel Desplegable Principal")]
    public GameObject panelLateral;

    [Header("Cierre al Clicar Fuera")]
    public GameObject fondoCierre;

    [Header("Reloj Digital de la Cabecera")]
    public TMP_Text textoReloj;

    [Header("UI Control de Simulación (Cabecera)")]
    public Toggle toggleModoBBDD;            // El botón ON/OFF de BBDD
    public Button btnPlay;                  // Botón PLAY
    public Button btnReset;                 // Botón RESET

    [Header("Botones Multiplicador de Velocidad")]
    public Button btnSpeedX1;
    public Button btnSpeedX2;
    public Button btnSpeedX5;

    [Header("Colores Multiplicadores (Pulsado / Inactivo por Defecto)")]
    public Color colorVelocidadActiva = new Color(0f, 0.65f, 1f, 1f);   // Azul destacado
    public Color colorTextoActivo = Color.white;                         // Texto blanco
    public Color colorVelocidadInactiva = Color.white;                   // Blanco por defecto de Unity
    public Color colorTextoInactivo = new Color(0.2f, 0.2f, 0.2f, 1f);   // Texto gris/negro nativo

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
    [Tooltip("Velocidad de reproducción del histórico (1 = Normal, 2 = Doble velocidad)")]
    public float multiplicadorVelocidad = 1.0f;

    [Header("Estado Actual (Lectura)")]
    public ModoOrigen modoSeleccionado = ModoOrigen.MQTT_Directo;
    public EstadoSimulacion estadoActual = EstadoSimulacion.Detenido;

    private ModoOrigen? modoEnEjecucion = null;

    private RectTransform rectPanel;
    private RectTransform rectSecciones;
    private float ultimoSegundoActualizado = -1f;
    private Coroutine corrutinaReplayBBDD;
    private bool historicoCompletado = false;

    // Persistencia tras el reset de escena
    private static bool autoStartPendiente = false;
    private static ModoOrigen autoStartModo = ModoOrigen.MQTT_Directo;
    private static DateTime autoStartFechaIni = DateTime.Today.AddHours(8);
    private static DateTime autoStartFechaFin = DateTime.Today.AddHours(18);
    private static float autoStartVelocidad = 1.0f;

    private void Start()
    {
        historicoCompletado = false;

        // 1. Configuración de paneles
        if (panelLateral != null)
        {
            rectPanel = panelLateral.GetComponent<RectTransform>();
            VerticalLayoutGroup layout = panelLateral.GetComponentInChildren<VerticalLayoutGroup>();
            if (layout != null) rectSecciones = layout.GetComponent<RectTransform>();
            panelLateral.SetActive(false);
        }

        if (fondoCierre != null) fondoCierre.SetActive(false);

        // 2. Configuración de tiempo y multiplicadores
        InicializarControlesTiempo();
        VincularBotonesVelocidad();

        // 3. Vincular botones principales
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

        // 4. Configuración del Toggle
        if (toggleModoBBDD != null)
        {
            toggleModoBBDD.onValueChanged.RemoveListener(OnToggleModoCambiado);
            toggleModoBBDD.onValueChanged.AddListener(OnToggleModoCambiado);
        }

        Time.timeScale = 1.0f;

        // 5. Autostart / Estado inicial
        if (autoStartPendiente)
        {
            autoStartPendiente = false;
            bool esBBDD = (autoStartModo == ModoOrigen.BaseDeDatos_Historico);

            if (toggleModoBBDD != null)
            {
                // 🟢 Evitamos disparar la corrutina de animación cuando el objeto está inactivo
                toggleModoBBDD.SetIsOnWithoutNotify(esBBDD);

                UI_ToggleSwitch switchComp = toggleModoBBDD.GetComponent<UI_ToggleSwitch>();
                if (switchComp != null)
                {
                    switchComp.ActualizarEstadoInstantaneo(esBBDD);
                }
            }

            SeleccionarVelocidad(autoStartVelocidad);
            OnToggleModoCambiado(esBBDD);

            if (esBBDD)
            {
                if (calendarInicio != null) calendarInicio.SetFechaInicial(autoStartFechaIni);
                if (calendarFin != null) calendarFin.SetFechaInicial(autoStartFechaFin);

                SetDropdownValor(dropdownHoraInicio, autoStartFechaIni.Hour);
                SetDropdownValor(dropdownMinInicio, autoStartFechaIni.Minute);
                SetDropdownValor(dropdownSegInicio, autoStartFechaIni.Second);

                SetDropdownValor(dropdownHoraFin, autoStartFechaFin.Hour);
                SetDropdownValor(dropdownMinFin, autoStartFechaFin.Minute);
                SetDropdownValor(dropdownSegFin, autoStartFechaFin.Second);
            }

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

            SeleccionarVelocidad(1.0f);
            OnToggleModoCambiado(false);
            ArrancarSimulacion();
        }
    }

    private void Update()
    {
        if (!historicoCompletado && (estadoActual == EstadoSimulacion.Detenido || modoEnEjecucion == ModoOrigen.MQTT_Directo))
        {
            if (textoReloj != null && Time.time - ultimoSegundoActualizado >= 1f)
            {
                ultimoSegundoActualizado = Time.time;
                ActualizarTextoReloj(DateTime.Now);
            }
        }
    }

    // ====================================================================
    // ⚡ LÓGICA Y ASPECTO DEL MULTIPLICADOR DE VELOCIDAD
    // ====================================================================

    private void VincularBotonesVelocidad()
    {
        if (btnSpeedX1 != null)
        {
            btnSpeedX1.onClick.RemoveAllListeners();
            btnSpeedX1.onClick.AddListener(() => SeleccionarVelocidad(1.0f));
        }

        if (btnSpeedX2 != null)
        {
            btnSpeedX2.onClick.RemoveAllListeners();
            btnSpeedX2.onClick.AddListener(() => SeleccionarVelocidad(2.0f));
        }

        if (btnSpeedX5 != null)
        {
            btnSpeedX5.onClick.RemoveAllListeners();
            btnSpeedX5.onClick.AddListener(() => SeleccionarVelocidad(5.0f));
        }
    }

    public void SeleccionarVelocidad(float velocidad)
    {
        multiplicadorVelocidad = velocidad;
        autoStartVelocidad = velocidad;

        ActualizarVisualizacionVelocidad();
    }

    private void ActualizarVisualizacionVelocidad()
    {
        ActualizarColorBotonSpeed(btnSpeedX1, Mathf.Approximately(multiplicadorVelocidad, 1.0f));
        ActualizarColorBotonSpeed(btnSpeedX2, Mathf.Approximately(multiplicadorVelocidad, 2.0f));
        ActualizarColorBotonSpeed(btnSpeedX5, Mathf.Approximately(multiplicadorVelocidad, 5.0f));
    }

    private void ActualizarColorBotonSpeed(Button btn, bool estaSeleccionado)
    {
        if (btn == null) return;

        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = estaSeleccionado ? colorVelocidadActiva : colorVelocidadInactiva;
        }

        TMP_Text txt = btn.GetComponentInChildren<TMP_Text>();
        if (txt != null)
        {
            txt.color = estaSeleccionado ? colorTextoActivo : colorTextoInactivo;
        }
    }

    // ====================================================================
    // CONTROL DEL TOGGLE (Lógica de Negocio)
    // ====================================================================

    public void OnToggleModoCambiado(bool modoBBDDActivo)
    {
        modoSeleccionado = modoBBDDActivo ? ModoOrigen.BaseDeDatos_Historico : ModoOrigen.MQTT_Directo;
        historicoCompletado = false;

        ActualizarEstadoBotones();
    }

    // ====================================================================
    // CONFIGURACIÓN DE DROPDOWNS Y TIEMPO
    // ====================================================================

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
                scrollRect.scrollSensitivity = 50f;
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

    private void ActualizarEstadoBotones()
    {
        if (btnPlay != null) btnPlay.interactable = (modoSeleccionado != modoEnEjecucion);
        if (btnReset != null) btnReset.interactable = true;
    }

    public void OnBotonPlayPulsado()
    {
        autoStartPendiente = true;
        autoStartModo = modoSeleccionado;

        if (ObtenerRangoFechas(out DateTime fIni, out DateTime fFin))
        {
            autoStartFechaIni = fIni;
            autoStartFechaFin = fFin;
        }

        RecargarEscenaLimpia();
    }

    private void ArrancarSimulacion()
    {
        estadoActual = EstadoSimulacion.Reproduciendo;
        modoEnEjecucion = modoSeleccionado;
        historicoCompletado = false;
        Time.timeScale = 1.0f;

        ActualizarEstadoBotones();

        if (modoSeleccionado == ModoOrigen.MQTT_Directo)
        {
            if (MQTTClient.Instance != null) { MQTTClient.Instance.enabled = true; MQTTClient.Instance.Connect(); }
            if (MQTT_InterfaceClient.Instance != null) { MQTT_InterfaceClient.Instance.enabled = true; }

            Debug.Log("<color=green>▶️ EN DIRECTO: Escuchando MQTT en tiempo real...</color>");
        }
        else if (modoSeleccionado == ModoOrigen.BaseDeDatos_Historico)
        {
            if (MQTTClient.Instance != null) { MQTTClient.Instance.enabled = true; MQTTClient.Instance.DesconectarRed(); }
            if (MQTT_InterfaceClient.Instance != null) { MQTT_InterfaceClient.Instance.enabled = true; MQTT_InterfaceClient.Instance.DesconectarRed(); }

            SetUIInteractables(false);

            if (ObtenerRangoFechas(out DateTime desde, out DateTime hasta))
            {
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

    public void OnBotonResetPulsado()
    {
        autoStartPendiente = false;
        RecargarEscenaLimpia();
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
        Time.timeScale = 1.0f;

        if (corrutinaReplayBBDD != null)
        {
            StopCoroutine(corrutinaReplayBBDD);
            corrutinaReplayBBDD = null;
        }

        SetUIInteractables(true);
        if (btnPlay != null) btnPlay.interactable = true;
    }

    private IEnumerator ProcesarHistoricoBBDD(DateTime desde, DateTime hasta)
    {
        if (InfluxDBClient.Instance != null)
        {
            yield return StartCoroutine(
                InfluxDBClient.Instance.DescargarYReproducirHistorico(
                    desde,
                    hasta,
                    () => multiplicadorVelocidad,
                    (horaMuestra) => ActualizarTextoReloj(horaMuestra)
                )
            );
        }

        historicoCompletado = true;
        Debug.Log("<color=green>✅ Fin de la reproducción BBDD.</color>");
        DetenerYResetearEstado();
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

    private void ActualizarTextoReloj(DateTime fechaHora)
    {
        if (textoReloj != null)
        {
            string fecha = fechaHora.ToString("dd / MM / yyyy");
            string hora = fechaHora.ToString("HH:mm:ss");
            textoReloj.text = fecha + "\n" + hora;
        }
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

        if (btnSpeedX1 != null) btnSpeedX1.interactable = true;
        if (btnSpeedX2 != null) btnSpeedX2.interactable = true;
        if (btnSpeedX5 != null) btnSpeedX5.interactable = true;
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