using UnityEngine;
using System.Collections;

public class ControladorSpawnPiecesHBW_mqtt : MonoBehaviour
{
    private static ControladorSpawnPiecesHBW_mqtt instance;
    public static ControladorSpawnPiecesHBW_mqtt Instance => instance;

    private string[] listaPendiente;
    private bool hayCambio = false;

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

    void Start()
    {
        PrecalcularOffsetsEnCajones();
        LimpiarSoloPiezas();
        StartCoroutine(SuscripcionSegura());
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

        string[] stockInicial = MQTTClient.Instance.GetInitialStock();

        if (stockInicial != null)
        {
            Debug.Log("<color=green><b>[HBW Spawn] Almacén inicial cargado mediante mensaje retenido.</b></color>");
            AlRecibirPiezas(stockInicial);
        }

        MQTTClient.Instance.OnHBWUpdatePiecesEvent += AlRecibirPiezas;
        Debug.Log("<color=orange><b>[HBW Spawn] Escuchando actualizaciones de stock en tiempo real...</b></color>");
    }

    private void AlRecibirPiezas(string[] piezas)
    {
        listaPendiente = piezas;
        hayCambio = true;
    }

    void Update()
    {
        if (hayCambio)
        {
            ActualizarVisualizacion(listaPendiente);
            hayCambio = false;
        }
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
        Debug.Log("<color=green><b>[HBW Spawn] Almacén pintado con éxito.</b></color>");
    }

    /// <summary>
    /// Utilizado en modo Simulación Offline para rellenar el almacén.
    /// </summary>
    public void LlenarAlmacenConTodasLasPiezas()
    {
        string[] stockCompleto = new string[9];
        for (int i = 0; i < 9; i++)
        {
            int fila = i / 3;
            if (fila == 0) stockCompleto[i] = "WHITE";
            else if (fila == 1) stockCompleto[i] = "RED";
            else stockCompleto[i] = "BLUE";
        }

        AlRecibirPiezas(stockCompleto);
    }

    /// <summary>
    /// Permite forzar la relectura del stock al restablecer la conexión MQTT.
    /// </summary>
    public void ForzarRelecturaStock()
    {
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

    private void OnDisable()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnHBWUpdatePiecesEvent -= AlRecibirPiezas;
        }
    }
}