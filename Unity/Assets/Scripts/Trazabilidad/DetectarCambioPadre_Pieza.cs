using UnityEngine;

/// <summary>
/// Herramienta de depuración (no forma parte de la lógica de la fábrica): se coloca como
/// componente sobre una pieza en la escena para vigilar de qué "contenedor" (padre en la
/// jerarquía de Unity) cuelga en cada momento. En este proyecto, el padre de una pieza suele
/// indicar en qué estación o hueco se encuentra (por ejemplo, dentro del hueco del HBW, sobre
/// la cinta del MPO, etc.), así que cada vez que ese padre cambia significa que la pieza se ha
/// movido de sitio en el gemelo digital. Es útil para rastrear, mensaje a mensaje, el camino
/// real que sigue una pieza física a través de las estaciones.
/// Este componente está enganchado a los prefabs reales de las piezas (blanca, roja, azul), así
/// que por defecto viene DESACTIVADO (<see cref="activarDebug"/> = false) para no llenar la
/// consola ni gastar CPU calculando el Stack Trace durante una demo normal; solo hay que marcar
/// la casilla en el Inspector cuando de verdad se necesite rastrear una pieza.
/// </summary>
public class DetectarCambioPadre : MonoBehaviour
{
    [Tooltip("Actívalo solo mientras estés depurando: registra en consola (con Stack Trace) cada vez que esta pieza cambia de padre.")]
    public bool activarDebug = false;

    // Este método lo ejecuta Unity AUTOMÁTICAMENTE cada vez que cambia el padre del objeto
    private void OnTransformParentChanged()
    {
        if (!activarDebug) return;

        // Si ya no tiene padre, significa que se ha "desemparentado" (sacado de cualquier contenedor).
        string nombrePadre = transform.parent != null ? transform.parent.name : "NINGUNO (Se ha desemparentado)";

        // Imprimimos en la consola el nuevo padre y el "Stack Trace" (la ruta del código)
        // El Stack Trace nos dice exactamente qué línea de código de qué script ha sido la
        // responsable de mover esta pieza, lo cual es oro puro a la hora de depurar errores.
        Debug.Log($"<color=red><b>[RASTREADOR JERARQUÍA]:</b> El padre de [{gameObject.name}] ha cambiado a: <b>{nombrePadre}</b></color>\n" +
                  $"<b>Culpable:</b>\n{System.Environment.StackTrace}");
    }
}
