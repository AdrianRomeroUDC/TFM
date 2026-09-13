using UnityEngine;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// Detector reutilizable de "ratón encima / ratón fuera" para un elemento de la interfaz.
/// No hace nada por sí mismo: simplemente avisa (mediante eventos) a quien lo esté
/// escuchando cuando el cursor del ratón entra o sale de la zona de este elemento.
/// Por ejemplo, <see cref="UI_StockController"/> lo añade automáticamente sobre cada
/// hueco (slot) del almacén (HBW) para saber cuándo el usuario está pasando el ratón
/// por encima de una pieza guardada y así poder mostrarle información de esa pieza.
/// </summary>
public class UI_SlotMouseDetector: MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // Eventos que dispararemos hacia el controlador principal
    public Action OnMouseOverSlot;
    public Action OnMouseExitSlot;

    /// <summary>
    /// Unity llama a esto automáticamente en el instante en que el cursor entra
    /// en la zona de este elemento. Avisamos a quien esté escuchando el evento.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        OnMouseOverSlot?.Invoke();
    }

    /// <summary>
    /// Unity llama a esto automáticamente en el instante en que el cursor sale
    /// de la zona de este elemento. Avisamos a quien esté escuchando el evento.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        OnMouseExitSlot?.Invoke();
    }
}
