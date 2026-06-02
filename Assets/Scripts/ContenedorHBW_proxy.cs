using UnityEngine;

public class ContenedorHBW_proxy : MonoBehaviour
{
    // Método que gestiona la entrada de piezas de colores dentro del volumen del contenedor
    private void OnTriggerEnter(Collider other)
    {
        // 1. LÓGICA DE PIEZAS: Filtramos y comprobamos si el objeto entrante es catalogado como una "pieza"
        if (other.name.Contains("pieza"))
        {
            // Si la pieza viene sujeta todavía por la ventosa de la grúa de vacío, abortamos para no interferir
            if (other.transform.parent != null && other.transform.parent.name.ToLower().Contains("ventosa"))
            {
                return;
            }
            // Si la pieza ya es formalmente hija de este cajón, salimos para prevenir llamadas redundantes
            if (other.transform.parent == this.transform)
            {
                return;
            }

            // Destruimos el Rigidbody de la pieza para neutralizar sus dinámicas físicas y que no genere peso extra
            Rigidbody rbPieza = other.GetComponent<Rigidbody>();
            if (rbPieza != null) Destroy(rbPieza);

            // Re-emparentamos la pieza para que sea hija del cajón y se traslade unificada a él
            other.transform.SetParent(this.transform);

            // Convertimos su collider en Trigger para evitar fricciones o colisiones internas caóticas dentro del cajón
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
        // Detectamos si el cajón ha colisionado físicamente contra la superficie dura de un estante/soporte
        // Evaluamos las variaciones de nombres más comunes usadas en la jerarquía del almacén
        if (collision.gameObject.name.ToLower().Contains("estante") || collision.gameObject.name.ToLower().Contains("shelf"))
        {
            // 1. CORTAR EL PARENTESCO CON EL TRANSELEVADOR
            // Al emparentar el contenedor directamente con el objeto físico del estante contra el que chocó,
            // el brazo del transelevador se libera y podrá encogerse de forma limpia sin arrastrarlo.
            this.transform.SetParent(collision.transform);

            // 2. ESTABILIZAR EL CAJÓN
            // Congelamos el contenedor transformándolo en Kinematic. Esto neutraliza de golpe las inercias
            // remanentes y previene las micro-vibraciones o desalineaciones ocasionadas por la física interactiva.
            if (this.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;  // Reseteamos a cero las fuerzas de velocidad lineal
                rb.angularVelocity = Vector3.zero; // Reseteamos a cero las fuerzas de velocidad angular
            }

            Debug.Log($"<color=green><b>[Físicas HBW]:</b> El cajón ha tocado el estante sólido {collision.gameObject.name} y se ha desvinculado del transelevador.</color>");
        }
    }
}