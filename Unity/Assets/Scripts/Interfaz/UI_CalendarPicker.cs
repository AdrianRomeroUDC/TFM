using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Selector de fecha tipo "calendario" que el usuario despliega pulsando un botón.
/// Todo el calendario (fondo oscuro, panel, cabecera con mes/año, flechas de mes
/// anterior/siguiente y la cuadrícula de días) se crea por código la primera vez que
/// se usa, sin necesidad de diseñarlo a mano en el Editor de Unity.
/// Se usa, por ejemplo, en <see cref="UI_SeccionBBDD"/> (una vez como fecha de inicio
/// y otra como fecha de fin) para que el usuario elija el rango de fechas del que
/// quiere consultar y reproducir el histórico guardado en InfluxDB.
/// Cuando el usuario elige un día, este script avisa mediante el evento
/// <see cref="OnFechaSeleccionada"/> para que quien esté escuchando (por ejemplo
/// <see cref="UI_SeccionBBDD"/>) pueda reaccionar, como habilitar el botón de "Play".
/// </summary>
public class UI_CalendarPicker : MonoBehaviour, IPointerClickHandler
{
    // 🟢 Evento añadido para avisar a UI_ControladorMenu cuando el usuario cambia el día
    public event Action<DateTime> OnFechaSeleccionada;

    // Fecha actualmente elegida por el usuario (por defecto, hoy)
    public DateTime FechaSeleccionada { get; private set; } = DateTime.Today;

    // Referencias a las piezas del calendario que se construyen dinámicamente
    private GameObject rootOverlay;
    private GameObject panelCalendarioModal;
    private TMP_Text textoFechaSeleccionada;
    private TMP_Text textoMesAnoHeader;
    private Transform contenedorDiasGrid;
    private DateTime mesVisualizado = DateTime.Today; // Mes que se está mostrando en el calendario (puede no coincidir con el elegido si el usuario navega con las flechas)
    private List<GameObject> objetosDiasInstanciados = new List<GameObject>();

    // Evita que un mismo clic dispare el toggle dos veces en el mismo frame
    private int ultimoFrameEjecucion = -1;

    private void Awake()
    {
        // Buscamos el texto del propio botón (donde se muestra la fecha elegida, ej "13-09-2026")
        // y ajustamos su tamaño para que quepa siempre en una sola línea
        textoFechaSeleccionada = GetComponentInChildren<TMP_Text>();
        if (textoFechaSeleccionada != null)
        {
            textoFechaSeleccionada.textWrappingMode = TextWrappingModes.NoWrap;
            textoFechaSeleccionada.fontSizeMin = 10;
            textoFechaSeleccionada.fontSizeMax = 14;
        }

        // Este propio objeto es un botón: al pulsarlo, se abre o cierra el calendario
        Button btnSelf = GetComponent<Button>();
        if (btnSelf != null)
        {
            btnSelf.onClick.RemoveAllListeners();
            btnSelf.onClick.AddListener(ToggleCalendario);
        }

        ActualizarTextoBoton();
    }

    /// <summary>
    /// Además del propio botón, este componente también escucha los clics directamente
    /// (por si el objeto no tuviera un Button configurado), para asegurar que siempre
    /// se pueda abrir el calendario al hacer clic.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        ToggleCalendario();
    }

    /// <summary>
    /// Abre el calendario si está cerrado, o lo cierra si ya estaba abierto.
    /// La primera vez que se abre, construye todo el calendario por código.
    /// </summary>
    public void ToggleCalendario()
    {
        // Protección para que un mismo clic no dispare esta función dos veces en el mismo frame
        if (Time.frameCount == ultimoFrameEjecucion) return;
        ultimoFrameEjecucion = Time.frameCount;

        // Si es la primera vez que se abre, construimos el calendario (fondo, panel, días...)
        if (rootOverlay == null)
        {
            ConstruirCalendarioPorCodigo();
        }

        if (rootOverlay == null) return;

        // Invertimos la visibilidad actual: si estaba oculto lo mostramos, y viceversa
        bool activo = !rootOverlay.activeSelf;
        rootOverlay.SetActive(activo);

        if (activo)
        {
            // Lo traemos al frente de todo (por si hay otros paneles encima) y mostramos
            // el mes de la fecha ya elegida
            rootOverlay.transform.SetAsLastSibling();
            mesVisualizado = FechaSeleccionada;
            RenderizarDias();
        }
    }

    /// <summary>
    /// Permite fijar la fecha seleccionada desde fuera (por ejemplo, al abrir la pantalla
    /// con un valor por defecto), sin que el usuario tenga que elegirla a mano.
    /// </summary>
    public void SetFechaInicial(DateTime fecha)
    {
        FechaSeleccionada = fecha;
        mesVisualizado = fecha;
        ActualizarTextoBoton();
    }

    /// <summary>
    /// Crea desde cero, con código, todos los elementos visuales del calendario:
    /// el fondo semitransparente que cubre la pantalla, el panel central, la cabecera
    /// con el mes/año y las flechas para cambiar de mes, la fila con las iniciales de
    /// los días de la semana, y la rejilla donde luego se colocarán los números de los días.
    /// Solo se ejecuta una vez, la primera vez que el usuario abre el calendario.
    /// </summary>
    private void ConstruirCalendarioPorCodigo()
    {
        // Necesitamos un Canvas de la escena donde "colgar" el calendario
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

        // --- Fondo oscuro que cubre toda la pantalla (para "apagar" lo que hay detrás) ---
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

        // Si el usuario pulsa fuera del panel (en el fondo oscuro), se cierra el calendario
        Button btnOverlay = rootOverlay.GetComponent<Button>();
        btnOverlay.onClick.AddListener(() => {
            if (rootOverlay != null) rootOverlay.SetActive(false);
        });

        // Le damos su propio Canvas para asegurarnos de que se dibuja por encima de todo lo demás
        Canvas canvasModal = rootOverlay.AddComponent<Canvas>();
        canvasModal.overrideSorting = true;
        canvasModal.sortingOrder = 999;
        rootOverlay.AddComponent<GraphicRaycaster>();

        // --- Panel central del calendario (el recuadro donde va todo el contenido) ---
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

        // Botón "invisible" a modo de bloqueador, para que un clic dentro del panel
        // no se propague hasta el fondo oscuro y lo cierre por error
        Button blockBtn = panelCalendarioModal.AddComponent<Button>();
        blockBtn.transition = Selectable.Transition.None;

        // --- Cabecera: flecha mes anterior, texto "Mes Año", flecha mes siguiente ---
        GameObject headerGo = new GameObject("Header", typeof(RectTransform));
        headerGo.transform.SetParent(panelCalendarioModal.transform, false);
        RectTransform rectHeader = headerGo.GetComponent<RectTransform>();
        rectHeader.anchorMin = new Vector2(0, 1);
        rectHeader.anchorMax = new Vector2(1, 1);
        rectHeader.pivot = new Vector2(0.5f, 1);
        rectHeader.anchoredPosition = new Vector2(0, -10);
        rectHeader.sizeDelta = new Vector2(-20, 40);

        // Flecha "<" para retroceder un mes
        Button btnPrev = CrearBotonTexto("<", headerGo.transform, new Vector2(30, 30));
        RectTransform rPrev = btnPrev.GetComponent<RectTransform>();
        rPrev.anchorMin = new Vector2(0, 0.5f);
        rPrev.anchorMax = new Vector2(0, 0.5f);
        rPrev.anchoredPosition = new Vector2(20, 0);
        btnPrev.onClick.AddListener(() => { mesVisualizado = mesVisualizado.AddMonths(-1); RenderizarDias(); });

        // Flecha ">" para avanzar un mes
        Button btnNext = CrearBotonTexto(">", headerGo.transform, new Vector2(30, 30));
        RectTransform rNext = btnNext.GetComponent<RectTransform>();
        rNext.anchorMin = new Vector2(1, 0.5f);
        rNext.anchorMax = new Vector2(1, 0.5f);
        rNext.anchoredPosition = new Vector2(-20, 0);
        btnNext.onClick.AddListener(() => { mesVisualizado = mesVisualizado.AddMonths(1); RenderizarDias(); });

        // Texto central de la cabecera, con el nombre del mes y el año (ej. "septiembre 2026")
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

        // --- Fila con las iniciales de los días de la semana (L, M, X, J, V, S, D) ---
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

        // --- Rejilla donde se colocarán los botones con los números de los días ---
        // (de momento se deja vacía; RenderizarDias() la rellena cada vez que cambia el mes)
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

        // Empezamos con el calendario oculto: solo se muestra cuando el usuario lo pide
        rootOverlay.SetActive(false);
    }

    /// <summary>
    /// Crea un botón sencillo con un texto centrado dentro (se usa tanto para las
    /// flechas de mes anterior/siguiente como para cada número de día del calendario).
    /// </summary>
    /// <param name="texto">Texto que se mostrará dentro del botón.</param>
    /// <param name="parent">Objeto padre donde se colocará el botón.</param>
    /// <param name="size">Ancho y alto del botón.</param>
    /// <returns>El componente Button ya creado y configurado.</returns>
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

    /// <summary>
    /// Vuelve a dibujar la cuadrícula de días para el mes que se esté visualizando
    /// (<see cref="mesVisualizado"/>): borra los botones de días del mes anterior,
    /// calcula cuántos huecos vacíos hay que dejar al principio (para que el día 1
    /// caiga en la columna correcta según el día de la semana) y crea un botón por
    /// cada día del mes, resaltando en azul el día que ya está seleccionado.
    /// </summary>
    private void RenderizarDias()
    {
        // Actualizamos el texto de la cabecera con el nombre del mes y el año, en español
        if (textoMesAnoHeader != null)
        {
            textoMesAnoHeader.text = mesVisualizado.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-ES"));
        }

        // Borramos los botones de días que hubiera del mes mostrado anteriormente
        foreach (var obj in objetosDiasInstanciados)
        {
            if (obj != null) Destroy(obj);
        }
        objetosDiasInstanciados.Clear();

        int diasEnMes = DateTime.DaysInMonth(mesVisualizado.Year, mesVisualizado.Month);
        DateTime primerDiaMes = new DateTime(mesVisualizado.Year, mesVisualizado.Month, 1);
        // Calculamos cuántos huecos vacíos hace falta dejar antes del día 1, para que
        // la semana empiece en lunes (DayOfWeek de .NET empieza en domingo = 0)
        int diaSemanaInicio = ((int)primerDiaMes.DayOfWeek + 6) % 7;

        // Creamos celdas vacías (sin número) para rellenar el hueco antes del día 1
        for (int i = 0; i < diaSemanaInicio; i++)
        {
            GameObject empty = new GameObject("Empty", typeof(RectTransform));
            empty.transform.SetParent(contenedorDiasGrid, false);
            objetosDiasInstanciados.Add(empty);
        }

        // Creamos un botón por cada día real del mes
        for (int dia = 1; dia <= diasEnMes; dia++)
        {
            int numDia = dia; // Copia local necesaria para que el listener del botón capture el número correcto
            Button btn = CrearBotonTexto(numDia.ToString(), contenedorDiasGrid, new Vector2(34, 28));

            // Si este día es el que ya está seleccionado, lo pintamos de otro color para destacarlo
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

    /// <summary>
    /// Se ejecuta cuando el usuario pulsa un número de día concreto en la cuadrícula.
    /// Guarda la nueva fecha elegida, cierra el calendario y avisa a quien esté
    /// escuchando el evento <see cref="OnFechaSeleccionada"/> (por ejemplo, para que
    /// se pueda consultar el histórico de InfluxDB con la nueva fecha).
    /// </summary>
    /// <param name="dia">Número del día del mes que se ha pulsado.</param>
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

    /// <summary>
    /// Refresca el texto del botón principal para que muestre siempre la fecha
    /// seleccionada actualmente, con el formato día-mes-año (ej. "13-09-2026").
    /// </summary>
    private void ActualizarTextoBoton()
    {
        if (textoFechaSeleccionada != null)
        {
            textoFechaSeleccionada.text = FechaSeleccionada.ToString("dd-MM-yyyy");
        }
    }

    /// <summary>
    /// Permite activar o desactivar el botón desde fuera (por ejemplo, si la pantalla
    /// de histórico se bloquea mientras se está reproduciendo algo). Si se desactiva
    /// mientras el calendario está abierto, este se cierra automáticamente.
    /// </summary>
    /// <param name="interactable">true para permitir que el usuario pueda abrir el calendario, false para impedirlo.</param>
    public void SetInteractable(bool interactable)
    {
        Button btnSelf = GetComponent<Button>();
        if (btnSelf != null) btnSelf.interactable = interactable;
        if (!interactable && rootOverlay != null) rootOverlay.SetActive(false);
    }
}
