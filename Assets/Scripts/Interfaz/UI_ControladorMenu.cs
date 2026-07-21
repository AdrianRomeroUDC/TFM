using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class ControladorMenu : MonoBehaviour
{
    [Header("Mi Panel Desplegable Principal")]
    public GameObject panelLateral;

    [Header("Cierre al Clicar Fuera")]
    public GameObject fondoCierre;

    [Header("Reloj Digital de la Cabecera")]
    public TMP_Text textoReloj;

    private RectTransform rectPanel;
    private RectTransform rectSecciones;
    private float ultimoSegundoActualizado = -1f;

    private void Start()
    {
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
    }

    private void Update()
    {
        // 🚀 OPTIMIZACIÓN: Solo redibuja la hora 1 vez por segundo
        if (textoReloj != null && Time.time - ultimoSegundoActualizado >= 1f)
        {
            ultimoSegundoActualizado = Time.time;
            string fecha = DateTime.Now.ToString("dd / MM / yyyy");
            string hora = DateTime.Now.ToString("HH:mm:ss");
            textoReloj.text = fecha + "\n" + hora;
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
}