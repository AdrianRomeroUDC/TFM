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

    private GameObject piezaActual;
    private GameObject piezaManoDSO;
    private Queue<string> colaDeOrdenes = new Queue<string>();
    private bool gripActivo = false;

    void Start()
    {
        StartCoroutine(IntentarSuscripcionSegura());
    }

    IEnumerator IntentarSuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;

        MQTTClient.Instance.OnDPSPiezaDSIEvent += ActualizarPiece;
        MQTTClient.Instance.OnDPSPiezaDSOEvent += ActualizarPiezaDSO;
        MQTTClient.Instance.OnDPSColorEvent += ActualizarColor;
        MQTTClient.Instance.OnVGRGripEvent += ActualizarGrip;

        Debug.Log("<color=green><b>DPS:</b> Suscripción completada con éxito.</color>");
    }

    private void OnDisable()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnDPSPiezaDSIEvent -= ActualizarPiece;
            MQTTClient.Instance.OnDPSPiezaDSOEvent -= ActualizarPiezaDSO;
            MQTTClient.Instance.OnDPSColorEvent -= ActualizarColor;
            MQTTClient.Instance.OnVGRGripEvent -= ActualizarGrip;
        }
    }

    private void ActualizarPiece(bool detectada)
    {
        lock (colaDeOrdenes)
        {
            if (detectada) colaDeOrdenes.Enqueue("SPAWN_BASE");
            else if (!gripActivo) colaDeOrdenes.Enqueue("DELETE");
        }
    }

    private void ActualizarPiezaDSO(bool detectada)
    {
        lock (colaDeOrdenes)
        {
            if (detectada) colaDeOrdenes.Enqueue("SPAWN_MANO_DSO");
            else colaDeOrdenes.Enqueue("DELETE_MANO_DSO");
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
            case "SPAWN_BASE":
                if (plataformaDSI == null)
                {
                    Debug.LogWarning("[DPS] No se ha asignado la referencia de la plataformaDSI en el Inspector.");
                    break;
                }

                if (piezaActual != null) Destroy(piezaActual);

                // 1. OBTENER CENTRO GEOMÉTRICO REAL DE LA PLATAFORMA DSI
                Collider colliderPlatDSI = plataformaDSI.GetComponent<Collider>();
                Vector3 centroPlatDSIMundo = (colliderPlatDSI != null) ? colliderPlatDSI.bounds.center : plataformaDSI.position;

                // 2. INSTANCIACIÓN Y ROTACIÓN INITIAL
                piezaActual = Instantiate(prefabBaseGris);
                piezaActual.name = "pieza_base";
                piezaActual.transform.rotation = plataformaDSI.rotation * Quaternion.Euler(-90f, 0f, 0f);

                // 3. DESFASE DE PIVOTE CAD
                BoxCollider colliderPiezaDSI = piezaActual.GetComponentInChildren<BoxCollider>();
                Vector3 centroPiezaLocalDSI = (colliderPiezaDSI != null) ? colliderPiezaDSI.center : Vector3.zero;

                centroPiezaLocalDSI.z = 0f;
                Vector3 offsetMundoPiezaDSI = piezaActual.transform.TransformDirection(centroPiezaLocalDSI);

                // 4. POSICIONAMIENTO MATEMÁTICO
                Vector3 posFinalDSI = centroPlatDSIMundo - offsetMundoPiezaDSI;
                posFinalDSI += piezaActual.transform.up * offsetAlturaDSO;
                piezaActual.transform.position = posFinalDSI;

                // 5. EMPARENTADO Y AJUSTE LOCAL EXACTO EN Y / Z (SOLO DSI)
                piezaActual.transform.SetParent(plataformaDSI, true);
                Vector3 posLocalLimpiaDSI = piezaActual.transform.localPosition;
                posLocalLimpiaDSI.y = offsetYDSI; // <--- FORZAMOS TU VALOR EXACTO EN Y (0.000572)
                posLocalLimpiaDSI.z = 0f;            // <--- CERO ABSOLUTO EN Z LOCAL
                piezaActual.transform.localPosition = posLocalLimpiaDSI;

                ConfigurarFisicas(piezaActual, "pieza_base");
                Debug.Log("<color=green><b>[GEMELO DIGITAL]:</b> Pieza DSI generada en el centro optimizado (Y=" + offsetYDSI + ", Z=0).</color>");
                break;

            case "SPAWN_MANO_DSO":
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

                if (!yaHayPieza && piezaManoDSO == null)
                {
                    piezaManoDSO = Instantiate(prefabBaseGris);
                    piezaManoDSO.name = "pieza_base_manual";
                    piezaManoDSO.transform.rotation = plataformaDSO.rotation * Quaternion.Euler(-90f, 0f, 0f);

                    BoxCollider colliderPiezaDSO = piezaManoDSO.GetComponentInChildren<BoxCollider>();
                    Vector3 centroPiezaLocalDSO = (colliderPiezaDSO != null) ? colliderPiezaDSO.center : Vector3.zero;
                    centroPiezaLocalDSO.z = 0f;

                    Vector3 offsetMundoPiezaDSO = piezaManoDSO.transform.TransformDirection(centroPiezaLocalDSO);
                    Vector3 posicionFinalMundoDSO = centroPlatDSOMundo - offsetMundoPiezaDSO;
                    posicionFinalMundoDSO += piezaManoDSO.transform.up * offsetAlturaDSO;
                    piezaManoDSO.transform.position = posicionFinalMundoDSO;

                    // RESTAURADO: Emparentado y limpieza en Z Local únicamente (Dejamos Y como estaba originalmente)
                    piezaManoDSO.transform.SetParent(plataformaDSO, true);
                    Vector3 posLocalLimpiaDSO = piezaManoDSO.transform.localPosition;
                    posLocalLimpiaDSO.z = 0f; // <--- CERO ABSOLUTO EN Z LOCAL sin sobreescribir la Y
                    piezaManoDSO.transform.localPosition = posLocalLimpiaDSO;

                    Rigidbody rb = piezaManoDSO.GetComponent<Rigidbody>();
                    if (rb == null) rb = piezaManoDSO.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    rb.useGravity = false;

                    Debug.Log("<color=cyan><b>[GEMELO DIGITAL]:</b> Pieza manual DSO generada (Y original preservada, Z=0).</color>");
                }
                break;

            case "DELETE_MANO_DSO":
                if (piezaManoDSO != null)
                {
                    Destroy(piezaManoDSO);
                    piezaManoDSO = null;
                }
                break;

            case "DELETE":
                if (piezaActual != null && !gripActivo && piezaActual.transform.parent == plataformaDSI)
                {
                    Destroy(piezaActual);
                    piezaActual = null;
                }
                break;

            case "DROP":
                if (piezaActual != null)
                {
                    if (piezaActual.transform.parent != null &&
                        (piezaActual.transform.parent.name.ToLower().Contains("cajon") ||
                         piezaActual.transform.parent.name.ToLower().Contains("container") ||
                         piezaActual.transform.parent.GetComponent<ContenedorHBW_proxy>() != null))
                    {
                        Debug.Log("<color=cyan><b>[DPS PROTECCIÓN]:</b> Drop ignorado. La pieza ya pertenece al contenedor HBW.</color>");
                        piezaActual = null;
                    }
                    else
                    {
                        piezaActual.transform.SetParent(null);

                        BoxCollider[] colliders = piezaActual.GetComponentsInChildren<BoxCollider>();
                        foreach (BoxCollider col in colliders)
                        {
                            if (col != null) col.isTrigger = false;
                        }

                        Rigidbody rb = piezaActual.GetComponent<Rigidbody>();
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
        if (piezaActual == null) return;

        GameObject prefabDestino = (color == "WHITE") ? prefabBlanco : (color == "RED") ? prefabRojo : prefabAzul;
        if (prefabDestino == null) return;

        Vector3 posicionVieja = piezaActual.transform.position;
        Quaternion rotacionVieja = piezaActual.transform.rotation;
        Transform padreViejo = piezaActual.transform.parent;

        GameObject piezaNueva = Instantiate(prefabDestino, posicionVieja, rotacionVieja);

        if (padreViejo != null)
        {
            piezaNueva.transform.SetParent(padreViejo);

            // Solo si el padre es la plataforma DSI, forzamos su Y exacta y su Z en 0
            if (padreViejo == plataformaDSI)
            {
                Vector3 lPos = piezaNueva.transform.localPosition;
                lPos.y = offsetYDSI; // <--- Mantiene el valor exacto en Y solo para DSI
                lPos.z = 0f;              // <--- Mantiene Z en cero
                piezaNueva.transform.localPosition = lPos;
            }
            // Si es cualquier otro padre (como la pinza o el DSO al cambiar de color), dejamos que conserve su posición relativa natural
        }

        ConfigurarFisicas(piezaNueva, "pieza_" + color.ToLower());

        Destroy(piezaActual);
        piezaActual = piezaNueva;

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