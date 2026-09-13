using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controla el gemelo digital del horno de la estación MPO (Multi-Processing Oven): la puerta que
/// abre/cierra, la luz interior, y la pequeña plataforma que entra y sale del horno llevando la pieza
/// a hornear. Se suscribe a <see cref="MQTTClient.OnHornoUpdateEvent"/> para recibir en tiempo real
/// las órdenes del PLC (autómata) real y mueve los objetos 3D exactamente igual que la máquina física.
/// También expone la propiedad pública <see cref="SensorHornoActivo"/> y el método público
/// <see cref="BuscarPlataformaRealHijo"/>, que usan otros scripts (como
/// <see cref="ControladorVGR_mqtt"/> y <see cref="ControladorBrazoMPO"/>) para saber si el sensor
/// real del horno detecta una pieza y para encontrar el objeto exacto de la plataforma, de modo que
/// puedan comprobar si sus propios agarres/entregas de piezas han funcionado de verdad en la fábrica
/// física. Por último, incluye un sistema de "auto-sanación": si el sensor real del horno detecta una
/// pieza pero en Unity no hay ninguna (por ejemplo, porque la pieza llegó sin pasar por el gemelo
/// digital del VGR o del brazo), crea una pieza de repuesto para que la escena no se quede vacía; y al
/// revés, si el sensor deja de detectar nada, destruye las piezas virtuales que hayan quedado en el horno.
/// </summary>
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

    [Header("Filtro Posicional (Brazo MPO)")]
    [Tooltip("Margen de error permisible (en metros) para dictaminar si el brazo MPO se encuentra físicamente sobre el horno.")]
    public float margenErrorZ = 0.005f;

    [Header("Posiciones (Usar clic derecho para capturar)")]
    [ContextMenuItem("Capturar Cerrada", "CapturarPuertaCerrada")] public Vector3 posPuertaCerrada;
    [ContextMenuItem("Capturar Abierta", "CapturarPuertaAbierta")] public Vector3 posPuertaAbierta;
    [ContextMenuItem("Capturar Fuera", "CapturarPlataformaFuera")] public Vector3 posPlataformaFuera;
    [ContextMenuItem("Capturar Dentro", "CapturarPlataformaDentro")] public Vector3 posPlataformaDentro;

    [Header("Configuración de Velocidad (Duración en segundos)")]
    public float duracionMovimientoPuerta = 1.5f;
    public float duracionMovimientoPlataforma = 1.0f;

    // 🌐 LECTURA PÚBLICA DEL SENSOR EN TIEMPO REAL PARA EL BRAZO MPO
    // Refleja en todo momento si el sensor real del horno (ovenSensor) detecta una pieza dentro.
    // Otros scripts (VGR, Brazo MPO) consultan esta propiedad para confirmar si sus entregas/agarres
    // de piezas han funcionado de verdad en la máquina física, sin tener que suscribirse ellos mismos
    // al evento MQTT del horno.
    public bool SensorHornoActivo { get; private set; } = false;

    // Cola de mensajes del horno pendientes de procesar (se llenan desde el evento MQTT y se vacían en Update()).
    private Queue<MPOHornoPayload> colaMensajes = new Queue<MPOHornoPayload>();
    private Coroutine movimientoPuerta;
    private Coroutine movimientoPlataforma;

    // Botones de calibración del Inspector: guardan la posición actual del modelo 3D como referencia
    // para la puerta abierta/cerrada y la plataforma dentro/fuera del horno.
    void CapturarPuertaCerrada() => posPuertaCerrada = puerta.position;
    void CapturarPuertaAbierta() => posPuertaAbierta = puerta.position;
    void CapturarPlataformaFuera() => posPlataformaFuera = plataformaPieza.position;
    void CapturarPlataformaDentro() => posPlataformaDentro = plataformaPieza.position;

    private void Start() => StartCoroutine(SuscripcionSegura());

    // Espera a que MQTTClient exista en la escena y entonces se suscribe a su evento del horno.
    // En vez de procesar el mensaje al vuelo, lo mete en una cola para tratarlo con calma en Update(),
    // ya en el hilo principal de Unity.
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
        // Vaciamos la cola de mensajes pendientes del horno, procesándolos uno a uno y en orden.
        lock (colaMensajes)
        {
            while (colaMensajes.Count > 0)
            {
                ProcesarHorno(colaMensajes.Dequeue());
            }
        }
    }

    /// <summary>
    /// Aplica al gemelo digital un mensaje del PLC real del horno: actualiza el sensor de presencia,
    /// enciende/apaga la luz, mueve la puerta y la plataforma hacia su posición objetivo, y decide si
    /// hay que crear o destruir una pieza virtual dentro del horno según lo que reporte el sensor real.
    /// </summary>
    /// <param name="data">Datos recibidos por MQTT con el estado de puertas, luces, plataforma y sensor del horno.</param>
    private void ProcesarHorno(MPOHornoPayload data)
    {
        // Actualizamos la variable de estado pública para consulta externa del Brazo MPO
        SensorHornoActivo = (data.ovenSensor == 1);

        if (luzHorno) luzHorno.enabled = (data.lights == 1);

        // MOVIMIENTO PUERTA
        // Si el PLC ordena abrir o cerrar la puerta, cancelamos cualquier movimiento de puerta en
        // curso y lanzamos uno nuevo hacia la posición correspondiente (abierta o cerrada).
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
        // Lo mismo que con la puerta, pero para la plataforma que entra (Ref5) o sale (Ref6) del horno.
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

        // SENSOR DEL HORNO (Gestión de presencia con filtro de Z y X del Brazo MPO)
        // Si el sensor real detecta algo, comprobamos primero que no sea el propio brazo MPO tapando
        // la barrera de luz (falso positivo); si es una detección real de pieza, intentamos crear la
        // pieza de respaldo. Si el sensor deja de detectar nada, limpiamos las piezas virtuales.
        if (data.ovenSensor == 1)
        {
            if (EsElBrazoEnElHorno())
            {
                Debug.Log("<color=yellow><b>[HORNO]:</b> Sensor activo pero IGNORADO de forma segura. El brazo MPO está abajo obstruyendo físicamente la barrera de luz.</color>");
            }
            else
            {
                IntentarSpawnPiezaHorno();
            }
        }
        else if (data.ovenSensor == 0)
        {
            IntentarLimpiezaPiezaHorno();
        }
    }

    // Comprueba si el propio brazo interno del MPO está en este instante posicionado justo encima
    // del horno y bajado, para no confundir su presencia física con la de una pieza real (evita que
    // el sensor del horno dé un falso positivo mientras el brazo tapa la barrera de luz).
    private bool EsElBrazoEnElHorno()
    {
        ControladorBrazoMPO brazoMPO = Object.FindFirstObjectByType<ControladorBrazoMPO>();
        if (brazoMPO == null || brazoMPO.ejeHorizontal == null) return false;

        float distanciaZ = Mathf.Abs(brazoMPO.ejeHorizontal.localPosition.z - brazoMPO.zHorno);
        bool estaEnZHorno = distanciaZ < margenErrorZ;

        bool estaAbajo = false;
        if (brazoMPO.ejeVertical != null)
        {
            float desvioXReposo = Mathf.Abs(brazoMPO.ejeVertical.localPosition.x - brazoMPO.xReposo);
            estaAbajo = desvioXReposo > 0.002f;
        }

        return estaEnZHorno && estaAbajo;
    }

    /// <summary>
    /// Busca y devuelve el objeto exacto de la plataforma real del horno (el hijo cuyo nombre
    /// contiene la palabra "plataforma"), partiendo del objeto raíz asignado en el Inspector. Otros
    /// scripts (como <see cref="ControladorVGR_mqtt"/> y <see cref="ControladorBrazoMPO"/>) usan este
    /// método público para saber exactamente dónde debe quedar posicionada una pieza dentro del horno.
    /// </summary>
    /// <returns>El transform de la plataforma real del horno, o el objeto raíz si no se encuentra un hijo más específico.</returns>
    public Transform BuscarPlataformaRealHijo()
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

    // Sistema de "auto-sanación": si el sensor real del horno detecta una pieza pero en Unity no hay
    // ninguna pieza virtual ahí (por ejemplo porque llegó sin pasar por el VGR o el brazo del MPO),
    // creamos una pieza de repuesto (gris) para que el gemelo digital no se quede vacío mientras la
    // máquina real sí tiene una pieza dentro.
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

        // Si el VGR está sujetando una pieza y está muy cerca del horno, es que la entrega la va a
        // hacer él mismo en cualquier momento: cancelamos el spawn de respaldo para no duplicar la pieza.
        ControladorVGR_mqtt vgr = Object.FindFirstObjectByType<ControladorVGR_mqtt>();
        if (vgr != null && vgr.ObtenerPiezaEnganchada() != null)
        {
            float distanciaAlHorno = Vector3.Distance(vgr.transform.position, plataformaReal.position);

            if (distanciaAlHorno < distanciaLimiteVGR)
            {
                Debug.Log($"<color=yellow><b>[HORNO SPAWN]:</b> El VGR tiene una pieza sujeta y está CERCA del horno ({distanciaAlHorno:F3}m < {distanciaLimiteVGR}m). Se cancela el Spawn de respaldo.</color>");
                return;
            }
        }

        Collider colPlat = plataformaReal.GetComponent<Collider>();
        Vector3 centroPlatMundo = (colPlat != null) ? colPlat.bounds.center : plataformaReal.position;

        // Comprobamos con una esfera de físicas si ya hay una pieza virtual cerca de la plataforma,
        // para no crear una segunda pieza duplicada encima de una que ya existe.
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
            // Creamos la pieza de repuesto, la colocamos como hija de la plataforma real, en la
            // posición y rotación calibradas exactas, y la dejamos cinemática (sin gravedad) para
            // que se quede fija dentro del horno como la pieza física real.
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

    // Cuando el sensor real deja de detectar pieza y la plataforma ya está fuera del horno, borramos
    // cualquier pieza virtual que hubiera quedado dentro, para que el gemelo digital no muestre una
    // pieza fantasma que ya no existe en la máquina real.
    private void IntentarLimpiezaPiezaHorno()
    {
        if (plataformaPieza == null) return;

        if (posPlataformaFuera == Vector3.zero) return;

        float distanciaAFuera = Vector3.Distance(plataformaPieza.position, posPlataformaFuera);
        if (distanciaAFuera > 0.01f) return;

        Transform plataformaReal = BuscarPlataformaRealHijo();
        if (plataformaReal == null) return;

        List<GameObject> piezasEliminar = new List<GameObject>();
        foreach (Transform hijo in plataformaReal)
        {
            string nombreHijo = hijo.name.ToLower();
            if (nombreHijo.Contains("pieza") || hijo.CompareTag("Pieza"))
            {
                piezasEliminar.Add(hijo.gameObject);
            }
        }

        if (piezasEliminar.Count > 0)
        {
            Debug.Log($"<color=red><b>[HORNO LIMPIEZA]:</b> Sensor reporta vacío con plataforma fuera. Destruyendo {piezasEliminar.Count} pieza(s) de la plataforma del horno.</color>");
            foreach (GameObject pieza in piezasEliminar)
            {
                Destroy(pieza);
            }
        }
    }

    // Corrutina genérica de movimiento suave: desplaza un objeto desde su posición actual hasta
    // "destino" a lo largo de "duracion" segundos, usando un SmoothStep para que arranque y frene
    // con suavidad (como haría un mecanismo real, no un salto brusco).
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

    // Ayuda visual solo para el editor de Unity (al seleccionar este objeto): dibuja la esfera de
    // seguridad alrededor de la plataforma del horno y una línea hacia el VGR, coloreada según si
    // el VGR lleva una pieza y está dentro o fuera de la distancia límite de seguridad.
    void OnDrawGizmosSelected()
    {
        Transform plataformaReal = BuscarPlataformaRealHijo();
        if (plataformaReal == null) return;

        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.15f);
        Gizmos.DrawSphere(plataformaReal.position, distanciaLimiteVGR);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(plataformaReal.position, distanciaLimiteVGR);

        ControladorVGR_mqtt vgr = Object.FindFirstObjectByType<ControladorVGR_mqtt>();
        if (vgr != null)
        {
            float distanciaActual = Vector3.Distance(vgr.transform.position, plataformaReal.position);
            Gizmos.color = (vgr.ObtenerPiezaEnganchada() != null) ?
                ((distanciaActual < distanciaLimiteVGR) ? Color.red : Color.cyan) : Color.gray;

            Gizmos.DrawLine(plataformaReal.position, vgr.transform.position);
            Gizmos.DrawSphere(vgr.transform.position, 0.008f);
        }
    }
}
