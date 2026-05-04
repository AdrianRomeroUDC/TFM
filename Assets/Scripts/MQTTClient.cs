using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System;
using System.Text;

[Serializable]
public class HBWStockPayload { public string[] piezas; }

[Serializable]
public class VGRPositionData { public float estirar; public float rotacion; public float vertical; }

[Serializable]
public class HBWPositionPayload { public float estirar; public float horizontal; public float vertical; }
[Serializable]
public class HBWBeltPayload { public float cintaHBWspeed; public string sentidoGiro; }
[Serializable]
public class MPOHornoPayload
{
    public int closeDoor;
    public int openDoor;
    public int lights;
    public int move2Ref5; // Meter
    public int move2Ref6; // Sacar
    public string ts;
}
[Serializable]
public class MPOTurntablePayload
{
    public int eject;
    public int move2Ref10; // Posición sierra
    public int move2Ref7;  // Posición horno
    public int move2Ref9;  // Posición cinta
    public int saw;        // 1=Dcha, -1=Izq, 0=Stop
    public string ts;
}

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

    public delegate void OnDPSPiezaUpdate(bool detectada);
    public event OnDPSPiezaUpdate OnDPSPiezaEvent;

    public delegate void OnDPSColorUpdate(string color);
    public event OnDPSColorUpdate OnDPSColorEvent;

    public delegate void OnVGRGripUpdate(bool activo);
    public event OnVGRGripUpdate OnVGRGripEvent;

    public delegate void OnHBWPiecesUpdate(string[] piezas);
    public event OnHBWPiecesUpdate OnHBWUpdatePiecesEvent;

    public delegate void OnVGRPositionUpdate(float rot, float vert, float ext);
    public event OnVGRPositionUpdate OnVGRPositionUpdateEvent;

    public delegate void OnHBWPositionUpdate(float hor, float vert, float ext);
    public event OnHBWPositionUpdate OnHBWPositionUpdateEvent;

    public delegate void OnBeltHBWUpdate(float speed, string direction);
    public event OnBeltHBWUpdate OnBeltHBWUpdateEvent;

    public delegate void OnHornoUpdate(MPOHornoPayload data);
    public event OnHornoUpdate OnHornoUpdateEvent;

    public delegate void OnTurntableUpdate(MPOTurntablePayload data);
    public event OnTurntableUpdate OnTurntableUpdateEvent;

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
                string[] topics = { "f/sld/belt", "f/sld/cylinder", "f/dps/pieza", "f/dps/color", "f/vgr/grip", "f/pieces_hbw", "f/pos_vgr", "f/pos_hbw", "f/hbw/cinta", "f/mpo/horno", "f/mpo/turntable" };
                byte[] qos = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
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
        if (topic == "f/dps/piezaDSI")
        {
            OnDPSPiezaEvent?.Invoke(msg == "1");
        }
        else if (topic == "f/dps/color")
        {
            OnDPSColorEvent?.Invoke(msg.ToUpper());
        }
        else if (topic == "f/vgr/grip")
        {
            OnVGRGripEvent?.Invoke(msg == "1");
        }
        else if (topic == "f/pieces_hbw") {
            lastHBWJson = msg;
            try {
                HBWStockPayload data = JsonUtility.FromJson<HBWStockPayload>(msg);
                OnHBWUpdatePiecesEvent?.Invoke(data.piezas);
            } catch { Debug.LogWarning("Error al parsear piezas HBW"); }
        }
        else if (topic == "f/pos_vgr")
        {
            try
            {
                VGRPositionData data = JsonUtility.FromJson<VGRPositionData>(msg);
                // ENVIAR EN EL ORDEN QUE ESPERA EL CONTROLADOR: rot, vert, ext
                OnVGRPositionUpdateEvent?.Invoke(data.rotacion, data.vertical, data.estirar);
            }
            catch { Debug.LogWarning("Error en JSON VGR"); }
        }
        else if (topic == "f/pos_hbw")
        {
            try
            {
                HBWPositionPayload data = JsonUtility.FromJson<HBWPositionPayload>(msg);
                // Enviamos los datos procesados al controlador
                OnHBWPositionUpdateEvent?.Invoke(data.horizontal, data.vertical, data.estirar);
            }
            catch { Debug.LogWarning("Error parseando f/pos_hbw"); }
        }
        else if (topic == "f/hbw/cinta")
        {
            try
            {
                HBWBeltPayload data = JsonUtility.FromJson<HBWBeltPayload>(msg);
                OnBeltHBWUpdateEvent?.Invoke(data.cintaHBWspeed, data.sentidoGiro);
            }
            catch { Debug.LogWarning("Error al parsear f/hbw/cinta"); }
        }
        else if (topic == "f/mpo/horno")
        {
            try
            {
                MPOHornoPayload data = JsonUtility.FromJson<MPOHornoPayload>(msg);
                if (data != null)
                {
                    OnHornoUpdateEvent?.Invoke(data);
                }
            }
            catch (Exception ex) { Debug.LogWarning("Error al parsear horno: " + ex.Message); }
        }
        else if (topic == "f/mpo/turntable")
        {
            try
            {
                MPOTurntablePayload data = JsonUtility.FromJson<MPOTurntablePayload>(msg);
                if (data != null)
                {
                    OnTurntableUpdateEvent?.Invoke(data);
                }
            }
            catch (Exception ex) { Debug.LogWarning("Error al parsear turntable: " + ex.Message); }
        }
    }

    public string GetLastHBWStatus() => lastHBWJson;

    private void OnApplicationQuit()
    {
        if (client != null && client.IsConnected) client.Disconnect();
    }
}