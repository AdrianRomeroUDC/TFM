using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Se encarga de crear y colocar en Unity las piezas 3D que hay guardadas dentro de los 9 huecos del
/// almacén HBW (cuadrícula 3x3), según el inventario real que reporta la fábrica o según el modo de
/// trabajo elegido en el menú (conectado por MQTT, simulación offline o reproducción de histórico de
/// InfluxDB). No mueve ningún eje: solo decide qué pieza (blanca, roja o azul) aparece en cada cajón
/// del almacén y cuándo debe destruirse o repintarse esa pieza.
/// </summary>
public class ControladorSpawnPiecesHBW_mqtt : MonoBehaviour
{
    private static ControladorSpawnPiecesHBW_mqtt instance;
    public static ControladorSpawnPiecesHBW_mqtt Instance => instance;

    private string[] listaPendiente;
    private bool hayCambio = false;

    // Evita volver a colocar piezas 3D más de una vez mientras seguimos conectados en modo MQTT Directo.
    private bool yaSpawneadoEnConexionActual = false;

    // Nos permite detectar en Update() cuándo el usuario cambia de modo de trabajo en el menú.
    private UI_ControladorMenu.ModoOrigen modoAnterior;
    private bool modoInicializado = false;

    [Header("Referencias de Escena (Objetos ColXFilX)")]
    public Transform[] puntosDeHueco;

    [Header("Prefabs de Pieza")]
    public GameObject prefabBlanco;
    public GameObject prefabRojo;
    public GameObject prefabAzul;

    void Awake()
    {
        instance = this;
    }

    private void OnEnable()
    {
        // Escuchamos solo el aviso de cuando arranca o termina una simulación offline (botón Play / Pedir Pieza).
        SimuladorOffline.OnEstadoSimulacionOfflineCambiado += OnEstadoSimulacionOfflineCambiado;
    }

    private void OnDisable()
    {
        SimuladorOffline.OnEstadoSimulacionOfflineCambiado -= OnEstadoSimulacionOfflineCambiado;

        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnHBWUpdatePiecesEvent -= AlRecibirPiezas;
            MQTTClient.Instance.OnFactoryHeartbeatEvent -= OnFactoryHeartbeat;
        }
    }

    void Start()
    {
        PrecalcularOffsetsEnCajones();
        LimpiarSoloPiezas();
        StartCoroutine(SuscripcionSegura());
    }

    // Cuando el usuario pulsa Play o "Pedir Pieza" en el modo simulación offline, llenamos el almacén
    // 3D al completo (los 9 huecos ocupados), simulando un almacén lleno de fábrica.
    private void OnEstadoSimulacionOfflineCambiado(bool enEjecucion)
    {
        if (enEjecucion)
        {
            if (UI_ControladorMenu.Instance != null && UI_ControladorMenu.Instance.EsModoSimulacionActivo)
            {
                Debug.Log("<color=green><b>[HBW Spawn] Ejecutando pedido/simulación -> Llenando almacén 3D (9/9).</b></color>");
                LlenarAlmacenConTodasLasPiezas();
            }
        }
    }

    // Antes de colocar ninguna pieza, calculamos y guardamos en cada cajón la posición/rotación exacta
    // que debería tener respecto a su hueco, para poder encajar las piezas siempre en el sitio correcto.
    void PrecalcularOffsetsEnCajones()
    {
        for (int i = 0; i < puntosDeHueco.Length; i++)
        {
            Transform padreEje = puntosDeHueco[i];
            if (padreEje == null || padreEje.childCount == 0) continue;

            Transform cajon = padreEje.GetChild(0);
            if (cajon.TryGetComponent<ContenedorHBW_proxy>(out ContenedorHBW_proxy proxy))
            {
                Vector3 posicionLocalTeorica = cajon.InverseTransformPoint(padreEje.position);
                Quaternion rotacionLocalTeorica = Quaternion.Inverse(cajon.rotation) * padreEje.rotation;
                proxy.RegistrarOffsetTeorico(posicionLocalTeorica, rotacionLocalTeorica);
            }
        }
        Debug.Log("<color=cyan><b>[HBW Precalculo]:</b> Posiciones teóricas calculadas en todos los contenedores.</color>");
    }

    // Espera a que el cliente MQTT exista y, si al arrancar la escena ya estamos en modo Conectado,
    // pide el inventario inicial real del almacén para pintarlo de golpe.
    IEnumerator SuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;

        MQTTClient.Instance.OnHBWUpdatePiecesEvent += AlRecibirPiezas;
        MQTTClient.Instance.OnFactoryHeartbeatEvent += OnFactoryHeartbeat;

        if (UI_ControladorMenu.Instance != null)
        {
            modoAnterior = UI_ControladorMenu.Instance.modoSeleccionado;
            modoInicializado = true;

            // Solo cargamos el inventario real si al arrancar la escena ya estamos en Modo Conectado.
            if (UI_ControladorMenu.Instance.modoSeleccionado == UI_ControladorMenu.ModoOrigen.MQTT_Directo)
            {
                string[] stockInicial = MQTTClient.Instance.GetInitialStock();
                if (stockInicial != null && !yaSpawneadoEnConexionActual)
                {
                    AlRecibirPiezas(stockInicial);
                }
            }
        }
    }

    // Si la fábrica real se desconecta, permitimos que al reconectar se vuelva a pedir el inventario
    // desde cero (para no quedarnos con datos de una conexión anterior que ya no valen).
    private void OnFactoryHeartbeat(bool connected, DateTime timestamp)
    {
        if (!connected)
        {
            yaSpawneadoEnConexionActual = false;
        }
    }

    void Update()
    {
        // Comprobamos si el usuario ha cambiado el modo de trabajo en el menú (Conectado / Simulación / Histórico).
        if (UI_ControladorMenu.Instance != null)
        {
            UI_ControladorMenu.ModoOrigen modoActual = UI_ControladorMenu.Instance.modoSeleccionado;

            if (!modoInicializado)
            {
                modoAnterior = modoActual;
                modoInicializado = true;
            }
            else if (modoActual != modoAnterior)
            {
                modoAnterior = modoActual;
                OnModoSeleccionadoCambiado(modoActual);
            }
        }

        // Si ha llegado un inventario nuevo pendiente de pintar, lo aplicamos aquí, en el hilo principal de Unity.
        if (hayCambio)
        {
            ActualizarVisualizacion(listaPendiente);
            hayCambio = false;
        }
    }

    // Simplemente informa por consola del modo activo: ningún cambio de modo toca el almacén 3D por
    // sí solo, siempre se espera a una acción concreta (Play, Pedir Pieza o Reset).
    private void OnModoSeleccionadoCambiado(UI_ControladorMenu.ModoOrigen nuevoModo)
    {
        if (nuevoModo == UI_ControladorMenu.ModoOrigen.MQTT_Directo)
        {
            Debug.Log("<color=cyan><b>[HBW Spawn] Toggle Conectado activo. Almacén 3D en espera de Play/Reset.</b></color>");
        }
        else if (nuevoModo == UI_ControladorMenu.ModoOrigen.Simulacion_Offline)
        {
            Debug.Log("<color=green><b>[HBW Spawn] Toggle Simulación activo. Almacén 3D en espera de Play/Pedir Pieza.</b></color>");
        }
        else if (nuevoModo == UI_ControladorMenu.ModoOrigen.BaseDeDatos_Historico)
        {
            Debug.Log("<color=yellow><b>[HBW Spawn] Toggle BBDD activo. Almacén 3D en espera de Play.</b></color>");
            yaSpawneadoEnConexionActual = false;
        }
    }

    /// <summary>
    /// Recibe la lista de colores del inventario real del almacén (uno por cada uno de los 9 huecos)
    /// y decide, según el modo de trabajo activo, si debe pintar ya el almacén 3D o esperar a una
    /// acción concreta del usuario (por ejemplo, en modo simulación se ignora hasta que se dé a Play).
    /// </summary>
    public void AlRecibirPiezas(string[] piezas)
    {
        UI_ControladorMenu.ModoOrigen modo = (UI_ControladorMenu.Instance != null)
            ? UI_ControladorMenu.Instance.modoSeleccionado
            : UI_ControladorMenu.ModoOrigen.MQTT_Directo;

        if (modo == UI_ControladorMenu.ModoOrigen.MQTT_Directo)
        {
            // Modo Conectado: solo pintamos el almacén una vez, justo al conectar (con el inventario inicial real).
            if (yaSpawneadoEnConexionActual) return;
            yaSpawneadoEnConexionActual = true;
        }
        else if (modo == UI_ControladorMenu.ModoOrigen.Simulacion_Offline)
        {
            // En modo simulación, el almacén solo se modifica al pulsar Play o Pedir Pieza, así que ignoramos este aviso.
            return;
        }
        else
        {
            // Modo Histórico (BBDD): InfluxDBClient ya se encarga de que este método solo se invoque
            // una vez por reproducción (con el estado previo cargado al principio); los "f/i/stock"
            // grabados que se reproducen después del arranque actualizan solo la interfaz.
            yaSpawneadoEnConexionActual = false;
        }

        listaPendiente = piezas;
        hayCambio = true;
    }

    /// <summary>
    /// Pinta de verdad el almacén 3D: recorre los 9 huecos, borra la pieza vieja que hubiera en cada
    /// uno y crea la pieza nueva (blanca, roja o azul) que le corresponde según la lista de colores
    /// recibida, dejando vacíos los huecos que no tengan color asignado.
    /// </summary>
    public void ActualizarVisualizacion(string[] listaColores)
    {
        if (listaColores == null) return;

        for (int i = 0; i < puntosDeHueco.Length; i++)
        {
            Transform padreEje = puntosDeHueco[i];
            if (padreEje == null || padreEje.childCount == 0) continue;

            Transform cajon = padreEje.GetChild(0);

            // Destruimos cualquier pieza vieja que hubiera dentro de este cajón antes de colocar la nueva.
            for (int j = cajon.childCount - 1; j >= 0; j--)
            {
                if (cajon.GetChild(j).name.ToLower().Contains("pieza"))
                    Destroy(cajon.GetChild(j).gameObject);
            }

            // Si la lista recibida no cubre este hueco, el cajón se queda vacío.
            if (i >= listaColores.Length || string.IsNullOrEmpty(listaColores[i])) continue;

            GameObject prefab = null;
            string color = listaColores[i].Trim().ToUpper();
            if (color == "WHITE") prefab = prefabBlanco;
            else if (color == "RED") prefab = prefabRojo;
            else if (color == "BLUE") prefab = prefabAzul;

            if (prefab != null)
            {
                GameObject nueva = Instantiate(prefab, padreEje.position, padreEje.rotation, cajon);
                nueva.transform.localScale = Vector3.one;
            }
        }
        Debug.Log("<color=green><b>[HBW Spawn] Almacén 3D pintado con éxito.</b></color>");
    }

    /// <summary>
    /// MODO SIMULACIÓN: Rellena siempre TODAS las piezas (3 Blancas, 3 Rojas, 3 Azules).
    /// </summary>
    public void LlenarAlmacenConTodasLasPiezas()
    {
        yaSpawneadoEnConexionActual = false;

        // Repartimos el almacén en 3 filas de 3 huecos: la primera fila blanca, la segunda roja y la tercera azul.
        string[] stockCompleto = new string[9];
        for (int i = 0; i < 9; i++)
        {
            int fila = i / 3;
            if (fila == 0) stockCompleto[i] = "WHITE";
            else if (fila == 1) stockCompleto[i] = "RED";
            else stockCompleto[i] = "BLUE";
        }

        listaPendiente = stockCompleto;
        hayCambio = true;
    }

    /// <summary>
    /// Forzar relectura del stock al volver al modo MQTT Directo o tras reconexión.
    /// </summary>
    public void ForzarRelecturaStock()
    {
        yaSpawneadoEnConexionActual = false;
        LimpiarSoloPiezas();

        if (MQTTClient.Instance != null)
        {
            string[] stockInicial = MQTTClient.Instance.GetInitialStock();
            if (stockInicial != null)
            {
                AlRecibirPiezas(stockInicial);
            }
        }
    }

    // Vacía todos los huecos del almacén, destruyendo únicamente las piezas (deja intactos los cajones y estantes).
    void LimpiarSoloPiezas()
    {
        foreach (Transform h in puntosDeHueco)
        {
            if (h != null && h.childCount > 0)
            {
                Transform cajon = h.GetChild(0);
                for (int j = cajon.childCount - 1; j >= 0; j--)
                {
                    if (cajon.GetChild(j).name.ToLower().Contains("pieza"))
                        Destroy(cajon.GetChild(j).gameObject);
                }
            }
        }
    }
}
