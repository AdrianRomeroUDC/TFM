using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class UI_SlotMouseDetector: MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // Eventos que dispararemos hacia el controlador principal
    public Action OnMouseOverSlot;
    public Action OnMouseExitSlot;

    public void OnPointerEnter(PointerEventData eventData)
    {
        OnMouseOverSlot?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        OnMouseExitSlot?.Invoke();
    }
}
