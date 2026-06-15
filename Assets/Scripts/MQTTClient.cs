using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

[Serializable] public class HBWStockPayload { public string[] piezas; }
[Serializable] public class VGRPositionData { public float estirar; public float rotacion; public float vertical; }
[Serializable] public class HBWPositionPayload { public float estirar; public float horizontal; public float vertical; }
[Serializable] public class HBWBeltPayload { public float cintaHBWspeed; public string sentidoGiro; }

[Serializable]
public class SLDBeltPayload
{
    public float velocidad;
    public int SensorEntrada;
    public int SensorCilindros;
    public string z_ts;
}

// 🔥 NUEVO: Payload estructurado para recibir el JSON de f/mpo/belt
[Serializable]
public class MPOBeltPayload
{
    public int estado;
    public int sensorSalida;
    public string z_ts;
}

[Serializable]
public class MPOHornoPayload
{
    public int closeDoor;
    public int openDoor;
    public int lights;
    public int move2Ref5;
    public int move2Ref6;
    public string ts;
}

[Serializable]
public class MPOTurntablePayload
{
    public int eject;
    public int move2Ref10;
    public int move2Ref7;
    public int move2Ref9;
    public int saw;
    public int rotation;
    public string ts;
}

[Serializable]
public class MPOBrazoPayload
{
    public int move2Ref3;
    public int move2Ref4;
    public int pickup;
    public int release;
    public string ts;
}

[Serializable]
public class SSCLEDsPayload
{
    public int LED_online;
    public int LEDs;
}

[Serializable]
public class SSCCamaraPayload
{
    public float pan;
    public float tilt;
    public string ts;
}

public class MQTTClient : MonoBehaviour
{
    private static MQTTClient instance;
    public static MQTTClient Instance { get { return instance; } }

    private MqttClient client;
    private string lastHBWJson = "";

    [Header("Configuración del Broker")]
    public string brokerHost = "4ca80baa3731405580bfa27dc37e6665.s1.eu.hivemq.cloud";
    public int puerto = 8883;

    [Header("Credenciales")]
    public string usuario = "LearningFactory";
    public string contrasena = "Fischertechnik1";

    // --- EVENTOS (Delegados) ---
    public delegate void OnBeltUpdate(SLDBeltPayload data);
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

    // 🔥 MODIFICADO: Ahora el delegado transmite el objeto estructurado MPOBeltPayload
    public delegate void OnMPOBeltUpdate(MPOBeltPayload data);
    public event OnMPOBeltUpdate OnMPOBeltUpdateEvent;

    public delegate void OnBrazoUpdate(MPOBrazoPayload data);
    public event OnBrazoUpdate OnBrazoUpdateEvent;

    public delegate void OnSSCLEDsUpdate(int ledOnline, int ledsValor);
    public event OnSSCLEDsUpdate OnSSCLEDsUpdateEvent;

    public delegate void OnSSCCamaraUpdate(float pan, float tilt);
    public event OnSSCCamaraUpdate OnSSCCamaraUpdateEvent;

    public Queue<MPOTurntablePayload> colaMensajes = new Queue<MPOTurntablePayload>();

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
            client = new MqttClient(brokerHost, puerto, false, null, null, MqttSslProtocols.None);
            client.MqttMsgPublishReceived += OnMessageReceived;
            client.Connect(Guid.NewGuid().ToString(), usuario, contrasena);

            if (client.IsConnected)
            {
                Debug.Log("<color=green><b>MQTT Conectado</b></color>");

                string[] topics = { "f/sld/belt", "f/sld/cylinder", "f/dps/piezaDSI", "f/dps/color", "f/vgr/grip", "f/pieces_hbw",
                    "f/pos_vgr", "f/pos_hbw", "f/hbw/cinta", "f/mpo/horno", "f/mpo/turntable", "f/mpo/belt", "f/mpo/brazo", "f/ssc/LEDs", "f/ssc/camara" };

                byte[] qos = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
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
            try
            {
                SLDBeltPayload data = JsonUtility.FromJson<SLDBeltPayload>(msg);
                if (data != null) OnBeltUpdateEvent?.Invoke(data);
            }
            catch (Exception ex) { Debug.LogWarning("Error al parsear JSON de f/sld/belt: " + ex.Message); }
        }
        else if (topic == "f/sld/cylinder")
        {
            string[] partes = msg.Split(',');
            if (partes.Length == 2 && int.TryParse(partes[1], out int state))
                OnCylinderUpdateEvent?.Invoke(partes[0].ToUpper(), state);
        }
        else if (topic == "f/dps/piezaDSI")
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
        else if (topic == "f/pieces_hbw")
        {
            lastHBWJson = msg;
            try
            {
                HBWStockPayload data = JsonUtility.FromJson<HBWStockPayload>(msg);
                OnHBWUpdatePiecesEvent?.Invoke(data.piezas);
            }
            catch { Debug.LogWarning("Error al parsear piezas HBW"); }
        }
        else if (topic == "f/pos_vgr")
        {
            try
            {
                VGRPositionData data = JsonUtility.FromJson<VGRPositionData>(msg);
                OnVGRPositionUpdateEvent?.Invoke(data.rotacion, data.vertical, data.estirar);
            }
            catch { Debug.LogWarning("Error en JSON VGR"); }
        }
        else if (topic == "f/pos_hbw")
        {
            try
            {
                HBWPositionPayload data = JsonUtility.FromJson<HBWPositionPayload>(msg);
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
                if (data != null) OnHornoUpdateEvent?.Invoke(data);
            }
            catch (Exception ex) { Debug.LogWarning("Error al parsear horno: " + ex.Message); }
        }
        else if (topic == "f/mpo/turntable")
        {
            try
            {
                var data = JsonUtility.FromJson<MPOTurntablePayload>(Encoding.UTF8.GetString(e.Message));
                lock (colaMensajes)
                {
                    colaMensajes.Enqueue(data);
                }
            }
            catch (Exception ex) { Debug.LogWarning("Error al parsear turntable: " + ex.Message); }
        }
        // 🔥 MODIFICADO: Procesamiento del nuevo formato JSON de f/mpo/belt
        else if (topic == "f/mpo/belt")
        {
            try
            {
                MPOBeltPayload data = JsonUtility.FromJson<MPOBeltPayload>(msg);
                if (data != null)
                {
                    OnMPOBeltUpdateEvent?.Invoke(data);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Error al parsear JSON de f/mpo/belt: " + ex.Message);
            }
        }
        else if (topic == "f/mpo/brazo")
        {
            try
            {
                MPOBrazoPayload data = JsonUtility.FromJson<MPOBrazoPayload>(Encoding.UTF8.GetString(e.Message));
                if (data != null) OnBrazoUpdateEvent?.Invoke(data);
            }
            catch (Exception ex) { Debug.LogWarning("Error al parsear brazo MPO: " + ex.Message); }
        }
        else if (topic == "f/ssc/LEDs")
        {
            try
            {
                SSCLEDsPayload data = JsonUtility.FromJson<SSCLEDsPayload>(Encoding.UTF8.GetString(e.Message));
                if (data != null) OnSSCLEDsUpdateEvent?.Invoke(data.LED_online, data.LEDs);
            }
            catch (Exception ex) { Debug.LogWarning("Error al parsear f/ssc/LEDs: " + ex.Message); }
        }
        else if (topic == "f/ssc/camara")
        {
            try
            {
                SSCCamaraPayload data = JsonUtility.FromJson<SSCCamaraPayload>(Encoding.UTF8.GetString(e.Message));
                if (data != null) OnSSCCamaraUpdateEvent?.Invoke(data.pan, data.tilt);
            }
            catch (Exception ex) { Debug.LogWarning("Error al parsear f/ssc/camara: " + ex.Message); }
        }
    }

    public string GetLastHBWStatus() => lastHBWJson;

    private void OnApplicationQuit()
    {
        if (client != null && client.IsConnected) client.Disconnect();
    }
}