using UnityEngine;

public class PlataformaDSO_proxy : MonoBehaviour
{
    private ControladorDPS_mqtt dps;

    void Start()
    {
        dps = Object.FindFirstObjectByType<ControladorDPS_mqtt>();

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true; // El colisionador actúa como zona de llegada
        }
        else
        {
            Debug.LogWarning($"<b>[DSO Proxy]:</b> El objeto {name} no tiene un Collider.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (dps == null) return;

        // 1. Buscamos si lo que ha entrado es una pieza real
        Transform rootPieza = EncontrarRaizPieza(other.transform);
        if (rootPieza == null) return;

        // 2. Salvaguarda: Ignoramos si la pieza sigue sujeta en la ventosa del VGR
        Rigidbody rb = rootPieza.GetComponent<Rigidbody>();
        if (rb != null && rb.isKinematic) return;

        // 3. ¡Colisión detectada en la plataforma! Delegamos la validación del sensor y el acoplamiento al DPS
        dps.AlinearPiezaEnDSO(rootPieza);
    }

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