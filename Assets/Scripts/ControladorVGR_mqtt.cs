using UnityEngine;
using System.Collections;

public class ControladorVGR_mqtt : MonoBehaviour
{
    private float lastRot, lastVert, lastExt;

    [Header("Referencias")]
    public Transform ejeRotacion;
    public Transform ejeVertical;
    public Transform ejeExtension;

    [Header("Calibración PLC")]
    public float plcRot_Min = 1395; public float plcRot_Max = 21;
    public float plcVert_Min = 20; public float plcVert_Max = 1272;
    public float plcExt_Min = 40; public float plcExt_Max = 1210;

    [Header("Calibración Unity (Click Derecho para Capturar)")]
    [ContextMenuItem("Capturar", "CapturarRotMin")] public float unityRot_Min;
    [ContextMenuItem("Capturar", "CapturarRotMax")] public float unityRot_Max;
    [ContextMenuItem("Capturar", "CapturarVertMin")] public float unityVert_Min;
    [ContextMenuItem("Capturar", "CapturarVertMax")] public float unityVert_Max;
    [ContextMenuItem("Capturar", "CapturarExtMin")] public float unityExt_Min;
    [ContextMenuItem("Capturar", "CapturarExtMax")] public float unityExt_Max;

    [Header("Ajustes")]
    public float lerpSpeed = 5f;

    void CapturarRotMin() => unityRot_Min = ejeRotacion.localEulerAngles.y;
    void CapturarRotMax() => unityRot_Max = ejeRotacion.localEulerAngles.y;
    void CapturarVertMin() => unityVert_Min = ejeVertical.localPosition.y;
    void CapturarVertMax() => unityVert_Max = ejeVertical.localPosition.y;
    void CapturarExtMin() => unityExt_Min = ejeExtension.localPosition.x;
    void CapturarExtMax() => unityExt_Max = ejeExtension.localPosition.x;

    void Start()
    {
        StartCoroutine(SuscripcionSegura());
    }

    IEnumerator SuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;
        MQTTClient.Instance.OnVGRPositionUpdateEvent += ActualizarPosicionDesdeMQTT;
        Debug.Log("<color=green>VGR Suscrito correctamente</color>");
    }

    private void OnDisable()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnVGRPositionUpdateEvent -= ActualizarPosicionDesdeMQTT;
    }

    // Recibe los datos ya extraídos del JSON
    private void ActualizarPosicionDesdeMQTT(float rot, float vert, float ext)
    {
        // Debug para verificar que los datos entran bien
        // Debug.Log($"R: {rot}, V: {vert}, E: {ext}"); 
        lastRot = rot;
        lastVert = vert;
        lastExt = ext;
    }

    void Update()
    {
        float speed = lerpSpeed * Time.deltaTime;

        // ROTACIÓN - Usando la fórmula que te funcionaba antes
        if (ejeRotacion)
        {
            float t = Mathf.InverseLerp(plcRot_Min, plcRot_Max, lastRot);
            // Fórmula original:
            float targetAngle = unityRot_Min + (unityRot_Max - unityRot_Min) * t;

            ejeRotacion.localRotation = Quaternion.Slerp(
                ejeRotacion.localRotation,
                Quaternion.Euler(0, targetAngle, 0),
                speed
            );
        }

        // VERTICAL
        if (ejeVertical)
        {
            float tV = Mathf.InverseLerp(plcVert_Min, plcVert_Max, lastVert);
            float targetY = Mathf.Lerp(unityVert_Min, unityVert_Max, tV);
            ejeVertical.localPosition = Vector3.Lerp(
                ejeVertical.localPosition,
                new Vector3(ejeVertical.localPosition.x, targetY, ejeVertical.localPosition.z),
                speed
            );
        }

        // EXTENSIÓN
        if (ejeExtension)
        {
            float tE = Mathf.InverseLerp(plcExt_Min, plcExt_Max, lastExt);
            float targetX = Mathf.Lerp(unityExt_Min, unityExt_Max, tE);
            ejeExtension.localPosition = Vector3.Lerp(
                ejeExtension.localPosition,
                new Vector3(targetX, ejeExtension.localPosition.y, ejeExtension.localPosition.z),
                speed
            );
        }
    }

}