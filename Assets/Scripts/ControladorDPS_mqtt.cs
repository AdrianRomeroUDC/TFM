using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ControladorDPS_mqtt : MonoBehaviour
{
    [Header("Referencias Plataformas")]
    [Tooltip("Arrastra aquí el objeto de la plataforma DSI")]
    public Transform plataformaDSI;
    [Tooltip("Arrastra aquí el objeto de la plataforma DSO")]
    public Transform plataformaDSO;

    [Header("Referencias Pinza")]
    public Transform pinzaVGR;

    // Configuración fija y oculta del Inspector (Privada)
    private const float offsetAlturaDSO = 0.02f;
    private const float offsetYDSI = 0.000572f; // <--- Solo para la plataforma DSI

    [Header("Prefabs Visuales")]
    public GameObject prefabBaseGris;
    public GameObject prefabBlanco;
    public GameObject prefabRojo;
    public GameObject prefabAzul;

    private GameObject piezaDPS;
    private GameObject piezaDSO;
    private Queue<string> colaDeOrdenes = new Queue<string>();
    private bool gripActivo = false;

    void Start()
    {
        StartCoroutine(IntentarSuscripcionSegura());
    }

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
            MQTTClient.Instance.OnDPSPiezaDSOEvent -= ActualizarPiezaDSO; // <--- CORREGIDO: Ya no da error de contexto
            MQTTClient.Instance.OnDPSColorEvent -= ActualizarColor;
            MQTTClient.Instance.OnVGRGripEvent -= ActualizarGrip;
        }
    }

    private void ActualizarPiezaDSI(bool detectada)
    {
        lock (colaDeOrdenes)
        {
            if (detectada) colaDeOrdenes.Enqueue("SPAWN_DSI");
            else if (!gripActivo) colaDeOrdenes.Enqueue("DELETE_DSI");
        }
    }

    private void ActualizarPiezaDSO(bool detectada)
    {
        lock (colaDeOrdenes)
        {
            if (detectada) colaDeOrdenes.Enqueue("SPAWN_DSO");
            else colaDeOrdenes.Enqueue("DELETE_DSO");
        }
    }

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

    private void ActualizarGrip(bool activo)
    {
        gripActivo = activo;
        lock (colaDeOrdenes)
        {
            if (!activo) colaDeOrdenes.Enqueue("DROP");
        }
    }

    void Update()
    {
        string ordenActual = null;
        lock (colaDeOrdenes)
        {
            if (colaDeOrdenes.Count > 0) ordenActual = colaDeOrdenes.Dequeue();
        }

        if (ordenActual != null) EjecutarOrden(ordenActual);
    }

    void EjecutarOrden(string orden)
    {
        switch (orden)
        {
            case "SPAWN_DSI":
                if (plataformaDSI == null)
                {
                    Debug.LogWarning("[DPS] No se ha asignado la referencia de la plataformaDSI en el Inspector.");
                    break;
                }

                if (piezaDPS != null) Destroy(piezaDPS);

                // 1. OBTENER CENTRO GEOMÉTRICO REAL DE LA PLATAFORMA DSI
                Collider colliderPlatDSI = plataformaDSI.GetComponent<Collider>();
                Vector3 centroPlatDSIMundo = (colliderPlatDSI != null) ? colliderPlatDSI.bounds.center : plataformaDSI.position;

                // 2. INSTANCIACIÓN Y ROTACIÓN INITIAL
                piezaDPS = Instantiate(prefabBaseGris);
                piezaDPS.name = "pieza_base_dsi";
                piezaDPS.transform.rotation = plataformaDSI.rotation * Quaternion.Euler(-90f, 0f, 0f);

                // 3. DESFASE DE PIVOTE CAD
                BoxCollider colliderPiezaDSI = piezaDPS.GetComponentInChildren<BoxCollider>();
                Vector3 centroPiezaLocalDSI = (colliderPiezaDSI != null) ? colliderPiezaDSI.center : Vector3.zero;

                centroPiezaLocalDSI.z = 0f;
                Vector3 offsetMundoPiezaDSI = piezaDPS.transform.TransformDirection(centroPiezaLocalDSI);

                // 4. POSICIONAMIENTO MATEMÁTICO
                Vector3 posFinalDSI = centroPlatDSIMundo - offsetMundoPiezaDSI;
                posFinalDSI += piezaDPS.transform.up * offsetAlturaDSO;
                piezaDPS.transform.position = posFinalDSI;

                // 5. EMPARENTADO Y AJUSTE LOCAL EXACTO EN Y / Z (SOLO DSI)
                piezaDPS.transform.SetParent(plataformaDSI, true);
                Vector3 posLocalLimpiaDSI = piezaDPS.transform.localPosition;
                posLocalLimpiaDSI.y = offsetYDSI;
                posLocalLimpiaDSI.z = 0f;
                piezaDPS.transform.localPosition = posLocalLimpiaDSI;

                ConfigurarFisicas(piezaDPS, "pieza_base_dsi");
                Debug.Log("<color=green><b>[GEMELO DIGITAL]:</b> Pieza DSI generada en el centro optimizado (Y=" + offsetYDSI + ", Z=0).</color>");
                break;

            case "SPAWN_DSO":
                if (plataformaDSO == null)
                {
                    Debug.LogWarning("[DPS] No se ha asignado la referencia de la plataformaDSO en el Inspector.");
                    break;
                }

                Collider colliderPlatDSO = plataformaDSO.GetComponent<Collider>();
                Vector3 centroPlatDSOMundo = (colliderPlatDSO != null) ? colliderPlatDSO.bounds.center : plataformaDSO.position;

                bool yaHayPieza = false;
                Collider[] collidersCercanos = Physics.OverlapSphere(centroPlatDSOMundo, 0.05f);
                foreach (Collider col in collidersCercanos)
                {
                    if (col.name.ToLower().Contains("pieza"))
                    {
                        yaHayPieza = true;
                        break;
                    }
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

                    Rigidbody rb = piezaDSO.GetComponent<Rigidbody>();
                    if (rb == null) rb = piezaDSO.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    rb.useGravity = false;

                    Debug.Log("<color=cyan><b>[GEMELO DIGITAL]:</b> Pieza DSO generada (Y original preservada, Z=0).</color>");
                }
                break;

            case "DELETE_DSO":
                if (piezaDSO != null)
                {
                    Destroy(piezaDSO);
                    piezaDSO = null;
                }

                if (plataformaDSO != null)
                {
                    Collider colPlatDSO = plataformaDSO.GetComponent<Collider>();
                    Vector3 centroDSO = (colPlatDSO != null) ? colPlatDSO.bounds.center : plataformaDSO.position;

                    Collider[] collidersContacto = Physics.OverlapSphere(centroDSO, 0.05f);
                    List<GameObject> objetosBorrar = new List<GameObject>();

                    foreach (Collider col in collidersContacto)
                    {
                        if (col.transform == plataformaDSO) continue;

                        Transform raizPieza = col.transform;
                        while (raizPieza.parent != null && raizPieza.parent != plataformaDSO && !raizPieza.parent.name.ToLower().Contains("plataforma"))
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
                        Debug.Log($"<color=yellow><b>[GEMELO DIGITAL]:</b> Se han limpiado {objetosBorrar.Count} pieza(s) detectada(s) en la plataforma DSO.</color>");
                    }
                }
                break;

            case "DELETE_DSI":
                if (piezaDPS != null && !gripActivo && piezaDPS.transform.parent == plataformaDSI)
                {
                    Destroy(piezaDPS);
                    piezaDPS = null;
                }
                break;

            // =======================================================================
            // UNICA MODIFICACIÓN: ESCUDO DE INTERCEPCIÓN EN EL DROP
            // =======================================================================
            case "DROP":
                if (piezaDPS != null)
                {
                    // Si la pieza ya NO es hija de la plataforma DSI (porque la lleva el VGR o ya entró al cajón)
                    // detenemos el DROP del DPS para que no rompa el parentesco del contenedor.
                    if (piezaDPS.transform.parent != plataformaDSI)
                    {
                        piezaDPS = null;
                        break;
                    }

                    if (piezaDPS.transform.parent != null &&
                        (piezaDPS.transform.parent.name.ToLower().Contains("cajon") ||
                         piezaDPS.transform.parent.name.ToLower().Contains("container") ||
                         piezaDPS.transform.parent.GetComponent<ContenedorHBW_proxy>() != null))
                    {
                        Debug.Log("<color=cyan><b>[DPS PROTECCIÓN]:</b> Drop ignorado. La pieza ya pertenece al contenedor HBW.</color>");
                        piezaDPS = null;
                    }
                    else
                    {
                        piezaDPS.transform.SetParent(null);

                        BoxCollider[] colliders = piezaDPS.GetComponentsInChildren<BoxCollider>();
                        foreach (BoxCollider col in colliders)
                        {
                            if (col != null) col.isTrigger = false;
                        }

                        Rigidbody rb = piezaDPS.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.isKinematic = false;
                            rb.useGravity = true;
                            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        }
                    }
                }
                break;

            case "WHITE":
            case "RED":
            case "BLUE":
                SustituirPorPrefabColor(orden);
                break;
        }
    }

    void SustituirPorPrefabColor(string color)
    {
        if (piezaDPS == null) return;

        GameObject prefabDestino = (color == "WHITE") ? prefabBlanco : (color == "RED") ? prefabRojo : prefabAzul;
        if (prefabDestino == null) return;

        Vector3 posicionVieja = piezaDPS.transform.position;
        Quaternion rotacionVieja = piezaDPS.transform.rotation;
        Transform padreViejo = piezaDPS.transform.parent;

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

        Destroy(piezaDPS);
        piezaDPS = piezaNueva;

        Debug.Log($"<color=lime><b>[DPS Sustitución]:</b> Pieza directa '{piezaNueva.name}' sustituida manteniendo alineación.</color>");
    }

    void ConfigurarFisicas(GameObject p, string nombreDestino)
    {
        p.name = nombreDestino;

        Rigidbody rb = p.GetComponent<Rigidbody>();
        if (rb == null) rb = p.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        BoxCollider[] colliders = p.GetComponentsInChildren<BoxCollider>();
        foreach (BoxCollider col in colliders)
        {
            if (col != null) col.isTrigger = gripActivo;
        }
    }
}