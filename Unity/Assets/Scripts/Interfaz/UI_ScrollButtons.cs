using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Se coloca sobre un botón (u otro elemento) que vive DENTRO de una lista con scroll
/// (por ejemplo, el menú lateral). El problema que resuelve: cuando el ratón está encima
/// de un botón, Unity a veces deja que el botón "se quede" con los gestos de rueda o
/// arrastre y el scroll de la lista deja de funcionar. Este script simplemente reenvía
/// esos gestos (rueda del ratón, empezar a arrastrar, arrastrar, soltar) al ScrollRect
/// padre, para que la lista se pueda seguir desplazando con normalidad aunque el cursor
/// esté encima de uno de sus botones.
/// </summary>
public class UI_ScrollButtons : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private ScrollRect scrollRectPadre;

    void Awake()
    {
        // Busca el ScrollRect del menú lateral automáticamente hacia arriba
        scrollRectPadre = GetComponentInParent<ScrollRect>();
    }

    // 1. Reenvía la rueda del ratón al ScrollView
    /// <summary>Reenvía el giro de la rueda del ratón al ScrollRect padre para que la lista se desplace.</summary>
    public void OnScroll(PointerEventData eventData)
    {
        if (scrollRectPadre != null)
        {
            scrollRectPadre.OnScroll(eventData);
        }
    }

    // 2. Reenvía el inicio del arrastre (click y mantener)
    /// <summary>Reenvía el inicio de un arrastre (clic y mantener) al ScrollRect padre.</summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (scrollRectPadre != null)
        {
            scrollRectPadre.OnBeginDrag(eventData);
        }
    }

    // 3. Reenvía el movimiento mientras se arrastra
    /// <summary>Reenvía el movimiento del ratón mientras dura el arrastre al ScrollRect padre.</summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (scrollRectPadre != null)
        {
            scrollRectPadre.OnDrag(eventData);
        }
    }

    // 4. Reenvía la suelta del click al terminar el arrastre
    /// <summary>Reenvía el momento en que se suelta el clic, terminando el arrastre, al ScrollRect padre.</summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (scrollRectPadre != null)
        {
            scrollRectPadre.OnEndDrag(eventData);
        }
    }
}
