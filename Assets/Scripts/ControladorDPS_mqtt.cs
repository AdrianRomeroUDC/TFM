using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System;
using System.Text;

public class ControladorDPS_mqtt : MonoBehaviour
{
    private MqttClient client;

    [Header("Configuración MQTT")]
    public string brokerHost = "4ca80baa3731405580bfa27dc37e6665.s1.eu.hivemq.cloud";
    public string username = "LearningFactory";
    public string password = "Fischertechnik1";

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

    void Awake() { LimpiarEscenaInmediata(); }
    void Start() { Connect(); }

    void Update()
    {
        // El Update corre en el HILO PRINCIPAL, aquí sí podemos tocar transforms
        if (modoPendiente != "")
        {
            if (modoPendiente == "SPAWN_BASE") SpawnBase();
            else if (modoPendiente == "DELETE") { if (piezaActual != null) Destroy(piezaActual); }
            else if (modoPendiente == "DROP") EjecutarSoltarFisico(); // Nueva orden segura
            else CambiarColorEnVGR(modoPendiente);

            modoPendiente = "";
        }

        // Lógica de sujeción
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

    void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        string msg = Encoding.UTF8.GetString(e.Message).Trim().ToUpper();
        string topic = e.Topic;

        // Aquí NO tocamos nada de Unity directamente, solo guardamos la orden
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
                modoPendiente = "DROP"; // Le decimos al Update que suelte la pieza
            }
        }
    }

    // Esta función ahora es llamada desde el Update (Hilo Principal)
    void EjecutarSoltarFisico()
    {
        if (piezaActual != null)
        {
            piezaActual.transform.SetParent(null);
            Rigidbody rb = piezaActual.GetComponent<Rigidbody>();
            if (rb == null) rb = piezaActual.AddComponent<Rigidbody>();

            rb.isKinematic = false;
            rb.useGravity = true;
            Debug.Log("Pieza soltada físicamente desde el hilo principal.");
        }
    }

    void CambiarColorEnVGR(string color)
    {
        if (piezaActual == null) return;
        GameObject prefab = (color == "WHITE") ? prefabBlanco : (color == "RED") ? prefabRojo : (color == "BLUE") ? prefabAzul : null;

        if (prefab != null)
        {
            Destroy(piezaActual);
            piezaActual = Instantiate(prefab);
            piezaActual.transform.SetParent(pinzaVGR);
            piezaActual.transform.localPosition = posicionEnPinza;
            piezaActual.transform.localEulerAngles = rotacionEnPinza;
            ConfigurarPieza(piezaActual, pinzaVGR);
        }
    }

    void SpawnBase()
    {
        if (piezaActual != null) Destroy(piezaActual);
        piezaActual = Instantiate(prefabBaseGris, puntoEntrada.position, puntoEntrada.rotation);
        ConfigurarPieza(piezaActual, puntoEntrada);
    }

    void ConfigurarPieza(GameObject pieza, Transform padre)
    {
        if (pieza.GetComponent<Rigidbody>() == null) pieza.AddComponent<Rigidbody>().isKinematic = true;
        if (pieza.GetComponent<Collider>() == null) pieza.AddComponent<MeshCollider>().convex = true;
    }

    void LimpiarEscenaInmediata()
    {
        foreach (GameObject obj in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (obj.name.Contains("(Clone)") && obj != this.gameObject) DestroyImmediate(obj);
    }

    void Connect()
    {
        try
        {
            client = new MqttClient(brokerHost, 8883, true, null, null, MqttSslProtocols.TLSv1_2);
            client.MqttMsgPublishReceived += OnMessageReceived;
            client.Connect(Guid.NewGuid().ToString(), username, password);
            client.Subscribe(new string[] { "f/dps/pieza", "f/dps/color", "f/vgr/grip" }, new byte[] { 0, 0, 0 });
        }
        catch (Exception ex) { Debug.LogError(ex.Message); }
    }
}
