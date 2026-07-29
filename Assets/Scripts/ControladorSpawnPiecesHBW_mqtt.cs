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

        // 2. Comprobamos si el Broker ya envió el mensaje retenido al conectar
        string[] stockInicial = MQTTClient.Instance.GetInitialStock();

        if (stockInicial != null)
        {
            Debug.Log("<color=green><b>[HBW Spawn] Almacén inicial cargado mediante mensaje retenido.</b></color>");
            AlRecibirPiezas(stockInicial);
        }

        // 🟢 3. Nos suscribimos permanentemente para escuchar CUALQUIER actualización futura de stock
        MQTTClient.Instance.OnHBWUpdatePiecesEvent += AlRecibirPiezas;
        Debug.Log("<color=orange><b>[HBW Spawn] Escuchando actualizaciones de stock en tiempo real...</b></color>");
    }

    private void AlRecibirPiezas(string[] piezas)
    {
        // 🟢 Eliminada la desuscripción restrictiva para que admita múltiples refrescos de stock
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

    void ActualizarVisualizacion(string[] listaColores)
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

            // Si la lista ya no llega a este hueco, el cajón se queda vacío
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
        if (MQTTClient.Instance != null) MQTTClient.Instance.OnHBWUpdatePiecesEvent -= AlRecibirPiezas;
    }
}