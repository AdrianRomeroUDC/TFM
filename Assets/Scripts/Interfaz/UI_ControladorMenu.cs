using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class ControladorMenu : MonoBehaviour
{
    [Header("Mi Panel Desplegable Principal")]
    [Tooltip("Arrastra aquí el Panel_LateralMenu")]
    public GameObject panelLateral;

    [Header("Cierre al Clicar Fuera")]
    [Tooltip("Arrastra aquí el objeto FondoCierre")]
    public GameObject fondoCierre;

    [Header("Reloj Digital de la Cabecera")]
    [Tooltip("Arrastra aquí tu objeto de texto Texto_FechaHora")]
    public TMP_Text textoReloj;

    private RectTransform rectPanel;
    private VerticalLayoutGroup layoutSecciones;

    private void Start()
    {
        if (panelLateral != null)
        {
            rectPanel = panelLateral.GetComponent<RectTransform>();
            layoutSecciones = panelLateral.GetComponentInChildren<VerticalLayoutGroup>();
            panelLateral.SetActive(false);
        }

        if (fondoCierre != null)
            fondoCierre.SetActive(false);
    }

    private void Update()
    {
        if (textoReloj != null)
        {
            string fecha = DateTime.Now.ToString("dd / MM / yyyy");
            string hora = DateTime.Now.ToString("HH:mm:ss");
            textoReloj.text = fecha + "\n" + hora;
        }
    }

    // Vinculado al botón de las 3 rayas
    public void ToggleMenu()
    {
        if (panelLateral != null)
        {
            bool nuevoEstado = !panelLateral.activeSelf;
            CambiarEstadoMenu(nuevoEstado);
        }
    }

    // Vinculado al Event Trigger (Pointer Click) de FondoCierre
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
            Canvas.ForceUpdateCanvases();

            if (layoutSecciones != null)
            {
                RectTransform rectSecciones = layoutSecciones.GetComponent<RectTransform>();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectSecciones);
            }

            if (rectPanel != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectPanel);
            }
        }
        else
        {
            // Si cerramos el menú, obligamos a las secciones a encogerse
            UI_SeccionAcordeon.CerrarCualquierSeccionAbierta();
        }
    }
}