using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System;
using System.Text;

public class MQTTClient : MonoBehaviour
{
    private static MQTTClient instance;
    public static MQTTClient Instance { get { return instance; } }

    private MqttClient client;
    private string lastHBWJson = ""; // Almacena el último estado del almacén

    [Header("Configuración del Broker")]
    public string brokerHost = "4ca80baa3731405580bfa27dc37e6665.s1.eu.hivemq.cloud";
    public int puerto = 8883;

    [Header("Credenciales")]
    public string usuario = "LearningFactory";
    public string contrasena = "Fischertechnik1";

    // --- EVENTOS (Delegados) ---
    public delegate void OnBeltUpdate(float speed);
    public event OnBeltUpdate OnBeltUpdateEvent;

    public delegate void OnCylinderUpdate(string color, int state);
    public event OnCylinderUpdate OnCylinderUpdateEvent;

    public delegate void OnDPSUpdate(string topic, string message);
    public event OnDPSUpdate OnDPSUpdateEvent;

    public delegate void OnHBWUpdate(string json);
    public event OnHBWUpdate OnHBWUpdateEvent;

    public delegate void OnVGRUpdate(string json);
    public event OnVGRUpdate OnVGRUpdateEvent;

    public delegate void OnHBWPositionUpdate(string json);
    public event OnHBWPositionUpdate OnHBWPositionUpdateEvent;

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
            client = new MqttClient(brokerHost, puerto, true, null, null, MqttSslProtocols.TLSv1_2);
            client.MqttMsgPublishReceived += OnMessageReceived;
            client.Connect(Guid.NewGuid().ToString(), usuario, contrasena);

            if (client.IsConnected)
            {
                Debug.Log("<color=green><b>MQTT Conectado</b></color>");
                string[] topics = { "f/sld/belt", "f/sld/cylinder", "f/dps/pieza", "f/dps/color", "f/vgr/grip", "f/pieces_hbw", "f/pos_vgr", "f/pos_hbw"};
                byte[] qos = { 0, 0, 0, 0, 0, 0, 0, 0 };
                client.Subscribe(topics, qos);
            }
        }
        catch (Exception ex) { Debug.LogError("Error MQTT: " + ex.Message); }
    }

    private void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        string msg = Encoding.UTF8.GetString(e.Message).Trim();
        string topic = e.Topic;

        if (topic == "f/sld/belt")
        {
            if (float.TryParse(msg, out float speed)) OnBeltUpdateEvent?.Invoke(speed);
        }
        else if (topic == "f/sld/cylinder")
        {
            string[] partes = msg.Split(',');
            if (partes.Length == 2 && int.TryParse(partes[1], out int state))
                OnCylinderUpdateEvent?.Invoke(partes[0].ToUpper(), state);
        }
        else if (topic.StartsWith("f/dps/") || topic == "f/vgr/grip")
        {
            OnDPSUpdateEvent?.Invoke(topic, msg);
        }
        else if (topic == "f/pieces_hbw")
        {
            lastHBWJson = msg; // Guardamos para suscriptores tardíos
            OnHBWUpdateEvent?.Invoke(msg);
        }
        else if (topic == "f/pos_vgr")
        {
            OnVGRUpdateEvent?.Invoke(msg);
        }
        else if (topic == "f/pos_hbw")
        {
            OnHBWPositionUpdateEvent?.Invoke(msg);
        }
    }

    public string GetLastHBWStatus() => lastHBWJson;

    private void OnApplicationQuit()
    {
        if (client != null && client.IsConnected) client.Disconnect();
    }
}