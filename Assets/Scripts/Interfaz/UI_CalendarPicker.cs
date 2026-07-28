using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

public class UI_CalendarPicker : MonoBehaviour, IPointerClickHandler
{
    // 🟢 Evento añadido para avisar a UI_ControladorMenu cuando el usuario cambia el día
    public event Action<DateTime> OnFechaSeleccionada;

    public DateTime FechaSeleccionada { get; private set; } = DateTime.Today;

    private GameObject rootOverlay;
    private GameObject panelCalendarioModal;
    private TMP_Text textoFechaSeleccionada;
    private TMP_Text textoMesAnoHeader;
    private Transform contenedorDiasGrid;
    private DateTime mesVisualizado = DateTime.Today;
    private List<GameObject> objetosDiasInstanciados = new List<GameObject>();

    private int ultimoFrameEjecucion = -1;

    private void Awake()
    {
        textoFechaSeleccionada = GetComponentInChildren<TMP_Text>();
        if (textoFechaSeleccionada != null)
        {
            textoFechaSeleccionada.textWrappingMode = TextWrappingModes.NoWrap;
            textoFechaSeleccionada.fontSizeMin = 10;
            textoFechaSeleccionada.fontSizeMax = 14;
        }

        Button btnSelf = GetComponent<Button>();
        if (btnSelf != null)
        {
            btnSelf.onClick.RemoveAllListeners();
            btnSelf.onClick.AddListener(ToggleCalendario);
        }

        ActualizarTextoBoton();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ToggleCalendario();
    }

    public void ToggleCalendario()
    {
        if (Time.frameCount == ultimoFrameEjecucion) return;
        ultimoFrameEjecucion = Time.frameCount;

        if (rootOverlay == null)
        {
            ConstruirCalendarioPorCodigo();
        }

        if (rootOverlay == null) return;

        bool activo = !rootOverlay.activeSelf;
        rootOverlay.SetActive(activo);

        if (activo)
        {
            rootOverlay.transform.SetAsLastSibling();
            mesVisualizado = FechaSeleccionada;
            RenderizarDias();
        }
    }

    public void SetFechaInicial(DateTime fecha)
    {
        FechaSeleccionada = fecha;
        mesVisualizado = fecha;
        ActualizarTextoBoton();
    }

    private void ConstruirCalendarioPorCodigo()
    {
        Canvas canvasPadre = GetComponentInParent<Canvas>();
        if (canvasPadre == null)
        {
            canvasPadre = FindFirstObjectByType<Canvas>();
        }

        if (canvasPadre == null)
        {
            Debug.LogError("❌ [CalendarPicker] No se encontró ningún Canvas en la escena.");
            return;
        }

        rootOverlay = new GameObject("Calendar_Overlay_Root", typeof(RectTransform), typeof(Image), typeof(Button));
        rootOverlay.transform.SetParent(canvasPadre.transform, false);

        RectTransform rOverlay = rootOverlay.GetComponent<RectTransform>();
        rOverlay.anchorMin = Vector2.zero;
        rOverlay.anchorMax = Vector2.one;
        rOverlay.offsetMin = Vector2.zero;
        rOverlay.offsetMax = Vector2.zero;
        rOverlay.localPosition = Vector3.zero;

        Image imgOverlay = rootOverlay.GetComponent<Image>();
        imgOverlay.color = new Color(0f, 0f, 0f, 0.4f);

        Button btnOverlay = rootOverlay.GetComponent<Button>();
        btnOverlay.onClick.AddListener(() => {
            if (rootOverlay != null) rootOverlay.SetActive(false);
        });

        Canvas canvasModal = rootOverlay.AddComponent<Canvas>();
        canvasModal.overrideSorting = true;
        canvasModal.sortingOrder = 999;
        rootOverlay.AddComponent<GraphicRaycaster>();

        panelCalendarioModal = new GameObject("Calendar_Modal_Panel", typeof(RectTransform), typeof(Image));
        panelCalendarioModal.transform.SetParent(rootOverlay.transform, false);

        RectTransform rectModal = panelCalendarioModal.GetComponent<RectTransform>();
        rectModal.anchorMin = new Vector2(0.5f, 0.5f);
        rectModal.anchorMax = new Vector2(0.5f, 0.5f);
        rectModal.pivot = new Vector2(0.5f, 0.5f);
        rectModal.anchoredPosition = Vector2.zero;
        rectModal.sizeDelta = new Vector2(280, 320);
        panelCalendarioModal.transform.localScale = Vector3.one;

        Image imgModal = panelCalendarioModal.GetComponent<Image>();
        imgModal.color = new Color(0.1f, 0.12f, 0.22f, 0.98f);

        Button blockBtn = panelCalendarioModal.AddComponent<Button>();
        blockBtn.transition = Selectable.Transition.None;

        GameObject headerGo = new GameObject("Header", typeof(RectTransform));
        headerGo.transform.SetParent(panelCalendarioModal.transform, false);
        RectTransform rectHeader = headerGo.GetComponent<RectTransform>();
        rectHeader.anchorMin = new Vector2(0, 1);
        rectHeader.anchorMax = new Vector2(1, 1);
        rectHeader.pivot = new Vector2(0.5f, 1);
        rectHeader.anchoredPosition = new Vector2(0, -10);
        rectHeader.sizeDelta = new Vector2(-20, 40);

        Button btnPrev = CrearBotonTexto("<", headerGo.transform, new Vector2(30, 30));
        RectTransform rPrev = btnPrev.GetComponent<RectTransform>();
        rPrev.anchorMin = new Vector2(0, 0.5f);
        rPrev.anchorMax = new Vector2(0, 0.5f);
        rPrev.anchoredPosition = new Vector2(20, 0);
        btnPrev.onClick.AddListener(() => { mesVisualizado = mesVisualizado.AddMonths(-1); RenderizarDias(); });

        Button btnNext = CrearBotonTexto(">", headerGo.transform, new Vector2(30, 30));
        RectTransform rNext = btnNext.GetComponent<RectTransform>();
        rNext.anchorMin = new Vector2(1, 0.5f);
        rNext.anchorMax = new Vector2(1, 0.5f);
        rNext.anchoredPosition = new Vector2(-20, 0);
        btnNext.onClick.AddListener(() => { mesVisualizado = mesVisualizado.AddMonths(1); RenderizarDias(); });

        GameObject txtHeaderGo = new GameObject("Text_MonthYear", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtHeaderGo.transform.SetParent(headerGo.transform, false);
        textoMesAnoHeader = txtHeaderGo.GetComponent<TMP_Text>();
        textoMesAnoHeader.alignment = TextAlignmentOptions.Center;
        textoMesAnoHeader.fontSize = 15;
        textoMesAnoHeader.fontStyle = FontStyles.Bold;
        textoMesAnoHeader.color = Color.white;
        RectTransform rTxtHeader = txtHeaderGo.GetComponent<RectTransform>();
        rTxtHeader.anchorMin = Vector2.zero;
        rTxtHeader.anchorMax = Vector2.one;
        rTxtHeader.offsetMin = new Vector2(40, 0);
        rTxtHeader.offsetMax = new Vector2(-40, 0);

        GameObject daysHeaderGo = new GameObject("DaysOfWeekHeader", typeof(RectTransform), typeof(GridLayoutGroup));
        daysHeaderGo.transform.SetParent(panelCalendarioModal.transform, false);
        RectTransform rDaysHeader = daysHeaderGo.GetComponent<RectTransform>();
        rDaysHeader.anchorMin = new Vector2(0, 1);
        rDaysHeader.anchorMax = new Vector2(1, 1);
        rDaysHeader.pivot = new Vector2(0.5f, 1);
        rDaysHeader.anchoredPosition = new Vector2(0, -50);
        rDaysHeader.sizeDelta = new Vector2(-20, 20);

        GridLayoutGroup gridHeader = daysHeaderGo.GetComponent<GridLayoutGroup>();
        gridHeader.cellSize = new Vector2(34, 18);
        gridHeader.spacing = new Vector2(3, 2);
        gridHeader.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridHeader.constraintCount = 7;

        string[] cabeceraDias = { "L", "M", "X", "J", "V", "S", "D" };
        foreach (string d in cabeceraDias)
        {
            GameObject tGo = new GameObject("D_" + d, typeof(RectTransform), typeof(TextMeshProUGUI));
            tGo.transform.SetParent(daysHeaderGo.transform, false);
            TMP_Text t = tGo.GetComponent<TMP_Text>();
            t.text = d;
            t.fontSize = 11;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.6f, 0.7f, 0.9f);
        }

        GameObject gridGo = new GameObject("Grid_Dias", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGo.transform.SetParent(panelCalendarioModal.transform, false);
        contenedorDiasGrid = gridGo.transform;

        RectTransform rGrid = gridGo.GetComponent<RectTransform>();
        rGrid.anchorMin = new Vector2(0, 1);
        rGrid.anchorMax = new Vector2(1, 1);
        rGrid.pivot = new Vector2(0.5f, 1);
        rGrid.anchoredPosition = new Vector2(0, -75);
        rGrid.sizeDelta = new Vector2(-20, 210);

        GridLayoutGroup grid = gridGo.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(34, 28);
        grid.spacing = new Vector2(3, 3);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 7;

        rootOverlay.SetActive(false);
    }

    private Button CrearBotonTexto(string texto, Transform parent, Vector2 size)
    {
        GameObject go = new GameObject("Btn_" + texto, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        go.transform.localScale = Vector3.one;

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.2f, 0.25f, 0.38f, 1f);

        GameObject txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(go.transform, false);
        TMP_Text txt = txtGo.GetComponent<TMP_Text>();
        txt.text = texto;
        txt.fontSize = 12;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;

        RectTransform rTxt = txtGo.GetComponent<RectTransform>();
        rTxt.anchorMin = Vector2.zero;
        rTxt.anchorMax = Vector2.one;
        rTxt.sizeDelta = Vector2.zero;

        return go.GetComponent<Button>();
    }

    private void RenderizarDias()
    {
        if (textoMesAnoHeader != null)
        {
            textoMesAnoHeader.text = mesVisualizado.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-ES"));
        }

        foreach (var obj in objetosDiasInstanciados)
        {
            if (obj != null) Destroy(obj);
        }
        objetosDiasInstanciados.Clear();

        int diasEnMes = DateTime.DaysInMonth(mesVisualizado.Year, mesVisualizado.Month);
        DateTime primerDiaMes = new DateTime(mesVisualizado.Year, mesVisualizado.Month, 1);
        int diaSemanaInicio = ((int)primerDiaMes.DayOfWeek + 6) % 7;

        for (int i = 0; i < diaSemanaInicio; i++)
        {
            GameObject empty = new GameObject("Empty", typeof(RectTransform));
            empty.transform.SetParent(contenedorDiasGrid, false);
            objetosDiasInstanciados.Add(empty);
        }

        for (int dia = 1; dia <= diasEnMes; dia++)
        {
            int numDia = dia;
            Button btn = CrearBotonTexto(numDia.ToString(), contenedorDiasGrid, new Vector2(34, 28));

            bool esSeleccionado = (mesVisualizado.Year == FechaSeleccionada.Year &&
                                   mesVisualizado.Month == FechaSeleccionada.Month &&
                                   numDia == FechaSeleccionada.Day);

            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = esSeleccionado ? new Color(0f, 0.65f, 1f, 1f) : new Color(0.18f, 0.22f, 0.32f, 1f);
            }

            btn.onClick.AddListener(() => SeleccionarDia(numDia));
            objetosDiasInstanciados.Add(btn.gameObject);
        }
    }

    private void SeleccionarDia(int dia)
    {
        FechaSeleccionada = new DateTime(mesVisualizado.Year, mesVisualizado.Month, dia);
        ActualizarTextoBoton();

        if (rootOverlay != null)
        {
            rootOverlay.SetActive(false);
        }

        // 🟢 Avisar a UI_ControladorMenu para habilitar el botón PLAY si cambió la fecha
        OnFechaSeleccionada?.Invoke(FechaSeleccionada);
    }

    private void ActualizarTextoBoton()
    {
        if (textoFechaSeleccionada != null)
        {
            textoFechaSeleccionada.text = FechaSeleccionada.ToString("dd-MM-yyyy");
        }
    }

    public void SetInteractable(bool interactable)
    {
        Button btnSelf = GetComponent<Button>();
        if (btnSelf != null) btnSelf.interactable = interactable;
        if (!interactable && rootOverlay != null) rootOverlay.SetActive(false);
    }
}