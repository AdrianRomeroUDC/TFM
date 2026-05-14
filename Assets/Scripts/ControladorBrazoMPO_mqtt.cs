using UnityEngine;
using System.Collections;

public class ControladorBrazoMPO : MonoBehaviour
{
    [Header("Componentes")]
    public Transform ejeHorizontal;
    public Transform ejeVertical;

    [Header("Posiciones Guardadas (Vector3)")]
    [ContextMenuItem("Capturar Posicion Actual", "CapturarPosHorno")]
    public Vector3 posHorno;
    [ContextMenuItem("Capturar Posicion Actual", "CapturarPosTurntable")]
    public Vector3 posTurntable;

    [Header("Ajustes Verticales (Local Y)")]
    [ContextMenuItem("Capturar Altura Actual", "CapturarReposo")]
    public float yReposo = 0f;
    [ContextMenuItem("Capturar Altura Actual", "CapturarPickup")]
    public float yPickup = -0.05f;

    [Header("Configuración de Velocidad")]
    public float velocidadH = 2f;
    public float velocidadV = 1.5f;

    private Vector3 targetPosH;
    private float targetYV;
    private bool ejecutandoPickup = false;

    // --- MÉTODOS DE CAPTURA ---
    void CapturarPosHorno() { if (ejeHorizontal != null) posHorno = ejeHorizontal.localPosition; Debug.Log("Pos Horno Capturada: " + posHorno); }
    void CapturarPosTurntable() { if (ejeHorizontal != null) posTurntable = ejeHorizontal.localPosition; Debug.Log("Pos Turntable Capturada: " + posTurntable); }
    void CapturarReposo() { if (ejeVertical != null) yReposo = ejeVertical.localPosition.y; Debug.Log("Y Reposo Capturada: " + yReposo); }
    void CapturarPickup() { if (ejeVertical != null) yPickup = ejeVertical.localPosition.y; Debug.Log("Y Pickup Capturada: " + yPickup); }

    void Start()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnBrazoUpdateEvent += AlRecibirComandoBrazo;
            Debug.Log("<color=cyan>Brazo suscrito a eventos MQTT</color>");
        }
        else
        {
            Debug.LogError("No se encontró MQTTClient.Instance en la escena.");
        }

        targetPosH = ejeHorizontal.localPosition;
        targetYV = yReposo;
    }

    void AlRecibirComandoBrazo(MPOBrazoPayload data)
    {
        Debug.Log($"MQTT recibido en Brazo - Ref3: {data.move2Ref3}, Ref4: {data.move2Ref4}, Pickup: {data.pickup}");

        if (data.move2Ref4 == 1)
        {
            targetPosH = posHorno;
            Debug.Log("Moviendo a Horno: " + posHorno);
        }
        else if (data.move2Ref3 == 1)
        {
            targetPosH = posTurntable;
            Debug.Log("Moviendo a Turntable: " + posTurntable);
        }

        if (data.pickup == 1 && !ejecutandoPickup)
        {
            Debug.Log("Iniciando secuencia Pickup");
            StartCoroutine(SecuenciaVertical());
        }
    }

    IEnumerator SecuenciaVertical()
    {
        ejecutandoPickup = true;
        targetYV = yPickup;
        // Esperar a que baje
        while (Mathf.Abs(ejeVertical.localPosition.y - targetYV) > 0.001f) yield return null;

        Debug.Log("Brazo abajo, esperando...");
        yield return new WaitForSeconds(0.6f);

        targetYV = yReposo;
        // Esperar a que suba
        while (Mathf.Abs(ejeVertical.localPosition.y - targetYV) > 0.001f) yield return null;

        Debug.Log("Brazo arriba, secuencia terminada.");
        ejecutandoPickup = false;
    }

    void Update()
    {
        // Movimiento horizontal
        ejeHorizontal.localPosition = Vector3.MoveTowards(ejeHorizontal.localPosition, targetPosH, velocidadH * Time.deltaTime);

        // Movimiento vertical
        Vector3 posV = ejeVertical.localPosition;
        posV.y = Mathf.MoveTowards(posV.y, targetYV, velocidadV * Time.deltaTime);
        ejeVertical.localPosition = posV;
    }

    private void OnDestroy()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnBrazoUpdateEvent -= AlRecibirComandoBrazo;
    }
}