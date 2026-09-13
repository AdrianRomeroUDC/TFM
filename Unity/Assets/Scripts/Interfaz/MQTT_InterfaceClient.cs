using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

// --- CLASES DE DATOS (DTOs) ---
// Igual que en MQTTClient.cs, estas clases son "moldes" para convertir automáticamente
// (con [Serializable] y JsonUtility) el JSON que llega por MQTT en objetos de C#.
// Aquí los mensajes no mueven el gemelo digital 3D: alimentan los paneles de la interfaz
// (gráficas de sensores, imagen de la cámara, resumen del almacén) de la estación SSC y del HBW.

// Datos del sensor ambiental BME680 de la SSC: temperatura (t), humedad (h), presión (p)
// y calidad del aire (iaq = índice de calidad de aire, aq = categoría, gr = resistencia del gas).
[Serializable] public class Bme680Payload { public string ts; public float t; public float h; public float p; public int iaq; public int aq; public float gr; }
// Datos del sensor de luz LDR (fotorresistencia) de la SSC: brillo (br) y valor crudo del sensor (ldr).
[Serializable] public class LdrPayload { public string ts; public float br; public int ldr; }
// Un fotograma de la cámara Pan-Tilt de la SSC, enviado como texto codificado en Base64.
[Serializable] public class CameraPayload { public string ts; public string data; }
// Inventario del almacén HBW tal y como lo usa la interfaz: lista de huecos ocupados.
[Serializable] public class StockPayload { public List<StockItem> stockItems; public string ts; }
// Representa una pieza física individual (con su identificador NFC, tipo y estado).
[Serializable] public class Workpiece { public string id; public string type; public string state; }
// Representa un hueco del almacén HBW: en qué posición está y qué pieza contiene.
[Serializable] public class StockItem { public string location; public Workpiece workpiece; }

// Mensaje que el usuario envía a la fábrica para pedir la fabricación de una pieza de un color.
[Serializable] public class OrderPayload { public string ts; public string type; }
// Comando de movimiento para la cámara Pan-Tilt (girar tantos grados en una dirección).
[Serializable] public class PtuPayload { public string ts; public string cmd; public int degree; }
// Configuración de la cámara: si debe estar encendida y a cuántos fotogramas por segundo (fps).
[Serializable] public class CamConfigPayload { public string ts; public bool on; public int fps; }
// Cada cuántos segundos debe la fábrica enviar una nueva lectura de un sensor (LDR o BME680).
[Serializable] public class SensorPeriodPayload { public string ts; public int period; }
// Comando para llevar la cámara Pan-Tilt a su posición de referencia ("home").
[Serializable] public class PtuHomePayload { public string ts; public string cmd; }

/// <summary>
/// Es el "teléfono" gemelo de <see cref="MQTTClient"/>, pero dedicado exclusivamente a la interfaz
/// de usuario: en vez de mover las piezas 3D del gemelo digital, alimenta las gráficas y paneles de
/// sensores (cámara Pan-Tilt, sensor ambiental BME680, sensor de luz LDR, resumen del almacén HBW).
/// Se conecta a un broker MQTT (por defecto uno en la red local, o el mismo que usa <c>MQTTClient</c>
/// si está disponible), se suscribe a los topics de estos sensores y dispara eventos de C# cada vez
/// que llega un dato nuevo. También permite enviar órdenes desde la interfaz hacia la fábrica
/// (pedir una pieza, mover la cámara, cambiar su configuración). <see cref="InfluxDBClient"/> también
/// usa <see cref="ProcesarMensajeExterno(string, string)"/> para reproducir mensajes históricos como
/// si vinieran en directo de la fábrica.
/// </summary>
public class MQTT_InterfaceClient : MonoBehaviour
{
    // Patrón Singleton: solo debe existir un MQTT_InterfaceClient en la escena,
    // accesible desde cualquier script como "MQTT_InterfaceClient.Instance".
    private static MQTT_InterfaceClient instance;
    public static MQTT_InterfaceClient Instance => instance;

    private MqttClient client;
    private readonly object lockObject = new object(); // Candado para proteger las colas entre el hilo de red y el hilo principal de Unity.

    private volatile bool estaActivo = true; // Se pone a false si el componente se desactiva, para dejar de procesar mensajes.

    // Indica a otros scripts si ahora mismo hay conexión real con el broker MQTT de la interfaz.
    public bool IsConnected => client != null && client.IsConnected;

    // Guarda el último inventario del HBW recibido, para que cualquier panel pueda consultarlo sin esperar a un evento nuevo.
    public StockPayload UltimoStock { get; private set; }

    // Igual que en MQTTClient, los mensajes MQTT llegan en un hilo de red aparte y no pueden tocar
    // Unity directamente. Se guardan en estas colas y se procesan de verdad en Update() (hilo principal).
    private Queue<Bme680Payload> bmeQueue = new Queue<Bme680Payload>();
    private Queue<LdrPayload> ldrQueue = new Queue<LdrPayload>();
    private Queue<string> camQueue = new Queue<string>();
    private Queue<StockPayload> stockQueue = new Queue<StockPayload>();

    // --- EVENTOS ---
    // Los paneles de la interfaz (por ejemplo UI_CameraController) se suscriben a estos eventos
    // con += para enterarse en el momento en que llega un dato nuevo de cada sensor.
    public event Action<Bme680Payload> OnBmeEnvironmentEvent;
    public event Action<LdrPayload> OnLdrLightEvent;
    public event Action<string> OnCameraImageEvent;
    public event Action<StockPayload> OnStockUpdateEvent;

    void Awake()
    {
        // Aplicamos el patrón Singleton: si ya existe un MQTT_InterfaceClient, este nuevo se destruye.
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        // Al arrancar la escena, intentamos conectar automáticamente con el broker de la interfaz.
        Connect();
    }

    /// <summary>
    /// Abre (o reabre) la conexión con el broker MQTT de la interfaz. Si <see cref="MQTTClient"/>
    /// ya está configurado en la escena, reutiliza sus mismos datos de conexión (host, puerto,
    /// usuario y contraseña); si no, usa unos valores por defecto pensados para la red local.
    /// La conexión se hace en un hilo aparte para no congelar Unity mientras se establece.
    /// </summary>
    public void Connect()
    {
        if (client != null && client.IsConnected) return;

        // Valores de conexión por defecto (red local de la fábrica).
        string brokerHost = "10.113.36.36";
        int puerto = 1884;
        string usuario = "LearningFactory";
        string contrasena = "Fischertechnik1";

        // Si el cliente MQTT "principal" (el que mueve el gemelo digital) ya existe y está configurado,
        // reutilizamos exactamente los mismos datos de conexión para no tener que mantenerlos duplicados.
        if (MQTTClient.Instance != null)
        {
            brokerHost = MQTTClient.Instance.brokerHost;
            puerto = MQTTClient.Instance.puerto;
            usuario = MQTTClient.Instance.usuario;
            contrasena = MQTTClient.Instance.contrasena;
        }

        string clientIdShort = "Unity_Interfaz_" + UnityEngine.Random.Range(10000, 99999);

        // Lanzamos la conexión en un hilo de fondo (Task.Run) para no bloquear el hilo principal de Unity.
        Task.Run(() => ConnectAsync(clientIdShort, brokerHost, puerto, usuario, contrasena));
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
    /// Corta la conexión con el broker MQTT de la interfaz manualmente (por ejemplo, al cambiar
    /// a modo simulación offline o histórico, donde no tiene sentido escuchar datos en vivo).
    /// </summary>
    public void DesconectarRed()
    {
        if (client != null && client.IsConnected)
        {
            try { client.Disconnect(); } catch { }
        }
    }

    // Update se ejecuta una vez por frame en el hilo principal de Unity. Aquí es donde
    // "recogemos" los mensajes que llegaron por red (en otro hilo) y los procesamos de forma segura.
    void Update()
    {
        List<Bme680Payload> bmeLista = null;
        List<LdrPayload> ldrLista = null;
        List<StockPayload> stockLista = null;
        List<string> camLista = null;

        // Copiamos y vaciamos cada cola dentro del candado, para no bloquear el hilo de red más de lo necesario.
        lock (lockObject)
        {
            if (bmeQueue.Count > 0) { bmeLista = new List<Bme680Payload>(bmeQueue); bmeQueue.Clear(); }
            if (ldrQueue.Count > 0) { ldrLista = new List<LdrPayload>(ldrQueue); ldrQueue.Clear(); }

            if (stockQueue.Count > 0)
            {
                stockLista = new List<StockPayload>(stockQueue);
                // Guardamos solo el último inventario recibido (el más actualizado) para consultas rápidas.
                UltimoStock = stockLista[stockLista.Count - 1];
                stockQueue.Clear();
            }

            if (camQueue.Count > 0) { camLista = new List<string>(camQueue); camQueue.Clear(); }
        }

        // Ya fuera del candado, disparamos los eventos para que los paneles de la interfaz se actualicen.
        if (bmeLista != null) foreach (var item in bmeLista) OnBmeEnvironmentEvent?.Invoke(item);
        if (ldrLista != null) foreach (var item in ldrLista) OnLdrLightEvent?.Invoke(item);
        if (stockLista != null) foreach (var item in stockLista) OnStockUpdateEvent?.Invoke(item);
        if (camLista != null) foreach (var item in camLista) OnCameraImageEvent?.Invoke(item);
    }

    // Se ejecuta en un hilo de fondo: aquí se crea el cliente MQTT, se conecta al broker
    // y, si todo va bien, se suscribe a los topics de sensores que necesita la interfaz.
    private void ConnectAsync(string clientId, string host, int port, string user, string pass)
    {
        try
        {
            bool usarSSL = (port == 8883);

            if (usarSSL)
            {
                // Puerto 8883 = MQTT seguro con TLS (por ejemplo si se reutiliza el broker en la nube).
                System.Net.ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, errors) => true;
                client = new MqttClient(host, port, true, null, null, MqttSslProtocols.TLSv1_2, (sender, cert, chain, errors) => true);
            }
            else
            {
                // Conexión sin cifrar, típica de un broker dentro de la red local de la fábrica.
                client = new MqttClient(host, port, false, null, null, MqttSslProtocols.None);
            }

            // Cada vez que llegue un mensaje nuevo del broker, se llamará a OnMessageReceived.
            client.MqttMsgPublishReceived += OnMessageReceived;
            client.Connect(clientId, user, pass);

            if (client.IsConnected)
            {
                // Topics de la interfaz: cámara, sensor ambiental BME680, sensor de luz LDR
                // y el inventario del almacén HBW (para mostrarlo en algún panel de resumen).
                string[] topics = { "i/cam", "i/bme680", "i/ldr", "f/i/stock" };
                client.Subscribe(topics, new byte[] { 0, 0, 0, 0 });

                Debug.Log($"<color=green><b>[MQTT Interfaz] ¡CONECTADO CON ÉXITO! ID: {clientId} a {host}:{port}</b></color>");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MQTT Interfaz] Error al conectar: {ex.Message}");
        }
    }

    // Este método lo llama la librería MQTT automáticamente cada vez que llega un mensaje nuevo,
    // pero se ejecuta en un hilo de red distinto al principal de Unity: por eso no procesamos
    // el mensaje aquí directamente, solo lo delegamos a ProcesarMensajeExterno, que lo encola.
    private void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        if (!estaActivo) return;

        string topic = e.Topic;
        string msg = Encoding.UTF8.GetString(e.Message).Trim();

        ProcesarMensajeExterno(topic, msg);
    }

    /// <summary>
    /// Recibe un mensaje MQTT en bruto (topic + JSON), lo convierte al tipo de dato correspondiente
    /// según el topic y lo mete en la cola adecuada para que <see cref="Update"/> dispare el evento
    /// correspondiente en el hilo principal. Este método también lo usa <c>InfluxDBClient</c> para
    /// "inyectar" mensajes históricos como si vinieran en directo de la fábrica.
    /// </summary>
    /// <param name="topic">Canal MQTT del mensaje (ej. "i/cam", "i/bme680", "i/ldr", "f/i/stock").</param>
    /// <param name="msg">Contenido del mensaje en formato JSON.</param>
    public void ProcesarMensajeExterno(string topic, string msg)
    {
        lock (lockObject)
        {
            try
            {
                if (topic == "i/cam")
                {
                    // Un fotograma nuevo de la cámara: solo nos interesa el último, así que
                    // vaciamos la cola antes de meter el nuevo (para no acumular fotogramas viejos).
                    CameraPayload camData = JsonUtility.FromJson<CameraPayload>(msg);

                    if (camData != null && !string.IsNullOrEmpty(camData.data))
                    {
                        camQueue.Clear();
                        camQueue.Enqueue(camData.data);
                    }
                }
                else
                {
                    // JsonUtility de Unity distingue mayúsculas/minúsculas en "true"/"false",
                    // así que normalizamos por si el mensaje llega con la capitalización de C#.
                    string msgClean = msg.Replace("True", "true").Replace("False", "false");

                    if (topic == "i/bme680" || topic == "i/bm680") bmeQueue.Enqueue(JsonUtility.FromJson<Bme680Payload>(msgClean));
                    else if (topic == "i/ldr") ldrQueue.Enqueue(JsonUtility.FromJson<LdrPayload>(msgClean));
                    else if (topic == "f/i/stock") stockQueue.Enqueue(JsonUtility.FromJson<StockPayload>(msgClean));
                }
            }
            catch (Exception ex) { Debug.LogWarning($"Error parseando JSON en {topic}: {ex.Message}"); }
        }
    }

    // Genera la marca de tiempo actual en formato ISO 8601 (el estándar que espera la fábrica en sus mensajes JSON).
    private string GetISO8601Timestamp() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    // Método interno de ayuda: convierte el JSON a bytes y lo publica en el topic indicado,
    // avisando por consola si no se pudo enviar porque no hay conexión.
    private void PublishJson(string topic, string json)
    {
        if (client != null && client.IsConnected)
        {
            client.Publish(topic, Encoding.UTF8.GetBytes(json), MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, false);
        }
        else
        {
            Debug.LogWarning($"[MQTT Interfaz] No se pudo publicar en {topic} porque el cliente no está conectado.");
        }
    }

    /// <summary>
    /// Envía a la fábrica real una orden de fabricación de una pieza del color indicado
    /// (publicando en el topic "f/o/order"), tal y como haría un operario pulsando un botón en la interfaz.
    /// </summary>
    /// <param name="workpieceType">Color/tipo de pieza pedida (por ejemplo "WHITE", "RED", "BLUE").</param>
    public void SendOrder(string workpieceType)
    {
        string tipoFormateado = workpieceType != null ? workpieceType.Trim() : "";

        var payload = new OrderPayload { ts = GetISO8601Timestamp(), type = tipoFormateado };
        string jsonPayload = JsonUtility.ToJson(payload);

        PublishJson("f/o/order", jsonPayload);
        Debug.Log($"<color=cyan>[MQTT Interfaz] Orden enviada a f/o/order: {jsonPayload}</color>");
    }

    /// <summary>
    /// Envía un comando de movimiento a la cámara Pan-Tilt de la SSC (publicando en "o/ptu").
    /// Si el comando es "home", se manda sin el campo de grados (la cámara vuelve a su posición de referencia).
    /// </summary>
    /// <param name="command">Comando de movimiento (por ejemplo "left", "right", "up", "down" o "home").</param>
    /// <param name="degree">Cuántos grados debe moverse la cámara (se ignora si el comando es "home").</param>
    public void SendPtuCommand(string command, int degree = 10)
    {
        if (command == "home")
        {
            var homePayload = new PtuHomePayload { ts = GetISO8601Timestamp(), cmd = command };
            PublishJson("o/ptu", JsonUtility.ToJson(homePayload));
        }
        else
        {
            var payload = new PtuPayload { ts = GetISO8601Timestamp(), cmd = command, degree = degree };
            PublishJson("o/ptu", JsonUtility.ToJson(payload));
        }
    }

    /// <summary>
    /// Envía a la fábrica la configuración deseada de la cámara: si debe estar encendida o apagada
    /// y a cuántos fotogramas por segundo debe transmitir (publicando en "c/cam").
    /// </summary>
    /// <param name="isOn">true para encender la transmisión de la cámara, false para apagarla.</param>
    /// <param name="fps">Fotogramas por segundo deseados.</param>
    public void SendCameraConfig(bool isOn, int fps)
    {
        var payload = new CamConfigPayload { ts = GetISO8601Timestamp(), on = isOn, fps = fps };
        string json = JsonUtility.ToJson(payload);

        PublishJson("c/cam", json);
        Debug.Log($"<color=cyan>[MQTT Cámara] Publicado en 'c/cam': {json}</color>");
    }

    /// <summary>
    /// Le dice a la fábrica cada cuántos segundos debe enviar una nueva lectura del sensor de luz LDR
    /// (publicando en "c/ldr").
    /// </summary>
    /// <param name="seconds">Periodo en segundos entre lecturas.</param>
    public void SendLdrPeriod(int seconds)
    {
        var payload = new SensorPeriodPayload { ts = GetISO8601Timestamp(), period = seconds };
        PublishJson("c/ldr", JsonUtility.ToJson(payload));
    }

    /// <summary>
    /// Le dice a la fábrica cada cuántos segundos debe enviar una nueva lectura del sensor ambiental
    /// BME680 (publicando en "c/bme680").
    /// </summary>
    /// <param name="seconds">Periodo en segundos entre lecturas.</param>
    public void SendBme680Period(int seconds)
    {
        var payload = new SensorPeriodPayload { ts = GetISO8601Timestamp(), period = seconds };
        PublishJson("c/bme680", JsonUtility.ToJson(payload));
    }

    private void OnApplicationQuit()
    {
        // Al cerrar Unity, avisamos a la fábrica de que apague la cámara (para no dejarla
        // transmitiendo innecesariamente) antes de cortar la conexión de forma ordenada.
        if (client != null && client.IsConnected)
        {
            try
            {
                SendCameraConfig(false, 2);
                System.Threading.Thread.Sleep(100); // Pequeña espera para dar tiempo a que el mensaje salga antes de desconectar.
                client.Disconnect();
            }
            catch { }
        }
    }
}
