using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

[Serializable] public class HBWStockPayload { public string[] piezas; }
[Serializable] public class VGRPositionData { public float rotation; public float vertical; public float extend; public string ts; }
[Serializable] public class VGRGripPayload { public bool active; public string ts; }
[Serializable] public class HBWPositionPayload { public float horizontal; public float vertical; public float extend; public string ts; }
[Serializable] public class HBWBeltPayload { public float cintaHBWspeed; public string sentidoGiro; }

[Serializable] public class SLDBeltPayload { public float velocidad; public int SensorEntrada; public int SensorCilindros; public string z_ts; }
[Serializable] public class MPOBeltPayload { public int estado; public int sensorSalida; public string z_ts; }
[Serializable] public class MPOHornoPayload { public int closeDoor; public int openDoor; public int lights; public int move2Ref5; public int move2Ref6; public int ovenSensor; public string ts; }
[Serializable] public class MPOTurntablePayload { public int eject; public int move2Ref7; public int move2Ref8; public int move2Ref9; public int move2Ref10; public int saw; public int rotation; public string ts; }
[Serializable] public class MPOBrazoPayload { public bool move2Ref3; public bool move2Ref4; public bool lowering; public bool vacuum; public string ts; }
[Serializable] public class SSCLEDsPayload { public int LED_online; public int LEDs; }
[Serializable] public class SSCCamaraPayload { public float pan; public float tilt; public string ts; }

[Serializable] public class JSON_DPSSensor { public bool dsi_sensor; public bool dso_sensor; }
[Serializable] public class JSON_DPSColor { public string color; }
[Serializable] public class JSON_SLDBelt { public bool cylinder_sensor; public bool entry_sensor; public float speed; public string ts; }

[Serializable]
public class JSON_SLDCylinder
{
    public string cyl_color;
    public bool active;
    public bool is_white;
    public bool is_red;
    public bool is_blue;
    public string ts;
}

[Serializable] public class JSON_MPOBelt { public bool active; public bool exit_sensor; public string ts; }
[Serializable] public class JSON_MPOOven { public bool oven_sensor; public bool close_door; public bool lights; public bool move2Ref5; public bool move2Ref6; public bool open_door; public string ts; }
[Serializable] public class JSON_MPOTurntable { public bool eject; public bool move2Ref7; public bool move2Ref8; public bool move2Ref9; public bool move2Ref10; public int rotation; public int saw; public string ts; }
[Serializable] public class JSON_SSCLEDs { public int led_online; public int leds_semaphore; }
[Serializable] public class JSON_SSCCamera { public float pan; public float tilt; }
[Serializable] public class JSON_HBWStock { public string[] stock; }
[Serializable] public class JSON_HBWBelt { public string ts; public float belt_speed; public bool isTrigeredIn; public bool isTriggeredOut; public string rot_direction; }
[Serializable] public class JSON_MPOArm { public bool move2Ref3; public bool move2Ref4; public bool lowering; public bool vacuum; public string ts; }
[Serializable] public class JSON_Workpiece { public string id; public string type; public string state; }
[Serializable] public class JSON_StockItem { public string location; public JSON_Workpiece workpiece; }
[Serializable] public class JSON_FullStock { public JSON_StockItem[] stockItems; public string ts; }

// Payload Heartbeat Fábrica
[Serializable] public class JSON_FactoryHeartbeat { public bool connected; public string ts; }

public class MQTTClient : MonoBehaviour
{
    private static MQTTClient instance;
    public static MQTTClient Instance { get { return instance; } }

    private MqttClient client;
    private string lastHBWJson = "";
    private string[] initialStock = null;

    private volatile bool estaActivo = true;

    public bool IsConnected => client != null && client.IsConnected;

    private struct MensajeMQTT
    {
        public string topic;
        public string payload;
    }
    private Queue<MensajeMQTT> colaMensajesRed = new Queue<MensajeMQTT>();
    private readonly object lockCola = new object();

    [Header("Configuración del Broker")]
    public string brokerHost = "4ca80baa3731405580bfa27dc37e6665.s1.eu.hivemq.cloud";
    public int puerto = 8883;
    public string usuario = "LearningFactory";
    public string contrasena = "Fischertechnik1";

    public delegate void OnSLDBeltUpdate(SLDBeltPayload data);
    public event OnSLDBeltUpdate OnBeltUpdateEvent;

    public delegate void OnCylinderUpdate(JSON_SLDCylinder data);
    public event OnCylinderUpdate OnCylinderUpdateEvent;

    public delegate void OnDPSPiezaDSIUpdate(bool detectada);
    public event OnDPSPiezaDSIUpdate OnDPSPiezaDSIEvent;
    public delegate void OnDPSPiezaDSOUpdate(bool detectada);
    public event OnDPSPiezaDSOUpdate OnDPSPiezaDSOEvent;
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
    public delegate void OnMPOBeltUpdate(MPOBeltPayload data);
    public event OnMPOBeltUpdate OnMPOBeltUpdateEvent;
    public delegate void OnBrazoUpdate(MPOBrazoPayload data);
    public event OnBrazoUpdate OnBrazoUpdateEvent;
    public delegate void OnSSCLEDsUpdate(int ledOnline, int ledsValor);
    public event OnSSCLEDsUpdate OnSSCLEDsUpdateEvent;
    public delegate void OnSSCCamaraUpdate(float pan, float tilt);
    public event OnSSCCamaraUpdate OnSSCCamaraUpdateEvent;

    // Evento para Heartbeat enviando estado y timestamp parseado
    public delegate void OnFactoryHeartbeatUpdate(bool connected, DateTime timestamp);
    public event OnFactoryHeartbeatUpdate OnFactoryHeartbeatEvent;

    public Queue<MPOTurntablePayload> colaMensajes = new Queue<MPOTurntablePayload>();

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }
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
        List<MensajeMQTT> copiaMensajes = null;

        lock (lockCola)
        {
            if (colaMensajesRed.Count > 0)
            {
                copiaMensajes = new List<MensajeMQTT>(colaMensajesRed);
                colaMensajesRed.Clear();
            }
        }

        if (copiaMensajes != null)
        {
            foreach (var msg in copiaMensajes)
            {
                ProcesarMensajeExterno(msg.topic, msg.payload);
            }
        }
    }

    public void Connect()
    {
        try
        {
            if (client != null && client.IsConnected) return;

            bool usarSSL = (puerto == 8883);

            if (usarSSL)
            {
                System.Net.ServicePointManager.ServerCertificateValidationCallback = RemoteCertificateValidationHandler;
                client = new MqttClient(brokerHost, puerto, true, null, null, MqttSslProtocols.TLSv1_2, RemoteCertificateValidationHandler);
            }
            else
            {
                client = new MqttClient(brokerHost, puerto, false, null, null, MqttSslProtocols.None);
            }

            client.MqttMsgPublishReceived += OnMessageReceived;
            string clientId = "Unity_Directo_" + UnityEngine.Random.Range(1000, 9999);
            client.Connect(clientId, usuario, contrasena);

            if (client.IsConnected)
            {
                Debug.Log($"<color=green><b>[MQTT Directo] ¡CONECTADO CON ÉXITO! a {brokerHost}:{puerto}</b></color>");

                string[] topics = {
                    "dt/sld/belt", "dt/sld/cylinder", "dt/dps/dsi", "dt/dps/dso", "dt/dps/color", "dt/vgr/grip",
                    "f/i/stock", "dt/vgr/pos", "dt/hbw/pos", "dt/hbw/belt", "dt/mpo/oven", "dt/mpo/turntable",
                    "dt/mpo/belt", "dt/mpo/arm", "dt/ssc/leds", "dt/ssc/camera", "dt/factory"
                };

                byte[] qos = new byte[topics.Length];
                client.Subscribe(topics, qos);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("❌ [MQTT Directo] Error al conectar: " + ex.Message);
        }
    }

    private bool RemoteCertificateValidationHandler(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        return true;
    }

    private void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        if (!estaActivo) return;

        string msg = Encoding.UTF8.GetString(e.Message).Trim();

        lock (lockCola)
        {
            colaMensajesRed.Enqueue(new MensajeMQTT { topic = e.Topic, payload = msg });
        }
    }

    public void ProcesarMensajeExterno(string topic, string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        msg = msg.Replace("True", "true").Replace("False", "false");

        if (topic == "dt/factory")
        {
            try
            {
                JSON_FactoryHeartbeat data = JsonUtility.FromJson<JSON_FactoryHeartbeat>(msg);
                if (data != null)
                {
                    DateTime tsParsed = DateTime.UtcNow;
                    if (!string.IsNullOrEmpty(data.ts))
                    {
                        DateTime.TryParse(data.ts, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out tsParsed);
                    }
                    OnFactoryHeartbeatEvent?.Invoke(data.connected, tsParsed);
                }
            }
            catch { }
        }
        else if (topic == "dt/sld/belt")
        {
            try
            {
                JSON_SLDBelt netData = JsonUtility.FromJson<JSON_SLDBelt>(msg);
                if (netData != null)
                {
                    SLDBeltPayload legacyData = new SLDBeltPayload
                    {
                        velocidad = netData.speed,
                        SensorEntrada = netData.entry_sensor ? 1 : 0,
                        SensorCilindros = netData.cylinder_sensor ? 1 : 0,
                        z_ts = netData.ts
                    };
                    OnBeltUpdateEvent?.Invoke(legacyData);
                }
            }
            catch { }
        }
        else if (topic == "dt/sld/cylinder")
        {
            try
            {
                JSON_SLDCylinder data = JsonUtility.FromJson<JSON_SLDCylinder>(msg);
                if (data != null) OnCylinderUpdateEvent?.Invoke(data);
            }
            catch { }
        }
        else if (topic == "dt/dps/dsi")
        {
            try
            {
                var data = JsonUtility.FromJson<JSON_DPSSensor>(msg);
                if (data != null) OnDPSPiezaDSIEvent?.Invoke(data.dsi_sensor);
            }
            catch { }
        }
        else if (topic == "dt/dps/dso")
        {
            try
            {
                var data = JsonUtility.FromJson<JSON_DPSSensor>(msg);
                if (data != null) OnDPSPiezaDSOEvent?.Invoke(data.dso_sensor);
            }
            catch { }
        }
        else if (topic == "dt/dps/color")
        {
            try
            {
                var data = JsonUtility.FromJson<JSON_DPSColor>(msg);
                if (data != null && !string.IsNullOrEmpty(data.color)) OnDPSColorEvent?.Invoke(data.color.ToUpper());
            }
            catch { }
        }
        else if (topic == "dt/vgr/grip")
        {
            try
            {
                var data = JsonUtility.FromJson<VGRGripPayload>(msg);
                if (data != null) OnVGRGripEvent?.Invoke(data.active);
            }
            catch { }
        }
        else if (topic == "dt/vgr/pos")
        {
            try
            {
                VGRPositionData data = JsonUtility.FromJson<VGRPositionData>(msg);
                if (data != null) OnVGRPositionUpdateEvent?.Invoke(data.rotation, data.vertical, data.extend);
            }
            catch { }
        }
        else if (topic == "f/i/stock")
        {
            lastHBWJson = msg;
            try
            {
                JSON_FullStock data = JsonUtility.FromJson<JSON_FullStock>(msg);
                if (data != null && data.stockItems != null)
                {
                    string[] flatStock = new string[9];
                    for (int i = 0; i < 9; i++) flatStock[i] = "";

                    foreach (var item in data.stockItems)
                    {
                        if (item == null || string.IsNullOrEmpty(item.location) || item.location.Length < 2) continue;

                        int col = char.ToUpper(item.location[0]) - 'A';
                        int row = item.location[1] - '1';

                        if (col >= 0 && col < 3 && row >= 0 && row < 3)
                        {
                            int idx = (row * 3) + col;

                            if (item.workpiece != null && !string.IsNullOrEmpty(item.workpiece.type))
                            {
                                flatStock[idx] = item.workpiece.type.ToUpper();
                            }
                        }
                    }

                    initialStock = flatStock;
                    OnHBWUpdatePiecesEvent?.Invoke(flatStock);
                }
            }
            catch { }
        }
        else if (topic == "dt/hbw/pos")
        {
            try
            {
                HBWPositionPayload data = JsonUtility.FromJson<HBWPositionPayload>(msg);
                if (data != null) OnHBWPositionUpdateEvent?.Invoke(data.horizontal, data.vertical, data.extend);
            }
            catch { }
        }
        else if (topic == "dt/hbw/belt")
        {
            try
            {
                JSON_HBWBelt netData = JsonUtility.FromJson<JSON_HBWBelt>(msg);
                if (netData != null) OnBeltHBWUpdateEvent?.Invoke(netData.belt_speed, netData.rot_direction);
            }
            catch { }
        }
        else if (topic == "dt/mpo/oven")
        {
            try
            {
                JSON_MPOOven netData = JsonUtility.FromJson<JSON_MPOOven>(msg);
                if (netData != null)
                {
                    MPOHornoPayload legacyData = new MPOHornoPayload
                    {
                        closeDoor = netData.close_door ? 1 : 0,
                        openDoor = netData.open_door ? 1 : 0,
                        lights = netData.lights ? 1 : 0,
                        move2Ref5 = netData.move2Ref5 ? 1 : 0,
                        move2Ref6 = netData.move2Ref6 ? 1 : 0,
                        ts = netData.ts,
                        ovenSensor = netData.oven_sensor ? 1 : 0
                    };
                    OnHornoUpdateEvent?.Invoke(legacyData);
                }
            }
            catch { }
        }
        else if (topic == "dt/mpo/turntable")
        {
            try
            {
                JSON_MPOTurntable netData = JsonUtility.FromJson<JSON_MPOTurntable>(msg);
                if (netData != null)
                {
                    MPOTurntablePayload legacyData = new MPOTurntablePayload
                    {
                        eject = netData.eject ? 1 : 0,
                        move2Ref7 = netData.move2Ref7 ? 1 : 0,
                        move2Ref8 = netData.move2Ref8 ? 1 : 0,
                        move2Ref9 = netData.move2Ref9 ? 1 : 0,
                        move2Ref10 = netData.move2Ref10 ? 1 : 0,
                        rotation = netData.rotation,
                        saw = netData.saw,
                        ts = netData.ts
                    };
                    lock (colaMensajes) { colaMensajes.Enqueue(legacyData); }
                }
            }
            catch { }
        }
        else if (topic == "dt/mpo/belt")
        {
            try
            {
                JSON_MPOBelt netData = JsonUtility.FromJson<JSON_MPOBelt>(msg);
                if (netData != null)
                {
                    MPOBeltPayload legacyData = new MPOBeltPayload
                    {
                        estado = netData.active ? 1 : 0,
                        sensorSalida = netData.exit_sensor ? 1 : 0,
                        z_ts = netData.ts
                    };
                    OnMPOBeltUpdateEvent?.Invoke(legacyData);
                }
            }
            catch { }
        }
        else if (topic == "dt/mpo/arm")
        {
            try
            {
                JSON_MPOArm netData = JsonUtility.FromJson<JSON_MPOArm>(msg);
                if (netData != null)
                {
                    MPOBrazoPayload legacyData = new MPOBrazoPayload
                    {
                        move2Ref3 = netData.move2Ref3,
                        move2Ref4 = netData.move2Ref4,
                        lowering = netData.lowering,
                        vacuum = netData.vacuum,
                        ts = netData.ts
                    };
                    OnBrazoUpdateEvent?.Invoke(legacyData);
                }
            }
            catch { }
        }
        else if (topic == "dt/ssc/leds")
        {
            try
            {
                JSON_SSCLEDs data = JsonUtility.FromJson<JSON_SSCLEDs>(msg);
                if (data != null) OnSSCLEDsUpdateEvent?.Invoke(data.led_online, data.leds_semaphore);
            }
            catch { }
        }
        else if (topic == "dt/ssc/camera")
        {
            try
            {
                JSON_SSCCamera data = JsonUtility.FromJson<JSON_SSCCamera>(msg);
                if (data != null) OnSSCCamaraUpdateEvent?.Invoke(data.pan, data.tilt);
            }
            catch { }
        }
    }

    public string GetLastHBWStatus() => lastHBWJson;
    public string[] GetInitialStock() => initialStock;

    public void ReemitirUltimoStock()
    {
        if (initialStock != null)
        {
            OnHBWUpdatePiecesEvent?.Invoke(initialStock);
        }
        else if (!string.IsNullOrEmpty(lastHBWJson))
        {
            ProcesarMensajeExterno("f/i/stock", lastHBWJson);
        }
    }

    private void OnApplicationQuit()
    {
        if (client != null && client.IsConnected) client.Disconnect();
    }
}