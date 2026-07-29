using UnityEngine;

public class PlataformaDSO_proxy : MonoBehaviour
{
    private ControladorDPS_mqtt dps;

    void Start()
    {
        // Buscamos el controlador central del DPS
        dps = Object.FindFirstObjectByType<ControladorDPS_mqtt>();

        // Nos aseguramos de que el collider de la plataforma actúe como Trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        else
        {
            Debug.LogWarning($"<b>[DSO Proxy]:</b> El objeto {name} no tiene un Collider. ¡Añádele uno para que detecte las piezas!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (dps == null) return;

        // 1. Buscamos si lo que ha entrado es una pieza (o hijo de ella)
        Transform rootPieza = EncontrarRaizPieza(other.transform);
        if (rootPieza == null) return;

        // 2. SALVAGUARDA CLAVE: Comprobamos si la pieza está libre (en caída/física activa)
        // Si el Rigidbody es Kinematic, significa que el VGR aún la tiene sujeta en su ventosa.
        // Solo la magnetizamos si ya ha sido soltada (isKinematic == false).
        Rigidbody rb = rootPieza.GetComponent<Rigidbody>();
        if (rb != null && rb.isKinematic) return;

        // 3. ¡Alineación Magnética! Delegamos el cálculo al DPS
        dps.AlinearPiezaEnDSO(rootPieza);
    }

    private void OnTriggerStay(Collider other)
    {
        if (dps == null) return;

        // 1. Buscamos si lo que ha entrado es una pieza (o hijo de ella)
        Transform rootPieza = EncontrarRaizPieza(other.transform);
        if (rootPieza == null) return;

        // 2. SALVAGUARDA CLAVE: Comprobamos si la pieza está libre (en caída/física activa)
        // Si el Rigidbody es Kinematic, significa que el VGR aún la tiene sujeta en su ventosa.
        // Solo la magnetizamos si ya ha sido soltada (isKinematic == false).
        Rigidbody rb = rootPieza.GetComponent<Rigidbody>();
        if (rb != null && rb.isKinematic) return;

        // 3. ¡Alineación Magnética! Delegamos el cálculo al DPS
        dps.AlinearPiezaEnDSO(rootPieza);
    }

    // Método auxiliar ultra-seguro para encontrar la pieza real
    private Transform EncontrarRaizPieza(Transform t)
    {
        Transform actual = t;
        while (actual != null)
        {
            if (actual.name.ToLower().Contains("pieza"))
            {
                return actual;
            }
            actual = actual.parent;
        }
        return null;
    }
}