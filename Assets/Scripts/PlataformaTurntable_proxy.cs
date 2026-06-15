using UnityEngine;

public class PlataformaTurntable_proxy : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Arrastra aquí el objeto que contiene el script ControladorTurntableMPO_mqtt.")]
    public ControladorTurntableMPO_mqtt controlador;

    [Header("Ajuste de Altura Global")]
    [Tooltip("Ajusta la altura en el eje Y global para que la pieza apoye perfectamente sobre el plato.")]
    public float offsetAlturaY = 0.05f;

    private void Start()
    {
        // Intenta buscar el controlador automáticamente si no se arrastró en el Inspector
        if (controlador == null)
        {
            controlador = Object.FindFirstObjectByType<ControladorTurntableMPO_mqtt>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.name.ToLower().Contains("pieza"))
        {
            // 🔥 FILTRO MAESTRO: Si el Pusher está activo o extendiéndose, la mesa se congela
            // y no procesa la pieza, evitando la guerra de parentesco (Tug of War).
            if (controlador != null && controlador.EjectorEstaActivo)
            {
                return;
            }

            Debug.Log($"[Turntable]: Pieza '{other.name}' detectada de forma segura. Centrando en el plato.");

            Quaternion rotacionMundoOriginal = other.transform.rotation;
            BoxCollider miCollider = GetComponent<BoxCollider>();

            if (miCollider != null)
            {
                Vector3 centroRealMundo = miCollider.bounds.center;
                other.transform.position = new Vector3(centroRealMundo.x, centroRealMundo.y + offsetAlturaY, centroRealMundo.z);
            }
            else
            {
                other.transform.position = this.transform.position;
            }

            other.transform.rotation = rotacionMundoOriginal;
            other.transform.SetParent(this.transform, true);

            Physics.SyncTransforms();

            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }
    }
}