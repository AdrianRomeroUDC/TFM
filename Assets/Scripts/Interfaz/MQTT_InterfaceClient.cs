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
    private Queue<StockPayload> stockQueue = new Queue<StockPayload>();

    // Eventos
    public event Action<Bme680Payload> OnBmeEnvironmentEvent;
    public event Action<LdrPayload> OnLdrLightEvent;
    public event Action<string> OnCameraImageEvent;
    public event Action<StockPayload> OnStockUpdateEvent;

    [Header("Configuración del Broker")]
    public string brokerHost = "4ca80baa3731405580bfa27dc37e6665.s1.eu.hivemq.cloud";
    public int puerto = 8883;

    [Header("Credenciales")]
    public string usuario = "LearningFactory";
    public string contrasena = "Fischertechnik1";

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }
        Connect();
    }

    void Update()
    {
        lock (lockObject)
        {
            while (bmeQueue.Count > 0) OnBmeEnvironmentEvent?.Invoke(bmeQueue.Dequeue());
            while (ldrQueue.Count > 0) OnLdrLightEvent?.Invoke(ldrQueue.Dequeue());
            while (camQueue.Count > 0) OnCameraImageEvent?.Invoke(camQueue.Dequeue());
            while (stockQueue.Count > 0) OnStockUpdateEvent?.Invoke(stockQueue.Dequeue());
        }
    }

    void Connect()
    {
        try
        {
            client = new MqttClient(brokerHost, puerto, false, null, null, MqttSslProtocols.None);
            client.MqttMsgPublishReceived += OnMessageReceived;

            client.Connect(Guid.NewGuid().ToString(), usuario, contrasena);

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
                else if (topic == "f/i/stock") stockQueue.Enqueue(JsonUtility.FromJson<StockPayload>(msg));
            }
            catch (Exception ex) { Debug.LogWarning($"Error parseando JSON: {ex.Message}"); }
        }
    }

    // --- MÉTODOS DE PUBLICACIÓN ---
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

    // =======================================================================
    // MÉTODO MODIFICADO: Envío crítico controlado antes de la desconexión total
    // =======================================================================
    private void OnApplicationQuit()
    {
        if (client != null && client.IsConnected)
        {
            try
            {
                // 1. Construimos el payload exacto usando las clases del script
                CamConfigPayload payloadApagado = new CamConfigPayload
                {
                    ts = GetISO8601Timestamp(),
                    on = false,
                    fps = 15
                };

                // 2. Convertimos el objeto a JSON estructurado string
                string jsonApagado = JsonUtility.ToJson(payloadApagado);

                // 3. Forzamos la publicación directa usando QoS 1 (Asegura entrega en brokers Cloud)
                client.Publish("c/cam",
                               Encoding.UTF8.GetBytes(jsonApagado),
                               MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE,
                               false);

                Debug.Log("<color=red><b>[MQTT Interface] Comando on:false enviado con éxito a c/cam</b></color>");

                // 4. CRÍTICO: Congelamos el hilo de Unity 250 milisegundos. 
                // Esto le da tiempo real a los buffers de Windows/Mac para vaciar la cola TCP hacia HiveMQ
                System.Threading.Thread.Sleep(250);
            }
            catch (Exception ex)
            {
                Debug.LogError("Error al procesar el envío de apagado de cámara: " + ex.Message);
            }

            // 5. Procedemos al cierre seguro de la conexión
            try
            {
                client.Disconnect();
            }
            catch { }
        }
    }
}