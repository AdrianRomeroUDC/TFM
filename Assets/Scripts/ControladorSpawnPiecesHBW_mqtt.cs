using UnityEngine;
using System;
using System.Collections;

public class ControladorSpawnPiecesHBW_mqtt : MonoBehaviour
{
    private static ControladorSpawnPiecesHBW_mqtt instance;
    public static ControladorSpawnPiecesHBW_mqtt Instance => instance;

    private string[] listaPendiente;
    private bool hayCambio = false;

    // Control para spawnear 3D solo UNA VEZ en modo Conectado
    private bool yaSpawneadoEnConexionActual = false;

    // Detección de cambio de modo en tiempo real
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
        // 🟢 Escuchamos únicamente cuando SE INICIA o finaliza una simulación offline (Play / Pedir Pieza)
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

    private void OnEstadoSimulacionOfflineCambiado(bool enEjecucion)
    {
        // 🟢 SOLO cuando se presiona Play / Pedir Pieza (enEjecucion == true), se llena el almacén 3D (9/9)
        if (enEjecucion)
        {
            if (UI_ControladorMenu.Instance != null && UI_ControladorMenu.Instance.EsModoSimulacionActivo)
            {
                Debug.Log("<color=green><b>[HBW Spawn] Ejecutando pedido/simulación -> Llenando almacén 3D (9/9).</b></color>");
                LlenarAlmacenConTodasLasPiezas();
            }
        }
    }

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

    IEnumerator SuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;

        MQTTClient.Instance.OnHBWUpdatePiecesEvent += AlRecibirPiezas;
        MQTTClient.Instance.OnFactoryHeartbeatEvent += OnFactoryHeartbeat;

        if (UI_ControladorMenu.Instance != null)
        {
            modoAnterior = UI_ControladorMenu.Instance.modoSeleccionado;
            modoInicializado = true;

            // Solo cargamos el stock si al arrancar la escena ya estamos en Modo Conectado
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

    private void OnFactoryHeartbeat(bool connected, DateTime timestamp)
    {
        if (!connected)
        {
            yaSpawneadoEnConexionActual = false;
        }
    }

    void Update()
    {
        // Detección de cambios de toggle en la UI
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

        if (hayCambio)
        {
            ActualizarVisualizacion(listaPendiente);
            hayCambio = false;
        }
    }

    private void OnModoSeleccionadoCambiado(UI_ControladorMenu.ModoOrigen nuevoModo)
    {
        // 🟢 NINGÚN cambio de toggle altera el almacén 3D por sí solo. 
        // Todos quedan a la espera de acciones concretas (Play, Pedir Pieza o Reset).
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

    public void AlRecibirPiezas(string[] piezas)
    {
        UI_ControladorMenu.ModoOrigen modo = (UI_ControladorMenu.Instance != null)
            ? UI_ControladorMenu.Instance.modoSeleccionado
            : UI_ControladorMenu.ModoOrigen.MQTT_Directo;

        if (modo == UI_ControladorMenu.ModoOrigen.MQTT_Directo)
        {
            // MODO CONECTADO: Solo spawnea 1 vez al conectar con f/i/stock
            if (yaSpawneadoEnConexionActual) return;
            yaSpawneadoEnConexionActual = true;
        }
        else if (modo == UI_ControladorMenu.ModoOrigen.Simulacion_Offline)
        {
            // En simulación solo se modifica al dar Play o Pedir Pieza
            return;
        }
        else
        {
            // MODO BBDD HISTÓRICO: Actualiza las piezas 3D dinámicamente según los datos reproducidos tras dar Play
            yaSpawneadoEnConexionActual = false;
        }

        listaPendiente = piezas;
        hayCambio = true;
    }

    public void ActualizarVisualizacion(string[] listaColores)
    {
        if (listaColores == null) return;

        for (int i = 0; i < puntosDeHueco.Length; i++)
        {
            Transform padreEje = puntosDeHueco[i];
            if (padreEje == null || padreEje.childCount == 0) continue;

            Transform cajon = padreEje.GetChild(0);

            // Destruir piezas viejas en este cajón
            for (int j = cajon.childCount - 1; j >= 0; j--)
            {
                if (cajon.GetChild(j).name.ToLower().Contains("pieza"))
                    Destroy(cajon.GetChild(j).gameObject);
            }

            // Si la lista no llega a este hueco, el cajón permanece vacío
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