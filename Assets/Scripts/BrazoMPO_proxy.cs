using UnityEngine;

public class BrazoMPO_proxy : MonoBehaviour
{
    private Transform piezaActual = null;
    private float cooldownSuelte = 0f; // Evita que vuelva a agarrar la pieza inmediatamente al soltarla

    // Variables para el control y detección del movimiento del brazo
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

        // Si el brazo se detiene, no está en cooldown y no tiene pieza, activa el escaneo por proximidad
        if (!estaBajando && cooldownSuelte <= 0f && piezaActual == null)
        {
            EscanearPiezaPorProximidad();
        }
    }

    private void EscanearPiezaPorProximidad()
    {
        if (miCollider == null) return;

        // Calculamos el centro del radar proyectándolo hacia abajo según el alcance definido
        Vector3 centroDeteccion = transform.TransformPoint(miCollider.center) + (Vector3.down * (alcanceDeteccion * 0.5f));

        // Expandimos ligeramente el tamaño del volumen de detección para asegurar la intersección
        Vector3 tamanoDeteccion = new Vector3(miCollider.size.x * 1.1f, alcanceDeteccion, miCollider.size.z * 1.1f);
        Vector3 mitadTamano = tamanoDeteccion * 0.5f;

        // Genera el volumen de detección invisible en el espacio de físicas
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

        // 1. Establecemos el parentesco para integrarla en el brazo
        pieza.SetParent(this.transform, true);

        // 2. Aplicamos la rotación local obligatoria en el eje X
        pieza.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        // =======================================================================================
        // ¡LÍNEA CLAVE!: Forzamos a Unity a actualizar la caché de físicas inmediatamente.
        // Esto hace que 'piezaCollider.bounds' conozca su verdadera altura YA ROTADA en este frame.
        // =======================================================================================
        Physics.SyncTransforms();

        BoxCollider piezaCollider = pieza.GetComponent<BoxCollider>();

        if (miCollider != null && piezaCollider != null)
        {
            // 3. Capturamos el pívot horizontal exacto del brazo (X, Z)
            Vector3 pivotGlobalVentosa = this.transform.position;

            // 4. Ahora los Bounds globales están 100% actualizados gracias al SyncTransforms
            Bounds ventosaBounds = miCollider.bounds;
            Bounds piezaBounds = piezaCollider.bounds;

            // 5. Calculamos el contacto perfecto superficie contra superficie en el eje Y:
            // El centro en Y de la pieza debe ser: el fondo de la ventosa menos la mitad del grosor de la pieza.
            float fondoVentosaY = ventosaBounds.center.y - ventosaBounds.extents.y;
            float centroObjetivoPiezaY = fondoVentosaY - piezaBounds.extents.y;

            Vector3 posicionObjetivoMundo = new Vector3(
                pivotGlobalVentosa.x,
                centroObjetivoPiezaY,
                pivotGlobalVentosa.z
            );

            // 6. Calculamos el vector de corrección respecto al centro real de la caja de la pieza
            Vector3 vectorCorreccion = posicionObjetivoMundo - piezaBounds.center;

            // 7. Desplazamos la pieza en posición global. Los colisionadores ahora se tocarán de forma milimétrica.
            pieza.position += vectorCorreccion;
        }
        else
        {
            // Valor de contingencia por si falla la lectura de algún componente
            pieza.localPosition = new Vector3(0f, -0.02f, 0f);
        }

        // Anulación de las dinámicas físicas del Rigidbody para el transporte cinemático seguro
        Rigidbody rb = pieza.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log($"[Brazo MPO]: Pieza '{pieza.name}' acoplada con éxito. Colisionadores tocándose al ras sin atravesarse.");
    }

    public void EjecutarRelease(Transform destino)
    {
        if (piezaActual != null)
        {
            Debug.Log("[Brazo MPO]: Transfiriendo la pieza al objeto de destino.");

            // Reasignación del padre al destino final
            piezaActual.SetParent(destino, true);
            cooldownSuelte = 1f;
            piezaActual = null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Dibuja una caja azul transparente en la pestaña Scene para calibrar el rango de captura visualmente
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