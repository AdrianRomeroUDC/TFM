using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Collections;
using System.Globalization;

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
    public Toggle toggleModoBBDD;            // Toggle BBDD (OFF = MQTT, ON = BBDD)
    public Button btnPlay;                  // Botón PLAY
    public Button btnReset;                 // Botón RESET

    [Header("Inputs de Fecha y Hora")]
    public TMP_InputField inputFechaInicio;  // DD-MM-YYYY
    public TMP_InputField inputHoraInicio;   // HH:MM:SS
    public TMP_InputField inputFechaFin;     // DD-MM-YYYY
    public TMP_InputField inputHoraFin;      // HH:MM:SS

    [Header("Ajustes de Reproducción BBDD")]
    [Tooltip("Velocidad de reproducción del histórico (1 = Normal, 2 = Doble velocidad)")]
    public float multiplicadorVelocidad = 1.0f;

    [Header("Estado Actual (Lectura)")]
    public ModoOrigen modoSeleccionado = ModoOrigen.MQTT_Directo;
    public EstadoSimulacion estadoActual = EstadoSimulacion.Detenido;

    private ModoOrigen? modoEnEjecucion = ModoOrigen.MQTT_Directo; // MQTT activo por defecto al iniciar

    private RectTransform rectPanel;
    private RectTransform rectSecciones;
    private float ultimoSegundoActualizado = -1f;
    private Coroutine corrutinaReplayBBDD;
    private bool historicoCompletado = false;

    // Variables estáticas para persistir la selección tras el RESET de escena por cambio de modo
    private static bool autoStartPendiente = false;
    private static ModoOrigen autoStartModo = ModoOrigen.MQTT_Directo;
    private static string autoStartFechaIni = "";
    private static string autoStartHoraIni = "";
    private static string autoStartFechaFin = "";
    private static string autoStartHoraFin = "";

    private void Awake()
    {
        VincularEventosUI();
    }

    private void Start()
    {
        historicoCompletado = false;

        // --- 1. INICIALIZACIÓN MENÚ LATERAL ---
        if (panelLateral != null)
        {
            rectPanel = panelLateral.GetComponent<RectTransform>();
            VerticalLayoutGroup layout = panelLateral.GetComponentInChildren<VerticalLayoutGroup>();
            if (layout != null)
            {
                rectSecciones = layout.GetComponent<RectTransform>();
            }
            panelLateral.SetActive(false);
        }

        if (fondoCierre != null)
            fondoCierre.SetActive(false);

        // --- 2. VALORES POR DEFECTO EN INPUTS ---
        string hoy = DateTime.Now.ToString("dd-MM-yyyy");
        if (inputFechaInicio != null && string.IsNullOrEmpty(inputFechaInicio.text)) inputFechaInicio.text = hoy;
        if (inputFechaFin != null && string.IsNullOrEmpty(inputFechaFin.text)) inputFechaFin.text = hoy;
        if (inputHoraInicio != null && string.IsNullOrEmpty(inputHoraInicio.text)) inputHoraInicio.text = "08:00:00";
        if (inputHoraFin != null && string.IsNullOrEmpty(inputHoraFin.text)) inputHoraFin.text = "18:00:00";

        Time.timeScale = 1.0f;

        // --- 3. VERIFICAR REINICIO AUTOMÁTICO ---
        if (autoStartPendiente)
        {
            autoStartPendiente = false;

            bool esBBDD = (autoStartModo == ModoOrigen.BaseDeDatos_Historico);
            if (toggleModoBBDD != null)
            {
                toggleModoBBDD.isOn = esBBDD;
                modoSeleccionado = autoStartModo;
            }

            if (esBBDD)
            {
                if (inputFechaInicio != null && !string.IsNullOrEmpty(autoStartFechaIni)) inputFechaInicio.text = autoStartFechaIni;
                if (inputHoraInicio != null && !string.IsNullOrEmpty(autoStartHoraIni)) inputHoraInicio.text = autoStartHoraIni;
                if (inputFechaFin != null && !string.IsNullOrEmpty(autoStartFechaFin)) inputFechaFin.text = autoStartFechaFin;
                if (inputHoraFin != null && !string.IsNullOrEmpty(autoStartHoraFin)) inputHoraFin.text = autoStartHoraFin;
            }

            Debug.Log("<color=green>🔄 Escena reseteada. Arrancando simulación automáticamente...</color>");
            ArrancarSimulacion();
        }
        else
        {
            if (toggleModoBBDD != null)
            {
                modoSeleccionado = toggleModoBBDD.isOn ? ModoOrigen.BaseDeDatos_Historico : ModoOrigen.MQTT_Directo;
            }
            modoEnEjecucion = ModoOrigen.MQTT_Directo;
            ActualizarEstadoBotones();
        }
    }

    private void VincularEventosUI()
    {
        if (toggleModoBBDD != null)
        {
            toggleModoBBDD.onValueChanged.RemoveAllListeners();
            toggleModoBBDD.onValueChanged.AddListener(OnToggleModoCambiado);
        }

        if (btnPlay != null)
        {
            btnPlay.onClick.RemoveAllListeners();
            btnPlay.onClick.AddListener(OnBotonPlayPulsado);
        }

        if (btnReset != null)
        {
            btnReset.onClick.RemoveAllListeners();
            btnReset.onClick.AddListener(OnBotonResetPulsado);
        }

        if (inputFechaInicio != null)
        {
            inputFechaInicio.onEndEdit.RemoveAllListeners();
            inputFechaInicio.onEndEdit.AddListener((texto) => {
                if (!string.IsNullOrEmpty(texto) && texto.Contains("/"))
                    inputFechaInicio.text = texto.Replace('/', '-');
            });
        }

        if (inputFechaFin != null)
        {
            inputFechaFin.onEndEdit.RemoveAllListeners();
            inputFechaFin.onEndEdit.AddListener((texto) => {
                if (!string.IsNullOrEmpty(texto) && texto.Contains("/"))
                    inputFechaFin.text = texto.Replace('/', '-');
            });
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

    public void ToggleMenu()
    {
        if (panelLateral != null)
        {
            bool nuevoEstado = !panelLateral.activeSelf;
            CambiarEstadoMenu(nuevoEstado);
        }
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

    private void OnToggleModoCambiado(bool modoBBDDActivo)
    {
        modoSeleccionado = modoBBDDActivo ? ModoOrigen.BaseDeDatos_Historico : ModoOrigen.MQTT_Directo;
        historicoCompletado = false;

        // 🟢 Evalúa si el modo seleccionado difiere del que está corriendo para habilitar/deshabilitar Play
        ActualizarEstadoBotones();

        Debug.Log($"<color=cyan>[Simulación] Modo seleccionado en UI: {modoSeleccionado}</color>");
    }

    private void ActualizarEstadoBotones()
    {
        // El botón Play solo está habilitado si el modo seleccionado es DIFERENTE al modo en ejecución
        if (btnPlay != null)
        {
            btnPlay.interactable = (modoSeleccionado != modoEnEjecucion);
        }
        if (btnReset != null) btnReset.interactable = true;
    }

    public void OnBotonPlayPulsado()
    {
        Debug.Log("<color=yellow>🔄 Botón Play pulsado. Limpiando escena y reseteando...</color>");

        autoStartPendiente = true;
        autoStartModo = modoSeleccionado;
        if (inputFechaInicio != null) autoStartFechaIni = inputFechaInicio.text;
        if (inputHoraInicio != null) autoStartHoraIni = inputHoraInicio.text;
        if (inputFechaFin != null) autoStartFechaFin = inputFechaFin.text;
        if (inputHoraFin != null) autoStartHoraFin = inputHoraFin.text;

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
            if (MQTTClient.Instance != null) MQTTClient.Instance.enabled = true;
            if (MQTT_InterfaceClient.Instance != null) MQTT_InterfaceClient.Instance.enabled = true;

            Debug.Log("<color=green>▶️ INICIADO: Modo Tiempo Real (MQTT Activo)</color>");
        }
        else if (modoSeleccionado == ModoOrigen.BaseDeDatos_Historico)
        {
            if (MQTTClient.Instance != null) MQTTClient.Instance.enabled = false;
            if (MQTT_InterfaceClient.Instance != null) MQTT_InterfaceClient.Instance.enabled = false;

            SetFechasInteractables(false);

            if (ObtenerRangoFechas(out DateTime desde, out DateTime hasta))
            {
                Debug.Log($"<color=green>▶️ INICIADO HISTÓRICO BBDD | Desde: {desde:dd-MM-yyyy HH:mm:ss} Hasta: {hasta:dd-MM-yyyy HH:mm:ss}</color>");
                corrutinaReplayBBDD = StartCoroutine(ProcesarHistoricoBBDD(desde, hasta));
            }
            else
            {
                Debug.LogError("❌ Formato de fecha u hora incorrecto.");
                DetenerYResetearEstado();
            }
        }
    }

    public void OnBotonResetPulsado()
    {
        autoStartPendiente = false;
        Debug.Log("<color=red>🔄 RESET: Recargando escena limpia de Unity...</color>");
        RecargarEscenaLimpia();
    }

    private void RecargarEscenaLimpia()
    {
        Time.timeScale = 1.0f;

        if (MQTTClient.Instance != null) MQTTClient.Instance.enabled = false;
        if (MQTT_InterfaceClient.Instance != null) MQTT_InterfaceClient.Instance.enabled = false;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void DetenerYResetearEstado()
    {
        estadoActual = EstadoSimulacion.Detenido;
        modoEnEjecucion = null; // Permite que se pueda volver a pulsar Play
        Time.timeScale = 1.0f;

        if (corrutinaReplayBBDD != null)
        {
            StopCoroutine(corrutinaReplayBBDD);
            corrutinaReplayBBDD = null;
        }

        if (MQTTClient.Instance != null) MQTTClient.Instance.enabled = false;
        if (MQTT_InterfaceClient.Instance != null) MQTT_InterfaceClient.Instance.enabled = false;

        SetFechasInteractables(true);
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
                    multiplicadorVelocidad,
                    (horaMuestra) => ActualizarTextoReloj(horaMuestra)
                )
            );
        }
        else
        {
            Debug.LogWarning("⚠️ InfluxDBClient no encontrado en la escena. Ejecutando simulación de prueba...");

            DateTime tiempoSimulado = desde;
            for (int i = 0; i < 20; i++)
            {
                ActualizarTextoReloj(tiempoSimulado);
                tiempoSimulado = tiempoSimulado.AddSeconds(1);
                yield return new WaitForSeconds(1.0f / Mathf.Max(0.1f, multiplicadorVelocidad));
            }
        }

        historicoCompletado = true;
        Debug.Log("<color=green>✅ Fin de la reproducción BBDD.</color>");
        DetenerYResetearEstado();
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

    private bool ObtenerRangoFechas(out DateTime fechaInicio, out DateTime fechaFin)
    {
        fechaInicio = DateTime.Today.AddHours(8);
        fechaFin = DateTime.Today.AddHours(18);

        if (inputFechaInicio == null || inputHoraInicio == null || inputFechaFin == null || inputHoraFin == null)
        {
            return true;
        }

        try
        {
            if (inputFechaInicio.text.Contains("/")) inputFechaInicio.text = inputFechaInicio.text.Replace('/', '-');
            if (inputFechaFin.text.Contains("/")) inputFechaFin.text = inputFechaFin.text.Replace('/', '-');

            string stringInicio = $"{inputFechaInicio.text} {inputHoraInicio.text}";
            string stringFin = $"{inputFechaFin.text} {inputHoraFin.text}";

            string[] formatos = new string[]
            {
                "dd-MM-yyyy HH:mm:ss",
                "dd-MM-yyyy HH:mm",
                "d-M-yyyy HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm"
            };

            DateTimeStyles estiloZona = DateTimeStyles.AssumeLocal;

            bool inicioOK = DateTime.TryParseExact(stringInicio, formatos, CultureInfo.InvariantCulture, estiloZona, out fechaInicio);
            if (!inicioOK) inicioOK = DateTime.TryParse(stringInicio, CultureInfo.InvariantCulture, estiloZona, out fechaInicio);

            bool finOK = DateTime.TryParseExact(stringFin, formatos, CultureInfo.InvariantCulture, estiloZona, out fechaFin);
            if (!finOK) finOK = DateTime.TryParse(stringFin, CultureInfo.InvariantCulture, estiloZona, out fechaFin);

            return inicioOK && finOK;
        }
        catch
        {
            return false;
        }
    }

    private void SetFechasInteractables(bool estado)
    {
        if (inputFechaInicio != null) inputFechaInicio.interactable = estado;
        if (inputHoraInicio != null) inputHoraInicio.interactable = estado;
        if (inputFechaFin != null) inputFechaFin.interactable = estado;
        if (inputHoraFin != null) inputHoraFin.interactable = estado;
    }
}