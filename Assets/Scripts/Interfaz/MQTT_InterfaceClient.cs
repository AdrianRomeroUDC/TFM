using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

// --- Estructuras de Datos ---
[Serializable] public class Bme680Payload { public string ts; public float t; public float h; public float p; public int iaq; public int aq; public float gr; }
[Serializable] public class LdrPayload { public string ts; public float br; public int ldr; }
[Serializable] public class CameraPayload { public string ts; public string data; }
[Serializable] public class StockPayload { public List<StockItem> stockItems; public string ts; }
[Serializable] public class Workpiece { public string id; public string type; public string state; }
[Serializable] public class StockItem { public string location; public Workpiece workpiece; }

// --- Estructuras para Publicar ---
[Serializable] public class OrderPayload { public string ts; public string type; }
[Serializable] public class PtuPayload { public string ts; public string cmd; public int degree; }
[Serializable] public class CamConfigPayload { public string ts; public bool on; public int fps; }
[Serializable] public class SensorPeriodPayload { public string ts; public int period; }
[Serializable] public class PtuHomePayload { public string ts; public string cmd; }

public class MQTT_InterfaceClient : MonoBehaviour
{
    private static MQTT_InterfaceClient instance;
    public static MQTT_InterfaceClient Instance { get { return instance; } }

    private MqttClient client;
    private readonly object lockObject = new object();

    // Colas para puente de hilos
    private Queue<Bme680Payload> bmeQueue = new Queue<Bme680Payload>();
    private Queue<LdrPayload> ldrQueue = new Queue<LdrPayload>();
    private Queue<string> camQueue = new Queue<string>();

    // Eventos
    public event Action<Bme680Payload> OnBmeEnvironmentEvent;
    public event Action<LdrPayload> OnLdrLightEvent;
    public event Action<string> OnCameraImageEvent;

    [Header("Configuración")]
    public string brokerHost = "tu-broker.cloud";
    public int puerto = 1883;

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }
        Connect();
    }

    void Update()
    {
        // Procesar colas en el hilo principal de Unity (evita errores de Thread)
        lock (lockObject)
        {
            while (bmeQueue.Count > 0) OnBmeEnvironmentEvent?.Invoke(bmeQueue.Dequeue());
            while (ldrQueue.Count > 0) OnLdrLightEvent?.Invoke(ldrQueue.Dequeue());
            while (camQueue.Count > 0) OnCameraImageEvent?.Invoke(camQueue.Dequeue());
        }
    }

    void Connect()
    {
        try
        {
            client = new MqttClient(brokerHost, puerto, false, null, null, MqttSslProtocols.None);
            client.MqttMsgPublishReceived += OnMessageReceived;
            client.Connect(Guid.NewGuid().ToString());

            string[] topics = { "i/cam", "i/bme680", "i/ldr", "f/i/stock" };
            client.Subscribe(topics, new byte[] { 0, 0, 0, 0 });
            Debug.Log("<color=green>MQTT Conectado</color>");
        }
        catch (Exception ex) { Debug.LogError("Error MQTT: " + ex.Message); }
    }

    private void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        string msg = Encoding.UTF8.GetString(e.Message).Trim().Replace("True", "true").Replace("False", "false");
        string topic = e.Topic;

        lock (lockObject)
        {
            try
            {
                if (topic == "i/bme680") bmeQueue.Enqueue(JsonUtility.FromJson<Bme680Payload>(msg));
                else if (topic == "i/ldr") ldrQueue.Enqueue(JsonUtility.FromJson<LdrPayload>(msg));
                else if (topic == "i/cam") camQueue.Enqueue(JsonUtility.FromJson<CameraPayload>(msg).data);
            }
            catch (Exception ex) { Debug.LogWarning($"Error parseando JSON: {ex.Message}"); }
        }
    }

    // --- MÉTODOS DE PUBLICACIÓN (Los que te faltaban) ---

    private string GetISO8601Timestamp() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    private void PublishJson(string topic, string json)
    {
        if (client != null && client.IsConnected)
        {
            client.Publish(topic, Encoding.UTF8.GetBytes(json), MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, false);
        }
    }

    public void SendOrder(string workpieceType)
    {
        var payload = new OrderPayload { ts = GetISO8601Timestamp(), type = workpieceType };
        PublishJson("f/o/order", JsonUtility.ToJson(payload));
    }

    public void SendPtuCommand(string command, int degree = 10)
    {
        if (command == "home")
        {
            var homePayload = new PtuHomePayload { ts = GetISO8601Timestamp(), cmd = command };
            PublishJson("o/ptu", JsonUtility.ToJson(homePayload));
        }
        else
        {
            var payload = new PtuPayload { ts = GetISO8601Timestamp(), cmd = command, degree = degree };
            PublishJson("o/ptu", JsonUtility.ToJson(payload));
        }
    }

    public void SendCameraConfig(bool isOn, int fps)
    {
        var payload = new CamConfigPayload { ts = GetISO8601Timestamp(), on = isOn, fps = fps };
        PublishJson("c/cam", JsonUtility.ToJson(payload));
    }

    public void SendLdrPeriod(int seconds)
    {
        var payload = new SensorPeriodPayload { ts = GetISO8601Timestamp(), period = seconds };
        PublishJson("c/ldr", JsonUtility.ToJson(payload));
    }

    public void SendBme680Period(int seconds)
    {
        var payload = new SensorPeriodPayload { ts = GetISO8601Timestamp(), period = seconds };
        PublishJson("c/bme680", JsonUtility.ToJson(payload));
    }

    private void OnApplicationQuit()
    {
        if (client != null && client.IsConnected) client.Disconnect();
    }
}