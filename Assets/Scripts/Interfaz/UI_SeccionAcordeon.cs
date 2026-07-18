using UnityEngine;
using UnityEngine.UI; // <-- ¡NUEVO! Necesario para usar el LayoutRebuilder
using TMPro;

public class SeccionAcordeon : MonoBehaviour
{
    [Header("Componentes del Acordeón")]
    public GameObject contenedorContenido;
    public TMP_Text textoCabecera; // Este es el texto único con la flecha y el nombre

    private bool estaAbierto = false;

    // Referencias automáticas para guardar las cajas físicas de la UI
    private RectTransform miRectTransform;
    private RectTransform rectPadre;

    private void Start()
    {
        estaAbierto = false;

        // Guardamos los componentes de posición automáticamente al arrancar
        miRectTransform = GetComponent<RectTransform>();
        if (transform.parent != null)
        {
            rectPadre = transform.parent.GetComponent<RectTransform>();
        }

        if (contenedorContenido != null) contenedorContenido.SetActive(false);
        // Inicializamos con el símbolo de cerrado
        ActualizarFlecha();
    }

    public void ToggleSeccion()
    {
        estaAbierto = !estaAbierto;

        if (contenedorContenido != null)
            contenedorContenido.SetActive(estaAbierto);

        ActualizarFlecha();

        // ¡NUEVO! Forzamos a Unity a recalcular todo el menú de arriba a abajo al instante
        ForzarRecalculoMenu();
    }

    private void ForzarRecalculoMenu()
    {
        // 1. Sincroniza los datos internos de la pantalla
        Canvas.ForceUpdateCanvases();

        // 2. Le dice a esta sección que adapte su tamaño al nuevo botón blanco
        if (miRectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(miRectTransform);
        }

        // 3. Le dice al contenedor principal ('Secciones') que se reajuste y empuje
        // de forma limpia a todas las demás secciones hacia abajo sin solaparse
        if (rectPadre != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectPadre);
        }
    }

    private void ActualizarFlecha()
    {
        if (textoCabecera != null)
        {
            // Tomamos el texto actual, quitamos el primer carácter y ponemos el nuevo
            string nombreSeccion = textoCabecera.text.Substring(1); // Mantiene el resto del texto
            string nuevaFlecha = estaAbierto ? "▼" : ">";
            textoCabecera.text = nuevaFlecha + nombreSeccion;
        }
    }
}