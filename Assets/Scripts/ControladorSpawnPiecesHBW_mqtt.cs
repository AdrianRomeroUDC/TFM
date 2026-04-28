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
        LimpiarSoloPiezas();
        StartCoroutine(SuscripcionSegura());
    }

    IEnumerator SuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;

        MQTTClient.Instance.OnHBWUpdatePiecesEvent += AlRecibirPiezas;

        // Cargar estado inicial si ya existe
        string inicial = MQTTClient.Instance.GetLastHBWStatus();
        if (!string.IsNullOrEmpty(inicial))
        {
            HBWStockPayload data = JsonUtility.FromJson<HBWStockPayload>(inicial);
            AlRecibirPiezas(data.piezas);
        }

        Debug.Log("<color=green><b>HBW Spawn:</b> Suscrito con éxito</color>");
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

    void ActualizarVisualizacion(string[] listaColores)
    {
        for (int i = 0; i < puntosDeHueco.Length; i++)
        {
            if (i >= listaColores.Length) break;

            Transform padreEje = puntosDeHueco[i];
            if (padreEje.childCount == 0) continue;

            Transform cajon = padreEje.GetChild(0);

            // Limpieza de piezas antiguas en el cajón
            for (int j = cajon.childCount - 1; j >= 0; j--)
            {
                Destroy(cajon.GetChild(j).gameObject);
            }

            // Selección de Prefab
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
                for (int j = cajon.childCount - 1; j >= 0; j--) Destroy(cajon.GetChild(j).gameObject);
            }
        }
    }

    private void OnDisable()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnHBWUpdatePiecesEvent -= AlRecibirPiezas;
    }
}