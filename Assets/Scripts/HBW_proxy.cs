using UnityEngine;

public class HBW_proxy : MonoBehaviour
{
    [Header("Referencia al Controlador")]
    public ControladorHBWposition_mqtt scriptPrincipal;

    private void OnTriggerEnter(Collider other)
    {
        // Detecta el cajón por nombre
        if (other.gameObject.name.ToLower().Contains("container"))
        {
            if (scriptPrincipal != null)
            {
                // Envía el cajón al script controlador para emparentarlo
                scriptPrincipal.ProcesarCaptura(other.transform, this.transform);
                Debug.Log("<color=cyan>[Proxy]</color> Colisión detectada con: " + other.gameObject.name);
            }
        }
    }
}