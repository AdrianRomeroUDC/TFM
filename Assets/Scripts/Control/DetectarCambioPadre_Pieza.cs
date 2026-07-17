using UnityEngine;

public class DetectarCambioPadre : MonoBehaviour
{
    // Este método lo ejecuta Unity AUTOMÁTICAMENTE cada vez que cambia el padre del objeto
    private void OnTransformParentChanged()
    {
        string nombrePadre = transform.parent != null ? transform.parent.name : "NINGUNO (Se ha desemparentado)";

        // Imprimimos en la consola el nuevo padre y el "Stack Trace" (la ruta del código)
        Debug.Log($"<color=red><b>[RASTREADOR JERARQUÍA]:</b> El padre de [{gameObject.name}] ha cambiado a: <b>{nombrePadre}</b></color>\n" +
                  $"<b>Culpable:</b>\n{System.Environment.StackTrace}");
    }
}
