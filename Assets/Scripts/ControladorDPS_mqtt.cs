using UnityEngine;
using System.Collections; // Necesario para las Corrutinas

public class ControladorDPS_mqtt : MonoBehaviour
{
    [Header("Referencias")]
    public Transform puntoEntrada;
    public Transform pinzaVGR;

    [Header("Prefabs")]
    public GameObject prefabBaseGris;
    public GameObject prefabBlanco;
    public GameObject prefabRojo;
    public GameObject prefabAzul;

    [Header("Ajustes Pinza")]
    public Vector3 posicionEnPinza = new Vector3(0f, 0f, -0.0002f);
    public Vector3 rotacionEnPinza = new Vector3(90f, 0f, 0f);

    private GameObject piezaActual;
    private string modoPendiente = "";
    private bool gripActivo = false;

    // --- SUSCRIPCIÓN SEGURA ---

    void Start()
    {
        // En lugar de OnEnable, usamos una corrutina para asegurar que MQTTClient existe
        StartCoroutine(IntentarSuscripcionSegura());
    }

    IEnumerator IntentarSuscripcionSegura()
    {
        // Esperamos hasta que la instancia de MQTT esté disponible
        while (MQTTClient.Instance == null)
        {
            yield return null; // Espera al siguiente frame
        }

        // Una vez que existe, nos suscribimos
        MQTTClient.Instance.OnDPSPiezaEvent += ActualizarPieza;
        MQTTClient.Instance.OnDPSColorEvent += ActualizarColor;
        MQTTClient.Instance.OnVGRGripEvent += ActualizarGrip;

        Debug.Log("<color=green><b>DPS:</b> Suscripción completada con éxito.</color>");
    }

    private void OnDisable()
    {
        // Importante: Desvincular siempre al destruir el objeto para evitar errores
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnDPSPiezaEvent -= ActualizarPieza;
            MQTTClient.Instance.OnDPSColorEvent -= ActualizarColor;
            MQTTClient.Instance.OnVGRGripEvent -= ActualizarGrip;
        }
    }

    // --- REACCIÓN A EVENTOS ---
    private void ActualizarPieza(bool detectada)
    {
        Debug.Log("Mensaje MQTT recibido - Pieza: " + detectada);
        if (detectada) modoPendiente = "SPAWN_BASE";
        else if (!gripActivo) modoPendiente = "DELETE";
    }

    private void ActualizarColor(string color)
    {
        Debug.Log("Mensaje MQTT recibido - Color: " + color);
        if (gripActivo && (color == "BLUE" || color == "RED" || color == "WHITE"))
            modoPendiente = color;
    }

    private void ActualizarGrip(bool activo)
    {
        Debug.Log("Mensaje MQTT recibido - Grip: " + activo);
        gripActivo = activo;
        if (!activo) modoPendiente = "DROP";
    }

    void Update()
    {
        if (modoPendiente != "")
        {
            EjecutarOrden();
            modoPendiente = "";
        }

        // Mantener pegado al VGR (esta lógica se ejecuta cada frame si hay grip)
        if (gripActivo && piezaActual != null)
        {
            if (piezaActual.transform.parent != pinzaVGR)
            {
                piezaActual.transform.SetParent(pinzaVGR);
                piezaActual.transform.localPosition = posicionEnPinza;
                piezaActual.transform.localEulerAngles = rotacionEnPinza;
                if (piezaActual.TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    rb.isKinematic = true;
                }
            }
        }
    }

    void EjecutarOrden()
    {
        switch (modoPendiente)
        {
            case "SPAWN_BASE":
                if (piezaActual != null) Destroy(piezaActual);
                piezaActual = Instantiate(prefabBaseGris, puntoEntrada.position, puntoEntrada.rotation);
                ConfigurarFisicas(piezaActual);
                break;
            case "DELETE":
                // Comentamos o eliminamos el Destroy para que la pieza persista en el almacén
                // if (piezaActual != null) Destroy(piezaActual); 
                piezaActual = null; // Perder la referencia para poder spawnear la siguiente base gris
                break;
            case "DROP":
                if (piezaActual != null)
                {
                    piezaActual.transform.SetParent(null);
                    Rigidbody rb = piezaActual.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.useGravity = true;
                    }
                }
                break;
            case "WHITE":
            case "RED":
            case "BLUE":
                CambiarColor(modoPendiente);
                break;
        }
    }

    void CambiarColor(string color)
    {
        if (piezaActual == null) return;
        GameObject prefab = (color == "WHITE") ? prefabBlanco : (color == "RED") ? prefabRojo : prefabAzul;

        // Guardamos posición actual antes de destruir
        Vector3 posActual = piezaActual.transform.position;
        Quaternion rotActual = piezaActual.transform.rotation;

        Destroy(piezaActual);
        piezaActual = Instantiate(prefab, posActual, rotActual);
        ConfigurarFisicas(piezaActual);
    }

    void ConfigurarFisicas(GameObject p)
    {
        // 1. Rigidbody: Lo configuramos como Kinematic inicialmente para que no se caiga al aparecer
        Rigidbody rb = p.GetComponent<Rigidbody>();
        if (rb == null) rb = p.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // 2. Limpieza de colliders antiguos (para evitar conflictos)
        foreach (var oldCol in p.GetComponents<Collider>())
        {
            Destroy(oldCol);
        }

        // 3. AÑADIR COLLIDER FÍSICO (Sólido para que no se atraviesen)
        BoxCollider colFisico = p.AddComponent<BoxCollider>();
        colFisico.isTrigger = false;
        // colFisico.size = new Vector3(0.05f, 0.05f, 0.05f); // Ajusta según tu pieza

        // 4. AÑADIR COLLIDER TRIGGER (Para que el VGR lo detecte)
        BoxCollider colTrigger = p.AddComponent<BoxCollider>();
        colTrigger.isTrigger = true;

    }
}