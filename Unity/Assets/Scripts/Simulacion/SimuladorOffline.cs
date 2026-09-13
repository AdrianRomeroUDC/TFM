using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

// Representa UN mensaje MQTT "grabado": en qué segundo (relativo al principio de la grabación)
// ocurrió, por qué topic (canal) se envió y qué contenido (payload JSON) llevaba.
// Es la unidad mínima de una secuencia generada por GenerarJSONSimulacion.
[Serializable]
public class EventoHistoricoPieza
{
    public float tiempoRelativoSegundos;
    public string topic;
    public string payloadJson;
}

// Representa la "grabación" completa del recorrido de un tipo de pieza (blanca, roja o azul)
// por toda la fábrica: una lista ordenada de eventos MQTT tal y como se recibieron en su día.
[Serializable]
public class SecuenciaPiezaData
{
    public string tipoPieza;
    public List<EventoHistoricoPieza> eventos = new List<EventoHistoricoPieza>();
}

/// <summary>
/// Simulador "modo offline" del gemelo digital: en vez de conectarse a la fábrica física real por
/// MQTT o descargar datos en directo de InfluxDB, carga unas "grabaciones" ya hechas de antemano
/// (los JSON generados por <see cref="GenerarJSONSimulacion"/>) y las reproduce inyectando esos
/// mismos mensajes en <see cref="MQTTClient"/> / <see cref="MQTT_InterfaceClient"/> exactamente
/// con el mismo ritmo (tiempos) con el que ocurrieron en la fábrica real. Para el resto de scripts
/// del proyecto (los que mueven las estaciones en 3D) es indistinguible de recibir mensajes reales:
/// por eso sirve para practicar y enseñar el funcionamiento del gemelo digital sin depender de que
/// la planta física esté encendida ni conectada a internet.
/// </summary>
public class SimuladorOffline : MonoBehaviour
{
    // Patrón Singleton: solo puede existir un SimuladorOffline en la escena, accesible desde
    // cualquier script como "SimuladorOffline.Instance".
    public static SimuladorOffline Instance { get; private set; }

    // Evento que avisa a otros scripts (por ejemplo la interfaz de usuario) cuando arranca o
    // termina una simulación offline, para que puedan activar/desactivar botones, etc.
    public static event Action<bool> OnEstadoSimulacionOfflineCambiado;

    // Los 3 archivos JSON (uno por color de pieza) que arrastras desde el Inspector: son las
    // "grabaciones" generadas previamente por GenerarJSONSimulacion.
    [Header("--- Archivos JSON de InfluxDB ---")]
    [SerializeField] private TextAsset jsonPiezaBlanca;
    [SerializeField] private TextAsset jsonPiezaRoja;
    [SerializeField] private TextAsset jsonPiezaAzul;

    // Diccionario en memoria con las 3 secuencias ya cargadas y listas para reproducir, indexadas
    // por el nombre del color ("WHITE", "RED", "BLUE").
    private readonly Dictionary<string, SecuenciaPiezaData> mapaSecuencias = new Dictionary<string, SecuenciaPiezaData>();
    private Coroutine corrutinaSimulacion;
    public bool EnEjecucion { get; private set; } = false;

    // Guardamos el último inventario del almacén (HBW) que hemos simulado, por si algún otro
    // script necesita consultarlo sin tener que esperar a un nuevo mensaje.
    private JSON_FullStock ultimoStockConocido = null;

    private void Awake()
    {
        // Nos aseguramos de que solo exista una instancia de este script en toda la escena.
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        // Al arrancar la escena, cargamos en memoria las 3 grabaciones (blanca, roja, azul).
        CargarDatosHistoricos();
    }

    /// <summary>
    /// Lee los 3 archivos JSON asignados en el Inspector y los convierte en objetos
    /// <see cref="SecuenciaPiezaData"/> listos para reproducir, guardándolos en
    /// <see cref="mapaSecuencias"/>.
    /// </summary>
    public void CargarDatosHistoricos()
    {
        mapaSecuencias.Clear();
        CargarSecuencia("WHITE", jsonPiezaBlanca);
        CargarSecuencia("RED", jsonPiezaRoja);
        CargarSecuencia("BLUE", jsonPiezaAzul);
    }

    // Convierte el texto de un único archivo JSON en un SecuenciaPiezaData, ordena sus eventos
    // por tiempo (por si acaso no vinieran ya ordenados) y lo guarda en el diccionario bajo la
    // clave indicada (el color de la pieza).
    private void CargarSecuencia(string clave, TextAsset asset)
    {
        if (asset == null || string.IsNullOrEmpty(asset.text)) return;

        try
        {
            SecuenciaPiezaData data = JsonUtility.FromJson<SecuenciaPiezaData>(asset.text);
            if (data != null && data.eventos != null && data.eventos.Count > 0)
            {
                data.eventos.Sort((a, b) => a.tiempoRelativoSegundos.CompareTo(b.tiempoRelativoSegundos));
                mapaSecuencias[clave] = data;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ [SimuladorOffline] Error al parsear JSON de {clave}: {ex.Message}");
        }
    }

    /// <summary>
    /// Deja la fábrica virtual en un estado "de fábrica" limpio antes de empezar a simular:
    /// vacía el plato giratorio del MPO, vacía la plataforma de entrega/recogida (DSO/DPS)
    /// y rellena el almacén (HBW) con el stock inicial por defecto (9 piezas, una en cada hueco).
    /// </summary>
    public void ComprobarYConfigurarModoOffline()
    {
        Debug.Log("🔌 [SimuladorOffline] Inicializando entorno y stock offline para simulación.");
        ResetearTurntable();
        LimpiarPlataformaDSO();
        InicializarStockPorDefecto();
    }

    /// <summary>
    /// Genera un inventario "de fábrica" completo y ficticio para el almacén HBW: una cuadrícula
    /// de 3x3 huecos (filas A, B, C y columnas 1, 2, 3), donde cada columna se llena con un color
    /// de pieza distinto (blanca, roja, azul). Después inyecta ese inventario como si fuera un
    /// mensaje MQTT real del topic "f/i/stock", para que el almacén 3D se actualice con él.
    /// </summary>
    public void InicializarStockPorDefecto()
    {
        JSON_FullStock stockFake = new JSON_FullStock();
        List<JSON_StockItem> listaItems = new List<JSON_StockItem>();

        string[] filas = { "A", "B", "C" };
        string[] colores = { "WHITE", "RED", "BLUE" };
        int idCounter = 0;

        // Recorremos las 3 columnas (una por color) y, dentro de cada una, las 3 filas,
        // creando así los 9 huecos del almacén con una pieza ficticia en cada uno.
        for (int col = 1; col <= 3; col++)
        {
            string colorColumna = colores[col - 1];
            foreach (string fila in filas)
            {
                listaItems.Add(new JSON_StockItem
                {
                    location = $"{fila}{col}",
                    workpiece = new JSON_Workpiece
                    {
                        id = $"OFFLINE_{idCounter++}",
                        type = colorColumna,
                        state = "RAW"
                    }
                });
            }
        }

        stockFake.stockItems = listaItems.ToArray();
        stockFake.ts = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        ultimoStockConocido = stockFake;
        Debug.Log("📦 [SimuladorOffline] Inicializando stock completo por defecto (9 piezas).");
        // Inyectamos el inventario ficticio como si fuera un mensaje MQTT real de la fábrica.
        InyectarMensajeOffline("f/i/stock", JsonUtility.ToJson(stockFake));
    }

    /// <summary>
    /// Vacía por completo el almacén 3D (simula que no queda ninguna pieza en ningún hueco) y,
    /// tras esperar un fotograma, lo vuelve a rellenar con el stock inicial por defecto. Se usa
    /// como "reinicio" del almacén antes de lanzar una nueva simulación, para que el modelo 3D
    /// se refresque visualmente en vez de simplemente sustituir los datos de golpe.
    /// </summary>
    public IEnumerator ResetearYRefrescarAlmacen3DCorrutina()
    {
        string tsNow = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        string payloadLimpieza = "{\"workpiece\":null,\"ts\":\"" + tsNow + "\"}";

        InyectarMensajeOffline("dt/hbw/state", payloadLimpieza);
        InyectarMensajeOffline("f/i/hbw", payloadLimpieza);

        JSON_FullStock stockVacio = new JSON_FullStock
        {
            stockItems = new JSON_StockItem[0],
            ts = tsNow
        };
        InyectarMensajeOffline("f/i/stock", JsonUtility.ToJson(stockVacio));

        // Esperamos un fotograma para que Unity tenga tiempo de "vaciar" visualmente el almacén
        // antes de volver a rellenarlo.
        yield return null;

        InicializarStockPorDefecto();
    }

    /// <summary>
    /// Simula que el plato giratorio del MPO (la estación de horno/fresado) queda vacío,
    /// enviando un mensaje de estado sin ninguna pieza encima.
    /// </summary>
    public void ResetearTurntable()
    {
        string tsNow = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        string payloadLimpieza = "{\"workpiece\":null,\"ts\":\"" + tsNow + "\"}";
        InyectarMensajeOffline("dt/mpo/state", payloadLimpieza);
        InyectarMensajeOffline("f/i/mpo", payloadLimpieza);
    }

    /// <summary>
    /// Simula que la plataforma de entrega/recogida del DPS (DSO) queda vacía,
    /// enviando un mensaje de estado sin ninguna pieza encima.
    /// </summary>
    public void LimpiarPlataformaDSO()
    {
        string tsNow = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        string payloadLimpieza = "{\"workpiece\":null,\"ts\":\"" + tsNow + "\"}";
        InyectarMensajeOffline("dt/dso/state", payloadLimpieza);
        InyectarMensajeOffline("f/i/dso", payloadLimpieza);
    }

    // Envía un par de mensajes "de arranque" que la fábrica real manda justo antes de empezar a
    // procesar un pedido: el sensor de la plataforma DSO marcado como libre (sin pieza detectada)
    // y el plato giratorio del MPO colocado en su posición de referencia inicial (move2Ref7).
    private void EnviarEstadosInicialesPrepedido()
    {
        string tsNow = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        string payloadDso = "{\"ts\":\"" + tsNow + "\",\"dso_sensor\":false}";
        InyectarMensajeOffline("dt/dps/dso", payloadDso);

        string payloadTurntable = "{\"ts\":\"" + tsNow + "\",\"eject\":false,\"move2Ref7\":true,\"move2Ref8\":false,\"move2Ref9\":false,\"move2Ref10\":false,\"rotation\":0,\"saw\":0}";
        InyectarMensajeOffline("dt/mpo/turntable", payloadTurntable);
    }

    /// <summary>
    /// Punto de entrada para pedir, en modo offline, que se simule el recorrido completo de una
    /// pieza (blanca, roja o azul) por la fábrica. Busca la grabación correspondiente y, si existe
    /// y no hay ya otra simulación en marcha, prepara el entorno y arranca la reproducción.
    /// </summary>
    /// <param name="tipoPieza">Color de la pieza pedida (acepta variantes en español o inglés, ej. "ROJA" o "RED").</param>
    public void PedirPieza(string tipoPieza)
    {
        // Si el "Modo BBDD" (conexión real a InfluxDB) está activo, no tiene sentido pedir una
        // pieza simulada offline a la vez: lo bloqueamos para evitar confusiones.
        if (UI_ControladorMenu.Instance != null && UI_ControladorMenu.Instance.EsModoBBDDActivo)
        {
            Debug.LogWarning("⚠️ [SimuladorOffline] No se pueden pedir piezas mientras el Modo BBDD esté activo.");
            return;
        }

        if (EnEjecucion)
        {
            Debug.LogWarning("⚠️ [SimuladorOffline] Ya hay una simulación en curso. Ignorando petición.");
            return;
        }

        // 🟢 Ejecutamos SIEMPRE la reproducción local cuando se pide desde la sección Simulación
        // Normalizamos el texto recibido (que puede venir en español o inglés) a la clave interna
        // que usamos en el diccionario de secuencias: "WHITE", "RED" o "BLUE".
        string claveUpper = tipoPieza.ToUpper();
        string claveMap = claveUpper.Contains("WHITE") || claveUpper.Contains("BLANC") ? "WHITE" :
                         claveUpper.Contains("RED") || claveUpper.Contains("ROJ") ? "RED" :
                         claveUpper.Contains("BLUE") || claveUpper.Contains("AZUL") ? "BLUE" : claveUpper;

        if (mapaSecuencias.TryGetValue(claveMap, out SecuenciaPiezaData secuencia))
        {
            // Si ya había una simulación en marcha (no debería, por el chequeo de arriba, pero por
            // seguridad), la detenemos antes de lanzar la nueva.
            if (corrutinaSimulacion != null)
            {
                StopCoroutine(corrutinaSimulacion);
                corrutinaSimulacion = null;
            }

            Debug.Log($"🚀 [SimuladorOffline] Preparando simulación offline para: {tipoPieza}");
            StartCoroutine(SecuenciaPreparacionYArrancar(secuencia));
        }
        else
        {
            Debug.LogError($"❌ [SimuladorOffline] No se encontró secuencia de datos para la pieza: {tipoPieza}");
        }
    }

    // Deja la fábrica virtual lista (plato del MPO vacío, plataforma DSO vacía, almacén con stock
    // por defecto) y envía los mensajes de arranque de pedido, antes de empezar a reproducir los
    // eventos grabados de la pieza solicitada.
    private IEnumerator SecuenciaPreparacionYArrancar(SecuenciaPiezaData secuencia)
    {
        EnEjecucion = true;
        OnEstadoSimulacionOfflineCambiado?.Invoke(true);

        ResetearTurntable();
        LimpiarPlataformaDSO();

        yield return StartCoroutine(ResetearYRefrescarAlmacen3DCorrutina());

        EnviarEstadosInicialesPrepedido();

        corrutinaSimulacion = StartCoroutine(ReproducirSecuencia(secuencia));
    }

    /// <summary>
    /// El corazón del reproductor: recorre, en orden, todos los eventos MQTT grabados de una
    /// secuencia y los va inyectando en el momento justo, respetando los tiempos originales entre
    /// un mensaje y el siguiente (para que la animación en el gemelo digital se vea igual de
    /// fluida que en la fábrica real). Tiene en cuenta si el usuario ha pausado la simulación o
    /// ha cambiado la velocidad de reproducción desde la interfaz.
    /// </summary>
    private IEnumerator ReproducirSecuencia(SecuenciaPiezaData secuencia)
    {
        float tiempoAcumulado = 0f;
        Debug.Log($"<color=green>▶️ [Modo Offline] Reproduciendo {secuencia.tipoPieza} ({secuencia.eventos.Count} eventos)...</color>");

        foreach (var ev in secuencia.eventos)
        {
            if (string.IsNullOrEmpty(ev.topic) || string.IsNullOrEmpty(ev.payloadJson)) continue;

            // Cuánto tiempo falta, desde donde vamos ahora mismo, hasta el instante en que debe
            // dispararse este evento.
            float tiempoEspera = ev.tiempoRelativoSegundos - tiempoAcumulado;

            while (tiempoEspera > 0f)
            {
                // Si el usuario ha pausado la simulación desde el menú, el multiplicador es 0 (el
                // tiempo no avanza); si no, usamos la velocidad elegida (x1, x2, etc.) o 1.0 por defecto.
                float multiplicador = (UI_ControladorMenu.Instance != null && UI_ControladorMenu.Instance.EsPausado) ? 0f :
                                     (UI_ControladorMenu.Instance != null ? UI_ControladorMenu.Instance.multiplicadorVelocidad : 1.0f);

                if (multiplicador > 0f)
                {
                    float delta = Time.deltaTime * multiplicador;
                    tiempoEspera -= delta;
                    tiempoAcumulado += delta;
                }

                yield return null;
            }

            // Tratamiento especial de stock: la UI se actualiza, el 3D no para no vaciar cajones
            if (ev.topic == "f/i/stock")
            {
                // Actualizamos la interfaz (lista de inventario) pero NO reenviamos este mensaje al
                // MQTTClient "de escena", para que el almacén 3D no se vacíe/rellene de golpe con
                // cada actualización de stock grabada.
                if (MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.isActiveAndEnabled)
                {
                    MQTT_InterfaceClient.Instance.ProcesarMensajeExterno(ev.topic, ev.payloadJson);
                }
                try
                {
                    JSON_FullStock stockActualizado = JsonUtility.FromJson<JSON_FullStock>(ev.payloadJson);
                    if (stockActualizado != null) ultimoStockConocido = stockActualizado;
                }
                catch { }

                continue;
            }

            // Para cualquier otro topic, inyectamos el mensaje tal cual, como si fuera un mensaje
            // MQTT real recién llegado de la fábrica.
            InyectarMensajeOffline(ev.topic, ev.payloadJson);
        }

        EnEjecucion = false;
        OnEstadoSimulacionOfflineCambiado?.Invoke(false);
        Debug.Log($"<color=green>✅ [Modo Offline] Finalizada reproducción de {secuencia.tipoPieza}.</color>");
    }

    /// <summary>
    /// Corta de golpe cualquier simulación offline en marcha (por ejemplo si el usuario pulsa
    /// "detener" desde la interfaz antes de que termine la reproducción).
    /// </summary>
    public void DetenerSimulacionForzada()
    {
        if (corrutinaSimulacion != null)
        {
            StopCoroutine(corrutinaSimulacion);
            corrutinaSimulacion = null;
        }

        EnEjecucion = false;
        OnEstadoSimulacionOfflineCambiado?.Invoke(false);
    }

    // Envía un mensaje "falso" (topic + payload) a los clientes MQTT de la escena, exactamente
    // igual que si hubiera llegado de verdad desde el broker de la fábrica física. Es el mismo
    // truco que usa InfluxDBClient para reproducir históricos: ambos aprovechan que MQTTClient
    // expone ProcesarMensajeExterno para "colar" mensajes sin necesidad de una conexión real.
    private void InyectarMensajeOffline(string topic, string payloadJson)
    {
        if (MQTTClient.Instance != null && MQTTClient.Instance.isActiveAndEnabled)
        {
            MQTTClient.Instance.ProcesarMensajeExterno(topic, payloadJson);
        }

        if (MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.isActiveAndEnabled)
        {
            MQTT_InterfaceClient.Instance.ProcesarMensajeExterno(topic, payloadJson);
        }
    }
}
