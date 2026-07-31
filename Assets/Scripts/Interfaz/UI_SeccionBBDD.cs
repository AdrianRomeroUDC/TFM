using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

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
    public TMP_Dropdown dropdownMinFin; // 👈 ¡Declarada aquí para solucionar el error!
    public TMP_Dropdown dropdownSegFin;

    // Persistencia global de fechas seleccionadas por el usuario
    private static bool fechasGuardadasInicializadas = false;
    private static DateTime fechaInicioGuardada;
    private static DateTime fechaFinGuardada;

    private Action callbackCambioControl;

    public void Inicializar(Action alCambiarControl)
    {
        Debug.Log("🔍 [UI_SeccionBBDD] -> Método Inicializar() llamado.");
        callbackCambioControl = alCambiarControl;

        VerificarReferenciasInspector();
        InicializarControlesTiempo();
        OcultarYColapsarMultiplicadores();
        VincularListenersDeCambioEnControles(alCambiarControl);
    }

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
        Debug.Log("🔍 [UI_SeccionBBDD] OnEnable() ejecutado. Iniciando corrutina de refresco visual.");
        StartCoroutine(RefrescarVisualsAlActivar());
    }

    private IEnumerator RefrescarVisualsAlActivar()
    {
        yield return null; // Esperar 1 frame
        RefrescarTodosLosDropdownsVisualmente();
    }

    private void OcultarYColapsarMultiplicadores()
    {
        if (contenedorMultiplicador != null) contenedorMultiplicador.SetActive(false);
    }

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
            calendarInicio.OnFechaSeleccionada -= ResponderACambio;
            calendarInicio.OnFechaSeleccionada += ResponderACambio;
        }

        if (calendarFin != null)
        {
            calendarFin.OnFechaSeleccionada -= ResponderACambio;
            calendarFin.OnFechaSeleccionada += ResponderACambio;
        }
    }

    private void VincularListenerDropdown(TMP_Dropdown dropdown, Action callback)
    {
        if (dropdown == null) return;
        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener((_) => callback?.Invoke());
    }

    private void ResponderACambio(DateTime fecha)
    {
        callbackCambioControl?.Invoke();
    }

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