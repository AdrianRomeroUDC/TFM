using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Muestra un pequeño tooltip (globo de texto) cuando el usuario pasa el ratón por encima del
/// icono informativo que aparece junto al botón PLAY, recordándole que debe pulsar PLAY para que
/// el cambio de modo (en vivo / histórico / simulación) se aplique de verdad. Este script se encarga
/// tanto de dar estilo automáticamente al panel del tooltip (fondo, márgenes, ajuste de tamaño y
/// salto de línea del texto) como de mostrarlo y ocultarlo al entrar y salir el ratón.
/// </summary>
public class UI_PanelInfoCambioModo : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Referencia al Panel Tooltip")]
    public GameObject panelTooltip;

    [Header("Contenido del Mensaje")]
    [TextArea]
    public string mensajeTooltip = "Recuerde darle al PLAY para cambiar de modo";

    [Header("Estilos de Color")]
    [Tooltip("Color de fondo de la caja del mensaje")]
    public Color colorFondo = new Color(0.12f, 0.15f, 0.22f, 0.95f);

    [Tooltip("Color del texto del mensaje")]
    public Color colorTexto = Color.white;

    [Header("Márgenes Internos (Padding)")]
    public int paddingHorizontal = 14;
    public int paddingVertical = 8;

    private void Awake()
    {
        // Preparamos el aspecto visual del tooltip y lo dejamos oculto hasta que el usuario pase el ratón por encima.
        ConfigurarEstilosYAutoTamano();

        if (panelTooltip != null)
            panelTooltip.SetActive(false);
    }

    // Configura por código los componentes visuales del panel tooltip, añadiéndolos si no existen
    // todavía, para que el globo de texto se ajuste automáticamente al contenido del mensaje.
    private void ConfigurarEstilosYAutoTamano()
    {
        if (panelTooltip == null) return;

        // 1. Configurar Imagen de Fondo
        Image imgFondo = panelTooltip.GetComponent<Image>();
        if (imgFondo == null) imgFondo = panelTooltip.AddComponent<Image>();

        imgFondo.color = colorFondo;
        imgFondo.raycastTarget = false; // El fondo no debe "robar" los clics ni interferir con el ratón.

        // 2. Configurar Layout Group
        HorizontalLayoutGroup layout = panelTooltip.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = panelTooltip.AddComponent<HorizontalLayoutGroup>();

        layout.padding = new RectOffset(paddingHorizontal, paddingHorizontal, paddingVertical, paddingVertical);
        layout.childControlWidth = true;   // El texto ocupará el ancho disponible del panel menos el padding
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 3. Configurar ContentSizeFitter (SOLO EN ALTURA)
        ContentSizeFitter fitter = panelTooltip.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = panelTooltip.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; // Respeta el ancho definido en el RectTransform
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;   // Calcula la altura según las líneas de texto

        // 4. Configurar Texto con salto de línea (Word Wrap)
        TMP_Text txtTMP = panelTooltip.GetComponentInChildren<TMP_Text>();
        if (txtTMP != null)
        {
            txtTMP.text = mensajeTooltip;
            txtTMP.color = colorTexto;
            txtTMP.raycastTarget = false;
            txtTMP.alignment = TextAlignmentOptions.Center;
            txtTMP.textWrappingMode = TextWrappingModes.Normal; // Activa el salto de línea al llegar al ancho límite
        }

        // También soportamos el componente Text nativo de Unity (por si el tooltip no usa TextMeshPro).
        Text txtNativo = panelTooltip.GetComponentInChildren<Text>();
        if (txtNativo != null)
        {
            txtNativo.text = mensajeTooltip;
            txtNativo.color = colorTexto;
            txtNativo.raycastTarget = false;
            txtNativo.alignment = TextAnchor.MiddleCenter;
            txtNativo.horizontalOverflow = HorizontalWrapMode.Wrap; // Salto de línea para Text nativo
        }
    }

    private void OnDisable()
    {
        // Si este componente se desactiva (por ejemplo al cambiar de panel), ocultamos el tooltip
        // para que no se quede visible "flotando" sin motivo.
        if (panelTooltip != null)
            panelTooltip.SetActive(false);
    }

    /// <summary>
    /// Se llama automáticamente cuando el puntero del ratón entra en la zona de este elemento de la UI.
    /// Actualiza el texto del tooltip (por si cambió mensajeTooltip) y lo muestra, forzando un
    /// recálculo inmediato del layout para que el tamaño se ajuste bien desde el primer instante.
    /// </summary>
    /// <param name="eventData">Datos del evento de puntero proporcionados por el sistema de UI de Unity.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (panelTooltip != null)
        {
            TMP_Text txtTMP = panelTooltip.GetComponentInChildren<TMP_Text>();
            if (txtTMP != null) txtTMP.text = mensajeTooltip;

            Text txtNativo = panelTooltip.GetComponentInChildren<Text>();
            if (txtNativo != null) txtNativo.text = mensajeTooltip;

            panelTooltip.SetActive(true);

            // Reconstruir el layout al instante, para que el tamaño del globo de texto sea correcto
            // desde el primer fotograma en que aparece (sin esperar al siguiente ciclo de Unity).
            RectTransform rect = panelTooltip.GetComponent<RectTransform>();
            if (rect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
        }
    }

    /// <summary>
    /// Se llama automáticamente cuando el puntero del ratón sale de la zona de este elemento de la UI.
    /// Simplemente oculta el tooltip.
    /// </summary>
    /// <param name="eventData">Datos del evento de puntero proporcionados por el sistema de UI de Unity.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (panelTooltip != null)
            panelTooltip.SetActive(false);
    }
}
