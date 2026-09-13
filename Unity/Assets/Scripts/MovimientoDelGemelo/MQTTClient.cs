using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

// --- CLASES DE DATOS (DTOs) ---
// Todas estas clases son "moldes" que representan la forma exacta de los mensajes JSON
// que viajan por MQTT entre la fábrica real y Unity. Cada estación de la planta
// (HBW, VGR, DPS, MPO, SLD, SSC) tiene su propio tipo de mensaje según lo que mide o mueve.
// [Serializable] le dice a Unity que puede convertir JSON <-> estas clases automáticamente.

[Serializable] public class HBWStockPayload { public string[] piezas; }

// Posición del brazo del VGR (robot de ventosa): rotación, altura y extensión de sus 3 ejes.
[Serializable] public class VGRPositionData { public float rotation; public float vertical; public float extend; public string ts; }

// Indica si la ventosa del VGR está activada (agarrando una pieza) o no.
[Serializable] public class VGRGripPayload { public bool active; public string ts; }

// Posición del carro que se mueve dentro del almacén HBW (horizontal, vertical, extensión hacia el estante).
[Serializable] public class HBWPositionPayload { public float horizontal; public float vertical; public float extend; public string ts; }
[Serializable] public class HBWBeltPayload { public float cintaHBWspeed; public string sentidoGiro; }

// Datos de la cinta de la estación clasificadora SLD (velocidad y qué sensores están activados).
[Serializable] public class SLDBeltPayload { public float velocidad; public int SensorEntrada; public int SensorCilindros; public string z_ts; }
// Datos de la cinta transportadora del horno/fresadora MPO.
[Serializable] public class MPOBeltPayload { public int estado; public int sensorSalida; public string z_ts; }
// Estado de la puerta y luces del horno de la estación MPO.
[Serializable] public class MPOHornoPayload { public int closeDoor; public int openDoor; public int lights; public int move2Ref5; public int move2Ref6; public int ovenSensor; public string ts; }
// Estado del plato giratorio y la sierra de la estación MPO.
[Serializable] public class MPOTurntablePayload { public int eject; public int move2Ref7; public int move2Ref8; public int move2Ref9; public int move2Ref10; public int saw; public int rotation; public string ts; }
// Estado del pequeño brazo que mueve piezas dentro de la estación MPO.
[Serializable] public class MPOBrazoPayload { public bool move2Ref3; public bool move2Ref4; public bool lowering; public bool vacuum; public string ts; }
// Estado de los LEDs/semáforo de la estación de supervisión SSC.
[Serializable] public class SSCLEDsPayload { public int LED_online; public int LEDs; }
// Ángulos de la cámara Pan-Tilt (giro horizontal y vertical) de la estación SSC.
[Serializable] public class SSCCamaraPayload { public float pan; public float tilt; public string ts; }

// Sensores de entrada (dsi) y salida (dso) de piezas en la estación DPS.
[Serializable] public class JSON_DPSSensor { public bool dsi_sensor; public bool dso_sensor; }
// Color detectado por la cámara/sensor de color en la estación DPS (blanco, rojo, azul).
[Serializable] public class JSON_DPSColor { public string color; }
// Payload "real" (tal como lo envía la planta física) de la cinta de la SLD.
[Serializable] public class JSON_SLDBelt { public bool cylinder_sensor; public bool entry_sensor; public float speed; public string ts; }

// Payload "real" de un pistón clasificador de la SLD: qué color empuja y si está activo.
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
// Payload "real" con el contenido del almacén HBW en formato simple (un array de piezas).
[Serializable] public class JSON_HBWStock { public string[] stock; }
[Serializable] public class JSON_HBWBelt { public string ts; public float belt_speed; public bool isTriggeredIn; public bool isTriggeredOut; public string rot_direction; }
[Serializable] public class JSON_MPOArm { public bool move2Ref3; public bool move2Ref4; public bool lowering; public bool vacuum; public string ts; }
// Representa una pieza física individual (con su identificador NFC, tipo y estado).
[Serializable] public class JSON_Workpiece { public string id; public string type; public string state; }
// Representa un hueco del almacén HBW: en qué posición está y qué pieza contiene (si hay alguna).
[Serializable] public class JSON_StockItem { public string location; public JSON_Workpiece workpiece; }
// Payload "real" y completo del inventario del HBW: la lista de todos los huecos del almacén.
[Serializable] public class JSON_FullStock { public JSON_StockItem[] stockItems; public string ts; }

// Payload Heartbeat Fábrica
[Serializable] public class JSON_FactoryHeartbeat { public bool connected; public string ts; }

/// <summary>
/// Este es el "teléfono" que conecta Unity con la fábrica física real a través de MQTT.
/// Se conecta a un broker (servidor intermediario de mensajes) en la nube, se suscribe a
/// todos los "temas" (topics) que publican las estaciones (HBW, VGR, DPS, MPO, SLD, SSC) y,
/// cada vez que llega un mensaje nuevo, lo traduce de JSON a datos de C# y avisa mediante
/// eventos a los scripts controladores de cada estación (por ejemplo <c>ControladorVGR_mqtt</c>,
/// <c>ControladorCintaHBW_mqtt</c>, etc.) para que muevan las piezas 3D del gemelo digital.
/// También es usado por <c>InfluxDBClient</c> (para reproducir históricos) y por
/// <c>SimuladorOffline</c> (para simular mensajes sin conexión real), ya que ambos llaman
/// a <see cref="ProcesarMensajeExterno(string, string)"/> como si fueran mensajes MQTT reales.
/// </summary>
public class MQTTClient : MonoBehaviour
{
    // Patrón Singleton: solo debe existir un MQTTClient en toda la escena,
    // así cualquier script puede acceder a él escribiendo "MQTTClient.Instance".
    private static MQTTClient instance;
    public static MQTTClient Instance { get { return instance; } }

    private MqttClient client;
    private string lastHBWJson = ""; // Guarda el último JSON de inventario del HBW recibido, por si hace falta reenviarlo.
    private string[] initialStock = null; // Guarda el inventario del HBW ya "aplanado" en un array de 9 posiciones (3x3).

    private volatile bool estaActivo = true; // Se pone a false si el componente se desactiva, para dejar de procesar mensajes.

    // Indica a otros scripts si ahora mismo hay conexión real con el broker MQTT de la fábrica.
    public bool IsConnected => client != null && client.IsConnected;

    // Estructura interna para guardar un mensaje recibido junto con el instante exacto (t1) en el que llegó,
    // dato que se usa para medir la latencia de red en MQTTLatencyLogger.
    private struct MensajeMQTT
    {
        public string topic;
        public string payload;
        public long t1_Recv;
    }
    // Los mensajes MQTT llegan en un hilo aparte (hilo de red), pero Unity solo permite tocar
    // el motor de juego desde el hilo principal. Por eso los mensajes se guardan aquí en una cola
    // y se procesan de verdad en el método Update() (que sí corre en el hilo principal).
    private Queue<MensajeMQTT> colaMensajesRed = new Queue<MensajeMQTT>();
    private readonly object lockCola = new object(); // Candado para que ambos hilos no toquen la cola a la vez.

    // Red de seguridad: en funcionamiento normal Update() vacía esta cola entera cada frame, así que
    // nunca debería acumular más de un puñado de mensajes. Este límite solo actuaría si Unity dejara
    // de llamar a Update() durante mucho tiempo (por ejemplo, una carga de escena larga), descartando
    // los mensajes más antiguos en vez de dejar que la cola crezca sin límite.
    private const int MAX_COLA_MENSAJES_RED = 500;

    [Header("Configuración del Broker")]
    public string brokerHost = "4ca80baa3731405580bfa27dc37e6665.s1.eu.hivemq.cloud";
    public int puerto = 8883;
    public string usuario = "LearningFactory";
    public string contrasena = "Fischertechnik1";

    // --- EVENTOS ---
    // Cada evento representa "algo que ha cambiado en la fábrica real". Los controladores de cada
    // estación se suscriben a estos eventos (con +=) para enterarse en el momento en que llega
    // un dato nuevo y así mover la parte correspondiente del gemelo digital en Unity.
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

    // Cola específica para los mensajes del plato giratorio del MPO: aquí no se usa un evento normal
    // porque su controlador necesita procesarlos uno a uno y en orden (por ejemplo, para no perder
    // ningún giro ni ninguna orden de expulsión de pieza).
    public Queue<MPOTurntablePayload> colaMensajes = new Queue<MPOTurntablePayload>();

    // Red de seguridad equivalente a MAX_COLA_MENSAJES_RED, pero para esta cola específica del turntable.
    private const int MAX_COLA_TURNTABLE = 100;

    void Awake()
    {
        // Aplicamos el patrón Singleton: si ya existe un MQTTClient, este nuevo se destruye
        // para evitar tener dos "teléfonos" abiertos a la vez con la fábrica.
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

    /// <summary>
    /// Corta la conexión con el broker MQTT manualmente (por ejemplo, al cambiar a modo simulación offline).
    /// </summary>
    public void DesconectarRed()
    {
        if (client != null && client.IsConnected)
        {
            try { client.Disconnect(); }
            catch (Exception ex) { Debug.LogWarning($"[MQTTClient] Error al desconectar del broker: {ex.Message}"); }
        }
    }

    /// <summary>
    /// Envía un mensaje "ping" al topic dt/ping. La fábrica (o un puente intermedio) responde
    /// con un "pong" que <see cref="MQTTLatencyLogger"/> usa para calcular cuánto tarda un mensaje
    /// en ir y volver por la red (latencia).
    /// </summary>
    public void PublishPing(string payload)
    {
        if (client != null && client.IsConnected)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(payload);
                client.Publish("dt/ping", bytes, MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, false);
            }
            catch (Exception ex)
            {
                Debug.LogError("Error publicando ping: " + ex.Message);
            }
        }
    }

    // Update se ejecuta una vez por frame en el hilo principal de Unity. Aquí es donde
    // "recogemos" los mensajes que llegaron por red (en otro hilo) y los procesamos de forma segura.
    void Update()
    {
        List<MensajeMQTT> copiaMensajes = null;

        // Copiamos y vaciamos la cola dentro del candado para no bloquear el hilo de red más de lo necesario.
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
            // Procesamos cada mensaje ya fuera del candado, en el hilo principal de Unity.
            foreach (var msg in copiaMensajes)
            {
                ProcesarMensajeExterno(msg.topic, msg.payload, msg.t1_Recv);
            }
        }
    }

    /// <summary>
    /// Abre la conexión real con el broker MQTT de la fábrica (en la nube) y se suscribe a todos
    /// los topics de las estaciones (HBW, VGR, DPS, MPO, SLD, SSC) para empezar a recibir datos en vivo.
    /// </summary>
    public void Connect()
    {
        try
        {
            if (client != null && client.IsConnected) return;

            bool usarSSL = (puerto == 8883);

            if (usarSSL)
            {
                // El puerto 8883 es el estándar para MQTT seguro (con cifrado TLS), como el que usa la nube de HiveMQ.
                System.Net.ServicePointManager.ServerCertificateValidationCallback = RemoteCertificateValidationHandler;
                client = new MqttClient(brokerHost, puerto, true, null, null, MqttSslProtocols.TLSv1_2, RemoteCertificateValidationHandler);
            }
            else
            {
                client = new MqttClient(brokerHost, puerto, false, null, null, MqttSslProtocols.None);
            }

            // Cada vez que llegue un mensaje nuevo del broker, se llamará a OnMessageReceived.
            client.MqttMsgPublishReceived += OnMessageReceived;
            string clientId = "Unity_Directo_" + UnityEngine.Random.Range(1000, 9999);
            client.Connect(clientId, usuario, contrasena);

            if (client.IsConnected)
            {
                Debug.Log($"<color=green><b>[MQTT Directo] ¡CONECTADO CON ÉXITO! a {brokerHost}:{puerto}</b></color>");

                // Lista de todos los "canales" (topics) de la fábrica real a los que Unity necesita escuchar:
                // uno por cada sensor/actuador de cada estación (HBW, VGR, DPS, MPO, SLD, SSC) más el heartbeat
                // general de la fábrica y el canal de pong (respuesta a nuestros pings de latencia).
                string[] topics = {
                    "dt/sld/belt", "dt/sld/cylinder", "dt/dps/dsi", "dt/dps/dso", "dt/dps/color", "dt/vgr/grip",
                    "f/i/stock", "dt/vgr/pos", "dt/hbw/pos", "dt/hbw/belt", "dt/mpo/oven", "dt/mpo/turntable",
                    "dt/mpo/belt", "dt/mpo/arm", "dt/ssc/leds", "dt/ssc/camera", "dt/factory", "dt/pong"
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

    // Aceptamos cualquier certificado SSL del broker (necesario en este proyecto educativo para
    // simplificar la conexión); en un entorno de producción real esto debería validarse correctamente.
    private bool RemoteCertificateValidationHandler(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        return true;
    }

    // Este método lo llama la librería MQTT automáticamente cada vez que llega un mensaje nuevo,
    // pero ojo: se ejecuta en un hilo de red distinto al hilo principal de Unity. Por eso aquí
    // NO procesamos el mensaje directamente, solo lo guardamos en la cola para tratarlo luego en Update().
    private void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        if (!estaActivo) return;

        string msg = Encoding.UTF8.GetString(e.Message).Trim();
        // Anotamos el instante exacto de llegada (t1) para poder medir latencia de red más adelante.
        long t1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        lock (lockCola)
        {
            colaMensajesRed.Enqueue(new MensajeMQTT { topic = e.Topic, payload = msg, t1_Recv = t1 });

            // Si por lo que sea la cola creciera más allá del límite de seguridad, descartamos los
            // mensajes más antiguos: es preferible perder un dato viejo de posición que acumular
            // retraso indefinido respecto a la fábrica real.
            while (colaMensajesRed.Count > MAX_COLA_MENSAJES_RED)
            {
                colaMensajesRed.Dequeue();
            }
        }
    }

    // Sobrecarga de método para mantener compatibilidad con SimuladorOffline e InfluxDBClient
    /// <summary>
    /// Punto de entrada que usan <c>SimuladorOffline</c> e <c>InfluxDBClient</c> para "inyectar"
    /// un mensaje como si viniera de verdad de la fábrica, pero sin pasar por la red real
    /// (por ejemplo, al reproducir un histórico o al simular sin conexión).
    /// </summary>
    public void ProcesarMensajeExterno(string topic, string msg)
    {
        long t1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ProcesarMensajeExterno(topic, msg, t1);
    }

    /// <summary>
    /// El método más importante de este script: recibe un mensaje MQTT en bruto (topic + JSON),
    /// averigua de qué estación viene según el topic, lo convierte (deserializa) al tipo de dato
    /// correspondiente y dispara el evento adecuado para que el controlador de esa estación mueva
    /// la parte del gemelo digital que le corresponde.
    /// </summary>
    /// <param name="topic">Canal MQTT del mensaje (ej. "dt/vgr/pos", "dt/hbw/belt"...), indica de qué estación viene.</param>
    /// <param name="msg">Contenido del mensaje en formato JSON, tal como lo envía la fábrica real.</param>
    /// <param name="t1">Marca de tiempo (en milisegundos) del instante exacto en que se recibió el mensaje, usada para medir latencia.</param>
    public void ProcesarMensajeExterno(string topic, string msg, long t1)
    {
        if (string.IsNullOrEmpty(msg)) return;

        // El topic "dt/pong" es la respuesta de la fábrica a nuestro ping: se delega directamente
        // al medidor de latencia y no se procesa como un dato normal de estación.
        if (topic == "dt/pong")
        {
            MQTTLatencyLogger.Instance?.ProcesarPong(msg, t1);
            return;
        }

        // JsonUtility de Unity es estricto con mayúsculas/minúsculas en "true"/"false",
        // así que normalizamos por si el mensaje llega con la capitalización de C# ("True"/"False").
        msg = msg.Replace("True", "true").Replace("False", "false");

        // Despachamos según el topic con un switch (el compilador de C# lo optimiza a una tabla
        // hash en vez de comparar cadena por cadena una a una): el comportamiento para cada topic
        // es exactamente el mismo que antes, solo cambia cómo se organiza el despacho.
        switch (topic)
        {
            case "dt/factory":
                // Heartbeat general: nos dice si la fábrica física sigue conectada y viva, y cuándo se envió.
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
                catch (Exception ex)
                {
                    // Registramos en la consola de Unity el topic y el motivo del fallo, para poder
                    // diagnosticar problemas de red o de formato de mensaje durante una demo en vivo.
                    Debug.LogWarning($"[MQTTClient] No se pudo procesar el mensaje del topic '{topic}': {ex.Message}");
                }
                break;

            case "dt/sld/belt":
                // Datos de la cinta de la estación clasificadora SLD: convertimos del formato "real"
                // (JSON_SLDBelt) al formato "legado" (SLDBeltPayload) que usan los scripts de Unity.
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
                catch (Exception ex)
                {
                    // Registramos en la consola de Unity el topic y el motivo del fallo, para poder
                    // diagnosticar problemas de red o de formato de mensaje durante una demo en vivo.
                    Debug.LogWarning($"[MQTTClient] No se pudo procesar el mensaje del topic '{topic}': {ex.Message}");
                }
                break;

            case "dt/sld/cylinder":
                // Un pistón de la SLD acaba de empujar (o no) una pieza de un color concreto.
                try
                {
                    JSON_SLDCylinder data = JsonUtility.FromJson<JSON_SLDCylinder>(msg);
                    if (data != null) OnCylinderUpdateEvent?.Invoke(data);
                }
                catch (Exception ex)
                {
                    // Registramos en la consola de Unity el topic y el motivo del fallo, para poder
                    // diagnosticar problemas de red o de formato de mensaje durante una demo en vivo.
                    Debug.LogWarning($"[MQTTClient] No se pudo procesar el mensaje del topic '{topic}': {ex.Message}");
                }
                break;

            case "dt/dps/dsi":
                // Sensor de entrada de piezas (Deposit Sensor In) de la estación DPS.
                try
                {
                    var data = JsonUtility.FromJson<JSON_DPSSensor>(msg);
                    if (data != null) OnDPSPiezaDSIEvent?.Invoke(data.dsi_sensor);
                }
                catch (Exception ex)
                {
                    // Registramos en la consola de Unity el topic y el motivo del fallo, para poder
                    // diagnosticar problemas de red o de formato de mensaje durante una demo en vivo.
                    Debug.LogWarning($"[MQTTClient] No se pudo procesar el mensaje del topic '{topic}': {ex.Message}");
                }
                break;

            case "dt/dps/dso":
                // Sensor de salida de piezas (Deposit Sensor Out) de la estación DPS.
                try
                {
                    var data = JsonUtility.FromJson<JSON_DPSSensor>(msg);
                    if (data != null) OnDPSPiezaDSOEvent?.Invoke(data.dso_sensor);
                }
                catch (Exception ex)
                {
                    // Registramos en la consola de Unity el topic y el motivo del fallo, para poder
                    // diagnosticar problemas de red o de formato de mensaje durante una demo en vivo.
                    Debug.LogWarning($"[MQTTClient] No se pudo procesar el mensaje del topic '{topic}': {ex.Message}");
                }
                break;

            case "dt/dps/color":
                // La cámara/sensor de color de la DPS ha identificado el color de la pieza que entra.
                try
                {
                    var data = JsonUtility.FromJson<JSON_DPSColor>(msg);
                    if (data != null && !string.IsNullOrEmpty(data.color)) OnDPSColorEvent?.Invoke(data.color.ToUpper());
                }
                catch (Exception ex)
                {
                    // Registramos en la consola de Unity el topic y el motivo del fallo, para poder
                    // diagnosticar problemas de red o de formato de mensaje durante una demo en vivo.
                    Debug.LogWarning($"[MQTTClient] No se pudo procesar el mensaje del topic '{topic}': {ex.Message}");
                }
                break;

            case "dt/vgr/grip":
                // La ventosa del robot VGR se ha activado o desactivado (agarra o suelta una pieza).
                try
                {
                    var data = JsonUtility.FromJson<VGRGripPayload>(msg);
                    if (data != null) OnVGRGripEvent?.Invoke(data.active);
                }
                catch (Exception ex)
                {
                    // Registramos en la consola de Unity el topic y el motivo del fallo, para poder
                    // diagnosticar problemas de red o de formato de mensaje durante una demo en vivo.
                    Debug.LogWarning($"[MQTTClient] No se pudo procesar el mensaje del topic '{topic}': {ex.Message}");
                }
                break;

            case "dt/vgr/pos":
                // Nueva posición de los 3 ejes del brazo VGR (rotación, altura, extensión).
                try
                {
                    VGRPositionData data = JsonUtility.FromJson<VGRPositionData>(msg);
                    if (data != null) OnVGRPositionUpdateEvent?.Invoke(data.rotation, data.vertical, data.extend);
                }
                catch (Exception ex)
                {
                    // Registramos en la consola de Unity el topic y el motivo del fallo, para poder
                    // diagnosticar problemas de red o de formato de mensaje durante una demo en vivo.
                    Debug.LogWarning($"[MQTTClient] No se pudo procesar el mensaje del topic '{topic}': {ex.Message}");
                }
                break;

            case "f/i/stock":
                // Inventario completo del almacén HBW. La fábrica lo manda como una lista de "huecos"
                // ocupados (location + pieza), y aquí lo transformamos en un array plano de 9 posiciones
                // (una cuadrícula de 3x3: columnas A-C y filas 1-3) para que sea fácil de usar en Unity.
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

                            // La ubicación llega como texto tipo "A1", "B2", etc: la letra es la columna y el número la fila.
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
                catch (Exception ex)
                {
                    // Registramos en la consola de Unity el topic y el motivo del fallo, para poder
                    // diagnosticar problemas de red o de formato de mensaje durante una demo en vivo.
                    Debug.LogWarning($"[MQTTClient] No se pudo procesar el mensaje del topic '{topic}': {ex.Message}");
                }
                break;

            case "dt/hbw/pos":
                // Nueva posición del carro que se mueve por dentro del almacén HBW.
                try
                {
                    HBWPositionPayload data = JsonUtility.FromJson<HBWPositionPayload>(msg);
                    if (data != null) OnHBWPositionUpdateEvent?.Invoke(data.horizontal, data.vertical, data.extend);
                }
                catch (Exception ex)
                {
                    // Registramos en la consola de Unity el topic y el motivo del fallo, para poder
                    // diagnosticar problemas de red o de formato de mensaje durante una demo en vivo.
                    Debug.LogWarning($"[MQTTClient] No se pudo procesar el mensaje del topic '{topic}': {ex.Message}");
                }
                break;

            case "dt/hbw/belt":
                // Velocidad y dirección de la cinta interna del HBW.
                try
                {
                    JSON_HBWBelt netData = JsonUtility.FromJson<JSON_HBWBelt>(msg);
                    if (netData != null) OnBeltHBWUpdateEvent?.Invoke(netData.belt_speed, netData.rot_direction);
                }
                catch (Exception ex)
                {
                    // Registramos en la consola de Unity el topic y el motivo del fallo, para poder
                    // diagnosticar problemas de red o de formato de mensaje durante una demo en vivo.
                    Debug.LogWarning($"[MQTTClient] No se pudo procesar el mensaje del topic '{topic}': {ex.Message}");
                }
                break;

            case "dt/mpo/oven":
                // Estado del horno de la estación MPO (puertas, luces, sensor de pieza dentro).
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
                catch (Exception ex)
                {
                    // Registramos en la consola de Unity el topic y el motivo del fallo, para poder
                    // diagnosticar problemas de red o de formato de mensaje durante una demo en vivo.
                    Debug.LogWarning($"[MQTTClient] No se pudo procesar el mensaje del topic '{topic}': {ex.Message}");
                }
                break;

            case "dt/mpo/turntable":
                // Estado del plato giratorio y la sierra del MPO. Estos mensajes se meten en una cola
                // (en vez de disparar el evento al momento) porque el controlador del plato necesita
                // procesarlos en el orden exacto en que ocurrieron, sin saltarse ninguno.
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
                        lock (colaMensajes)
                        {
                            colaMensajes.Enqueue(legacyData);

                            // Misma red de seguridad que en colaMensajesRed: en funcionamiento normal el
                            // controlador del plato giratorio vacía esta cola entera cada frame, así que
                            // este límite no debería alcanzarse nunca salvo que el turntable se quede sin
                            // procesar mensajes durante mucho tiempo.
                            while (colaMensajes.Count > MAX_COLA_TURNTABLE)
                            {
                                colaMensajes.Dequeue();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Registramos en la consola de Unity el topic y el motivo del fallo, para poder
                    // diagnosticar problemas de red o de formato de mensaje durante una demo en vivo.
                    Debug.LogWarning($"[MQTTClient] No se pudo procesar el mensaje del topic '{topic}': {ex.Message}");
                }
                break;

            case "dt/mpo/belt":
                // Estado de la cinta transportadora del MPO (activa/parada, sensor de salida de pieza).
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
                catch (Exception ex)
                {
                    // Registramos en la consola de Unity el topic y el motivo del fallo, para poder
                    // diagnosticar problemas de red o de formato de mensaje durante una demo en vivo.
                    Debug.LogWarning($"[MQTTClient] No se pudo procesar el mensaje del topic '{topic}': {ex.Message}");
                }
                break;

            case "dt/mpo/arm":
                // Estado del pequeño brazo interno del MPO que traslada piezas entre cinta, horno y sierra.
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
                catch (Exception ex)
                {
                    // Registramos en la consola de Unity el topic y el motivo del fallo, para poder
                    // diagnosticar problemas de red o de formato de mensaje durante una demo en vivo.
                    Debug.LogWarning($"[MQTTClient] No se pudo procesar el mensaje del topic '{topic}': {ex.Message}");
                }
                break;

            case "dt/ssc/leds":
                // Estado de los LEDs/semáforo de la estación de supervisión SSC.
                try
                {
                    JSON_SSCLEDs data = JsonUtility.FromJson<JSON_SSCLEDs>(msg);
                    if (data != null) OnSSCLEDsUpdateEvent?.Invoke(data.led_online, data.leds_semaphore);
                }
                catch (Exception ex)
                {
                    // Registramos en la consola de Unity el topic y el motivo del fallo, para poder
                    // diagnosticar problemas de red o de formato de mensaje durante una demo en vivo.
                    Debug.LogWarning($"[MQTTClient] No se pudo procesar el mensaje del topic '{topic}': {ex.Message}");
                }
                break;

            case "dt/ssc/camera":
                // Nuevos ángulos de la cámara Pan-Tilt de la estación SSC.
                try
                {
                    JSON_SSCCamera data = JsonUtility.FromJson<JSON_SSCCamera>(msg);
                    if (data != null) OnSSCCamaraUpdateEvent?.Invoke(data.pan, data.tilt);
                }
                catch (Exception ex)
                {
                    // Registramos en la consola de Unity el topic y el motivo del fallo, para poder
                    // diagnosticar problemas de red o de formato de mensaje durante una demo en vivo.
                    Debug.LogWarning($"[MQTTClient] No se pudo procesar el mensaje del topic '{topic}': {ex.Message}");
                }
                break;
        }
    }

    /// <summary>Devuelve el último JSON de inventario del HBW recibido, tal cual llegó de la fábrica.</summary>
    public string GetLastHBWStatus() => lastHBWJson;
    /// <summary>Devuelve el inventario del HBW ya convertido a un array plano de 9 posiciones (cuadrícula 3x3).</summary>
    public string[] GetInitialStock() => initialStock;

    /// <summary>
    /// Vuelve a disparar el evento de inventario del HBW con los últimos datos guardados, sin
    /// esperar a que llegue un mensaje nuevo. Útil cuando un script se suscribe tarde y necesita
    /// "ponerse al día" con el estado actual del almacén.
    /// </summary>
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
        // Cerramos la conexión de forma ordenada al cerrar Unity, para no dejar el socket colgado.
        if (client != null && client.IsConnected) client.Disconnect();
    }
}
