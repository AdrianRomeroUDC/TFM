using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ControladorHorno_mqtt : MonoBehaviour
{
    [Header("Componentes")]
    public Transform puerta;
    [Tooltip("Asigna aquí el objeto raíz 'Horno_BasePonerPieza'. El script buscará automáticamente el hijo que contenga la palabra 'plataforma'.")]
    public Transform plataformaPieza;
    public Light luzHorno;

    [Header("Prefabs de Auto-Sanación")]
    [Tooltip("Arrastra aquí el prefab de tu pieza base gris (el mismo que usa el DPS).")]
    public GameObject prefabBaseGris;

    [Header("Ajustes de Seguridad VGR")]
    [Tooltip("Distancia límite (en metros) para considerar que el VGR está 'cerca' del horno. Si está más cerca de este valor con pieza, se cancela el spawn.")]
    public float distanciaLimiteVGR = 0.15f;

    [Header("Posiciones (Usar clic derecho para capturar)")]
    [ContextMenuItem("Capturar Cerrada", "CapturarPuertaCerrada")] public Vector3 posPuertaCerrada;
    [ContextMenuItem("Capturar Abierta", "CapturarPuertaAbierta")] public Vector3 posPuertaAbierta;
    [ContextMenuItem("Capturar Fuera", "CapturarPlataformaFuera")] public Vector3 posPlataformaFuera;
    [ContextMenuItem("Capturar Dentro", "CapturarPlataformaDentro")] public Vector3 posPlataformaDentro;

    [Header("Configuración de Velocidad (Duración en segundos)")]
    public float duracionMovimientoPuerta = 1.5f;
    public float duracionMovimientoPlataforma = 1.0f;

    private Queue<MPOHornoPayload> colaMensajes = new Queue<MPOHornoPayload>();
    private Coroutine movimientoPuerta;
    private Coroutine movimientoPlataforma;

    void CapturarPuertaCerrada() => posPuertaCerrada = puerta.position;
    void CapturarPuertaAbierta() => posPuertaAbierta = puerta.position;
    void CapturarPlataformaFuera() => posPlataformaFuera = plataformaPieza.position;
    void CapturarPlataformaDentro() => posPlataformaDentro = plataformaPieza.position;

    private void Start() => StartCoroutine(SuscripcionSegura());

    private IEnumerator SuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;

        MQTTClient.Instance.OnHornoUpdateEvent += (data) => {
            lock (colaMensajes) { colaMensajes.Enqueue(data); }
        };
        Debug.Log("<color=cyan>Controlador Horno suscrito correctamente</color>");
    }

    private void Update()
    {
        lock (colaMensajes)
        {
            while (colaMensajes.Count > 0)
            {
                ProcesarHorno(colaMensajes.Dequeue());
            }
        }
    }

    private void ProcesarHorno(MPOHornoPayload data)
    {
        if (luzHorno) luzHorno.enabled = (data.lights == 1);

        // MOVIMIENTO PUERTA
        if (data.openDoor == 1)
        {
            if (movimientoPuerta != null) StopCoroutine(movimientoPuerta);
            movimientoPuerta = StartCoroutine(MoverObjeto(puerta, posPuertaAbierta, duracionMovimientoPuerta));
        }
        else if (data.closeDoor == 1)
        {
            if (movimientoPuerta != null) StopCoroutine(movimientoPuerta);
            movimientoPuerta = StartCoroutine(MoverObjeto(puerta, posPuertaCerrada, duracionMovimientoPuerta));
        }

        // MOVIMIENTO PLATAFORMA
        if (data.move2Ref5 == 1)
        {
            if (movimientoPlataforma != null) StopCoroutine(movimientoPlataforma);
            movimientoPlataforma = StartCoroutine(MoverObjeto(plataformaPieza, posPlataformaDentro, duracionMovimientoPlataforma));
        }
        else if (data.move2Ref6 == 1)
        {
            if (movimientoPlataforma != null) StopCoroutine(movimientoPlataforma);
            movimientoPlataforma = StartCoroutine(MoverObjeto(plataformaPieza, posPlataformaFuera, duracionMovimientoPlataforma));
        }

        // SENSOR DEL HORNO (Gestión de presencia bidireccional)
        if (data.ovenSensor == 1)
        {
            IntentarSpawnPiezaHorno();
        }
        else if (data.ovenSensor == 0)
        {
            IntentarLimpiezaPiezaHorno();
        }
    }

    private Transform BuscarPlataformaRealHijo()
    {
        if (plataformaPieza == null) return null;

        if (plataformaPieza.name.ToLower().Contains("plataforma")) return plataformaPieza;

        foreach (Transform t in plataformaPieza.GetComponentsInChildren<Transform>(true))
        {
            if (t != plataformaPieza && t.name.ToLower().Contains("plataforma"))
            {
                return t;
            }
        }

        return plataformaPieza;
    }

    private void IntentarSpawnPiezaHorno()
    {
        if (plataformaPieza == null) return;

        if (prefabBaseGris == null)
        {
            Debug.LogError("<color=red><b>[HORNO SPAWN - ERROR]:</b> ¡Falta asignar el Prefab Base Gris en el Inspector!</color>");
            return;
        }

        Transform plataformaReal = BuscarPlataformaRealHijo();
        if (plataformaReal == null) return;

        // =======================================================================
        // NUEVO: FILTRO DE DETECCIÓN DEL BRAZO MPO EN Z_HORNO
        // =======================================================================
        ControladorBrazoMPO brazoMPO = Object.FindFirstObjectByType<ControladorBrazoMPO>();
        if (brazoMPO != null && brazoMPO.ejeHorizontal != null)
        {
            float distanciaZ = Mathf.Abs(brazoMPO.ejeHorizontal.localPosition.z - brazoMPO.zHorno);
            if (distanciaZ < 0.00005f) // Margen de precisión de 5 milímetros
            {
                Debug.Log($"<color=yellow><b>[HORNO SPAWN]:</b> El brazo MPO está en posición de horno Z ({distanciaZ:F5}m). Se bloquea el spawn.</color>");
                return; // Cancelamos el spawn de inmediato
            }
        }

        // 🛡️ ESCUDO DE PROTECCIÓN DISTANCIAL VGR
        ControladorVGR_mqtt vgr = Object.FindFirstObjectByType<ControladorVGR_mqtt>();
        if (vgr != null && vgr.ObtenerPiezaEnganchada() != null)
        {
            float distanciaAlHorno = Vector3.Distance(vgr.transform.position, plataformaReal.position);

            if (distanciaAlHorno < distanciaLimiteVGR)
            {
                Debug.Log($"<color=yellow><b>[HORNO SPAWN]:</b> El VGR tiene una pieza sujeta y está CERCA del horno ({distanciaAlHorno:F3}m < {distanciaLimiteVGR}m). Se cancela el Spawn de respaldo.</color>");
                return;
            }
            else
            {
                Debug.Log($"<color=cyan><b>[HORNO SPAWN]:</b> El VGR tiene una pieza pero está LEJOS ({distanciaAlHorno:F3}m >= {distanciaLimiteVGR}m). Se permite el Spawn de respaldo.</color>");
            }
        }

        Collider colPlat = plataformaReal.GetComponent<Collider>();
        Vector3 centroPlatMundo = (colPlat != null) ? colPlat.bounds.center : plataformaReal.position;

        // Evitar duplicaciones
        bool yaHayPieza = false;
        Collider[] collidersCercanos = Physics.OverlapSphere(centroPlatMundo, 0.05f);

        foreach (Collider col in collidersCercanos)
        {
            string nombreCol = col.name.ToLower();

            if (col.transform == plataformaReal ||
                col.transform == plataformaPieza ||
                nombreCol.Contains("plataforma") ||
                nombreCol.Contains("puerta") ||
                nombreCol.Contains("door"))
            {
                continue;
            }

            if (nombreCol.Contains("pieza"))
            {
                yaHayPieza = true;
                break;
            }
        }

        if (!yaHayPieza)
        {
            GameObject nuevaPieza = Instantiate(prefabBaseGris);
            nuevaPieza.name = "pieza_base_horno";
            nuevaPieza.transform.localScale = prefabBaseGris.transform.localScale;

            nuevaPieza.transform.SetParent(plataformaReal, true);

            Vector3 posicionSincronizada = PlataformaHorno_proxy.PosicionCalibradaPieza;
            nuevaPieza.transform.localPosition = posicionSincronizada;
            nuevaPieza.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            Rigidbody rb = nuevaPieza.GetComponent<Rigidbody>();
            if (rb == null) rb = nuevaPieza.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            BoxCollider[] colliders = nuevaPieza.GetComponentsInChildren<BoxCollider>();
            foreach (BoxCollider col in colliders) if (col != null) col.isTrigger = false;

            Debug.Log($"<color=green><b>[HORNO SPAWN]:</b> Pieza de respaldo 'pieza_base_horno' instanciada en posición calibrada ({posicionSincronizada.x:F6}, {posicionSincronizada.y:F6}, {posicionSincronizada.z:F6}).</color>");
        }
    }

    // ==========================================
    // NUEVO MÉTODO: AUTO-LIMPIEZA DE LA PLATAFORMA DEL HORNO
    // ==========================================
    private void IntentarLimpiezaPiezaHorno()
    {
        if (plataformaPieza == null) return;

        // 1. Evitar ejecuciones si no se ha calibrado la posición "Fuera" todavía
        if (posPlataformaFuera == Vector3.zero) return;

        // 2. Verificar si la plataforma está físicamente en la posición "Fuera" (con un margen de 1cm)
        float distanciaAFuera = Vector3.Distance(plataformaPieza.position, posPlataformaFuera);
        if (distanciaAFuera > 0.01f) return;

        Transform plataformaReal = BuscarPlataformaRealHijo();
        if (plataformaReal == null) return;

        // 3. Buscar cualquier pieza que esté apoyada (que sea hija directa) en la plataforma del horno
        List<GameObject> piezasEliminar = new List<GameObject>();
        foreach (Transform hijo in plataformaReal)
        {
            string nombreHijo = hijo.name.ToLower();
            if (nombreHijo.Contains("pieza") || hijo.CompareTag("Pieza"))
            {
                piezasEliminar.Add(hijo.gameObject);
            }
        }

        // 4. Eliminar la pieza de la simulación
        if (piezasEliminar.Count > 0)
        {
            Debug.Log($"<color=red><b>[HORNO LIMPIEZA]:</b> Sensor reporta vacío con plataforma fuera. Destruyendo {piezasEliminar.Count} pieza(s) de la plataforma del horno.</color>");
            foreach (GameObject pieza in piezasEliminar)
            {
                Destroy(pieza);
            }
        }
    }

    private IEnumerator MoverObjeto(Transform objeto, Vector3 destino, float duracion)
    {
        Vector3 inicio = objeto.position;
        float tiempo = 0f;

        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            float t = tiempo / duracion;

            t = Mathf.SmoothStep(0f, 1f, t);

            objeto.position = Vector3.Lerp(inicio, destino, t);
            yield return null;
        }
        objeto.position = destino;
    }

    // --- EL GIZMO VISUAL ---
    void OnDrawGizmosSelected()
    {
        Transform plataformaReal = BuscarPlataformaRealHijo();
        if (plataformaReal == null) return;

        // 1. Dibujar el volumen de peligro en amarillo translúcido
        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.15f); // Amarillo suave y transparente
        Gizmos.DrawSphere(plataformaReal.position, distanciaLimiteVGR);

        // 2. Dibujar la silueta exterior en amarillo sólido
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(plataformaReal.position, distanciaLimiteVGR);

        // 3. Trazar línea de estado hacia el VGR
        ControladorVGR_mqtt vgr = Object.FindFirstObjectByType<ControladorVGR_mqtt>();
        if (vgr != null)
        {
            float distanciaActual = Vector3.Distance(vgr.transform.position, plataformaReal.position);

            if (vgr.ObtenerPiezaEnganchada() != null)
            {
                // Rojo si está cerca con pieza, Celeste si está lejos con pieza
                Gizmos.color = (distanciaActual < distanciaLimiteVGR) ? Color.red : Color.cyan;
            }
            else
            {
                // Gris si no tiene pieza
                Gizmos.color = Color.gray;
            }

            // Dibujar línea conectando ambos puntos
            Gizmos.DrawLine(plataformaReal.position, vgr.transform.position);

            // Una esfera pequeña en el VGR
            Gizmos.DrawSphere(vgr.transform.position, 0.008f);
        }
    }
}