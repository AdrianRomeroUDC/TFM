using UnityEngine;

public class BrazoMPO_proxy : MonoBehaviour
{
    private Transform piezaActual = null;
    private float cooldownSuelte = 0f;

    private float lastY = 0f;
    private bool estaBajando = false;
    private BoxCollider miCollider;

    [Header("Configuración de Proximidad")]
    [Tooltip("Distancia vertical hacia abajo que se extenderá el radar de detección.")]
    public float alcanceDeteccion = 0.06f;

    void Start()
    {
        miCollider = GetComponent<BoxCollider>();
        lastY = transform.position.y;
    }

    // Exponer de forma pública si la ventosa tiene una pieza real sujeta
    public bool TienePieza()
    {
        return piezaActual != null;
    }

    // Permite al controlador gatillar el escaneo exacto al terminar el descenso
    public void ForzarEscaneoInmediato()
    {
        EscanearPiezaPorProximidad();
    }

    void Update()
    {
        if (cooldownSuelte > 0f)
        {
            cooldownSuelte -= Time.deltaTime;
        }

        float currentY = transform.position.y;
        estaBajando = (currentY < lastY - 0.0001f);
        lastY = currentY;

        // Escaneo automático de seguridad pasiva
        if (!estaBajando && cooldownSuelte <= 0f && piezaActual == null)
        {
            EscanearPiezaPorProximidad();
        }
    }

    private void EscanearPiezaPorProximidad()
    {
        if (miCollider == null) return;

        Vector3 centroDeteccion = transform.TransformPoint(miCollider.center) + (Vector3.down * (alcanceDeteccion * 0.5f));
        Vector3 tamanoDeteccion = new Vector3(miCollider.size.x * 1.1f, alcanceDeteccion, miCollider.size.z * 1.1f);
        Vector3 mitadTamano = tamanoDeteccion * 0.5f;

        Collider[] detectados = Physics.OverlapBox(centroDeteccion, mitadTamano, transform.rotation);

        foreach (Collider col in detectados)
        {
            if (col.name.ToLower().Contains("pieza"))
            {
                AgarrarPieza(col.transform);
                return;
            }
        }
    }

    private void AgarrarPieza(Transform pieza)
    {
        piezaActual = pieza;
        pieza.SetParent(this.transform, true);
        pieza.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        Physics.SyncTransforms();

        BoxCollider piezaCollider = pieza.GetComponent<BoxCollider>();

        if (miCollider != null && piezaCollider != null)
        {
            Vector3 pivotGlobalVentosa = this.transform.position;
            Bounds ventosaBounds = miCollider.bounds;
            Bounds piezaBounds = piezaCollider.bounds;

            float fondoVentosaY = ventosaBounds.center.y - ventosaBounds.extents.y;
            float centroObjetivoPiezaY = fondoVentosaY - piezaBounds.extents.y;

            Vector3 posicionObjetivoMundo = new Vector3(pivotGlobalVentosa.x, centroObjetivoPiezaY, pivotGlobalVentosa.z);
            Vector3 vectorCorreccion = posicionObjetivoMundo - piezaBounds.center;

            pieza.position += vectorCorreccion;
        }
        else
        {
            pieza.localPosition = new Vector3(0f, -0.02f, 0f);
        }

        Rigidbody rb = pieza.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log($"<color=lime><b>[Brazo MPO]:</b> Pieza '{pieza.name}' acoplada FISICAMENTE al ras.</color>");
    }

    public void EjecutarRelease(Transform destino)
    {
        if (piezaActual != null)
        {
            Debug.Log($"<color=orange><b>[Brazo MPO]:</b> Liberando pieza '{piezaActual.name}' en destino: {destino.name}.</color>");

            Transform piezaASueltar = piezaActual;

            // Rompemos el lazo con el brazo y lo entregamos al destino real
            piezaASueltar.SetParent(destino, true);

            cooldownSuelte = 1f;
            piezaActual = null;

            // Si el destino es el horno, forzamos su script proxy para que la registre y calibre en Y al instante
            PlataformaHorno_proxy horno = destino.GetComponent<PlataformaHorno_proxy>();
            if (horno != null)
            {
                horno.AcoplarPiezaEnPuntoDeContacto(piezaASueltar);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider collider = GetComponent<BoxCollider>();
        if (collider == null) return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Vector3 centroDeteccion = transform.TransformPoint(collider.center) + (Vector3.down * (alcanceDeteccion * 0.5f));
        Vector3 tamanoDeteccion = new Vector3(collider.size.x * 1.1f, alcanceDeteccion, collider.size.z * 1.1f);

        Gizmos.matrix = Matrix4x4.TRS(centroDeteccion, transform.rotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, tamanoDeteccion);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Vector3.zero, tamanoDeteccion);
    }
}