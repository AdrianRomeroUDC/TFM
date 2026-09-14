using UnityEngine;

/// <summary>
/// Este script controla la "ventosa" del brazo elevador del MPO (la estación de horneado y
/// fresado/sierra). Va colocado en la pieza 3D que sube y baja para coger piezas de la cinta o de
/// la plataforma del horno y llevarlas a otro sitio (por ejemplo, al plato giratorio o turntable).
/// A diferencia del VGR, aquí el "agarre" no depende de un mensaje MQTT explícito: el propio script
/// vigila con un sensor de proximidad si hay una pieza justo debajo cuando el brazo deja de bajar,
/// y la engancha automáticamente, igual que la ventosa real del MPO se activa al llegar abajo.
/// </summary>
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

    /// <summary>
    /// Indica si la ventosa del brazo tiene ahora mismo una pieza enganchada.
    /// </summary>
    public bool TienePieza()
    {
        return piezaActual != null;
    }

    /// <summary>
    /// Devuelve la pieza que está agarrada en este momento (o nada, si la ventosa está vacía).
    /// </summary>
    public Transform ObtenerPiezaActual()
    {
        return piezaActual;
    }

    /// <summary>
    /// Fuerza a comprobar ahora mismo si hay una pieza justo debajo del brazo, sin esperar al
    /// siguiente fotograma. Lo usan otros scripts cuando necesitan asegurarse al instante de si
    /// el brazo ha recogido o no una pieza (por ejemplo, justo después de una entrega).
    /// </summary>
    public void ForzarEscaneoInmediato()
    {
        EscanearPiezaPorProximidad();
    }

    void Update()
    {
        // Vamos descontando el tiempo de espera tras soltar una pieza, para no volver a
        // engancharla sin querer justo después de haberla liberado.
        if (cooldownSuelte > 0f)
        {
            cooldownSuelte -= Time.deltaTime;
        }

        // Comparamos la altura actual con la del fotograma anterior para saber si el brazo
        // está bajando en este instante (igual que el brazo real desciende hacia la pieza).
        float currentY = transform.position.y;
        estaBajando = (currentY < lastY - 0.0001f);
        lastY = currentY;

        // Solo buscamos una pieza para agarrar cuando el brazo ya no está bajando, no acabamos
        // de soltar nada y la ventosa está libre: así evitamos enganchar piezas a mitad de camino.
        if (!estaBajando && cooldownSuelte <= 0f && piezaActual == null)
        {
            EscanearPiezaPorProximidad();
        }
    }

    // Lanza una caja de detección (radar) justo debajo del brazo para comprobar si hay alguna
    // pieza al alcance, imitando el sensor real de la ventosa del MPO.
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

    // Engancha de verdad la pieza detectada a la ventosa del brazo: la hace hija del brazo,
    // la coloca justo pegada por debajo (sin huecos ni superposición) y desactiva su física
    // para que se mueva solidaria con el brazo en vez de caer por gravedad.
    private void AgarrarPieza(Transform pieza)
    {
        piezaActual = pieza;
        pieza.SetParent(this.transform, true);
        pieza.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        Physics.SyncTransforms();

        BoxCollider piezaCollider = pieza.GetComponent<BoxCollider>();

        if (miCollider != null && piezaCollider != null)
        {
            // Calculamos la posición exacta para que la parte de arriba de la pieza quede
            // pegada justo al fondo de la ventosa, sin flotar ni atravesarla.
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
            // Si por lo que sea no tenemos los colliders para calcular la posición exacta,
            // usamos un desplazamiento aproximado de seguridad.
            pieza.localPosition = new Vector3(0f, -0.02f, 0f);
        }

        // Congelamos la pieza (sin gravedad ni velocidad) para que viaje pegada a la ventosa.
        Rigidbody rb = pieza.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log($"<color=lime><b>[Brazo MPO]:</b> Pieza '{pieza.name}' acoplada FÍSICAMENTE al ras.</color>");
    }

    /// <summary>
    /// MÉTODO DE REVERSIÓN: deshace un agarre que en realidad no ha ocurrido en la máquina física
    /// (por ejemplo, si el sensor real del horno indica que la pieza sigue allí) y la devuelve
    /// exactamente al sitio y postura donde estaba antes, para que el gemelo digital no se
    /// desincronice de la fábrica real.
    /// </summary>
    public void CancelarAgarreYDevolver(Transform nuevoPadre, Vector3 localPos, Quaternion localRot)
    {
        if (piezaActual == null) return;

        Transform piezaADevolver = piezaActual;
        piezaActual = null; // Desvinculamos la pieza de la ventosa

        piezaADevolver.SetParent(nuevoPadre, true);
        piezaADevolver.localPosition = localPos;
        piezaADevolver.localRotation = localRot;

        Rigidbody rb = piezaADevolver.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        BoxCollider[] colliders = piezaADevolver.GetComponentsInChildren<BoxCollider>();
        foreach (BoxCollider col in colliders) if (col != null) col.isTrigger = false;

        Debug.Log($"<color=red><b>[MPO FALLO AGARRE]:</b> Pieza '{piezaADevolver.name}' devuelta al horno. Coincide con sensor activo del PLC.</color>");
    }

    /// <summary>
    /// Suelta la pieza que lleva agarrada la ventosa del brazo del MPO en el destino indicado
    /// (por ejemplo, el horno o el plato giratorio). Si el destino tiene su propio script de
    /// acople (horno o turntable), le pasamos la pieza para que la encaje bien colocada; si no,
    /// simplemente la dejamos caer con física normal, como pasaría con la pieza real.
    /// </summary>
    public void EjecutarRelease(Transform destino)
    {
        if (piezaActual != null)
        {
            Debug.Log($"<color=orange><b>[Brazo MPO]:</b> Liberando pieza '{piezaActual.name}' en destino: {destino.name}.</color>");

            Transform piezaASueltar = piezaActual;

            piezaASueltar.SetParent(destino, true);

            cooldownSuelte = 1f;
            piezaActual = null;

            PlataformaHorno_proxy horno = destino.GetComponent<PlataformaHorno_proxy>() ?? destino.GetComponentInChildren<PlataformaHorno_proxy>();
            PlataformaTurntable_proxy turntable = destino.GetComponent<PlataformaTurntable_proxy>() ?? destino.GetComponentInChildren<PlataformaTurntable_proxy>();

            if (horno != null)
            {
                // El destino es el horno: dejamos que su propio script coloque la pieza en el
                // punto de contacto exacto calibrado dentro del horno.
                horno.AcoplarPiezaEnPuntoDeContacto(piezaASueltar);
            }
            else if (turntable != null)
            {
                // El destino es el plato giratorio: dejamos que su script la centre sobre la mesa.
                turntable.AcoplarPiezaEnMesa(piezaASueltar);
            }
            else
            {
                // No hay un sitio especial de acople: la soltamos con física normal (gravedad)
                // para que caiga de forma realista.
                Rigidbody rb = piezaASueltar.GetComponent<Rigidbody>() ?? piezaASueltar.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }
    }

    // Dibuja en el editor de Unity (solo visible al seleccionar el objeto) la caja del radar de
    // detección, para poder ver y calibrar visualmente dónde "mira" la ventosa del MPO.
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
