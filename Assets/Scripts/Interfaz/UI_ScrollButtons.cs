using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_ScrollButtons : MonoBehaviour, IScrollHandler
{
    private ScrollRect scrollRectPadre;

    void Awake()
    {
        // Busca el ScrollRect del menú lateral automáticamente hacia arriba
        scrollRectPadre = GetComponentInParent<ScrollRect>();
    }

    public void OnScroll(PointerEventData eventData)
    {
        // Si el usuario usa la rueda del ratón sobre el botón, se la pasa al menú
        if (scrollRectPadre != null)
        {
            scrollRectPadre.OnScroll(eventData);
        }
    }
}