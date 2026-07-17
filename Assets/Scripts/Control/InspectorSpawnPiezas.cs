using UnityEngine;
using System;

public class InspectorSpawnPiezas : MonoBehaviour
{
    void Awake()
    {
        // Se ejecuta en el microsegundo exacto en que la pieza se crea (Instantiate)
        Debug.Log($"<color=red><b>[DETECTIVE]:</b> ¡La pieza '{gameObject.name}' ha sido CREADA en la escena!</color>\n" +
                  "<b>Ruta del código que la ha instanciado (StackTrace):</b>\n" + Environment.StackTrace);
    }
}