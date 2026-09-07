using UnityEngine;

public class PlataformaTurntable_proxy : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Arrastra aquí el objeto que contiene el script ControladorTurntableMPO_mqtt.")]
    public ControladorTurntableMPO_mqtt controlador;

    [Header("Ajuste de Altura Global")]
    [Tooltip("Ajusta la altura en el eje Y global para que la pieza apoye perfectamente sobre el plato.")]
    public float offsetAlturaY = 0.01f; // Ajustado por defecto a un valor más real (1cm)

    private void Start()
    {
        if (controlador == null)
        {
            controlador = Object.FindFirstObjectByType<ControladorTurntableMPO_mqtt>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.name.ToLower().Contains("pieza"))
        {
            // Si la pieza ya es hija directa de la mesa (porque la acopló el brazo),
            // ignoramos el trigger para evitar re-cálculos innecesarios.
            if (other.transform.parent == this.transform) return;

            AcoplarPiezaEnMesa(other.transform);
        }
    }

    // =======================================================================
    // METODO DE ACOPLE DIRECTO (Invocado de forma segura por el brazo MPO)
    // =======================================================================
    public void AcoplarPiezaEnMesa(Transform pieza)
    {
        if (controlador != null && controlador.EjectorEstaActivo)
        {
            return;
        }

        Debug.Log($"[Turntable - ACOPLE]: Fijando pieza '{pieza.name}' de forma segura en el centro del plato.");

        Quaternion rotacionMundoOriginal = pieza.rotation;
        BoxCollider miCollider = GetComponent<BoxCollider>();

        if (miCollider != null)
        {
            Vector3 centroRealMundo = miCollider.bounds.center;
            // Posicionamos la pieza exactamente sobre el colisionador usando el offset del Inspector
            pieza.position = new Vector3(centroRealMundo.x, centroRealMundo.y + offsetAlturaY, centroRealMundo.z);
        }
        else
        {
            pieza.position = this.transform.position;
        }

        pieza.rotation = rotacionMundoOriginal;
        pieza.SetParent(this.transform, true);

        Physics.SyncTransforms();

        Rigidbody rb = pieza.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }
}