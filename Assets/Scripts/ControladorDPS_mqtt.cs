using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ControladorDPS_mqtt : MonoBehaviour
{
    [Header("Referencias")]
    public Transform puntoEntrada;
    public Transform pinzaVGR;
    [Tooltip("Arrastra aquí el objeto de la plataforma DSO")]
    public Transform plataformaDSO;

    // Configuración fija y oculta del Inspector (Privada)
    private const float offsetFlechaVerde = 0.02f;

    [Header("Prefabs Visuales")]
    public GameObject prefabBaseGris;
    public GameObject prefabBlanco;
    public GameObject prefabRojo;
    public GameObject prefabAzul;

    private GameObject piezaActual;
    private GameObject piezaManoDSO; // Para controlar la pieza que se pone a mano
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
                if (piezaActual != null) Destroy(piezaActual);
                piezaActual = Instantiate(prefabBaseGris, puntoEntrada.position, puntoEntrada.rotation);
                ConfigurarFisicas(piezaActual, "pieza_base");
                break;

            case "SPAWN_MANO_DSO":
                if (plataformaDSO == null)
                {
                    Debug.LogWarning("[DPS] No se ha asignado la referencia de la plataformaDSO en el Inspector.");
                    break;
                }

                // OBTENER CENTRO GEOMÉTRICO REAL DE LA PLATAFORMA (Usa el BoxCollider)
                Collider colliderPlat = plataformaDSO.GetComponent<Collider>();
                Vector3 centroPlatMundo = (colliderPlat != null) ? colliderPlat.bounds.center : plataformaDSO.position;

                // ESCUDO ANTIDUPLICADOS
                bool yaHayPieza = false;
                Collider[] collidersCercanos = Physics.OverlapSphere(centroPlatMundo, 0.05f);
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
                    // 1. Instanciamos la pieza libre en el mundo
                    piezaManoDSO = Instantiate(prefabBaseGris);
                    piezaManoDSO.name = "pieza_base_manual";

                    // 2. ROTACIÓN: Copiamos la de la plataforma y sumamos -90º locales en X
                    piezaManoDSO.transform.rotation = plataformaDSO.rotation * Quaternion.Euler(-90f, 0f, 0f);

                    // 3. NEUTRALIZACIÓN DE PIVOTE CAD: Ajustamos según el BoxCollider centrado
                    BoxCollider colliderPieza = piezaManoDSO.GetComponentInChildren<BoxCollider>();
                    Vector3 centroPiezaLocal = (colliderPieza != null) ? colliderPieza.center : Vector3.zero;

                    // CAMBIO: Forzamos el eje Z local de la pieza a 0 para que no sufra desvíos en esa dirección
                    centroPiezaLocal.z = 0f;

                    // Convertimos el desfase local modificado a dirección de mundo
                    Vector3 offsetMundoPieza = piezaManoDSO.transform.TransformDirection(centroPiezaLocal);

                    // 4. POSICIONAMIENTO MATEMÁTICO:
                    Vector3 posicionFinalMundo = centroPlatMundo - offsetMundoPieza;

                    // Desplazamos la pieza en su flecha verde (eje Y local) de forma fija (0.02)
                    posicionFinalMundo += piezaManoDSO.transform.up * offsetFlechaVerde;

                    piezaManoDSO.transform.position = posicionFinalMundo;

                    // 5. EMPARENTADO (Manteniendo la posición de mundo actual)
                    piezaManoDSO.transform.SetParent(plataformaDSO, true);

                    // SOLUCIÓN: Forzamos el eje Z local exacto a 0 tras el emparentado 
                    // Esto limpia cualquier residuo de precisión matemática (como el -0.0001718)
                    Vector3 posLocalLimpia = piezaManoDSO.transform.localPosition;
                    posLocalLimpia.z = 0f;
                    piezaManoDSO.transform.localPosition = posLocalLimpia;

                    // Congelamos las físicas
                    Rigidbody rb = piezaManoDSO.GetComponent<Rigidbody>();
                    if (rb == null) rb = piezaManoDSO.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    rb.useGravity = false;

                    Debug.Log("<color=cyan><b>[GEMELO DIGITAL]:</b> Pieza manual emparentada y forzada a Z local = 0 absoluto.</color>");
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
                if (piezaActual != null && !gripActivo && piezaActual.transform.parent == null)
                {
                    Destroy(piezaActual);
                }
                piezaActual = null;
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
        }

        ConfigurarFisicas(piezaNueva, "pieza_" + color.ToLower());

        Destroy(piezaActual);
        piezaActual = piezaNueva;

        Debug.Log($"<color=lime><b>[DPS Sustitución]:</b> Pieza directa '{piezaNueva.name}' sustituida con éxito.</color>");
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