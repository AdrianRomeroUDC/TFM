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

    [Header("Configuración del Broker")]
    public string brokerHost = "4ca80baa3731405580bfa27dc37e6665.s1.eu.hivemq.cloud";
    public int puerto = 8883;

    [Header("Credenciales (Públicas)")]
    public string usuario = "LearningFactory";
    public string contrasena = "Fischertechnik1";

    // --- DEFINICIÓN DE DELEGADOS (Events) ---
    public delegate void OnBeltUpdate(float speed);
    public event OnBeltUpdate OnBeltUpdateEvent;

    public delegate void OnCylinderUpdate(string color, int state);
    public event OnCylinderUpdate OnCylinderUpdateEvent;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        Connect();
    }

    void Connect()
    {
        try
        {
            // Configuración para HiveMQ Cloud (TLS activo con puerto 8883)
            client = new MqttClient(brokerHost, puerto, true, null, null, MqttSslProtocols.TLSv1_2);
            client.MqttMsgPublishReceived += OnMessageReceived;

            // Usamos las variables públicas para conectar
            string clientId = Guid.NewGuid().ToString();
            client.Connect(clientId, usuario, contrasena);

            if (client.IsConnected)
            {
                Debug.Log($"<color=green>MQTT Conectado:</color> Broker {brokerHost} con usuario {usuario}");
                // Suscripción a los topics necesarios
                client.Subscribe(new string[] { "f/sld/belt", "f/sld/cylinder" }, new byte[] { 0, 0 });
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("<color=red>Error de conexión MQTT:</color> " + ex.Message);
        }
    }

    private void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        string msg = Encoding.UTF8.GetString(e.Message).Trim();
        string topic = e.Topic;

        if (topic == "f/sld/belt")
        {
            if (float.TryParse(msg, out float speed))
            {
                OnBeltUpdateEvent?.Invoke(speed);
            }
        }
        else if (topic == "f/sld/cylinder")
        {
            string[] partes = msg.Split(',');
            if (partes.Length == 2)
            {
                if (int.TryParse(partes[1], out int state))
                {
                    OnCylinderUpdateEvent?.Invoke(partes[0].ToUpper(), state);
                }
            }
        }
    }

    private void OnApplicationQuit()
    {
        if (client != null && client.IsConnected)
        {
            client.Disconnect();
        }
    }
}