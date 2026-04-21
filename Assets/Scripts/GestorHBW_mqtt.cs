using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System;
using System.Text;

// Clase para convertir el JSON de Python a datos de Unity
[Serializable]
public class HBWStockPayload
{
    public string[] piezas;
}

public class GestorHBW_mqtt : MonoBehaviour
{
    private MqttClient client;
    private string lastJsonReceived;
    private bool pendingUpdate = false;

    [Header("Configuración MQTT")]
    public string brokerHost = "4ca80baa3731405580bfa27dc37e6665.s1.eu.hivemq.cloud";
    public string username = "LearningFactory";
    public string password = "Fischertechnik1";

    [Header("Referencias de Escena")]
    public Transform[] puntosDeHueco; // Tamaño 9 en el Inspector

    [Header("Prefabs de Pieza")]
    public GameObject prefabBlanco;
    public GameObject prefabRojo;
    public GameObject prefabAzul;

    void Start()
    {
        LimpiarEstantes();
        Connect();
    }

    void LimpiarEstantes()
    {
        // Recorre los 9 puntos de hueco y borra lo que tengan dentro
        foreach (Transform hueco in puntosDeHueco)
        {
            foreach (Transform hijo in hueco)
            {
                Destroy(hijo.gameObject);
            }
        }
        Debug.Log("Estantes limpiados para el inicio de simulación.");
    }

    void Update()
    {
        // Solo actualizamos si ha llegado un mensaje nuevo por MQTT
        if (pendingUpdate)
        {
            try
            {
                HBWStockPayload data = JsonUtility.FromJson<HBWStockPayload>(lastJsonReceived);
                ActualizarVisualizacion(data.piezas);
            }
            catch (Exception e)
            {
                Debug.LogError("Error al procesar JSON: " + e.Message);
            }
            pendingUpdate = false;
        }
    }

    void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        // Guardamos el mensaje y avisamos al Update para que lo procese en el hilo principal
        lastJsonReceived = Encoding.UTF8.GetString(e.Message);
        pendingUpdate = true;
    }

    void ActualizarVisualizacion(string[] listaColores)
    {
        for (int i = 0; i < puntosDeHueco.Length; i++)
        {
            // Seguridad: si el PLC manda menos de 9 posiciones
            if (i >= listaColores.Length) break;

            // 1. Limpiar el hueco (borrar pieza anterior si existe)
            foreach (Transform child in puntosDeHueco[i])
            {
                Destroy(child.gameObject);
            }

            // 2. Seleccionar el molde (prefab) según el color
            GameObject prefabAInstanciar = null;
            switch (listaColores[i].Trim().ToUpper())
            {
                case "WHITE": prefabAInstanciar = prefabBlanco; break;
                case "RED": prefabAInstanciar = prefabRojo; break;
                case "BLUE": prefabAInstanciar = prefabAzul; break;
                    // Si es "NONE" o cualquier otra cosa, el prefab queda null y el hueco vacío
            }

            // 3. Crear la pieza en el sitio exacto
            if (prefabAInstanciar != null)
            {
                GameObject nuevaPieza = Instantiate(prefabAInstanciar, puntosDeHueco[i].position, puntosDeHueco[i].rotation);
                nuevaPieza.transform.SetParent(puntosDeHueco[i]);
            }
        }
        Debug.Log("Vista del almacén actualizada.");
    }

    void Connect()
    {
        try
        {
            // Conexión segura usando TLS
            client = new MqttClient(brokerHost, 8883, true, null, null, MqttSslProtocols.TLSv1_2);
            string clientId = Guid.NewGuid().ToString();

            client.MqttMsgPublishReceived += OnMessageReceived;
            client.Connect(clientId, username, password);

            // Suscribirse al topic que configuramos en Python
            client.Subscribe(new string[] { "f/pieces_hbw" }, new byte[] { 0 });

            Debug.Log("<color=green>MQTT HBW: Conectado y suscrito.</color>");
        }
        catch (Exception e)
        {
            Debug.LogError("Error de conexión MQTT: " + e.Message);
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
