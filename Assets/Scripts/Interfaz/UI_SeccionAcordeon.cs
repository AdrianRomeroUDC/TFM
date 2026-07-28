using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UI_SeccionAcordeon : MonoBehaviour
{
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

        ActualizarFlecha();

        yield return new WaitForEndOfFrame();

        ForzarRecalculoMenu();
    }

    public void ToggleSeccion()
    {
        if (!estaAbierto)
        {
            if (seccionAbiertaActualmente != null && seccionAbiertaActualmente != this)
            {
                seccionAbiertaActualmente.CerrarSeccionForzado();
            }

            seccionAbiertaActualmente = this;
        }
        else
        {
            if (seccionAbiertaActualmente == this)
            {
                seccionAbiertaActualmente = null;
            }
        }

        estaAbierto = !estaAbierto;

        if (contenedorContenido != null)
            contenedorContenido.SetActive(estaAbierto);

        ActualizarFlecha();
        ForzarRecalculoMenu();
    }

    public static void CerrarCualquierSeccionAbierta()
    {
        if (seccionAbiertaActualmente != null)
        {
            seccionAbiertaActualmente.CerrarSeccionForzado();
            seccionAbiertaActualmente = null;
        }
    }

    public void CerrarSeccionForzado()
    {
        estaAbierto = false;

        if (contenedorContenido != null)
            contenedorContenido.SetActive(false);

        ActualizarFlecha();
        ForzarRecalculoMenu();
    }

    public bool EstaAbierta()
    {
        return estaAbierto;
    }

    public void AbrirSeccionDirecto()
    {
        if (seccionAbiertaActualmente != null && seccionAbiertaActualmente != this)
        {
            seccionAbiertaActualmente.CerrarSeccionForzado();
        }

        seccionAbiertaActualmente = this;
        estaAbierto = true;

        if (contenedorContenido != null)
            contenedorContenido.SetActive(true);

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

            ScrollRect scrollview = rectPadre.GetComponentInParent<ScrollRect>();
            if (scrollview != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollview.GetComponent<RectTransform>());
            }
        }
    }

    private void ActualizarFlecha()
    {
        if (textoCabecera != null && !string.IsNullOrEmpty(textoCabecera.text))
        {
            string textoActual = textoCabecera.text;
            string nuevaFlecha = estaAbierto ? "▲" : "►";

            if (textoActual.StartsWith("▲") || textoActual.StartsWith("►"))
            {
                textoCabecera.text = nuevaFlecha + textoActual.Substring(1);
            }
            else
            {
                textoCabecera.text = nuevaFlecha + textoActual;
            }
        }
    }
}