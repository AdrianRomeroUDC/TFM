using UnityEngine;

/// <summary>
/// Este script va sobre la plataforma de salida DSO de la DPS (la estación de entrada/salida de
/// piezas), el sitio donde el VGR deja las piezas ya terminadas para que salgan de la fábrica.
/// Su única misión es detectar cuándo una pieza real llega a esa plataforma y avisar al
/// controlador de la DPS para que compruebe el sensor real y coloque la pieza bien alineada,
/// igual que ocurre quando la pieza física se detiene sobre la bandeja de salida.
/// </summary>
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

    // Se dispara cuando algo entra en la zona de la plataforma de salida. Comprobamos que sea de
    // verdad una pieza (y no la ventosa del robot todavía sujetándola) antes de darla por llegada.
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

    // Sube por la jerarquía de objetos desde el punto de contacto hasta encontrar el objeto que
    // representa la pieza en sí, por si lo que ha tocado el sensor es solo una parte suya.
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
