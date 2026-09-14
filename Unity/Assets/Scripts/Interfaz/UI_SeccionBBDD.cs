using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Panel de la interfaz que permite al usuario elegir un rango de fechas y horas (inicio y fin)
/// para reproducir el histórico de la fábrica guardado en InfluxDB. Gestiona el calendario y los
/// desplegables de hora/minuto/segundo de ambos extremos del rango, valida y expone ese rango a
/// <see cref="UI_ControladorMenu"/> (que es quien realmente arranca la reproducción llamando a
/// <c>InfluxDBClient</c>), y recuerda la última selección del usuario entre reinicios de la escena
/// mediante variables estáticas.
/// </summary>
public class UI_SeccionBBDD : MonoBehaviour
{
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
    public TMP_Dropdown dropdownMinFin; // Dropdown de minutos del rango FIN.
    public TMP_Dropdown dropdownSegFin;

    // Persistencia global de fechas seleccionadas por el usuario: al ser "static", estos valores
    // sobreviven a la recarga de la escena (que se usa, por ejemplo, al pulsar PLAY para cambiar de modo).
    private static bool fechasGuardadasInicializadas = false;
    private static DateTime fechaInicioGuardada;
    private static DateTime fechaFinGuardada;

    // Callback que se ejecuta cada vez que el usuario cambia cualquier control de fecha/hora,
    // para que UI_ControladorMenu pueda reevaluar si el botón PLAY debe mostrarse como "cambio pendiente".
    private Action callbackCambioControl;

    /// <summary>
    /// Prepara todo el panel: comprueba que las referencias del Inspector estén bien asignadas,
    /// rellena los desplegables de hora/minuto/segundo, oculta los botones de multiplicador de
    /// velocidad (no usados en este panel) y conecta los listeners de cambio en los controles.
    /// La llama <see cref="UI_ControladorMenu"/> al arrancar la escena.
    /// </summary>
    /// <param name="alCambiarControl">Función a ejecutar cada vez que el usuario modifique una fecha u hora.</param>
    public void Inicializar(Action alCambiarControl)
    {
        Debug.Log("🔍 [UI_SeccionBBDD] -> Método Inicializar() llamado.");
        callbackCambioControl = alCambiarControl;

        VerificarReferenciasInspector();
        InicializarControlesTiempo();
        OcultarYColapsarMultiplicadores();
        VincularListenersDeCambioEnControles(alCambiarControl);
    }

    // Comprueba que todas las referencias que deberían haberse arrastrado en el Inspector de Unity
    // estén realmente asignadas, avisando por consola si falta alguna (ayuda a detectar errores de configuración).
    private void VerificarReferenciasInspector()
    {
        Debug.Log("🔍 [UI_SeccionBBDD] Comprobando referencias del Inspector...");

        if (dropdownHoraInicio == null) Debug.LogError("❌ [UI_SeccionBBDD] 'dropdownHoraInicio' es NULL en el Inspector.");
        if (dropdownMinInicio == null) Debug.LogError("❌ [UI_SeccionBBDD] 'dropdownMinInicio' es NULL en el Inspector.");
        if (dropdownSegInicio == null) Debug.LogError("❌ [UI_SeccionBBDD] 'dropdownSegInicio' es NULL en el Inspector.");

        if (dropdownHoraFin == null) Debug.LogError("❌ [UI_SeccionBBDD] 'dropdownHoraFin' es NULL en el Inspector.");
        if (dropdownMinFin == null) Debug.LogError("❌ [UI_SeccionBBDD] 'dropdownMinFin' es NULL en el Inspector.");
        if (dropdownSegFin == null) Debug.LogError("❌ [UI_SeccionBBDD] 'dropdownSegFin' es NULL en el Inspector.");

        if (calendarInicio == null) Debug.LogWarning("⚠️ [UI_SeccionBBDD] 'calendarInicio' es NULL.");
        if (calendarFin == null) Debug.LogWarning("⚠️ [UI_SeccionBBDD] 'calendarFin' es NULL.");
    }

    private void OnEnable()
    {
        // Cada vez que este panel se vuelve a activar (por ejemplo al abrir el menú), refrescamos
        // visualmente los desplegables, porque a veces Unity no repinta bien su texto tras estar ocultos.
        Debug.Log("🔍 [UI_SeccionBBDD] OnEnable() ejecutado. Iniciando corrutina de refresco visual.");
        StartCoroutine(RefrescarVisualsAlActivar());
    }

    // Espera un frame antes de refrescar, para dar tiempo a que Unity termine de activar todos los
    // componentes del panel antes de tocar su contenido visual.
    private IEnumerator RefrescarVisualsAlActivar()
    {
        yield return null; // Esperar 1 frame
        RefrescarTodosLosDropdownsVisualmente();
    }

    // Este panel no usa los botones de multiplicador de velocidad (x1, x2, x5), así que se ocultan
    // por completo si el contenedor está asignado.
    private void OcultarYColapsarMultiplicadores()
    {
        if (contenedorMultiplicador != null) contenedorMultiplicador.SetActive(false);
    }

    // Conecta el callback de cambio a cada desplegable y a cada calendario, para que cualquier
    // modificación que haga el usuario dispare la función indicada (normalmente, reevaluar el botón PLAY).
    private void VincularListenersDeCambioEnControles(Action alCambiarControl)
    {
        VincularListenerDropdown(dropdownHoraInicio, alCambiarControl);
        VincularListenerDropdown(dropdownMinInicio, alCambiarControl);
        VincularListenerDropdown(dropdownSegInicio, alCambiarControl);

        VincularListenerDropdown(dropdownHoraFin, alCambiarControl);
        VincularListenerDropdown(dropdownMinFin, alCambiarControl);
        VincularListenerDropdown(dropdownSegFin, alCambiarControl);

        if (calendarInicio != null)
        {
            // Nos aseguramos de no dejar el listener duplicado si Inicializar() se llama más de una vez.
            calendarInicio.OnFechaSeleccionada -= ResponderACambio;
            calendarInicio.OnFechaSeleccionada += ResponderACambio;
        }

        if (calendarFin != null)
        {
            calendarFin.OnFechaSeleccionada -= ResponderACambio;
            calendarFin.OnFechaSeleccionada += ResponderACambio;
        }
    }

    // Ayuda a no repetir código: limpia los listeners previos del dropdown y añade uno nuevo
    // que simplemente reenvía el aviso al callback recibido, ignorando el valor concreto elegido.
    private void VincularListenerDropdown(TMP_Dropdown dropdown, Action callback)
    {
        if (dropdown == null) return;
        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener((_) => callback?.Invoke());
    }

    // Adaptador para que el evento OnFechaSeleccionada del calendario (que manda la fecha elegida)
    // encaje con el callback genérico "sin parámetros" que usa el resto del panel.
    private void ResponderACambio(DateTime fecha)
    {
        callbackCambioControl?.Invoke();
    }

    // Rellena los desplegables de horas (0-23) y de minutos/segundos (0-59), usando por defecto
    // la última fecha guardada (o, si es la primera vez, un rango de 8:00 a 18:00 del día actual).
    private void InicializarControlesTiempo()
    {
        if (!fechasGuardadasInicializadas)
        {
            fechaInicioGuardada = DateTime.Today.AddHours(8);
            fechaFinGuardada = DateTime.Today.AddHours(18);
            fechasGuardadasInicializadas = true;
            Debug.Log($"🕒 [UI_SeccionBBDD] Fechas inicializadas por defecto: Inicio={fechaInicioGuardada}, Fin={fechaFinGuardada}");
        }

        List<string> horas = new List<string>();
        for (int i = 0; i < 24; i++) horas.Add(i.ToString("D2"));

        List<string> minSeg = new List<string>();
        for (int i = 0; i < 60; i++) minSeg.Add(i.ToString("D2"));

        Debug.Log("🛠️ [UI_SeccionBBDD] Poblando desplegables de tiempo...");
        PoblarDropdown(dropdownHoraInicio, horas, fechaInicioGuardada.Hour);
        PoblarDropdown(dropdownMinInicio, minSeg, fechaInicioGuardada.Minute);
        PoblarDropdown(dropdownSegInicio, minSeg, fechaInicioGuardada.Second);

        PoblarDropdown(dropdownHoraFin, horas, fechaFinGuardada.Hour);
        PoblarDropdown(dropdownMinFin, minSeg, fechaFinGuardada.Minute);
        PoblarDropdown(dropdownSegFin, minSeg, fechaFinGuardada.Second);

        if (calendarInicio != null) calendarInicio.SetFechaInicial(fechaInicioGuardada);
        if (calendarFin != null) calendarFin.SetFechaInicial(fechaFinGuardada);
    }

    /// <summary>
    /// Ajusta todos los controles del panel (calendarios y desplegables) para reflejar un rango de
    /// fechas concreto. Se usa cuando la escena se recarga en modo "auto-arranque" (por ejemplo,
    /// tras pulsar PLAY) y hay que restaurar exactamente la selección que el usuario había hecho antes.
    /// </summary>
    /// <param name="fechaIni">Fecha y hora de inicio del rango a restaurar.</param>
    /// <param name="fechaFin">Fecha y hora de fin del rango a restaurar.</param>
    public void ConfigurarEstadoPorAutoStart(DateTime fechaIni, DateTime fechaFin)
    {
        Debug.Log($"🔄 [UI_SeccionBBDD] ConfigurarEstadoPorAutoStart: {fechaIni} -> {fechaFin}");
        fechaInicioGuardada = fechaIni;
        fechaFinGuardada = fechaFin;

        if (calendarInicio != null) calendarInicio.SetFechaInicial(fechaIni);
        if (calendarFin != null) calendarFin.SetFechaInicial(fechaFin);

        SetDropdownValor(dropdownHoraInicio, fechaIni.Hour);
        SetDropdownValor(dropdownMinInicio, fechaIni.Minute);
        SetDropdownValor(dropdownSegInicio, fechaIni.Second);

        SetDropdownValor(dropdownHoraFin, fechaFin.Hour);
        SetDropdownValor(dropdownMinFin, fechaFin.Minute);
        SetDropdownValor(dropdownSegFin, fechaFin.Second);
    }

    /// <summary>
    /// Lee los controles del panel (calendarios + desplegables de hora/minuto/segundo) y construye
    /// las dos fechas completas (inicio y fin) que el usuario ha seleccionado para el histórico.
    /// También guarda ese rango en las variables estáticas para que sobreviva a un reinicio de escena.
    /// </summary>
    /// <param name="fechaInicio">Fecha y hora de inicio resultante.</param>
    /// <param name="fechaFin">Fecha y hora de fin resultante.</param>
    /// <returns>true si se pudo construir el rango correctamente; false si ocurrió algún error.</returns>
    public bool ObtenerRangoFechas(out DateTime fechaInicio, out DateTime fechaFin)
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

            fechaInicioGuardada = fechaInicio;
            fechaFinGuardada = fechaFin;

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ [UI_SeccionBBDD] Error al obtener rango de fechas: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Activa o desactiva todos los controles del panel (calendarios y desplegables). Se usa para
    /// bloquear el panel cuando el modo activo no es el histórico de base de datos, evitando que el
    /// usuario cambie fechas mientras no tiene efecto.
    /// </summary>
    /// <param name="estado">true para permitir la interacción, false para bloquearla.</param>
    public void SetUIInteractables(bool estado)
    {
        if (calendarInicio != null) calendarInicio.SetInteractable(estado);
        if (calendarFin != null) calendarFin.SetInteractable(estado);

        SetDropdownInteractable(dropdownHoraInicio, estado);
        SetDropdownInteractable(dropdownMinInicio, estado);
        SetDropdownInteractable(dropdownSegInicio, estado);

        SetDropdownInteractable(dropdownHoraFin, estado);
        SetDropdownInteractable(dropdownMinFin, estado);
        SetDropdownInteractable(dropdownSegFin, estado);
    }

    private void SetDropdownInteractable(TMP_Dropdown dropdown, bool interactable)
    {
        if (dropdown != null)
        {
            dropdown.interactable = interactable;
        }
    }

    // Rellena un desplegable con la lista de opciones dada y selecciona el índice por defecto indicado,
    // dejando además avisos por consola si falta alguna referencia (para depurar errores de configuración del Inspector).
    private void PoblarDropdown(TMP_Dropdown dropdown, List<string> opciones, int indiceDefecto)
    {
        if (dropdown == null)
        {
            Debug.LogError("❌ [UI_SeccionBBDD] PoblarDropdown omitido: El campo TMP_Dropdown es NULL.");
            return;
        }

        string nombreObjeto = dropdown.gameObject.name;

        dropdown.ClearOptions();
        dropdown.AddOptions(opciones);

        int targetIndex = Mathf.Clamp(indiceDefecto, 0, opciones.Count - 1);

        // Truco: forzamos primero un valor "-1" para asegurarnos de que al asignar targetIndex
        // se refresque bien el texto mostrado, incluso si targetIndex coincide con el valor previo.
        dropdown.SetValueWithoutNotify(-1);
        dropdown.value = targetIndex;
        dropdown.RefreshShownValue();

        if (dropdown.captionText == null)
        {
            Debug.LogError($"❌ [UI_SeccionBBDD] CRÍTICO: El dropdown '{nombreObjeto}' NO tiene asignada la referencia 'Caption Text' en su componente TMP_Dropdown del Inspector de Unity!");
        }
        else
        {
            if (targetIndex < opciones.Count)
            {
                dropdown.captionText.text = opciones[targetIndex];
                Debug.Log($"✅ [UI_SeccionBBDD] Dropdown '{nombreObjeto}' poblado correctamente. Opciones: {opciones.Count}. Valor asignado: [{opciones[targetIndex]}]. Texto CaptionText final: '{dropdown.captionText.text}'");
            }
        }
    }

    // Cambia el valor seleccionado de un desplegable ya poblado (sin volver a rellenar sus opciones),
    // usado por ConfigurarEstadoPorAutoStart para restaurar una selección previa.
    private void SetDropdownValor(TMP_Dropdown dropdown, int valor)
    {
        if (dropdown == null)
        {
            Debug.LogError("❌ [UI_SeccionBBDD] SetDropdownValor omitido: dropdown es NULL.");
            return;
        }

        if (dropdown.options != null && dropdown.options.Count > 0)
        {
            int targetIndex = Mathf.Clamp(valor, 0, dropdown.options.Count - 1);

            dropdown.SetValueWithoutNotify(-1);
            dropdown.value = targetIndex;
            dropdown.RefreshShownValue();

            if (dropdown.captionText != null)
            {
                dropdown.captionText.text = dropdown.options[targetIndex].text;
                Debug.Log($"🔹 [UI_SeccionBBDD] Dropdown '{dropdown.name}' cambiado a valor {valor} -> Texto: '{dropdown.captionText.text}'");
            }
            else
            {
                Debug.LogError($"❌ [UI_SeccionBBDD] Dropdown '{dropdown.name}' no tiene CaptionText!");
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ [UI_SeccionBBDD] Dropdown '{dropdown.name}' no tiene opciones para seleccionar el valor {valor}.");
        }
    }

    // Lee el número (hora, minuto o segundo) representado por el texto de la opción actualmente
    // seleccionada en el desplegable; si algo falla, devuelve el valor por defecto indicado.
    private int ObtenerValorDropdown(TMP_Dropdown dropdown, int valorPorDefecto)
    {
        if (dropdown != null && dropdown.options != null && dropdown.options.Count > 0)
        {
            int index = dropdown.value;
            if (index >= 0 && index < dropdown.options.Count)
            {
                if (int.TryParse(dropdown.options[index].text, out int res))
                    return res;
            }
        }
        Debug.LogWarning($"⚠️ [UI_SeccionBBDD] No se pudo leer valor de Dropdown '{(dropdown != null ? dropdown.name : "NULL")}'. Usando valor por defecto: {valorPorDefecto}");
        return valorPorDefecto;
    }

    // Fuerza el repintado del texto mostrado en todos los desplegables del panel (a veces Unity
    // no actualiza bien el texto visible tras activar/desactivar el panel).
    private void RefrescarTodosLosDropdownsVisualmente()
    {
        Debug.Log("🔄 [UI_SeccionBBDD] Refrescando visualmente todos los desplegables...");
        RefrescarDropdown(dropdownHoraInicio);
        RefrescarDropdown(dropdownMinInicio);
        RefrescarDropdown(dropdownSegInicio);
        RefrescarDropdown(dropdownHoraFin);
        RefrescarDropdown(dropdownMinFin);
        RefrescarDropdown(dropdownSegFin);
    }

    private void RefrescarDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown != null && dropdown.options != null && dropdown.options.Count > 0)
        {
            dropdown.RefreshShownValue();
            int idx = Mathf.Clamp(dropdown.value, 0, dropdown.options.Count - 1);
            if (dropdown.captionText != null)
            {
                dropdown.captionText.text = dropdown.options[idx].text;
            }
        }
    }
}
