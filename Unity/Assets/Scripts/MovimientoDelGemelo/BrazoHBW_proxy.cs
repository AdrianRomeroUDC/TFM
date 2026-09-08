using UnityEngine;

public class BrazoHBW_proxy : MonoBehaviour
{
    [Header("Referencia al Controlador")]
    public ControladorHBWposition_mqtt scriptPrincipal;

    // Disparador que detecta la cercanía del brazo extractor con el contenedor
    private void OnTriggerEnter(Collider other)
    {
        // Filtramos la detección asegurando que el objeto contenga la palabra clave de un contenedor
        if (other.gameObject.name.ToLower().Contains("container"))
        {
            // --- SOLUCIÓN AL DESCOLOQUE DEL CAJÓN VACÍO ---
            // Localizamos de forma activa el controlador de la cinta en la escena actual
            ControladorCintaHBW_mqtt cintaHBW = FindFirstObjectByType<ControladorCintaHBW_mqtt>();
            if (cintaHBW != null)
            {
                // Le quitamos inmediatamente el control de movimiento de la cinta al cajón para evitar tirones
                cintaHBW.EliminarObjeto(other.transform);
                Debug.Log("<color=red>[Proxy]</color> Contenedor liberado de la Cinta HBW para recogida.");
            }

            if (scriptPrincipal != null)
            {
                // Envía el cajón al script controlador maestro para emparentarlo con la plataforma móvil
                scriptPrincipal.ProcesarCaptura(other.transform, this.transform);
                Debug.Log("<color=cyan>[Proxy]</color> Colisión detectada con: " + other.gameObject.name);
            }
        }
    }
}