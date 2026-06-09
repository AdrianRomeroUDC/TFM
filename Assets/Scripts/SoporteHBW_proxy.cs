using UnityEngine;

public class SoporteHBW_proxy : MonoBehaviour
{
    // Ya no hace falta la variable 'casillaPadreCorrespondiente' ya que el contenedor recuerda su posición solo.

    private void OnTriggerEnter(Collider other)
    {
        // 1. Buscamos si el objeto que entra es un contenedor
        bool esContenedor = other.gameObject.name.ToLower().Contains("container") ||
                            (other.transform.root.name.ToLower().Contains("container")) ||
                            (other.attachedRigidbody != null && other.attachedRigidbody.name.ToLower().Contains("container"));

        if (esContenedor)
        {
            // Conseguimos la transformación real del contenedor (priorizando la raíz con Rigidbody)
            Transform contenedorTransform = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;

            // 2. Localizamos el transelevador en la escena
            ControladorHBWposition_mqtt transelevador = FindFirstObjectByType<ControladorHBWposition_mqtt>();

            // Verificamos si este contenedor en específico es el que lleva el brazo actualmente sujeto
            if (transelevador != null && transelevador.objetoCogido == contenedorTransform)
            {
                Debug.Log($"<color=cyan><b>[Soporte HBW]:</b> ¡Trigger Detectado! Ordenando retorno inmediato de: {contenedorTransform.name}</color>");

                // 3. MANDAR AL CONTENEDOR A SU POSICIÓN DE ORIGEN
                if (contenedorTransform.TryGetComponent<ContenedorHBW_proxy>(out ContenedorHBW_proxy proxyContenedor))
                {
                    proxyContenedor.RetornarAPosicionInicial();
                }
                else
                {
                    // Plan de respaldo por si el componente está en un objeto hijo
                    ContenedorHBW_proxy proxyHijo = contenedorTransform.GetComponentInChildren<ContenedorHBW_proxy>();
                    if (proxyHijo != null) proxyHijo.RetornarAPosicionInicial();
                }

                // 4. LIBERACIÓN MECÁNICA DEL BRAZO EN EL CONTROLADOR MAESTRO
                transelevador.NotificarCajonLiberado();

                // 5. ESTABILIZACIÓN FÍSICA INMEDIATA
                if (contenedorTransform.TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }
    }
}