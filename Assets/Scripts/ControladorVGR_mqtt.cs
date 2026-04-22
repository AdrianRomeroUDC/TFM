using UnityEngine;
using System;

[Serializable]
public class VGRData
{
    public float estirar;
    public float rotacion;
    public float vertical;
}

public class ControladorVGR_mqtt : MonoBehaviour
{
    private float lastRot, lastVert, lastExt;

    [Header("Referencias")]
    public Transform ejeRotacion;
    public Transform ejeVertical;
    public Transform ejeExtension;

    [Header("Calibración PLC")]
    public float plcRot_Min = 1395;
    public float plcRot_Max = 21;
    public float plcVert_Min = 20;
    public float plcVert_Max = 1272;
    public float plcExt_Min = 40;
    public float plcExt_Max = 1210;

    [Header("Calibración Unity (Click Derecho para Capturar)")]
    [ContextMenuItem("Capturar", "CapturarRotMin")] public float unityRot_Min;
    [ContextMenuItem("Capturar", "CapturarRotMax")] public float unityRot_Max;
    [ContextMenuItem("Capturar", "CapturarVertMin")] public float unityVert_Min;
    [ContextMenuItem("Capturar", "CapturarVertMax")] public float unityVert_Max;
    [ContextMenuItem("Capturar", "CapturarExtMin")] public float unityExt_Min;
    [ContextMenuItem("Capturar", "CapturarExtMax")] public float unityExt_Max;

    [Header("Ajustes")]
    public float lerpSpeed = 5f;

    // Métodos para el menú contextual del inspector
    void CapturarRotMin() => unityRot_Min = ejeRotacion.localEulerAngles.y;
    void CapturarRotMax() => unityRot_Max = ejeRotacion.localEulerAngles.y;
    void CapturarVertMin() => unityVert_Min = ejeVertical.localPosition.y;
    void CapturarVertMax() => unityVert_Max = ejeVertical.localPosition.y;
    void CapturarExtMin() => unityExt_Min = ejeExtension.localPosition.x;
    void CapturarExtMax() => unityExt_Max = ejeExtension.localPosition.x;

    // --- SUSCRIPCIÓN AL CLIENTE CENTRAL ---
    void Start()
    {
        InvokeRepeating("IntentarSuscripcion", 0f, 1f);
    }

    void IntentarSuscripcion()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnVGRUpdateEvent += ActualizarPosicionDesdeMQTT;
            Debug.Log("<color=green><b>VGR:</b> Suscrito al evento de posición.</color>");
            CancelInvoke("IntentarSuscripcion");
        }
    }

    private void OnDisable()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnVGRUpdateEvent -= ActualizarPosicionDesdeMQTT;
    }

    // Este método es invocado por el MQTT_Client
    private void ActualizarPosicionDesdeMQTT(string json)
    {
        try
        {
            VGRData data = JsonUtility.FromJson<VGRData>(json);
            lastRot = data.rotacion;
            lastVert = data.vertical;
            lastExt = data.estirar;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Error parseando JSON de VGR: " + ex.Message);
        }
    }

    void Update()
    {
        float speed = lerpSpeed * Time.deltaTime;

        // ROTACIÓN
        if (ejeRotacion)
        {
            float t = Mathf.InverseLerp(plcRot_Min, plcRot_Max, lastRot);
            float targetAngle = unityRot_Min + (unityRot_Max - unityRot_Min) * t;
            ejeRotacion.localRotation = Quaternion.Slerp(ejeRotacion.localRotation, Quaternion.Euler(0, targetAngle, 0), speed);
        }

        // VERTICAL
        if (ejeVertical)
        {
            float tV = Mathf.InverseLerp(plcVert_Min, plcVert_Max, lastVert);
            float targetY = Mathf.Lerp(unityVert_Min, unityVert_Max, tV);
            ejeVertical.localPosition = Vector3.Lerp(ejeVertical.localPosition, new Vector3(ejeVertical.localPosition.x, targetY, ejeVertical.localPosition.z), speed);
        }

        // EXTENSIÓN
        if (ejeExtension)
        {
            float tE = Mathf.InverseLerp(plcExt_Min, plcExt_Max, lastExt);
            float targetX = Mathf.Lerp(unityExt_Min, unityExt_Max, tE);
            ejeExtension.localPosition = Vector3.Lerp(ejeExtension.localPosition, new Vector3(targetX, ejeExtension.localPosition.y, ejeExtension.localPosition.z), speed);
        }
    }
}