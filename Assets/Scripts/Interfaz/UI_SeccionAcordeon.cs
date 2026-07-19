using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UI_SeccionAcordeon : MonoBehaviour
{
    // ¡EL TRUCO!: Una variable estática que recuerda QUÉ sección concreta está abierta en toda la app
    private static UI_SeccionAcordeon seccionAbiertaActualmente = null;

    [Header("Componentes del Acordeón")]
    public GameObject contenedorContenido;
    public TMP_Text textoCabecera;

    private bool estaAbierto = false;

    private RectTransform miRectTransform;
    private RectTransform rectPadre;

    private IEnumerator Start()
    {
        estaAbierto = false;

        miRectTransform = GetComponent<RectTransform>();
        if (transform.parent != null)
        {
            rectPadre = transform.parent.GetComponent<RectTransform>();
        }

        if (contenedorContenido != null) contenedorContenido.SetActive(false);

        // Aplica el símbolo de cerrado '►' al arrancar
        ActualizarFlecha();

        yield return new WaitForEndOfFrame();

        ForzarRecalculoMenu();
    }

    public void ToggleSeccion()
    {
        // 1. Si el usuario va a ABRIR esta sección...
        if (!estaAbierto)
        {
            // ...y resulta que ya había OTRA sección abierta en el menú, la obligamos a cerrarse
            if (seccionAbiertaActualmente != null && seccionAbiertaActualmente != this)
            {
                seccionAbiertaActualmente.CerrarSeccionForzado();
            }

            // Ahora esta sección pasa a ser la reina del menú
            seccionAbiertaActualmente = this;
        }
        else
        {
            // Si el usuario está cerrando voluntariamente esta misma sección, liberamos el trono
            if (seccionAbiertaActualmente == this)
            {
                seccionAbiertaActualmente = null;
            }
        }

        // 2. Continuamos con tu lógica normal de encendido/apagado
        estaAbierto = !estaAbierto;

        if (contenedorContenido != null)
            contenedorContenido.SetActive(estaAbierto);

        ActualizarFlecha();
        ForzarRecalculoMenu();
    }

    // ¡NUEVO!: Permite que otros scripts (como el ControladorMenu) cierren el acordeón de golpe
    public static void CerrarCualquierSeccionAbierta()
    {
        if (seccionAbiertaActualmente != null)
        {
            seccionAbiertaActualmente.CerrarSeccionForzado();
            seccionAbiertaActualmente = null; // Vaciamos el rastro
        }
    }

    // ¡NUEVO!: Esta función la llamará una sección compañera cuando quiera que nos cerremos
    public void CerrarSeccionForzado()
    {
        estaAbierto = false;

        if (contenedorContenido != null)
            contenedorContenido.SetActive(false);

        ActualizarFlecha();
        ForzarRecalculoMenu();
    }

    private void ForzarRecalculoMenu()
    {
        Canvas.ForceUpdateCanvases();

        if (miRectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(miRectTransform);
        }

        if (rectPadre != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectPadre);
        }
    }

    private void UpdateVisuals() // Mantengo tu método intacto
    {
        ActualizarFlecha();
    }

    private void ActualizarFlecha()
    {
        if (textoCabecera != null)
        {
            string nombreSeccion = textoCabecera.text.Substring(1);
            string nuevaFlecha = estaAbierto ? "▲" : "►";
            textoCabecera.text = nuevaFlecha + nombreSeccion;
        }
    }
}