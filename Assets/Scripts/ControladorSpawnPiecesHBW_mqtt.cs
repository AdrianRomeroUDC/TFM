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
        // 1. Precalculamos los offsets en todos los cajones vacíos
        PrecalcularOffsetsEnCajones();

        // 2. Limpieza e inicio normal
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
                // Simulamos exactamente lo que hace 'Instantiate(prefab, padreEje.position, padreEje.rotation, cajon)'
                // Calculamos la posición y rotación relativas de 'padreEje' respecto a 'cajon'
                Vector3 posicionLocalTeorica = cajon.InverseTransformPoint(padreEje.position);

                // Calculamos la rotación relativa
                Quaternion rotacionLocalTeorica = Quaternion.Inverse(cajon.rotation) * padreEje.rotation;

                // Guardamos el cálculo en el proxy del cajón
                proxy.RegistrarOffsetTeorico(posicionLocalTeorica, rotacionLocalTeorica);
            }
        }
        Debug.Log("<color=cyan><b>[HBW Precalculo]:</b> Posiciones teóricas calculadas en todos los contenedores.</color>");
    }

    IEnumerator SuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;
        MQTTClient.Instance.OnHBWUpdatePiecesEvent += AlRecibirPiezas;

        string inicial = MQTTClient.Instance.GetLastHBWStatus();
        if (!string.IsNullOrEmpty(inicial))
        {
            HBWStockPayload data = JsonUtility.FromJson<HBWStockPayload>(inicial);
            AlRecibirPiezas(data.piezas);
        }
    }

    private void AlRecibirPiezas(string[] piezas) { listaPendiente = piezas; hayCambio = true; }
    void Update() { if (hayCambio) { ActualizarVisualizacion(listaPendiente); hayCambio = false; } }

    void ActualizarVisualizacion(string[] listaColores)
    {
        for (int i = 0; i < puntosDeHueco.Length; i++)
        {
            if (i >= listaColores.Length) break;

            Transform padreEje = puntosDeHueco[i];
            if (padreEje.childCount == 0) continue;

            Transform cajon = padreEje.GetChild(0);

            for (int j = cajon.childCount - 1; j >= 0; j--)
            {
                // No destruimos otros componentes, solo las piezas visuales antiguas
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
    }

    void LimpiarSoloPiezas()
    {
        foreach (Transform h in puntosDeHueco)
        {
            if (h.childCount > 0)
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

    private void OnDisable() { if (MQTTClient.Instance != null) MQTTClient.Instance.OnHBWUpdatePiecesEvent -= AlRecibirPiezas; }
}