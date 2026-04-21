using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System;
using System.Text;

public class ControladorCilindrosSLD_mqtt : MonoBehaviour
{
    private MqttClient client;
    public string brokerHost = "4ca80baa3731405580bfa27dc37e6665.s1.eu.hivemq.cloud";
    public string topicCylinders = "f/sld/cylinder";

    [Header("Referencias de Pistones (Vástagos)")]
    public Transform pistonBlanco;
    public Transform pistonRojo;
    public Transform pistonAzul;

    [Header("Coordenadas Exactas (Eje X local)")]
    public float xReposo = 0.001122198f;
    public float xEstirado = 0.000826f;
    public float velocidadPiston = 0.001f; // Al ser valores tan pequeños, usa una velocidad baja

    private float targetBlanco, targetRojo, targetAzul;

    void Start() => Connect();

    void Update()
    {
        MoverPiston(pistonBlanco, ref targetBlanco);
        MoverPiston(pistonRojo, ref targetRojo);
        MoverPiston(pistonAzul, ref targetAzul);
    }

    void MoverPiston(Transform piston, ref float estadoActual)
    {
        if (piston == null) return;

        // Calculamos la X objetivo: si estado es 1 usa xEstirado, si es 0 usa xReposo
        float xObjetivo = Mathf.Lerp(xReposo, xEstirado, estadoActual);

        Vector3 pos = piston.localPosition;
        pos.x = Mathf.MoveTowards(pos.x, xObjetivo, velocidadPiston * Time.deltaTime);
        piston.localPosition = pos;
    }

    void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        string msg = Encoding.UTF8.GetString(e.Message).Trim().ToUpper();
        string[] partes = msg.Split(',');
        if (partes.Length != 2) return;

        string color = partes[0];
        if (float.TryParse(partes[1], out float valor))
        {
            if (color == "WHITE") targetBlanco = valor;
            else if (color == "RED") targetRojo = valor;
            else if (color == "BLUE") targetAzul = valor;
        }
    }

    void Connect()
    {
        try
        {
            client = new MqttClient(brokerHost, 8883, true, null, null, MqttSslProtocols.TLSv1_2);
            client.MqttMsgPublishReceived += OnMessageReceived;
            client.Connect(Guid.NewGuid().ToString(), "LearningFactory", "Fischertechnik1");
            client.Subscribe(new string[] { topicCylinders }, new byte[] { 0 });
        }
        catch (Exception ex) { Debug.LogError(ex.Message); }
    }
}
