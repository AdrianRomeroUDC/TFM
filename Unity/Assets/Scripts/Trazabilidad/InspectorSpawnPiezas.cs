using UnityEngine;
using System;

/// <summary>
/// Herramienta de depuración ("el detective"): se coloca sobre el prefab de una pieza para que,
/// en el mismo instante en que Unity la crea en la escena (por ejemplo cuando el HBW recibe una
/// pieza nueva o el simulador genera una de prueba), quede registrado en la consola quién la ha
/// creado y desde qué punto exacto del código. No influye en el comportamiento de la fábrica ni
/// del gemelo digital: solo sirve para entender, cuando algo no cuadra, de dónde ha salido cada
/// pieza que aparece en la escena.
/// </summary>
public class InspectorSpawnPiezas : MonoBehaviour
{
    void Awake()
    {
        // Se ejecuta en el microsegundo exacto en que la pieza se crea (Instantiate)
        // El StackTrace muestra la cadena completa de llamadas de código que terminó creando este
        // objeto, así se puede localizar exactamente qué script (y qué línea) hizo el Instantiate.
        Debug.Log($"<color=red><b>[DETECTIVE]:</b> ¡La pieza '{gameObject.name}' ha sido CREADA en la escena!</color>\n" +
                  "<b>Ruta del código que la ha instanciado (StackTrace):</b>\n" + Environment.StackTrace);
    }
}
