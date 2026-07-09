using UnityEngine;
using System.Collections;

public class ControladorSpawnPiecesHBW_mqtt : MonoBehaviour
{
    private string[] listaPendiente;
    private bool hayCambio = false;

    [Header("Referencias de Escena (Objetos ColXFilX)")]
    public Transform[] puntosDeHueco;

    [Header("Prefabs de Pieza")]
    public GameObject prefabBlanco;
    public GameObject prefabRojo;
    public GameObject prefabAzul;

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
        // 1. Esperamos a que la arquitectura MQTT despierte
        while (MQTTClient.Instance == null) yield return null;

        // 2. Comprobamos si el Broker ya envió el mensaje retenido al conectar el cliente MQTT en Awake
        string[] stockInicial = MQTTClient.Instance.GetInitialStock();

        if (stockInicial != null)
        {
            // SI HAY MENSAJE RETENIDO EN EL SERVIDOR: Lo inyectamos de inmediato
            Debug.Log("<color=green><b>[HBW Spawn] Almacén inicial cargado mediante mensaje retenido.</b></color>");
            AlRecibirPiezas(stockInicial);
        }
        else
        {
            // SI NO HAY NADA PUBLICADO TODAVÍA: Nos enganchamos al evento y esperamos pacientemente al primero en vivo
            Debug.Log("<color=orange><b>[HBW Spawn] Almacén vacío en red. Esperando a que la fábrica publique el primer f/i/stock...</b></color>");
            MQTTClient.Instance.OnHBWUpdatePiecesEvent += AlRecibirPiezas;
        }
    }

    private void AlRecibirPiezas(string[] piezas)
    {
        // CONTROL C# LOCAL: Nos desvinculamos del evento de inmediato para asegurar lectura única
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnHBWUpdatePiecesEvent -= AlRecibirPiezas;
        }

        listaPendiente = piezas;
        hayCambio = true;
    }

    void Update() { if (hayCambio) { ActualizarVisualizacion(listaPendiente); hayCambio = false; } }

    void ActualizarVisualizacion(string[] listaColores)
    {
        if (listaColores == null) return;

        for (int i = 0; i < puntosDeHueco.Length; i++)
        {
            if (i >= listaColores.Length) break;

            Transform padreEje = puntosDeHueco[i];
            if (padreEje == null || padreEje.childCount == 0) continue;

            Transform cajon = padreEje.GetChild(0);

            for (int j = cajon.childCount - 1; j >= 0; j--)
            {
                if (cajon.GetChild(j).name.ToLower().Contains("pieza"))
                    Destroy(cajon.GetChild(j).gameObject);
            }

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
        Debug.Log("<color=green><b>[HBW Spawn] Almacén pintado con éxito por única vez.</b></color>");
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
        // Redundancia de seguridad: si desactivas el objeto antes de recibir datos, liberamos el evento de C#
        if (MQTTClient.Instance != null) MQTTClient.Instance.OnHBWUpdatePiecesEvent -= AlRecibirPiezas;
    }
}