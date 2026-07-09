using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

// =================================================================
// 1. ESTRUCTURAS DE COMPATIBILIDAD (Para no romper tus controladores)
// =================================================================
[Serializable] public class HBWStockPayload { public string[] piezas; }
[Serializable] public class VGRPositionData { public float rotation; public float vertical; public float extend; public string ts; }
[Serializable] public class VGRGripPayload { public bool active; public string ts; }
[Serializable] public class HBWPositionPayload { public float horizontal; public float vertical; public float extend; public string ts; }
[Serializable] public class HBWBeltPayload { public float cintaHBWspeed; public string sentidoGiro; }

[Serializable]
public class SLDBeltPayload
{
    public float velocidad;
    public int SensorEntrada;
    public int SensorCilindros;
    public string z_ts;
}

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

[Serializable] public class SSCLEDsPayload { public int LED_online; public int LEDs; }
[Serializable] public class SSCCamaraPayload { public float pan; public float tilt; public string ts; }

// =================================================================
// 2. NUEVAS ESTRUCTURAS INTERNAS DE RED (Mapean los nuevos JSON)
// =================================================================
[Serializable] internal class JSON_DPSSensor { public bool dsi_sensor; public bool dso_sensor; }
[Serializable] internal class JSON_DPSColor { public string color; }
[Serializable] internal class JSON_SLDBelt { public bool cylinder_sensor; public bool entry_sensor; public float speed; public string ts; }
[Serializable] internal class JSON_SLDCylinder { public string cyl_color; public bool active; }
[Serializable] internal class JSON_MPOBelt { public bool active; public bool exit_sensor; public string ts; }
[Serializable] internal class JSON_MPOOven { public bool oven_sensor; public bool close_door; public bool lights; public bool move2Ref5; public bool move2Ref6; public bool open_door; public string ts; }
[Serializable] internal class JSON_MPOArm { public bool move2Ref3; public bool move2Ref4; public bool pickup; public bool release; public string ts; }
[Serializable] internal class JSON_MPOTurntable { public bool eject; public bool move2Ref7; public bool move2Ref9; public bool move2Ref10; public int rotation; public int saw; public string ts; }
[Serializable] internal class JSON_SSCLEDs { public int led_online; public int leds_semaphore; }
[Serializable] internal class JSON_SSCCamera { public float pan; public float tilt; }
[Serializable] internal class JSON_HBWStock { public string[] stock; }
[Serializable] internal class JSON_HBWBelt { public string ts; public float belt_speed; public bool isTrigeredIn; public bool isTriggeredOut; public string rot_direction; }

// --- NUEVAS ESTRUCTURAS PARA EL NUEVO ALMACÉN (f/i/stock) ---
[Serializable] internal class JSON_Workpiece { public string id; public string type; public string state; }
[Serializable] internal class JSON_StockItem { public string location; public JSON_Workpiece workpiece; }
[Serializable] internal class JSON_FullStock { public JSON_StockItem[] stockItems; public string ts; }


public class MQTTClient : MonoBehaviour
{
    private static MQTTClient instance;
    public static MQTTClient Instance { get { return instance; } }

    private MqttClient client;
    private string lastHBWJson = "";
    private string[] initialStock = null; // Guarda el primer almacén procesado que llegue de la red

    [Header("Configuración del Broker")]
    public string brokerHost = "4ca80baa3731405580bfa27dc37e6665.s1.eu.hivemq.cloud";
    public int puerto = 8883;

    [Header("Credenciales")]
    public string usuario = "LearningFactory";
    public string contrasena = "Fischertechnik1";

    // --- EVENTOS MANTENIDOS INTACTOS ---
    public delegate void OnSLDBeltUpdate(SLDBeltPayload data);
    public event OnSLDBeltUpdate OnBeltUpdateEvent;

    public delegate void OnCylinderUpdate(string color, int state);
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

                string[] topics = {
                    "dt/sld/belt", "dt/sld/cylinder", "dt/dps/dsi", "dt/dps/dso", "dt/dps/color", "dt/vgr/grip",
                    "f/i/stock", "dt/vgr/pos", "dt/hbw/pos", "dt/hbw/belt", "dt/mpo/oven", "dt/mpo/turntable",
                    "dt/mpo/belt", "dt/mpo/arm", "dt/ssc/leds", "dt/ssc/camera"
                };

                byte[] qos = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                client.Subscribe(topics, qos);
            }
        }
        catch (Exception ex) { Debug.LogError("Error MQTT: " + ex.Message); }
    }

    private void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        string msg = Encoding.UTF8.GetString(e.Message).Trim().Replace("True", "true").Replace("False", "false");
        string topic = e.Topic;

        // --- ESTACIÓN SLD ---
        if (topic == "dt/sld/belt")
        {
            try
            {
                JSON_SLDBelt netData = JsonUtility.FromJson<JSON_SLDBelt>(msg);
                SLDBeltPayload legacyData = new SLDBeltPayload
                {
                    velocidad = netData.speed,
                    SensorEntrada = netData.entry_sensor ? 1 : 0,
                    SensorCilindros = netData.cylinder_sensor ? 1 : 0,
                    z_ts = netData.ts
                };
                OnBeltUpdateEvent?.Invoke(legacyData);
            }
            catch (Exception ex) { Debug.LogWarning("Error en dt/sld/belt: " + ex.Message); }
        }
        else if (topic == "dt/sld/cylinder")
        {
            try
            {
                JSON_SLDCylinder data = JsonUtility.FromJson<JSON_SLDCylinder>(msg);
                OnCylinderUpdateEvent?.Invoke(data.cyl_color.ToUpper(), data.active ? 1 : 0);
            }
            catch (Exception ex) { Debug.LogWarning("Error en dt/sld/cylinder: " + ex.Message); }
        }

        // --- ESTACIÓN DPS ---
        else if (topic == "dt/dps/dsi")
        {
            try { OnDPSPiezaDSIEvent?.Invoke(JsonUtility.FromJson<JSON_DPSSensor>(msg).dsi_sensor); } catch { }
        }
        else if (topic == "dt/dps/dso")
        {
            try { OnDPSPiezaDSOEvent?.Invoke(JsonUtility.FromJson<JSON_DPSSensor>(msg).dso_sensor); } catch { }
        }
        else if (topic == "dt/dps/color")
        {
            try { OnDPSColorEvent?.Invoke(JsonUtility.FromJson<JSON_DPSColor>(msg).color.ToUpper()); } catch { }
        }

        // --- ESTACIÓN VGR ---
        else if (topic == "dt/vgr/grip")
        {
            try { OnVGRGripEvent?.Invoke(JsonUtility.FromJson<VGRGripPayload>(msg).active); } catch { }
        }
        else if (topic == "dt/vgr/pos")
        {
            try { var data = JsonUtility.FromJson<VGRPositionData>(msg); OnVGRPositionUpdateEvent?.Invoke(data.rotation, data.vertical, data.extend); } catch { }
        }

        // --- NUEVO PROCESAMIENTO ALMACÉN HBW (f/i/stock) ---
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
                        if (string.IsNullOrEmpty(item.location) || item.location.Length < 2) continue;

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

                    // 1. Guardamos el stock en memoria RAM por si el script visual pregunta antes de tiempo
                    initialStock = flatStock;

                    // 2. Disparamos el evento C# hacia el controlador visual
                    OnHBWUpdatePiecesEvent?.Invoke(flatStock);

                    // 3. SE CORTA EL CANAL: Le ordenamos al broker MQTT dejar de enviarnos este topic para siempre
                    client.Unsubscribe(new string[] { "f/i/stock" });
                    Debug.Log("<color=cyan><b>[MQTT] Primer f/i/stock procesado correctamente. Canal de red cerrado (Unsubscribed).</b></color>");
                }
            }
            catch (Exception ex) { Debug.LogWarning("Error procesando f/i/stock: " + ex.Message); }
        }
        else if (topic == "dt/hbw/pos")
        {
            try { var data = JsonUtility.FromJson<HBWPositionPayload>(msg); OnHBWPositionUpdateEvent?.Invoke(data.horizontal, data.vertical, data.extend); } catch { }
        }
        else if (topic == "dt/hbw/belt")
        {
            try
            {
                JSON_HBWBelt netData = JsonUtility.FromJson<JSON_HBWBelt>(msg);
                OnBeltHBWUpdateEvent?.Invoke(netData.belt_speed, netData.rot_direction);
            }
            catch (Exception ex) { Debug.LogWarning("Error al procesar dt/hbw/belt: " + ex.Message); }
        }

        // --- ESTACIÓN MPO ---
        else if (topic == "dt/mpo/oven")
        {
            try
            {
                JSON_MPOOven netData = JsonUtility.FromJson<JSON_MPOOven>(msg);
                MPOHornoPayload legacyData = new MPOHornoPayload
                {
                    closeDoor = netData.close_door ? 1 : 0,
                    openDoor = netData.open_door ? 1 : 0,
                    lights = netData.lights ? 1 : 0,
                    move2Ref5 = netData.move2Ref5 ? 1 : 0,
                    move2Ref6 = netData.move2Ref6 ? 1 : 0,
                    ts = netData.ts
                };
                OnHornoUpdateEvent?.Invoke(legacyData);
            }
            catch (Exception ex) { Debug.LogWarning("Error en dt/mpo/oven: " + ex.Message); }
        }
        else if (topic == "dt/mpo/turntable")
        {
            try
            {
                JSON_MPOTurntable netData = JsonUtility.FromJson<JSON_MPOTurntable>(msg);
                MPOTurntablePayload legacyData = new MPOTurntablePayload
                {
                    eject = netData.eject ? 1 : 0,
                    move2Ref7 = netData.move2Ref7 ? 1 : 0,
                    move2Ref9 = netData.move2Ref9 ? 1 : 0,
                    move2Ref10 = netData.move2Ref10 ? 1 : 0,
                    rotation = netData.rotation,
                    saw = netData.saw,
                    ts = netData.ts
                };
                lock (colaMensajes) { colaMensajes.Enqueue(legacyData); }
            }
            catch (Exception ex) { Debug.LogWarning("Error en dt/mpo/turntable: " + ex.Message); }
        }
        else if (topic == "dt/mpo/belt")
        {
            try
            {
                JSON_MPOBelt netData = JsonUtility.FromJson<JSON_MPOBelt>(msg);
                MPOBeltPayload legacyData = new MPOBeltPayload
                {
                    estado = netData.active ? 1 : 0,
                    sensorSalida = netData.exit_sensor ? 1 : 0,
                    z_ts = netData.ts
                };
                OnMPOBeltUpdateEvent?.Invoke(legacyData);
            }
            catch (Exception ex) { Debug.LogWarning("Error en dt/mpo/belt: " + ex.Message); }
        }
        else if (topic == "dt/mpo/arm")
        {
            try
            {
                JSON_MPOArm netData = JsonUtility.FromJson<JSON_MPOArm>(msg);
                MPOBrazoPayload legacyData = new MPOBrazoPayload
                {
                    move2Ref3 = netData.move2Ref3 ? 1 : 0,
                    move2Ref4 = netData.move2Ref4 ? 1 : 0,
                    pickup = netData.pickup ? 1 : 0,
                    release = netData.release ? 1 : 0,
                    ts = netData.ts
                };
                OnBrazoUpdateEvent?.Invoke(legacyData);
            }
            catch (Exception ex) { Debug.LogWarning("Error en dt/mpo/arm: " + ex.Message); }
        }

        // --- ESTACIÓN SSC ---
        else if (topic == "dt/ssc/leds")
        {
            try
            {
                JSON_SSCLEDs data = JsonUtility.FromJson<JSON_SSCLEDs>(msg);
                OnSSCLEDsUpdateEvent?.Invoke(data.led_online, data.leds_semaphore);
            }
            catch (Exception ex) { Debug.LogWarning("Error en dt/ssc/leds: " + ex.Message); }
        }
        else if (topic == "dt/ssc/camera")
        {
            try
            {
                JSON_SSCCamera data = JsonUtility.FromJson<JSON_SSCCamera>(msg);
                OnSSCCamaraUpdateEvent?.Invoke(data.pan, data.tilt);
            }
            catch (Exception ex) { Debug.LogWarning("Error en dt/ssc/camera: " + ex.Message); }
        }
    }

    public string GetLastHBWStatus() => lastHBWJson;

    // Método simple para que el controlador verifique si los datos ya entraron por caché
    public string[] GetInitialStock() => initialStock;

    private void OnApplicationQuit()
    {
        if (client != null && client.IsConnected) client.Disconnect();
    }
}