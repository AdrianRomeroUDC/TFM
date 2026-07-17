using UnityEngine;
using TMPro; // <-- ¡NUEVO! Necesario para poder controlar TextMeshPro
using System; // <-- ¡NUEVO! Necesario para obtener la fecha de tu ordenador

public class ControladorMenu : MonoBehaviour
{
    [Header("Mi Panel Desplegable")]
    [Tooltip("Arrastra aquí el Panel_Lateral_Menu")]
    public GameObject panelLateral;

    [Header("Reloj Digital de la Cabecera")]
    [Tooltip("Arrastra aquí tu objeto de texto Texto_FechaHora")]
    public TMP_Text textoReloj;

    // Start se ejecuta una sola vez al pulsar Play
    private void Start()
    {
        if (panelLateral != null)
        {
            // Forzamos que el panel empiece siempre desactivado
            panelLateral.SetActive(false);
        }
    }

    // Update se ejecuta automáticamente en cada frame del juego
    private void Update()
    {
        if (textoReloj != null)
        {
            // Obtenemos la fecha y la hora por separado
            string fecha = DateTime.Now.ToString("dd / MM / yyyy");
            string hora = DateTime.Now.ToString("HH:mm:ss"); // :ss muestra los segundos corriendo

            // Las unimos poniendo un salto de línea (\n) en medio
            textoReloj.text = fecha + "\n" + hora;
        }
    }

    // Esta función se ejecutará al levantar el clic (Pointer Up)
    public void ToggleMenu()
    {
        if (panelLateral != null)
        {
            // Invierte el estado actual del panel: si está apagado lo enciende, y viceversa
            panelLateral.SetActive(!panelLateral.activeSelf);
        }
    }
}