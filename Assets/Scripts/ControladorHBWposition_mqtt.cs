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
    public float unityH_Min;
    public float unityH_Max;
    public float unityV_Min;
    public float unityV_Max;
    public float unityE_Estirado;
    public float unityE_Recogido;

    [Header("Ajustes de Animación")]
    public float lerpSpeed = 5f;
    public float tiempoAnimacion = 4f;

    [Header("Estado del Agarre")]
    public Transform objetoEnganchado = null;
    private Transform padreOriginalEstante = null;
    private Coroutine corrutinaExtension;

    // --- FUNCIÓN DE CAPTURA (Llamada por el Proxy) ---
    public void ProcesarCaptura(Transform cajon, Transform plataformaBrazo)
    {
        if (objetoEnganchado == null)
        {
            objetoEnganchado = cajon;
            padreOriginalEstante = cajon.parent; // Guarda "Col1Fil1"

            // TRASPLANTE: Cambia el padre del cajón a la plataforma roja
            cajon.SetParent(plataformaBrazo, true);

            // DESACTIVAR FÍSICA: Evita que el cajón se caiga o vibre al moverse
            if (cajon.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            Debug.Log("<color=green><b>[ÉXITO]</b></color> Cajón unido a PlataformaBrazo.");
        }
    }

    private void SoltarCajon()
    {
        if (objetoEnganchado != null)
        {
            // Devuelve el cajón a su hueco original en el estante
            objetoEnganchado.SetParent(padreOriginalEstante, true);

            if (objetoEnganchado.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
            objetoEnganchado = null;
            Debug.Log("<color=yellow>Cajón devuelto al estante.</color>");
        }
    }

    // --- LÓGICA MQTT ---
    void Start() { InvokeRepeating("IntentarSuscripcion", 0f, 1f); }

    void IntentarSuscripcion()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnHBWPositionUpdateEvent += ActualizarPosicionDesdeMQTT;
            CancelInvoke("IntentarSuscripcion");
        }
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
            else if (data.estirar == 0) lastE = 0;
        }
        catch { }
    }

    // --- MOVIMIENTO ---
    void Update()
    {
        if (hayNuevaOrdenEstirar)
        {
            hayNuevaOrdenEstirar = false;
            IniciarAnimacionExtension(lastE == -512 ? unityE_Estirado : unityE_Recogido);
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

            // Condición para soltar (cuando el eje vertical baja al mínimo)
            if (objetoEnganchado != null && lastV < plcV_Min + 20) SoltarCajon();
        }
    }

    void IniciarAnimacionExtension(float d)
    {
        if (corrutinaExtension != null) StopCoroutine(corrutinaExtension);
        corrutinaExtension = StartCoroutine(AnimarBrazo(d));
    }

    IEnumerator AnimarBrazo(float d)
    {
        float t = 0;
        float inicioX = ejeExtension.localPosition.x;
        while (t < tiempoAnimacion)
        {
            t += Time.deltaTime;
            float progreso = t / tiempoAnimacion;
            float vX = Mathf.Lerp(inicioX, d, Mathf.SmoothStep(0, 1, progreso));
            ejeExtension.localPosition = new Vector3(vX, ejeExtension.localPosition.y, ejeExtension.localPosition.z);
            yield return null;
        }
        ejeExtension.localPosition = new Vector3(d, ejeExtension.localPosition.y, ejeExtension.localPosition.z);
    }
}