using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controla la estación DPS (Deposit &amp; Processing Station): la puerta de entrada y salida de
/// piezas de toda la fábrica. Gestiona el sensor de entrada DSI (donde aparece la pieza nueva que
/// meten en la fábrica y se detecta su color), el sensor de salida DSO (donde se deja la pieza ya
/// terminada para que la recojan) y el sensor de color. Expone las plataformas <see cref="plataformaDSI"/>
/// y <see cref="plataformaDSO"/> para que otros scripts (sobre todo <see cref="ControladorVGR_mqtt"/>)
/// sepan exactamente dónde coger o dejar piezas en esta estación. Todas las órdenes que llegan por
/// MQTT se meten en una cola y se van resolviendo una a una en <see cref="Update"/>, para no crear o
/// destruir piezas 3D fuera del hilo principal de Unity.
/// </summary>
public class ControladorDPS_mqtt : MonoBehaviour
{
    [Header("Referencias Plataformas")]
    [Tooltip("Arrastra aquí el objeto de la plataforma DSI")]
    public Transform plataformaDSI;
    [Tooltip("Arrastra aquí el objeto de la plataforma DSO")]
    public Transform plataformaDSO;

    [Header("Referencias Pinza")]
    [Tooltip("Arrastra aquí el objeto 'puntoAnclajeVentosa' del VGR")]
    public Transform pinzaVGR;

    private const float offsetAlturaDSO = 0.02f;
    private const float offsetYDSI = 0.000572f;

    [Header("Prefabs Visuales")]
    public GameObject prefabBaseGris;
    public GameObject prefabBlanco;
    public GameObject prefabRojo;
    public GameObject prefabAzul;

    private GameObject piezaDPS;
    private GameObject piezaDSO;
    private Queue<string> colaDeOrdenes = new Queue<string>();
    private bool gripActivo = false;

    // Guarda si el sensor real de la plataforma de salida (dso_sensor) detecta una pieza ahora mismo,
    // para que el VGR pueda comprobar si una entrega en DSO ha funcionado de verdad.
    private bool dsoSensorActivo = false;
    public bool DsoSensorActivo => dsoSensorActivo;

    void Start()
    {
        StartCoroutine(IntentarSuscripcionSegura());
    }

    // Espera a que el cliente MQTT exista antes de suscribirse a los sensores de la estación DPS.
    IEnumerator IntentarSuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;

        MQTTClient.Instance.OnDPSPiezaDSIEvent += ActualizarPiezaDSI;
        MQTTClient.Instance.OnDPSPiezaDSOEvent += ActualizarPiezaDSO;
        MQTTClient.Instance.OnDPSColorEvent += ActualizarColor;
        MQTTClient.Instance.OnVGRGripEvent += ActualizarGrip;

        Debug.Log("<color=green><b>DPS:</b> Suscripción completada con éxito.</color>");
    }

    private void OnDisable()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnDPSPiezaDSIEvent -= ActualizarPiezaDSI;
            MQTTClient.Instance.OnDPSPiezaDSOEvent -= ActualizarPiezaDSO;
            MQTTClient.Instance.OnDPSColorEvent -= ActualizarColor;
            MQTTClient.Instance.OnVGRGripEvent -= ActualizarGrip;
        }
    }

    // El sensor real de entrada (dsi_sensor) avisa aquí cuando detecta o deja de detectar una pieza:
    // si aparece una pieza nueva pedimos que se cree en 3D, y si desaparece (y la ventosa no la tiene
    // agarrada) pedimos que se borre, porque ha sido retirada manualmente de la plataforma real.
    private void ActualizarPiezaDSI(bool detectada)
    {
        lock (colaDeOrdenes)
        {
            if (detectada) colaDeOrdenes.Enqueue("SPAWN_DSI");
            else if (!gripActivo) colaDeOrdenes.Enqueue("DELETE_DSI");
        }
    }

    // El sensor real de salida (dso_sensor) avisa aquí cuando detecta o deja de detectar una pieza en
    // la plataforma de recogida: guardamos su estado y encolamos crear o borrar la pieza 3D correspondiente.
    private void ActualizarPiezaDSO(bool detectada)
    {
        dsoSensorActivo = detectada;
        lock (colaDeOrdenes)
        {
            if (detectada) colaDeOrdenes.Enqueue("SPAWN_DSO");
            else colaDeOrdenes.Enqueue("DELETE_DSO");
        }
    }

    // El sensor de color real identifica de qué color es la pieza que acaba de entrar por DSI, y
    // encolamos ese color para que la pieza gris genérica se sustituya por la pieza del color correcto.
    private void ActualizarColor(string color)
    {
        if (string.IsNullOrEmpty(color)) return;
        string colorLimpio = color.Trim().ToUpper();

        lock (colaDeOrdenes)
        {
            if (colorLimpio == "BLUE" || colorLimpio == "RED" || colorLimpio == "WHITE")
            {
                colaDeOrdenes.Enqueue(colorLimpio);
            }
        }
    }

    // Se entera de cuándo la ventosa del VGR suelta una pieza, para comprobar si esa pieza cae dentro
    // de alguna de las plataformas de esta estación.
    private void ActualizarGrip(bool activo)
    {
        gripActivo = activo;
        lock (colaDeOrdenes)
        {
            if (!activo) colaDeOrdenes.Enqueue("DROP");
        }
    }

    // Permite que otros scripts pidan vaciar la plataforma de salida DSO a mano (por ejemplo, al reiniciar la simulación).
    public void LimpiarDSO()
    {
        lock (colaDeOrdenes)
        {
            colaDeOrdenes.Enqueue("DELETE_DSO");
        }
    }

    void Update()
    {
        // Sacamos una orden de la cola (si hay alguna esperando) y la ejecutamos aquí, en el hilo principal de Unity.
        string ordenActual = null;
        lock (colaDeOrdenes)
        {
            if (colaDeOrdenes.Count > 0) ordenActual = colaDeOrdenes.Dequeue();
        }

        if (ordenActual != null) EjecutarOrden(ordenActual);
    }

    // Aplica de verdad cada tipo de orden que ha llegado desde los sensores reales de la estación DPS.
    void EjecutarOrden(string orden)
    {
        switch (orden)
        {
            case "SPAWN_DSI":
                // Crea la pieza gris genérica en la plataforma de entrada, tal y como el sensor DSI real
                // acaba de detectar que ha entrado una pieza nueva en la fábrica.
                if (plataformaDSI == null)
                {
                    Debug.LogWarning("[DPS] No se ha asignado la referencia de la plataformaDSI en el Inspector.");
                    break;
                }

                if (piezaDPS != null)
                {
                    if (piezaDPS.transform.parent != plataformaDSI)
                    {
                        // La pieza anterior ya no está en la plataforma DSI (probablemente el VGR se la ha llevado):
                        // simplemente dejamos de vigilarla, sin destruirla.
                        Debug.Log("<color=yellow><b>[DPS]:</b> Liberando puntero de la pieza anterior (está en el robot). No se destruirá.</color>");
                        piezaDPS = null;
                    }
                    else
                    {
                        Destroy(piezaDPS);
                    }
                }

                // Calculamos el centro real de la plataforma DSI en el mundo, usando su collider si existe.
                Collider colliderPlatDSI = plataformaDSI.GetComponent<Collider>();
                Vector3 centroPlatDSIMundo = (colliderPlatDSI != null) ? colliderPlatDSI.bounds.center : plataformaDSI.position;

                piezaDPS = Instantiate(prefabBaseGris);
                piezaDPS.name = "pieza_base_dsi";
                piezaDPS.transform.rotation = plataformaDSI.rotation * Quaternion.Euler(-90f, 0f, 0f);

                // Ajustamos la posición para que el centro de la pieza (según su propio collider) quede
                // exactamente sobre el centro de la plataforma, en vez de usar el origen del modelo 3D.
                BoxCollider colliderPiezaDSI = piezaDPS.GetComponentInChildren<BoxCollider>();
                Vector3 centroPiezaLocalDSI = (colliderPiezaDSI != null) ? colliderPiezaDSI.center : Vector3.zero;
                centroPiezaLocalDSI.z = 0f;
                Vector3 offsetMundoPiezaDSI = piezaDPS.transform.TransformDirection(centroPiezaLocalDSI);

                Vector3 posFinalDSI = centroPlatDSIMundo - offsetMundoPiezaDSI;
                posFinalDSI += piezaDPS.transform.up * offsetAlturaDSO;
                piezaDPS.transform.position = posFinalDSI;

                // Hacemos que la pieza sea hija de la plataforma y afinamos su posición local para dejarla perfectamente centrada.
                piezaDPS.transform.SetParent(plataformaDSI, true);
                Vector3 posLocalLimpiaDSI = piezaDPS.transform.localPosition;
                posLocalLimpiaDSI.y = offsetYDSI;
                posLocalLimpiaDSI.z = 0f;
                piezaDPS.transform.localPosition = posLocalLimpiaDSI;

                ConfigurarFisicas(piezaDPS, "pieza_base_dsi");
                Debug.Log("<color=green><b>[GEMELO DIGITAL]:</b> Pieza DSI generada de forma segura.</color>");
                break;

            case "SPAWN_DSO":
                // Crea la pieza gris genérica en la plataforma de salida, porque el sensor DSO real
                // acaba de detectar que ha llegado una pieza terminada para su recogida.
                if (plataformaDSO == null) { Debug.LogWarning("[DPS] Falta plataformaDSO."); break; }
                Collider colliderPlatDSO = plataformaDSO.GetComponent<Collider>();
                Vector3 centroPlatDSOMundo = (colliderPlatDSO != null) ? colliderPlatDSO.bounds.center : plataformaDSO.position;

                // Comprobamos con un radar de físicas si ya hay alguna pieza cerca, para no duplicarla.
                bool yaHayPieza = false;
                Collider[] collidersCercanos = Physics.OverlapSphere(centroPlatDSOMundo, 0.05f);
                foreach (Collider col in collidersCercanos)
                {
                    if (col.name.ToLower().Contains("pieza")) { yaHayPieza = true; break; }
                }

                if (!yaHayPieza && piezaDSO == null)
                {
                    piezaDSO = Instantiate(prefabBaseGris);
                    piezaDSO.name = "pieza_base_dso";
                    piezaDSO.transform.rotation = plataformaDSO.rotation * Quaternion.Euler(-90f, 0f, 0f);

                    BoxCollider colliderPiezaDSO = piezaDSO.GetComponentInChildren<BoxCollider>();
                    Vector3 centroPiezaLocalDSO = (colliderPiezaDSO != null) ? colliderPiezaDSO.center : Vector3.zero;
                    centroPiezaLocalDSO.z = 0f;

                    Vector3 offsetMundoPiezaDSO = piezaDSO.transform.TransformDirection(centroPiezaLocalDSO);
                    Vector3 posicionFinalMundoDSO = centroPlatDSOMundo - offsetMundoPiezaDSO;
                    posicionFinalMundoDSO += piezaDSO.transform.up * offsetAlturaDSO;
                    piezaDSO.transform.position = posicionFinalMundoDSO;

                    piezaDSO.transform.SetParent(plataformaDSO, true);
                    Vector3 posLocalLimpiaDSO = piezaDSO.transform.localPosition;
                    posLocalLimpiaDSO.z = 0f;
                    piezaDSO.transform.localPosition = posLocalLimpiaDSO;

                    // Dejamos la pieza fija (sin gravedad) porque está apoyada sobre la plataforma de salida, esperando a ser recogida.
                    Rigidbody rb = piezaDSO.GetComponent<Rigidbody>();
                    if (rb == null)
                    {
                        rb = piezaDSO.AddComponent<Rigidbody>();
                    }
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }
                break;

            case "DELETE_DSO":
                // El sensor DSO real ya no detecta ninguna pieza: borramos la pieza 3D de salida y, por
                // seguridad, también cualquier otra pieza que pudiera haber quedado pegada a la plataforma.
                if (piezaDSO != null)
                {
                    Destroy(piezaDSO);
                    piezaDSO = null;
                }

                if (plataformaDSO != null)
                {
                    Collider colPlatDSO = plataformaDSO.GetComponent<Collider>();
                    Vector3 centroDSO = (colPlatDSO != null) ? colPlatDSO.bounds.center : plataformaDSO.position;

                    // Radar de limpieza: buscamos cualquier pieza que siga tocando la plataforma de salida.
                    Collider[] collidersContacto = Physics.OverlapSphere(centroDSO, 0.06f);
                    List<GameObject> objetosBorrar = new List<GameObject>();

                    foreach (Collider col in collidersContacto)
                    {
                        if (col.transform == plataformaDSO) continue;

                        // Subimos por la jerarquía hasta encontrar el objeto raíz de la pieza (y no una pieza suya interna).
                        Transform raizPieza = col.transform;
                        while (raizPieza.parent != null &&
                               raizPieza.parent != plataformaDSO &&
                               !raizPieza.parent.name.ToLower().Contains("plataforma"))
                        {
                            raizPieza = raizPieza.parent;
                        }

                        if (raizPieza.name.ToLower().Contains("pieza"))
                        {
                            if (!objetosBorrar.Contains(raizPieza.gameObject))
                            {
                                objetosBorrar.Add(raizPieza.gameObject);
                            }
                        }
                    }

                    foreach (GameObject obj in objetosBorrar)
                    {
                        if (obj == piezaDPS) piezaDPS = null;
                        if (obj == piezaDSO) piezaDSO = null;

                        Destroy(obj);
                    }

                    if (objetosBorrar.Count > 0)
                    {
                        Debug.Log($"<color=yellow><b>[DPS LIMPIEZA DSO]:</b> Se eliminaron {objetosBorrar.Count} pieza(s) físicas mediante radar.</color>");
                    }
                }
                break;

            case "DELETE_DSI":
                // El sensor DSI real ya no ve la pieza (y la ventosa no la agarró): la pieza fue retirada
                // manualmente de la plataforma de entrada, así que borramos también su versión 3D.
                if (piezaDPS != null && !gripActivo && piezaDPS.transform.parent == plataformaDSI)
                {
                    Destroy(piezaDPS); piezaDPS = null;
                }
                break;

            case "DROP":
                // La ventosa del VGR acaba de soltar una pieza: decidimos qué hacer con ella según dónde haya caído.
                if (piezaDPS != null)
                {
                    if (piezaDPS.transform.parent == plataformaDSI) break;

                    if (piezaDPS.transform.parent == plataformaDSO)
                    {
                        // Ya la había colocado antes AlinearPiezaEnDSO con precisión: no hace falta simular la caída física.
                        Debug.Log("<color=cyan><b>[DPS]:</b> La pieza ya está acoplada y alineada en DSO. Se ignora caída física.</color>");
                        break;
                    }

                    if (piezaDPS.transform.parent != null &&
                        (piezaDPS.transform.parent.name.ToLower().Contains("cajon") ||
                         piezaDPS.transform.parent.name.ToLower().Contains("container")))
                    {
                        // La pieza ha quedado dentro de un cajón del HBW: dejamos de vigilarla desde la DPS.
                        piezaDPS = null;
                    }
                    else
                    {
                        // No ha caído en ningún sitio especial: la dejamos caer con física normal (gravedad).
                        piezaDPS.transform.SetParent(null);
                        BoxCollider[] colliders = piezaDPS.GetComponentsInChildren<BoxCollider>();
                        foreach (BoxCollider col in colliders) if (col != null) col.isTrigger = false;

                        Rigidbody rb = piezaDPS.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.isKinematic = false; rb.useGravity = true;
                            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        }
                    }
                }
                break;

            case "WHITE":
            case "RED":
            case "BLUE":
                // El sensor de color real ha identificado la pieza que acaba de entrar: sustituimos la pieza gris genérica por la del color correcto.
                SustituirPorPrefabColor(orden);
                break;
        }
    }

    // ==========================================
    // ALINEACIÓN MAGNÉTICA PERFECTA (Llamada por el Proxy de forma segura)
    // ==========================================
    /// <summary>
    /// Coloca una pieza que trae el VGR exactamente centrada y alineada sobre la plataforma de salida
    /// DSO, pero solo si el sensor real dso_sensor confirma que ahí hay de verdad una pieza física;
    /// si el sensor real no la detecta, se elimina la pieza del gemelo digital para no desincronizarse.
    /// </summary>
    public void AlinearPiezaEnDSO(Transform pieza)
    {
        if (plataformaDSO == null || pieza == null) return;

        // Comprobación clave: si el sensor real de la plataforma de salida no detecta nada, la entrega
        // ha fallado en la máquina física, así que eliminamos la pieza fantasma del gemelo digital.
        if (!dsoSensorActivo)
        {
            Debug.Log("<color=red><b>[DPS DSO]:</b> Pieza soltada en DSO pero dso_sensor = False (no hay pieza real). Eliminando pieza fantasma.</color>");
            Destroy(pieza.gameObject);
            if (pieza.gameObject == piezaDSO) piezaDSO = null;
            if (pieza.gameObject == piezaDPS) piezaDPS = null;
            return;
        }

        // 1. Soltamos la pieza de la ventosa del robot.
        pieza.SetParent(null);

        // 2. Aplicamos la rotación exacta que debe tener apoyada sobre la plataforma de salida.
        pieza.rotation = plataformaDSO.rotation * Quaternion.Euler(-90f, 0f, 0f);

        // 3. Calculamos su posición final usando los colliders reales de la plataforma y de la pieza.
        Collider colliderPlatDSO = plataformaDSO.GetComponent<Collider>();
        Vector3 centroPlatDSOMundo = (colliderPlatDSO != null) ? colliderPlatDSO.bounds.center : plataformaDSO.position;

        BoxCollider colliderPiezaDSO = pieza.GetComponentInChildren<BoxCollider>();
        Vector3 centroPiezaLocalDSO = (colliderPiezaDSO != null) ? colliderPiezaDSO.center : Vector3.zero;
        centroPiezaLocalDSO.z = 0f;

        Vector3 offsetMundoPiezaDSO = pieza.TransformDirection(centroPiezaLocalDSO);
        Vector3 posicionFinalMundoDSO = centroPlatDSOMundo - offsetMundoPiezaDSO;
        posicionFinalMundoDSO += pieza.up * offsetAlturaDSO;
        pieza.position = posicionFinalMundoDSO;

        // 4. La pieza pasa a depender de la plataforma de salida.
        pieza.SetParent(plataformaDSO, true);

        // 5. Afinamos el centrado local en el eje Z para que quede perfectamente alineada.
        Vector3 posLocalLimpiaDSO = pieza.localPosition;
        posLocalLimpiaDSO.z = 0f;
        pieza.localPosition = posLocalLimpiaDSO;

        // 6. Apagamos la física de la pieza: queda fijada del todo sobre la plataforma.
        Rigidbody rb = pieza.GetComponent<Rigidbody>();
        if (rb == null) rb = pieza.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        BoxCollider[] colliders = pieza.GetComponentsInChildren<BoxCollider>();
        foreach (BoxCollider col in colliders) if (col != null) col.isTrigger = false;

        // 7. Actualizamos las referencias internas para que el resto del script sepa que esta es ahora la pieza de la plataforma DSO.
        piezaDSO = pieza.gameObject;

        if (pieza.gameObject == piezaDPS)
        {
            piezaDPS = pieza.gameObject;
        }

        Debug.Log("<color=lime><b>[DPS SNAPPING]:</b> Pieza acoplada y alineada magnéticamente en DSO de forma perfecta.</color>");
    }

    // Busca la pieza gris genérica que lleva la ventosa del VGR (o la que está en la plataforma) y la
    // sustituye por la versión del color correcto que acaba de identificar el sensor de color real,
    // conservando su posición, rotación y quién la tiene agarrada en ese momento.
    void SustituirPorPrefabColor(string color)
    {
        GameObject piezaAColorar = null;

        // Buscamos entre los "hijos" de la ventosa del VGR si hay alguna pieza colgando de ella ahora mismo.
        if (pinzaVGR != null)
        {
            foreach (Transform hijo in pinzaVGR.GetComponentsInChildren<Transform>())
            {
                if (hijo.name.ToLower().Contains("pieza"))
                {
                    piezaAColorar = hijo.gameObject;
                    break;
                }
            }
        }

        if (piezaAColorar == null)
        {
            Debug.Log($"<color=orange><b>[DPS]:</b> Se recibió cambio de color '{color}' pero la ventosa del VGR está vacía. Comando descartado de forma segura.</color>");
            return;
        }

        GameObject prefabDestino = (color == "WHITE") ? prefabBlanco : (color == "RED") ? prefabRojo : prefabAzul;
        if (prefabDestino == null) return;

        // Guardamos la posición, rotación y quién la tenía agarrada antes de destruir la pieza gris.
        Vector3 posicionVieja = piezaAColorar.transform.position;
        Quaternion rotacionVieja = piezaAColorar.transform.rotation;
        Transform padreViejo = piezaAColorar.transform.parent;

        GameObject piezaNueva = Instantiate(prefabDestino, posicionVieja, rotacionVieja);

        if (padreViejo != null)
        {
            piezaNueva.transform.SetParent(padreViejo);

            if (padreViejo == plataformaDSI)
            {
                Vector3 lPos = piezaNueva.transform.localPosition;
                lPos.y = offsetYDSI;
                lPos.z = 0f;
                piezaNueva.transform.localPosition = lPos;
            }
        }

        ConfigurarFisicas(piezaNueva, "pieza_" + color.ToLower());

        if (piezaAColorar == piezaDPS)
        {
            piezaDPS = piezaNueva;
        }

        // Si la pieza vieja estaba agarrada por la ventosa del VGR, avisamos al VGR de que ahora
        // la pieza agarrada es esta nueva (con el color correcto), para que no pierda la referencia.
        if (pinzaVGR != null && (padreViejo == pinzaVGR || padreViejo.IsChildOf(pinzaVGR)))
        {
            ControladorVGR_mqtt vgrScript = pinzaVGR.GetComponentInParent<ControladorVGR_mqtt>();
            if (vgrScript == null)
            {
                vgrScript = Object.FindFirstObjectByType<ControladorVGR_mqtt>();
            }

            if (vgrScript != null)
            {
                vgrScript.AsignarPiezaEnganchada(piezaNueva.transform);
            }
        }

        Destroy(piezaAColorar);
        Debug.Log($"<color=lime><b>[DPS MATRICIAL]:</b> Cambio de color '{color}' aplicado con éxito al objetivo correcto.</color>");
    }

    // Deja preparada una pieza recién creada con su nombre, su Rigidbody cinemático (sin gravedad) y
    // sus colliders configurados como "trigger" solo si la ventosa la tiene agarrada en ese instante.
    void ConfigurarFisicas(GameObject p, string nombreDestino)
    {
        p.name = nombreDestino;

        Rigidbody rb = p.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = p.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;

        BoxCollider[] colliders = p.GetComponentsInChildren<BoxCollider>();
        foreach (BoxCollider col in colliders) if (col != null) col.isTrigger = gripActivo;
    }
}
