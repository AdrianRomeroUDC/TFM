using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

// =================================================================
// ESTRUCTURAS DE DATOS EXCLUSIVAS PARA LA INTERFAZ (LECTURA)
// =================================================================
[Serializable] public class CameraPayload { public string ts; public string data; }
[Serializable] public class Bme680Payload { public string ts; public float t; public float h; public float p; public int iaq; public int aq; public float gr; }
[Serializable] public class LdrPayload { public string ts; public float br; public int ldr; }
[Serializable] public class Workpiece { public string id; public string type; public string state; }
[Serializable] public class StockItem { public string location; public Workpiece workpiece; }
[Serializable] public class StockPayload { public List<StockItem> stockItems; public string ts; }

// =================================================================
// ESTRUCTURAS DE DATOS PARA PUBLICACIÓN desde la UI
// =================================================================
[Serializable] public class OrderPayload { public string ts; public string type; }
[Serializable] public class PtuPayload { public string ts; public string cmd; public int degree; }
[Serializable] public class CamConfigPayload { public string ts; public bool on; public int fps; }
[Serializable] public class SensorPeriodPayload { public string ts; public int period; }

// Estructura especial para el comando "home" (evita enviar la clave 'degree')
[Serializable] public class PtuHomePayload { public string ts; public string cmd; }

public class MQTT_InterfaceClient : MonoBehaviour
{
    private static MQTT_InterfaceClient instance;
    public static MQTT_InterfaceClient Instance { get { return instance; } }

    private MqttClient client;

    [Header("Configuración del Broker (Interfaz)")]
    public string brokerHost = "tu-broker-de-interfaz.cloud";
    public int puerto = 1883; // O 8883 si usas SSL

    [Header("Credenciales (Interfaz)")]
    public string usuario = "UserInterfaz";
    public string contrasena = "PassInterfaz";

    // =================================================================
    // DELEGADOS Y EVENTOS PARA LA UI
    // =================================================================
    public delegate void OnCameraImageUpdate(string base64Data);
    public event OnCameraImageUpdate OnCameraImageEvent;

    public delegate void OnBmeEnvironmentUpdate(Bme680Payload data);
    public event OnBmeEnvironmentUpdate OnBmeEnvironmentEvent;

    public delegate void OnLdrLightUpdate(LdrPayload data);
    public event OnLdrLightUpdate OnLdrLightEvent;

    public delegate void OnStockWarehouseUpdate(StockPayload data);
    public event OnStockWarehouseUpdate OnStockWarehouseEvent;

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        Connect();
    }

    void Connect()
    {
        try
        {
            // Cambiar MqttSslProtocols si este segundo broker requiere seguridad
            client = new MqttClient(brokerHost, puerto, false, null, null, MqttSslProtocols.None);
            client.MqttMsgPublishReceived += OnMessageReceived;
            //client.Connect(Guid.NewGuid().ToString(), usuario, contrasena);   // Descomentar si el broker requiere Usuario/Contraseña
            client.Connect(Guid.NewGuid().ToString());

            if (client.IsConnected)
            {
                Debug.Log("<color=cyan><b>MQTT Interfaz Conectado</b></color>");

                // Suscripción ÚNICAMENTE a los topics de lectura de la interfaz
                string[] topics = { "i/cam", "i/bm680", "i/ldr", "f/i/stock" };
                byte[] qos = { 0, 0, 0, 0 };

                client.Subscribe(topics, qos);
            }
        }
        catch (Exception ex) { Debug.LogError("Error MQTT Interfaz: " + ex.Message); }
    }

    private void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        string msg = Encoding.UTF8.GetString(e.Message).Trim().Replace("True", "true").Replace("False", "false");
        string topic = e.Topic;

        if (topic == "i/cam")
        {
            try { var data = JsonUtility.FromJson<CameraPayload>(msg); OnCameraImageEvent?.Invoke(data.data); } catch { }
        }
        else if (topic == "i/bm680")
        {
            try { var data = JsonUtility.FromJson<Bme680Payload>(msg); OnBmeEnvironmentEvent?.Invoke(data); } catch { }
        }
        else if (topic == "i/ldr")
        {
            try { var data = JsonUtility.FromJson<LdrPayload>(msg); OnLdrLightEvent?.Invoke(data); } catch { }
        }
        else if (topic == "f/i/stock")
        {
            try { var data = JsonUtility.FromJson<StockPayload>(msg); OnStockWarehouseEvent?.Invoke(data); } catch { }
        }
    }

    // =================================================================
    // MÉTODOS PÚBLICOS PARA PUBLICAR DESDE LOS BOTONES/SLIDERS DE LA UI
    // =================================================================

    private string GetISO8601Timestamp() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    private void PublishJson(string topic, string json)
    {
        if (client != null && client.IsConnected)
        {
            client.Publish(topic, Encoding.UTF8.GetBytes(json), MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, false);
            Debug.Log($"[MQTT Interfaz] Publicado en {topic}: {json}");
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
            // Si el comando es "home", enviamos el JSON simplificado (sin la propiedad degree)
            var homePayload = new PtuHomePayload { ts = GetISO8601Timestamp(), cmd = command };
            PublishJson("o/ptu", JsonUtility.ToJson(homePayload));
        }
        else
        {
            // Para relmove_up, relmove_down, relmove_left y relmove_right enviamos los grados
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