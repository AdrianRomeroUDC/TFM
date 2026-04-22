using UnityEngine;
using System;

public class ControladorDPS_mqtt : MonoBehaviour
{
    [Header("Posiciones y Referencias")]
    public Transform puntoEntrada;
    public Transform pinzaVGR;

    [Header("Valores de Calibración")]
    public Vector3 posicionEnPinza = new Vector3(0f, 0f, -0.0002f);
    public Vector3 rotacionEnPinza = new Vector3(90f, 0f, 0f);

    [Header("Prefabs")]
    public GameObject prefabBaseGris;
    public GameObject prefabBlanco;
    public GameObject prefabRojo;
    public GameObject prefabAzul;

    private GameObject piezaActual;
    private string modoPendiente = "";
    private bool gripActivo = false;

    // --- CICLO DE VIDA Y SUSCRIPCIÓN ---

    void Awake()
    {
        LimpiarEscenaInmediata();
    }

    void Start()
    {
        // Intentamos suscribirnos cada segundo hasta que MQTTClient.Instance no sea null
        InvokeRepeating("IntentarSuscripcion", 0f, 1f);
    }

    void IntentarSuscripcion()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnDPSUpdateEvent += ProcesarMensajeMqtt;
            Debug.Log("<color=green><b>DPS:</b> Conectado con éxito al sistema central.</color>");
            CancelInvoke("IntentarSuscripcion");
        }
    }

    private void OnDisable()
    {
        // Desvincular el evento para evitar errores de referencia nula al cerrar
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnDPSUpdateEvent -= ProcesarMensajeMqtt;
    }

    // --- PROCESAMIENTO DE DATOS ---

    private void ProcesarMensajeMqtt(string topic, string message)
    {
        string msg = message.Trim().ToUpper();

        if (topic == "f/dps/pieza")
        {
            if (msg == "1") modoPendiente = "SPAWN_BASE";
            else if (msg == "0" && !gripActivo) modoPendiente = "DELETE";
        }
        else if (topic == "f/dps/color")
        {
            if (gripActivo && (msg == "BLUE" || msg == "RED" || msg == "WHITE"))
                modoPendiente = msg;
        }
        else if (topic == "f/vgr/grip")
        {
            if (msg == "1")
            {
                gripActivo = true;
            }
            else
            {
                gripActivo = false;
                modoPendiente = "DROP";
            }
        }
    }

    void Update()
    {
        // Ejecución de órdenes en el Hilo Principal (Seguro para Unity)
        if (modoPendiente != "")
        {
            if (modoPendiente == "SPAWN_BASE") SpawnBase();
            else if (modoPendiente == "DELETE") { if (piezaActual != null) Destroy(piezaActual); }
            else if (modoPendiente == "DROP") EjecutarSoltarFisico();
            else CambiarColorEnVGR(modoPendiente);

            modoPendiente = "";
        }

        // Mantener la pieza pegada a la pinza si el grip está activo
        if (gripActivo && piezaActual != null)
        {
            if (piezaActual.transform.parent != pinzaVGR)
            {
                piezaActual.transform.SetParent(pinzaVGR);
                piezaActual.transform.localPosition = posicionEnPinza;
                piezaActual.transform.localEulerAngles = rotacionEnPinza;

                Rigidbody rb = piezaActual.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
            }
        }
    }

    // --- ACCIONES FÍSICAS ---

    void EjecutarSoltarFisico()
    {
        if (piezaActual != null)
        {
            piezaActual.transform.SetParent(null);
            Rigidbody rb = piezaActual.GetComponent<Rigidbody>();
            if (rb == null) rb = piezaActual.AddComponent<Rigidbody>();

            rb.isKinematic = false;
            rb.useGravity = true;
            Debug.Log("Pieza soltada físicamente.");
        }
    }

    void CambiarColorEnVGR(string color)
    {
        if (piezaActual == null) return;

        GameObject prefab = null;
        if (color == "WHITE") prefab = prefabBlanco;
        else if (color == "RED") prefab = prefabRojo;
        else if (color == "BLUE") prefab = prefabAzul;

        if (prefab != null)
        {
            Destroy(piezaActual);
            piezaActual = Instantiate(prefab);
            piezaActual.transform.SetParent(pinzaVGR);
            piezaActual.transform.localPosition = posicionEnPinza;
            piezaActual.transform.localEulerAngles = rotacionEnPinza;
            ConfigurarPieza(piezaActual);
        }
    }

    void SpawnBase()
    {
        if (piezaActual != null) Destroy(piezaActual);
        piezaActual = Instantiate(prefabBaseGris, puntoEntrada.position, puntoEntrada.rotation);
        ConfigurarPieza(piezaActual);
        Debug.Log("Base Gris instanciada en punto de entrada.");
    }

    void ConfigurarPieza(GameObject pieza)
    {
        if (pieza.GetComponent<Rigidbody>() == null) pieza.AddComponent<Rigidbody>().isKinematic = true;
        if (pieza.GetComponent<Collider>() == null) pieza.AddComponent<MeshCollider>().convex = true;
    }

    void LimpiarEscenaInmediata()
    {
        foreach (GameObject obj in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (obj.name.Contains("(Clone)")) DestroyImmediate(obj);
        }
    }
}