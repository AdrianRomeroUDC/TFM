using UnityEngine;

public class SoporteHBW_proxy : MonoBehaviour
{
    [Header("Configuración de Casilla")]
    [Tooltip("Arrastra aquí el GameObject correspondiente de la cuadrícula (ej: Col1Fil1) que debe ser el padre de este contenedor")]
    public Transform casillaPadreCorrespondiente;

    // Cambiamos a OnTriggerEnter para que funcione perfectamente entre cuerpos Kinematic
    private void OnTriggerEnter(Collider other)
    {
        // 1. Buscamos si el objeto o su Rigidbody raíz contienen la palabra "container"
        bool esContenedor = other.gameObject.name.ToLower().Contains("container") ||
                            (other.transform.root.name.ToLower().Contains("container")) ||
                            (other.attachedRigidbody != null && other.attachedRigidbody.name.ToLower().Contains("container"));

        if (esContenedor)
        {
            // Conseguimos la transformación real del contenedor (priorizando la raíz con Rigidbody)
            Transform contenedor = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;

            // 2. Localizamos el transelevador en la escena
            ControladorHBWposition_mqtt transelevador = FindFirstObjectByType<ControladorHBWposition_mqtt>();

            if (transelevador != null && transelevador.objetoEnganchado == contenedor)
            {
                Debug.Log($"<color=cyan><b>[Soporte HBW]:</b> ¡Trigger Detectado! Procesando entrega de: {contenedor.name}</color>");

                // 3. ASIGNACIÓN ASOCIATIVA DE JERARQUÍA
                if (casillaPadreCorrespondiente != null)
                {
                    contenedor.SetParent(casillaPadreCorrespondiente, true);
                    Debug.Log($"<color=lime><b>[Soporte HBW]:</b> Contenedor reemparentado con éxito a su casilla lógica: {casillaPadreCorrespondiente.name}</color>");
                }
                else
                {
                    contenedor.SetParent(this.transform, true);
                    Debug.LogWarning($"<color=yellow><b>[Soporte HBW]:</b> Referencia faltante en {gameObject.name}. Asignado padre provisional.</color>");
                }

                // 4. LIBERACIÓN MECÁNICA DEL BRAZO EN EL CONTROLADOR
                transelevador.NotificarCajonLiberado();

                // 5. ESTABILIZACIÓN FÍSICA INMEDIATA
                if (contenedor.TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }
    }
}