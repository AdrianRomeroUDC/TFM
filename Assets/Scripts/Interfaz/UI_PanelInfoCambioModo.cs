using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

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
        ConfigurarEstilosYAutoTamano();

        if (panelTooltip != null)
            panelTooltip.SetActive(false);
    }

    private void ConfigurarEstilosYAutoTamano()
    {
        if (panelTooltip == null) return;

        // 1. Configurar Imagen de Fondo
        Image imgFondo = panelTooltip.GetComponent<Image>();
        if (imgFondo == null) imgFondo = panelTooltip.AddComponent<Image>();

        imgFondo.color = colorFondo;
        imgFondo.raycastTarget = false;

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

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; // 👈 Respeta el ancho definido en el RectTransform
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;   // 👈 Calcula la altura según las líneas

        // 4. Configurar Texto con salto de línea (Word Wrap)
        TMP_Text txtTMP = panelTooltip.GetComponentInChildren<TMP_Text>();
        if (txtTMP != null)
        {
            txtTMP.text = mensajeTooltip;
            txtTMP.color = colorTexto;
            txtTMP.raycastTarget = false;
            txtTMP.alignment = TextAlignmentOptions.Center;
            txtTMP.textWrappingMode = TextWrappingModes.Normal; // 👈 Activa el salto de línea al llegar al ancho límite
        }

        Text txtNativo = panelTooltip.GetComponentInChildren<Text>();
        if (txtNativo != null)
        {
            txtNativo.text = mensajeTooltip;
            txtNativo.color = colorTexto;
            txtNativo.raycastTarget = false;
            txtNativo.alignment = TextAnchor.MiddleCenter;
            txtNativo.horizontalOverflow = HorizontalWrapMode.Wrap; // 👈 Salto de línea para Text nativo
        }
    }

    private void OnDisable()
    {
        if (panelTooltip != null)
            panelTooltip.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (panelTooltip != null)
        {
            TMP_Text txtTMP = panelTooltip.GetComponentInChildren<TMP_Text>();
            if (txtTMP != null) txtTMP.text = mensajeTooltip;

            Text txtNativo = panelTooltip.GetComponentInChildren<Text>();
            if (txtNativo != null) txtNativo.text = mensajeTooltip;

            panelTooltip.SetActive(true);

            // Reconstruir el layout al instante
            RectTransform rect = panelTooltip.GetComponent<RectTransform>();
            if (rect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (panelTooltip != null)
            panelTooltip.SetActive(false);
    }
}