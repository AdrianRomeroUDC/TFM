using UnityEngine;
using System;
using System.Collections;

[Serializable]
public class HBWPositionData
{
    public float estirar;
    public float horizontal;
    public float vertical;
}

public class ControladorHBWposition_mqtt : MonoBehaviour
{
    private float lastH, lastV, lastE;
    private bool hayNuevaOrdenEstirar = false; // Bandera para el hilo principal

    [Header("Referencias de los Ejes")]
    public Transform ejeHorizontal;
    public Transform ejeVertical;
    public Transform ejeExtension;

    [Header("Calibración PLC")]
    public float plcH_Min = 0;
    public float plcH_Max = 1985;
    public float plcV_Min = 0;
    public float plcV_Max = 850;

    [Header("Calibración Unity")]
    [ContextMenuItem("Capturar", "CapturarHMin")] public float unityH_Min;
    [ContextMenuItem("Capturar", "CapturarHMax")] public float unityH_Max;
    [ContextMenuItem("Capturar", "CapturarVMin")] public float unityV_Min;
    [ContextMenuItem("Capturar", "CapturarVMax")] public float unityV_Max;
    [ContextMenuItem("Capturar", "CapturarE_Estirado")] public float unityE_Estirado;
    [ContextMenuItem("Capturar", "CapturarE_Recogido")] public float unityE_Recogido;

    [Header("Ajustes de Animación")]
    public float lerpSpeed = 5f;
    public float tiempoAnimacion = 4f;

    private Coroutine corrutinaExtension;

    // --- CAPTURAS ---
    void CapturarHMin() { if (ejeHorizontal) unityH_Min = ejeHorizontal.localPosition.z; }
    void CapturarHMax() { if (ejeHorizontal) unityH_Max = ejeHorizontal.localPosition.z; }
    void CapturarVMin() { if (ejeVertical) unityV_Min = ejeVertical.localPosition.y; }
    void CapturarVMax() { if (ejeVertical) unityV_Max = ejeVertical.localPosition.y; }
    void CapturarE_Estirado() { if (ejeExtension) unityE_Estirado = ejeExtension.localPosition.x; }
    void CapturarE_Recogido() { if (ejeExtension) unityE_Recogido = ejeExtension.localPosition.x; }

    void Start() { InvokeRepeating("IntentarSuscripcion", 0f, 1f); }

    void IntentarSuscripcion()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnHBWPositionUpdateEvent += ActualizarPosicionDesdeMQTT;
            CancelInvoke("IntentarSuscripcion");
        }
    }

    private void OnDisable()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnHBWPositionUpdateEvent -= ActualizarPosicionDesdeMQTT;
    }

    // Este método corre en un HILO SECUNDARIO
    private void ActualizarPosicionDesdeMQTT(string json)
    {
        try
        {
            HBWPositionData data = JsonUtility.FromJson<HBWPositionData>(json);
            lastH = data.horizontal;
            lastV = data.vertical;

            // Si el valor cambia, activamos la bandera para que el Update la vea
            if (data.estirar != lastE && (data.estirar == -512 || data.estirar == 512))
            {
                lastE = data.estirar;
                hayNuevaOrdenEstirar = true;
            }
            else if (data.estirar == 0)
            {
                lastE = 0; // Guardamos el 0 pero no activamos bandera
            }
        }
        catch (Exception ex)
        {
            // Ya no dará error de Main Thread aquí
        }
    }

    void Update()
    {
        // 1. REVISAR SI HAY ORDENES DE EXTENSIÓN (Hilo Principal)
        if (hayNuevaOrdenEstirar)
        {
            hayNuevaOrdenEstirar = false; // Reset de bandera
            if (lastE == -512) IniciarAnimacionExtension(unityE_Estirado);
            else if (lastE == 512) IniciarAnimacionExtension(unityE_Recogido);
        }

        float dt = Time.deltaTime;

        // 2. MOVIMIENTO HORIZONTAL
        if (ejeHorizontal)
        {
            float tH = Mathf.InverseLerp(plcH_Min, plcH_Max, lastH);
            float targetZ = Mathf.Lerp(unityH_Min, unityH_Max, tH);
            Vector3 p = ejeHorizontal.localPosition;
            p.z = Mathf.Lerp(p.z, targetZ, lerpSpeed * dt);
            ejeHorizontal.localPosition = p;
        }

        // 3. MOVIMIENTO VERTICAL
        if (ejeVertical)
        {
            float tV = Mathf.InverseLerp(plcV_Min, plcV_Max, lastV);
            float targetY = Mathf.Lerp(unityV_Min, unityV_Max, tV);
            Vector3 p = ejeVertical.localPosition;
            p.y = Mathf.Lerp(p.y, targetY, lerpSpeed * dt);
            ejeVertical.localPosition = p;
        }
    }

    void IniciarAnimacionExtension(float destinoX)
    {
        if (corrutinaExtension != null) StopCoroutine(corrutinaExtension);
        corrutinaExtension = StartCoroutine(AnimarBrazo(destinoX));
    }

    IEnumerator AnimarBrazo(float destinoX)
    {
        float tiempoTranscurrido = 0;
        float inicioX = ejeExtension.localPosition.x;

        while (tiempoTranscurrido < tiempoAnimacion)
        {
            tiempoTranscurrido += Time.deltaTime;
            float progreso = tiempoTranscurrido / tiempoAnimacion;
            float valorX = Mathf.Lerp(inicioX, destinoX, Mathf.SmoothStep(0, 1, progreso));
            ejeExtension.localPosition = new Vector3(valorX, ejeExtension.localPosition.y, ejeExtension.localPosition.z);
            yield return null;
        }
        ejeExtension.localPosition = new Vector3(destinoX, ejeExtension.localPosition.y, ejeExtension.localPosition.z);
    }
}