using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_ScrollButtons : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private ScrollRect scrollRectPadre;

    void Awake()
    {
        // Busca el ScrollRect del menú lateral automáticamente hacia arriba
        scrollRectPadre = GetComponentInParent<ScrollRect>();
    }

    // 1. Reenvía la rueda del ratón al ScrollView
    public void OnScroll(PointerEventData eventData)
    {
        if (scrollRectPadre != null)
        {
            scrollRectPadre.OnScroll(eventData);
        }
    }

    // 2. Reenvía el inicio del arrastre (click y mantener)
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (scrollRectPadre != null)
        {
            scrollRectPadre.OnBeginDrag(eventData);
        }
    }

    // 3. Reenvía el movimiento mientras se arrastra
    public void OnDrag(PointerEventData eventData)
    {
        if (scrollRectPadre != null)
        {
            scrollRectPadre.OnDrag(eventData);
        }
    }

    // 4. Reenvía la suelta del click al terminar el arrastre
    public void OnEndDrag(PointerEventData eventData)
    {
        if (scrollRectPadre != null)
        {
            scrollRectPadre.OnEndDrag(eventData);
        }
    }
}