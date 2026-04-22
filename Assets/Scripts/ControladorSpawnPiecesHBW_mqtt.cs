using UnityEngine;
using System;

[Serializable]
public class HBWStockPayload { public string[] piezas; }

public class ControladorSpawnPiecesHBW_mqtt : MonoBehaviour
{
    private string lastJsonReceived;
    private bool pendingUpdate = false;

    [Header("Referencias de Escena")]
    public Transform[] puntosDeHueco;

    [Header("Prefabs de Pieza")]
    public GameObject prefabBlanco;
    public GameObject prefabRojo;
    public GameObject prefabAzul;

    void Start()
    {
        LimpiarEstantes();
        InvokeRepeating("IntentarSuscripcion", 0f, 1f);
    }

    void IntentarSuscripcion()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnHBWUpdateEvent += ProcesarMensajeHBW;

            // Si el mensaje llegó antes de que yo naciera, lo pido ahora
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
            foreach (Transform hijo in puntosDeHueco[i]) Destroy(hijo.gameObject);

            GameObject prefab = null;
            string color = listaColores[i].Trim().ToUpper();
            if (color == "WHITE") prefab = prefabBlanco;
            else if (color == "RED") prefab = prefabRojo;
            else if (color == "BLUE") prefab = prefabAzul;

            if (prefab != null)
            {
                GameObject nueva = Instantiate(prefab, puntosDeHueco[i].position, puntosDeHueco[i].rotation);
                nueva.transform.SetParent(puntosDeHueco[i]);
                if (nueva.GetComponent<Rigidbody>()) nueva.GetComponent<Rigidbody>().isKinematic = true;
            }
        }
    }

    void LimpiarEstantes() { 
        foreach (var h in puntosDeHueco) 
            foreach (Transform hijo in h) 
                Destroy(hijo.gameObject); 
    }

    private void OnDisable() { 
        if (MQTTClient.Instance != null) 
            MQTTClient.Instance.OnHBWUpdateEvent -= ProcesarMensajeHBW; 
    }
}