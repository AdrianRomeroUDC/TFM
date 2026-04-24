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
    private bool hayNuevaOrdenEstirar = false;

    [Header("Referencias de los Ejes")]
    public Transform ejeHorizontal;
    public Transform ejeVertical;
    public Transform ejeExtension;

    [Header("Calibración PLC")]
    public float plcH_Min = 0;
    public float plcH_Max = 1985;
    public float plcV_Min = 0;
    public float plcV_Max = 845;

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

    [Header("Agarre de Objetos")]
    private Transform objetoEnganchado = null;
    private Transform padreOriginalObjeto = null;

    private Coroutine corrutinaExtension;

    // --- LOGICA DE AGARRE ---
    // Detectamos cuando la plataforma del brazo toca el cajón
    private void OnCollisionEnter(Collision collision)
    {
        // Si el objeto tocado es un cajón y el brazo está estirado (o estirándose)
        if (collision.gameObject.name.Contains("container") && objetoEnganchado == null)
        {
            objetoEnganchado = collision.transform;
            padreOriginalObjeto = objetoEnganchado.parent; // Guardamos el ColXFilX

            // Hacemos que el cajón sea hijo del eje de extensión
            objetoEnganchado.SetParent(ejeExtension);

            // Si tiene Rigidbody, lo ponemos en Kinematic para que no vibre
            if (objetoEnganchado.TryGetComponent<Rigidbody>(out Rigidbody rb))
                rb.isKinematic = true;

            Debug.Log("Cajón enganchado: " + objetoEnganchado.name);
        }
    }

    // Detectamos cuando el brazo suelta el objeto (ej: al dejarlo en el estante y bajar el eje vertical)
    private void OnCollisionExit(Collision collision)
    {
        if (objetoEnganchado != null && collision.transform == objetoEnganchado)
        {
            // Opcional: Podrías implementar una lógica para soltarlo aquí 
            // o basarte en la posición del PLC
        }
    }

    // --- RESTO DEL SCRIPT ORIGINAL ---
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

    private void ActualizarPosicionDesdeMQTT(string json)
    {
        try
        {
            HBWPositionData data = JsonUtility.FromJson<HBWPositionData>(json);
            lastH = data.horizontal;
            lastV = data.vertical;

            if (data.estirar != lastE && (data.estirar == -512 || data.estirar == 512))
            {
                lastE = data.estirar;
                hayNuevaOrdenEstirar = true;
            }
            else if (data.estirar == 0)
            {
                lastE = 0;
            }
        }
        catch (Exception) { }
    }

    void Update()
    {
        if (hayNuevaOrdenEstirar)
        {
            hayNuevaOrdenEstirar = false;
            if (lastE == -512) IniciarAnimacionExtension(unityE_Estirado);
            else if (lastE == 512) IniciarAnimacionExtension(unityE_Recogido);
        }

        float dt = Time.deltaTime;

        if (ejeHorizontal)
        {
            float tH = Mathf.InverseLerp(plcH_Min, plcH_Max, lastH);
            float targetZ = Mathf.Lerp(unityH_Min, unityH_Max, tH);
            Vector3 p = ejeHorizontal.localPosition;
            p.z = Mathf.Lerp(p.z, targetZ, lerpSpeed * dt);
            ejeHorizontal.localPosition = p;
        }

        if (ejeVertical)
        {
            float tV = Mathf.InverseLerp(plcV_Min, plcV_Max, lastV);
            float targetY = Mathf.Lerp(unityV_Min, unityV_Max, tV);
            Vector3 p = ejeVertical.localPosition;
            p.y = Mathf.Lerp(p.y, targetY, lerpSpeed * dt);
            ejeVertical.localPosition = p;

            // SI EL BRAZO BAJA Y TENEMOS ALGO, SOLTAMOS
            if (objetoEnganchado != null && lastV < plcV_Min + 10) // Umbral pequeño
            {
                // Aquí podrías devolverlo a su padre original si es necesario
            }
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