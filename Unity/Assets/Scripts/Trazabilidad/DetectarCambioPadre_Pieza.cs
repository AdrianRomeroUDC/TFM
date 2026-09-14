using UnityEngine;

/// <summary>
/// Herramienta de depuración (no forma parte de la lógica de la fábrica): se coloca como
/// componente sobre una pieza en la escena para vigilar de qué "contenedor" (padre en la
/// jerarquía de Unity) cuelga en cada momento. En este proyecto, el padre de una pieza suele
/// indicar en qué estación o hueco se encuentra (por ejemplo, dentro del hueco del HBW, sobre
/// la cinta del MPO, etc.), así que cada vez que ese padre cambia significa que la pieza se ha
/// movido de sitio en el gemelo digital. Es útil para rastrear, mensaje a mensaje, el camino
/// real que sigue una pieza física a través de las estaciones.
/// </summary>
public class DetectarCambioPadre : MonoBehaviour
{
    // Este método lo ejecuta Unity AUTOMÁTICAMENTE cada vez que cambia el padre del objeto
    private void OnTransformParentChanged()
    {
        // Si ya no tiene padre, significa que se ha "desemparentado" (sacado de cualquier contenedor).
        string nombrePadre = transform.parent != null ? transform.parent.name : "NINGUNO (Se ha desemparentado)";

        // Imprimimos en la consola el nuevo padre y el "Stack Trace" (la ruta del código)
        // El Stack Trace nos dice exactamente qué línea de código de qué script ha sido la
        // responsable de mover esta pieza, lo cual es oro puro a la hora de depurar errores.
        Debug.Log($"<color=red><b>[RASTREADOR JERARQUÍA]:</b> El padre de [{gameObject.name}] ha cambiado a: <b>{nombrePadre}</b></color>\n" +
                  $"<b>Culpable:</b>\n{System.Environment.StackTrace}");
    }
}
