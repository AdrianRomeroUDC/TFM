using UnityEngine;

public class SoporteHBW_proxy : MonoBehaviour
{
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

            // MODIFICACIÓN AQUÍ: Añadimos "&& transelevador.esOperacionDeEntrega"
            // El soporte SOLO actuará si el transelevador viene con la orden explícita de DEJAR un cajón.
            if (transelevador != null && transelevador.objetoCogido == contenedorTransform && transelevador.esOperacionDeEntrega)
            {
                Debug.Log($"<color=cyan><b>[Soporte HBW]:</b> ¡Entrega Confirmada! Registrando retorno en estante de: {contenedorTransform.name}</color>");

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