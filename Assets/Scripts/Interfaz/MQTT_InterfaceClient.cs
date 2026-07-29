using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

[Serializable] public class Bme680Payload { public string ts; public float t; public float h; public float p; public int iaq; public int aq; public float gr; }
[Serializable] public class LdrPayload { public string ts; public float br; public int ldr; }
[Serializable] public class CameraPayload { public string ts; public string data; }
[Serializable] public class StockPayload { public List<StockItem> stockItems; public string ts; }
[Serializable] public class Workpiece { public string id; public string type; public string state; }
[Serializable] public class StockItem { public string location; public Workpiece workpiece; }

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

    private volatile bool estaActivo = true;

    // Propiedad pública para consultar el estado de la conexión
    public bool IsConnected => client != null && client.IsConnected;

    public StockPayload UltimoStock { get; private set; }

    private Queue<Bme680Payload> bmeQueue = new Queue<Bme680Payload>();
    private Queue<LdrPayload> ldrQueue = new Queue<LdrPayload>();
    private Queue<string> camQueue = new Queue<string>();
    private Queue<StockPayload> stockQueue = new Queue<StockPayload>();

    public event Action<Bme680Payload> OnBmeEnvironmentEvent;
    public event Action<LdrPayload> OnLdrLightEvent;
    public event Action<string> OnCameraImageEvent;
    public event Action<StockPayload> OnStockUpdateEvent;

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        // Se conecta en Start para asegurar que MQTTClient.Instance ya existe
        string clientIdShort = "Unity_Interfaz_" + UnityEngine.Random.Range(10000, 99999);
        Task.Run(() => ConnectAsync(clientIdShort));
    }

    void OnEnable()
    {
        estaActivo = true;
    }

    void OnDisable()
    {
        estaActivo = false;
    }

    public void DesconectarRed()
    {
        if (client != null && client.IsConnected)
        {
            try { client.Disconnect(); } catch { }
        }
    }

    void Update()
    {
        List<Bme680Payload> bmeLista = null;
        List<LdrPayload> ldrLista = null;
        List<StockPayload> stockLista = null;
        List<string> camLista = null;

        lock (lockObject)
        {
            if (bmeQueue.Count > 0) { bmeLista = new List<Bme680Payload>(bmeQueue); bmeQueue.Clear(); }
            if (ldrQueue.Count > 0) { ldrLista = new List<LdrPayload>(ldrQueue); ldrQueue.Clear(); }

            if (stockQueue.Count > 0)
            {
                stockLista = new List<StockPayload>(stockQueue);
                UltimoStock = stockLista[stockLista.Count - 1];
                stockQueue.Clear();
            }

            if (camQueue.Count > 0) { camLista = new List<string>(camQueue); camQueue.Clear(); }
        }

        if (bmeLista != null) foreach (var item in bmeLista) OnBmeEnvironmentEvent?.Invoke(item);
        if (ldrLista != null) foreach (var item in ldrLista) OnLdrLightEvent?.Invoke(item);
        if (stockLista != null) foreach (var item in stockLista) OnStockUpdateEvent?.Invoke(item);
        if (camLista != null) foreach (var item in camLista) OnCameraImageEvent?.Invoke(item);
    }

    private void ConnectAsync(string clientId)
    {
        try
        {
            // Valores de respaldo por defecto
            string brokerHost = "10.113.36.36";
            int puerto = 1884;
            string usuario = "LearningFactory";
            string contrasena = "Fischertechnik1";

            // Lee automáticamente la configuración desde MQTTClient
            if (MQTTClient.Instance != null)
            {
                brokerHost = MQTTClient.Instance.brokerHost;
                puerto = MQTTClient.Instance.puerto;
                usuario = MQTTClient.Instance.usuario;
                contrasena = MQTTClient.Instance.contrasena;
            }

            client = new MqttClient(brokerHost, puerto, false, null, null, MqttSslProtocols.None);
            client.MqttMsgPublishReceived += OnMessageReceived;

            client.Connect(clientId, usuario, contrasena);

            if (client.IsConnected)
            {
                string[] topics = { "i/cam", "i/bme680", "i/ldr", "f/i/stock" };
                client.Subscribe(topics, new byte[] { 0, 0, 0, 0 });

                Debug.Log($"<color=green><b>[MQTT Interfaz] ¡CONECTADO CON ÉXITO! ID: {clientId} en {brokerHost}:{puerto}</b></color>");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MQTT Interfaz] Error al conectar: {ex.Message}");
        }
    }

    private void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        if (!estaActivo) return;

        string topic = e.Topic;
        string msg = Encoding.UTF8.GetString(e.Message).Trim();

        ProcesarMensajeExterno(topic, msg);
    }

    public void ProcesarMensajeExterno(string topic, string msg)
    {
        lock (lockObject)
        {
            try
            {
                if (topic == "i/cam")
                {
                    CameraPayload camData = JsonUtility.FromJson<CameraPayload>(msg);

                    if (camData != null && !string.IsNullOrEmpty(camData.data))
                    {
                        camQueue.Clear();
                        camQueue.Enqueue(camData.data);
                    }
                }
                else
                {
                    string msgClean = msg.Replace("True", "true").Replace("False", "false");

                    if (topic == "i/bme680" || topic == "i/bm680") bmeQueue.Enqueue(JsonUtility.FromJson<Bme680Payload>(msgClean));
                    else if (topic == "i/ldr") ldrQueue.Enqueue(JsonUtility.FromJson<LdrPayload>(msgClean));
                    else if (topic == "f/i/stock") stockQueue.Enqueue(JsonUtility.FromJson<StockPayload>(msgClean));
                }
            }
            catch (Exception ex) { Debug.LogWarning($"Error parseando JSON en {topic}: {ex.Message}"); }
        }
    }

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
        if (client != null && client.IsConnected)
        {
            try
            {
                SendCameraConfig(false, 2);
                Debug.Log("<color=yellow>[MQTT Interfaz] Enviando orden de apagado de cámara (c/cam) antes de salir...</color>");
                System.Threading.Thread.Sleep(100);
                client.Disconnect();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MQTT Interfaz] Error al enviar apagar cámara en OnApplicationQuit: {ex.Message}");
            }
        }
    }
}