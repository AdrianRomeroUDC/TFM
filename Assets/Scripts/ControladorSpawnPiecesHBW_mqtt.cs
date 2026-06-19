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

        string inicial = MQTTClient.Instance.GetLastHBWStatus();
        if (!string.IsNullOrEmpty(inicial))
        {
            // CORRECCIÓN CRÍTICA: El JSON retenido usa la estructura de red nueva ("stock")
            // Deserializamos con la clase interna correcta para que no devuelva null
            JSON_HBWStock data = JsonUtility.FromJson<JSON_HBWStock>(inicial);
            if (data != null && data.stock != null)
            {
                AlRecibirPiezas(data.stock);
            }
        }
    }

    private void AlRecibirPiezas(string[] piezas) { listaPendiente = piezas; hayCambio = true; }

    void Update() { if (hayCambio) { ActualizarVisualizacion(listaPendiente); hayCambio = false; } }

    void ActualizarVisualizacion(string[] listaColores)
    {
        // SALVAVIDAS 1: Si por alguna razón la lista llega nula de la red, abortamos sin romper nada
        if (listaColores == null) return;

        for (int i = 0; i < puntosDeHueco.Length; i++)
        {
            if (i >= listaColores.Length) break;

            Transform padreEje = puntosDeHueco[i];

            // SALVAVIDAS 2: Si hay algún hueco sin asignar en el Inspector, lo saltamos limpiamente
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

    private void OnDisable() { if (MQTTClient.Instance != null) MQTTClient.Instance.OnHBWUpdatePiecesEvent -= AlRecibirPiezas; }
}