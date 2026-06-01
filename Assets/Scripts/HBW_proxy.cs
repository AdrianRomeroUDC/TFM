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
            // --- SOLUCON AL DESCOLOQUE DEL CAJÓN VACÍO ---
            // Buscamos el controlador de la cinta en la escena
            ControladorCintaHBW_mqtt cintaHBW = FindFirstObjectByType<ControladorCintaHBW_mqtt>();
            if (cintaHBW != null)
            {
                // Le quitamos inmediatamente el control de movimiento de la cinta al cajón
                cintaHBW.EliminarObjeto(other.transform);
                Debug.Log("<color=red>[Proxy]</color> Contenedor liberado de la Cinta HBW para recogida.");
            }

            if (scriptPrincipal != null)
            {
                // Envía el cajón al script controlador para emparentarlo con la plataforma
                scriptPrincipal.ProcesarCaptura(other.transform, this.transform);
                Debug.Log("<color=cyan>[Proxy]</color> Colisión detectada con: " + other.gameObject.name);
            }
        }
    }
}