using UnityEngine;
using TMPro;
using System;

public class ControladorMenu : MonoBehaviour
{
    [Header("Mi Panel Desplegable Principal")]
    [Tooltip("Arrastra aquí el Panel_Lateral_Menu")]
    public GameObject panelLateral;

    [Header("Reloj Digital de la Cabecera")]
    [Tooltip("Arrastra aquí tu objeto de texto Texto_FechaHora")]
    public TMP_Text textoReloj;

    private void Start()
    {
        if (panelLateral != null) panelLateral.SetActive(false);
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

    public void ToggleMenu()
    {
        if (panelLateral != null)
        {
            panelLateral.SetActive(!panelLateral.activeSelf);
        }
    }
}