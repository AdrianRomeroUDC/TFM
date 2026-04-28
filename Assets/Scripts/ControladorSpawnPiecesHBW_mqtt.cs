using UnityEngine;
using System;
using System.Collections;

[Serializable]
public class HBWStockPayload { public string[] piezas; }

public class ControladorSpawnPiecesHBW_mqtt : MonoBehaviour
{
    private string lastJsonReceived;
    private bool pendingUpdate = false;

    [Header("Referencias de Escena (Objetos ColXFilX)")]
    public Transform[] puntosDeHueco;

    [Header("Prefabs de Pieza")]
    public GameObject prefabBlanco;
    public GameObject prefabRojo;
    public GameObject prefabAzul;

    void Start()
    {
        LimpiarSoloPiezas();
        InvokeRepeating("IntentarSuscripcion", 0f, 1f);
    }

    void IntentarSuscripcion()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnHBWUpdatePiecesEvent += ProcesarMensajeHBW;
            string inicial = MQTTClient.Instance.GetLastHBWStatus();
            if (!string.IsNullOrEmpty(inicial)) ProcesarMensajeHBW(inicial);
            Debug.Log("<color=green>HBW Conectado</color>");
            CancelInvoke("IntentarSuscripcion");
        }
    }

    private void ProcesarMensajeHBW(string json)
    {
        lastJsonReceived = json;
        pendingUpdate = true;
    }

    void Update()
    {
        if (pendingUpdate)
        {
            try
            {
                HBWStockPayload data = JsonUtility.FromJson<HBWStockPayload>(lastJsonReceived);
                ActualizarVisualizacion(data.piezas);
            }
            catch (Exception e) { Debug.LogError("Error HBW JSON: " + e.Message); }
            pendingUpdate = false;
        }
    }

    void ActualizarVisualizacion(string[] listaColores)
    {
        for (int i = 0; i < puntosDeHueco.Length; i++)
        {
            if (i >= listaColores.Length) break;

            Transform padreEje = puntosDeHueco[i];

            // Verificamos que el padre tenga al menos un hijo (el cajón)
            if (padreEje.childCount == 0) continue;

            Transform cajon = padreEje.GetChild(0);

            // Limpiamos solo piezas antiguas dentro del cajón
            // Usamos un bucle inverso para evitar errores al destruir mientras recorremos
            for (int j = cajon.childCount - 1; j >= 0; j--)
            {
                Destroy(cajon.GetChild(j).gameObject);
            }

            GameObject prefab = null;
            string color = listaColores[i].Trim().ToUpper();
            if (color == "WHITE") prefab = prefabBlanco;
            else if (color == "RED") prefab = prefabRojo;
            else if (color == "BLUE") prefab = prefabAzul;

            if (prefab != null)
            {
                // Instanciamos usando la posición del PADRE pero emparentando al CAJÓN
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
                    Destroy(cajon.GetChild(j).gameObject);
                }
            }
        }
    }

    private void OnDisable()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnHBWUpdatePiecesEvent -= ProcesarMensajeHBW;
    }
}