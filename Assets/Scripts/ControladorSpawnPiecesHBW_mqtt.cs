using UnityEngine;
using System.Collections;
using System;

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
        while (MQTTClient.Instance == null) yield return null;
        MQTTClient.Instance.OnHBWUpdatePiecesEvent += AlRecibirPiezas;

        string inicial = MQTTClient.Instance.GetLastHBWStatus();
        if (!string.IsNullOrEmpty(inicial))
        {
            try
            {
                // CAMBIADO: Deserializar usando el nuevo formato complejo retenido
                JSON_FullStock data = JsonUtility.FromJson<JSON_FullStock>(inicial);
                if (data != null && data.stockItems != null)
                {
                    string[] flatStock = new string[9];
                    for (int i = 0; i < 9; i++) flatStock[i] = "";

                    foreach (var item in data.stockItems)
                    {
                        if (string.IsNullOrEmpty(item.location) || item.location.Length < 2) continue;

                        int col = char.ToUpper(item.location[0]) - 'A';
                        int row = item.location[1] - '1';

                        if (col >= 0 && col < 3 && row >= 0 && row < 3)
                        {
                            int idx = (row * 3) + col;
                            if (item.workpiece != null && !string.IsNullOrEmpty(item.workpiece.type))
                            {
                                flatStock[idx] = item.workpiece.type.ToUpper();
                            }
                        }
                    }
                    AlRecibirPiezas(flatStock);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ControladorSpawnPieces] Error al procesar stock retenido inicial: " + ex.Message);
            }
        }
    }

    private void AlRecibirPiezas(string[] piezas)
    {
        listaPendiente = piezas;
        hayCambio = true;

        // CRÍTICO: Desvincular el evento C# inmediatamente para garantizar lectura UNICA al inicio
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnHBWUpdatePiecesEvent -= AlRecibirPiezas;
        }
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
        // Desuscripción redundante de seguridad por si se desactiva el script antes de recibir datos
        if (MQTTClient.Instance != null) MQTTClient.Instance.OnHBWUpdatePiecesEvent -= AlRecibirPiezas;
    }
}