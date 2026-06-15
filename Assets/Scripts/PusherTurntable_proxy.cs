using UnityEngine;

public class PusherTurntable_proxy : MonoBehaviour
{
    [Header("Referencias")]
    public ControladorTurntableMPO_mqtt controlador;

    private Rigidbody miRigidbody;

    private void Start()
    {
        // Guardamos nuestro propio Rigidbody para poder controlarlo
        miRigidbody = GetComponent<Rigidbody>();

        if (controlador == null)
        {
            controlador = Object.FindFirstObjectByType<ControladorTurntableMPO_mqtt>();
        }
    }

    private void Update()
    {
        // DESPERTADOR DE FÍSICAS: Si el eyector se activa y nuestro Rigidbody está "dormido",
        // lo forzamos a despertar para que Unity calcule el OnTriggerStay sí o sí.
        if (controlador != null && controlador.EjectorEstaActivo && miRigidbody != null)
        {
            if (miRigidbody.IsSleeping())
            {
                miRigidbody.WakeUp();
            }
        }
    }

    // 1. Si la pieza entra al detector por primera vez
    private void OnTriggerEnter(Collider other)
    {
        // CÁPSULA DE SEGURIDAD: Si la pieza ya es hija de la cinta, no la toques ni la re-vincules
        if (other.transform.parent != null && other.transform.parent.name.ToLower().Contains("raupenbelag"))
        {
            return; // Salir de la función de inmediato sin hacer nada
        }

        EvaluarCapturaPieza(other, "ENTER");
    }

    // 2. Si la pieza ya estaba dentro y el Pusher empieza a moverse
    private void OnTriggerStay(Collider other)
    {
        // CÁPSULA DE SEGURIDAD: Si la pieza ya es hija de la cinta, no la toques ni la re-vincules
        if (other.transform.parent != null && other.transform.parent.name.ToLower().Contains("raupenbelag"))
        {
            return; // Salir de la función de inmediato sin hacer nada
        }

        EvaluarCapturaPieza(other, "STAY");
    }

    // Lógica unificada para evitar duplicar código
    private void EvaluarCapturaPieza(Collider other, string origenEvento)
    {
        if (other.name.ToLower().Contains("pieza"))
        {
            if (controlador == null) return;

            // NUEVA CONDICIÓN: Si la pieza ya es hija de un eslabón de la cinta, la ignoramos completamente
            if (other.transform.parent != null && other.transform.parent.name.ToLower().Contains("raupenbelag"))
            {
                return;
            }

            // Condición original: Eyector activo y la pieza aún no es nuestra hija
            if (controlador.EjectorEstaActivo && other.transform.parent != this.transform)
            {
                Debug.Log($"<color=cyan>[Pusher - ¡CAPTURADA DE CORRECCIÓN!]: Detectada vía {origenEvento}. Asegurando pieza '{other.name}'.</color>");

                other.transform.SetParent(this.transform, true);

                Rigidbody rbPieza = other.GetComponent<Rigidbody>();
                if (rbPieza != null)
                {
                    rbPieza.isKinematic = true;
                    rbPieza.useGravity = false;
                }

                Physics.SyncTransforms();
            }
        }
    }
}