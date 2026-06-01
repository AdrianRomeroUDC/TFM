using UnityEngine;

public class AlmacenContenedor : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 1. LÓGICA DE PIEZAS (Se queda igual): Solo nos interesa si entra una "pieza"
        if (other.name.Contains("pieza"))
        {
            if (other.transform.parent != null && other.transform.parent.name.ToLower().Contains("ventosa"))
            {
                return;
            }
            if (other.transform.parent == this.transform)
            {
                return;
            }

            Rigidbody rbPieza = other.GetComponent<Rigidbody>();
            if (rbPieza != null) Destroy(rbPieza);

            other.transform.SetParent(this.transform);

            if (other.TryGetComponent<BoxCollider>(out BoxCollider col))
            {
                col.isTrigger = true;
            }

            Debug.Log($"<color=green>Pieza almacenada con éxito en: {this.name}</color>");
        }
    }

    // --- NUEVO: DETECCIÓN DE IMPACTO CON ESTANTE SÓLIDO ---
    private void OnCollisionEnter(Collision collision)
    {
        // Detectamos si el cajón ha chocado físicamente contra un estante
        // Ajusta el nombre según cómo se llamen tus estantes en la jerarquía (ej: "Estante", "Shelf", "celda")
        if (collision.gameObject.name.ToLower().Contains("estante") || collision.gameObject.name.ToLower().Contains("shelf"))
        {
            // 1. CORTAR EL PARENTESCO CON EL TRANSELEVADOR
            // Al emparentarlo con el propio estante (o dejarlo en null), la plataforma 
            // se retirará libremente sin arrastrar el cajón de vuelta.
            this.transform.SetParent(collision.transform);

            // 2. ESTABILIZAR EL CAJÓN
            // Hacemos que el Rigidbody del cajón se vuelva Kinematic para que se quede 
            // perfectamente congelado y alineado en el estante, evitando micro-vibraciones.
            if (this.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero; // En Unity antiguo usa 'velocity'
                rb.angularVelocity = Vector3.zero;
            }

            Debug.Log($"<color=green><b>[Físicas HBW]:</b> El cajón ha tocado el estante sólido {collision.gameObject.name} y se ha desvinculado del transelevador.</color>");
        }
    }
}