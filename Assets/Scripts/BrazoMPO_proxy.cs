using UnityEngine;

public class BrazoMPO_proxy : MonoBehaviour
{
    private Transform piezaActual = null;
    private float cooldownSuelte = 0f; // Evita que vuelva a agarrar la pieza inmediatamente al soltarla

    // Variables para el control y detección del movimiento del brazo
    private float lastY = 0f;
    private bool estaBajando = false;

    void Start()
    {
        // Registro de la posición Y inicial del brazo
        lastY = transform.position.y;
    }

    void Update()
    {
        if (cooldownSuelte > 0f)
        {
            cooldownSuelte -= Time.deltaTime;
        }

        // Monitoreo de la dirección del movimiento en el eje vertical
        float currentY = transform.position.y;
        estaBajando = (currentY < lastY - 0.0001f);
        lastY = currentY;
    }

    private void OnTriggerStay(Collider other)
    {
        // Se bloquea el agarre si el brazo aún se encuentra en la fase de descenso
        if (piezaActual != null || cooldownSuelte > 0f || estaBajando) return;

        if (other.name.ToLower().Contains("pieza"))
        {
            AgarrarPieza(other.transform);
        }
    }

    private void AgarrarPieza(Transform pieza)
    {
        piezaActual = pieza;

        // Establecimiento de la relación de jerarquía con la ventosa
        pieza.SetParent(this.transform, true);

        // Rotación local fija en el eje X
        pieza.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        // Determinación del offset vertical para el posicionamiento estricto borde con borde
        BoxCollider miCollider = GetComponent<BoxCollider>();
        BoxCollider piezaCollider = pieza.GetComponent<BoxCollider>();

        float offsetVertical = -0.02f; // Valor de contingencia

        if (miCollider != null && piezaCollider != null)
        {
            // Combinación de las mitades de las alturas de ambos colisionadores
            offsetVertical = -(miCollider.size.y * 0.5f + piezaCollider.size.y * 0.5f);
        }

        // Reposicionamiento local inmediato de la pieza en el extremo inferior
        pieza.localPosition = new Vector3(0f, offsetVertical, 0f);

        // Anulación de las dinámicas físicas del Rigidbody para el transporte cinemático
        Rigidbody rb = pieza.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log("[Brazo MPO]: Pieza acoplada con éxito mediante detención sin contacto previo.");
    }

    public void EjecutarRelease(Transform destino)
    {
        if (piezaActual != null)
        {
            Debug.Log("[Brazo MPO]: Transfiriendo la pieza al objeto de destino.");

            // Reasignación del padre al destino final sin modificar propiedades físicas adicionales
            piezaActual.SetParent(destino, true);
            cooldownSuelte = 1f;
            piezaActual = null;
        }
    }
}