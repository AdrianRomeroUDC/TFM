using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class ControladorDPS_mqtt : MonoBehaviour
{
    [Header("Referencias")]
    public Transform puntoEntrada;
    public Transform pinzaVGR;

    [Header("Prefabs Visuales")]
    public GameObject prefabBaseGris;
    public GameObject prefabBlanco;
    public GameObject prefabRojo;
    public GameObject prefabAzul;

    private GameObject piezaActual;
    private Queue<string> colaDeOrdenes = new Queue<string>();
    private bool gripActivo = false;

    void Start()
    {
        StartCoroutine(IntentarSuscripcionSegura());
    }

    IEnumerator IntentarSuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;

        MQTTClient.Instance.OnDPSPiezaEvent += ActualizarPieza;
        MQTTClient.Instance.OnDPSColorEvent += ActualizarColor;
        MQTTClient.Instance.OnVGRGripEvent += ActualizarGrip;

        Debug.Log("<color=green><b>DPS:</b> Suscripción completada con éxito.</color>");
    }

    private void OnDisable()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnDPSPiezaEvent -= ActualizarPieza;
            MQTTClient.Instance.OnDPSColorEvent -= ActualizarColor;
            MQTTClient.Instance.OnVGRGripEvent -= ActualizarGrip;
        }
    }

    private void ActualizarPieza(bool detectada)
    {
        lock (colaDeOrdenes)
        {
            if (detectada) colaDeOrdenes.Enqueue("SPAWN_BASE");
            else if (!gripActivo) colaDeOrdenes.Enqueue("DELETE");
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
                    // --- ESCUDO DE PROTECCIÓN INTER-ESTACIÓN ---
                    // Si la pieza ya tiene un padre y ese padre es un cajón/contenedor, el DPS pierde el control y NO la desemparenta
                    if (piezaActual.transform.parent != null &&
                        (piezaActual.transform.parent.name.ToLower().Contains("cajon") ||
                         piezaActual.transform.parent.name.ToLower().Contains("container") ||
                         piezaActual.transform.parent.GetComponent<ContenedorHBW_proxy>() != null))
                    {
                        Debug.Log("<color=cyan><b>[DPS PROTECCIÓN]:</b> Drop ignorado. La pieza ya pertenece de forma segura al contenedor HBW.</color>");
                        piezaActual = null; // Liberamos la referencia local para los siguientes ciclos
                    }
                    else
                    {
                        // Lógica original del DPS si se suelta de forma normal en su propia zona
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
            col.isTrigger = gripActivo;
        }
    }
}