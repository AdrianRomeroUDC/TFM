using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Controla el comportamiento de una "sección acordeón" del menú lateral de la interfaz.
/// Un acordeón es esa típica cabecera plegable que, al pulsarla, despliega o esconde
/// el contenido que tiene debajo (por ejemplo, la sección "HBW", la sección "SSC", etc.).
///
/// Este script se coloca en cada una de esas cabeceras. Además de abrir/cerrar su propio
/// contenido, coordina con las demás secciones para que, si el diseño así lo requiere,
/// solo haya una sección abierta a la vez (al abrir una, se cierra la que estuviera abierta).
///
/// También recuerda qué secciones estaban abiertas mediante la lista estática
/// <see cref="UI_ControladorMenu.seccionesAbiertasPrevias"/>, de forma que si la escena
/// se recarga, el menú puede "restaurar" el estado en el que estaba antes.
/// </summary>
public class UI_SeccionAcordeon : MonoBehaviour
{
    // Referencia estática (compartida por TODAS las secciones acordeón) a la sección
    // que está abierta en este momento. Al ser estática, todas las instancias de esta
    // clase pueden consultarla y modificarla para saber "quién más" está abierto.
    private static UI_SeccionAcordeon seccionAbiertaActualmente = null;

    [Header("Componentes del Acordeón")]
    public GameObject contenedorContenido; // El panel que se muestra/oculta al plegar o desplegar
    public TMP_Text textoCabecera;         // El texto de la cabecera, donde se dibuja la flechita ▲ / ►

    // Guarda si ESTA sección concreta está abierta o cerrada ahora mismo
    private bool estaAbierto = false;

    // Referencias a los RectTransform propios y del padre, necesarias para forzar
    // que Unity recalcule el tamaño del menú cuando el contenido cambia de alto
    private RectTransform miRectTransform;
    private RectTransform rectPadre;

    /// <summary>
    /// Se ejecuta al arrancar el objeto. Se usa una Corrutina (IEnumerator) en lugar de un
    /// Start() normal porque, al final, necesitamos esperar un frame completo antes de
    /// recalcular el layout (para que Unity ya haya colocado todos los elementos).
    /// </summary>
    private IEnumerator Start()
    {
        // Guardamos las referencias a nuestro propio RectTransform y al de nuestro padre
        miRectTransform = GetComponent<RectTransform>();
        if (transform.parent != null)
        {
            rectPadre = transform.parent.GetComponent<RectTransform>();
        }

        // Restaurar el estado si la sección estaba abierta antes de recargar la escena
        // Comprobamos si el nombre de este GameObject está en la lista de secciones que
        // quedaron abiertas la última vez (por ejemplo, tras cambiar de escena)
        if (UI_ControladorMenu.seccionesAbiertasPrevias.Contains(gameObject.name))
        {
            seccionAbiertaActualmente = this;
            estaAbierto = true;
            if (contenedorContenido != null) contenedorContenido.SetActive(true);
        }
        else
        {
            estaAbierto = false;
            if (contenedorContenido != null) contenedorContenido.SetActive(false);
        }

        // Actualizamos la flecha (▲ si está abierto, ► si está cerrado) según el estado inicial
        ActualizarFlecha();

        // Esperamos a que termine el frame actual, para que Unity haya terminado de
        // dibujar/colocar todos los elementos antes de forzar el recálculo del menú
        yield return new WaitForEndOfFrame();

        ForzarRecalculoMenu();
    }

    /// <summary>
    /// Alterna el estado de esta sección: si está cerrada la abre, y si está abierta la cierra.
    /// Se llama normalmente desde el botón (OnClick) de la cabecera del acordeón.
    /// Si se abre esta sección y había otra sección distinta abierta, esa otra se cierra
    /// automáticamente (solo una sección abierta a la vez).
    /// </summary>
    public void ToggleSeccion()
    {
        if (!estaAbierto)
        {
            // Vamos a ABRIR esta sección: si hay otra sección abierta que no sea esta, la cerramos
            if (seccionAbiertaActualmente != null && seccionAbiertaActualmente != this)
            {
                seccionAbiertaActualmente.CerrarSeccionForzado();
            }

            seccionAbiertaActualmente = this;

            // Registra esta sección como abierta
            UI_ControladorMenu.seccionesAbiertasPrevias.Clear(); // Limpia si solo se permite 1 abierta a la vez
            UI_ControladorMenu.seccionesAbiertasPrevias.Add(gameObject.name);
        }
        else
        {
            // Vamos a CERRAR esta sección: si era la que estaba marcada como "la abierta", la desmarcamos
            if (seccionAbiertaActualmente == this)
            {
                seccionAbiertaActualmente = null;
            }

            // Remueve el registro al cerrar
            UI_ControladorMenu.seccionesAbiertasPrevias.Remove(gameObject.name);
        }

        // Invertimos el estado (de abierto a cerrado o viceversa)
        estaAbierto = !estaAbierto;

        // Mostramos u ocultamos el contenido según el nuevo estado
        if (contenedorContenido != null)
            contenedorContenido.SetActive(estaAbierto);

        ActualizarFlecha();
        ForzarRecalculoMenu();
    }

    /// <summary>
    /// Método estático de utilidad: cierra la sección que esté abierta en este momento,
    /// sea cual sea. Útil, por ejemplo, cuando se cambia de vista y queremos que el menú
    /// vuelva a un estado "todo cerrado" sin tener que saber cuál era la sección activa.
    /// </summary>
    public static void CerrarCualquierSeccionAbierta()
    {
        if (seccionAbiertaActualmente != null)
        {
            seccionAbiertaActualmente.CerrarSeccionForzado();
            seccionAbiertaActualmente = null;
        }

        // Limpia el registro estático
        UI_ControladorMenu.seccionesAbiertasPrevias.Clear();
    }

    /// <summary>
    /// Cierra ESTA sección concreta de forma directa, sin pasar por la lógica de
    /// alternancia de <see cref="ToggleSeccion"/>. Se usa, por ejemplo, cuando otra
    /// sección se abre y necesita cerrar esta.
    /// </summary>
    public void CerrarSeccionForzado()
    {
        estaAbierto = false;

        // Elimina el registro de la sección
        UI_ControladorMenu.seccionesAbiertasPrevias.Remove(gameObject.name);

        if (contenedorContenido != null)
            contenedorContenido.SetActive(false);

        ActualizarFlecha();
        ForzarRecalculoMenu();
    }

    /// <summary>
    /// Indica si esta sección concreta está abierta actualmente.
    /// </summary>
    /// <returns>true si el contenido de esta sección está visible, false si está oculto.</returns>
    public bool EstaAbierta()
    {
        return estaAbierto;
    }

    /// <summary>
    /// Abre esta sección de forma directa (sin alternar), cerrando antes cualquier otra
    /// sección que estuviera abierta. Útil para abrir una sección concreta por código,
    /// por ejemplo al pulsar un acceso directo desde otra parte de la interfaz.
    /// </summary>
    public void AbrirSeccionDirecto()
    {
        if (seccionAbiertaActualmente != null && seccionAbiertaActualmente != this)
        {
            seccionAbiertaActualmente.CerrarSeccionForzado();
        }

        seccionAbiertaActualmente = this;
        estaAbierto = true;

        // Registra esta sección como abierta
        UI_ControladorMenu.seccionesAbiertasPrevias.Clear();
        UI_ControladorMenu.seccionesAbiertasPrevias.Add(gameObject.name);

        if (contenedorContenido != null)
            contenedorContenido.SetActive(true);

        ActualizarFlecha();
        ForzarRecalculoMenu();
    }

    /// <summary>
    /// Obliga a Unity a recalcular el tamaño y la posición de los elementos del menú
    /// (Layout Groups, ScrollView, etc.) justo después de abrir o cerrar una sección.
    /// Esto es necesario porque, al mostrar/ocultar el contenido, el alto de la sección
    /// cambia y el resto de elementos del menú deben "acomodarse" a ese nuevo tamaño.
    /// </summary>
    private void ForzarRecalculoMenu()
    {
        // Le decimos a Unity que actualice ya mismo todos los Canvas pendientes de refrescar
        Canvas.ForceUpdateCanvases();

        if (miRectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(miRectTransform);
        }

        if (rectPadre != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectPadre);

            // Si el acordeón está dentro de un ScrollView, también recalculamos ese
            // ScrollView para que la barra de scroll se ajuste al nuevo contenido
            ScrollRect scrollview = rectPadre.GetComponentInParent<ScrollRect>();
            if (scrollview != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollview.GetComponent<RectTransform>());
            }
        }
    }

    /// <summary>
    /// Cambia el primer carácter del texto de la cabecera por una flecha que indica
    /// el estado del acordeón: ▲ si está abierto, ► si está cerrado.
    /// </summary>
    private void ActualizarFlecha()
    {
        if (textoCabecera != null && !string.IsNullOrEmpty(textoCabecera.text))
        {
            string textoActual = textoCabecera.text;
            string nuevaFlecha = estaAbierto ? "▲" : "►";

            // Si el texto ya empezaba por una de las dos flechas, la sustituimos
            if (textoActual.StartsWith("▲") || textoActual.StartsWith("►"))
            {
                textoCabecera.text = nuevaFlecha + textoActual.Substring(1);
            }
            else
            {
                // Si no había flecha todavía (primera vez), simplemente la añadimos delante
                textoCabecera.text = nuevaFlecha + textoActual;
            }
        }
    }
}
