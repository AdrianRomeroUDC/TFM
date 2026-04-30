using UnityEngine;
using System.Collections;

public class ControladorVGR_mqtt : MonoBehaviour
{
    private float lastRot, lastVert, lastExt;

    // Cambiamos a estas variables para manejar el hilo de Unity
    private bool estadoGripPendiente = false;
    private bool cambioGripDetectado = false;

    private Transform piezaCercana;
    private Transform piezaEnganchada;

    [Header("Referencias")]
    public Transform ejeRotacion;
    public Transform ejeVertical;
    public Transform ejeExtension;
    public Transform puntoAnclajeVentosa;

    [Header("Ajustes Agarre")]
    public Vector3 posicionEnPinza = new Vector3(0f, 0.05f, 0f);
    public Vector3 rotacionEnPinza = Vector3.zero;

    [Header("Calibración PLC")]
    public float plcRot_Min = 1395; public float plcRot_Max = 21;
    public float plcVert_Min = 20; public float plcVert_Max = 1272;
    public float plcExt_Min = 40; public float plcExt_Max = 1210;

    [Header("Calibración Unity")]
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
        MQTTClient.Instance.OnVGRGripEvent += RecibirGripMQTT;
        Debug.Log("<color=green>VGR Suscrito correctamente</color>");
    }

    private void OnDisable()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnVGRPositionUpdateEvent -= ActualizarPosicionDesdeMQTT;
            MQTTClient.Instance.OnVGRGripEvent -= RecibirGripMQTT;
        }
    }

    // Este método sigue corriendo en el hilo de MQTT (segundo plano)
    private void ActualizarPosicionDesdeMQTT(float rot, float vert, float ext)
    {
        lastRot = rot;
        lastVert = vert;
        lastExt = ext;
    }

    // Solo guardamos el estado, no ejecutamos lógica de Unity aquí
    private void RecibirGripMQTT(bool activo)
    {
        estadoGripPendiente = activo;
        cambioGripDetectado = true;
    }

    public void SetPiezaCercana(Transform pieza) => piezaCercana = pieza;

    void Update()
    {
        // 1. PROCESAR CAMBIO DE GRIP (HILO PRINCIPAL)
        if (cambioGripDetectado)
        {
            ProcesarLogicaGrip(estadoGripPendiente);
            cambioGripDetectado = false;
        }

        // 2. LÓGICA DE MOVIMIENTO
        float speed = lerpSpeed * Time.deltaTime;

        if (ejeRotacion)
        {
            float t = Mathf.InverseLerp(plcRot_Min, plcRot_Max, lastRot);
            float targetAngle = unityRot_Min + (unityRot_Max - unityRot_Min) * t;
            ejeRotacion.localRotation = Quaternion.Slerp(ejeRotacion.localRotation, Quaternion.Euler(0, targetAngle, 0), speed);
        }

        if (ejeVertical)
        {
            float tV = Mathf.InverseLerp(plcVert_Min, plcVert_Max, lastVert);
            float targetY = Mathf.Lerp(unityVert_Min, unityVert_Max, tV);
            ejeVertical.localPosition = Vector3.Lerp(ejeVertical.localPosition, new Vector3(ejeVertical.localPosition.x, targetY, ejeVertical.localPosition.z), speed);
        }

        if (ejeExtension)
        {
            float tE = Mathf.InverseLerp(plcExt_Min, plcExt_Max, lastExt);
            float targetX = Mathf.Lerp(unityExt_Min, unityExt_Max, tE);
            ejeExtension.localPosition = Vector3.Lerp(ejeExtension.localPosition, new Vector3(targetX, ejeExtension.localPosition.y, ejeExtension.localPosition.z), speed);
        }
    }

    // Esta función ahora corre dentro del Update, por lo que SetParent funcionará
    private void ProcesarLogicaGrip(bool activo)
    {
        if (activo)
        {
            if (piezaCercana != null && piezaEnganchada == null)
            {
                piezaEnganchada = piezaCercana;

                Rigidbody rb = piezaEnganchada.GetComponent<Rigidbody>();
                if (rb == null) rb = piezaEnganchada.gameObject.AddComponent<Rigidbody>();

                // Mientras la llevas: quieta y fantasma
                rb.isKinematic = true;
                rb.useGravity = false;

                if (piezaEnganchada.TryGetComponent<BoxCollider>(out BoxCollider col))
                    col.isTrigger = true;

                piezaEnganchada.SetParent(puntoAnclajeVentosa);
                piezaEnganchada.position = puntoAnclajeVentosa.position;
                piezaEnganchada.rotation = Quaternion.Euler(rotacionEnPinza);
                piezaEnganchada.localScale = Vector3.one;
                piezaEnganchada.Translate(posicionEnPinza, Space.Self);
            }
        }
        else
        {
            if (piezaEnganchada != null)
            {
                // 1. Soltamos la pieza
                piezaEnganchada.SetParent(null);

                // 2. ¡CAÍDA INMEDIATA!
                if (piezaEnganchada.TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    rb.isKinematic = false; // Permite que caiga ya
                    rb.useGravity = true;   // Activa la gravedad
                }

                // 3. Mantenemos 'isTrigger = true' para que caiga atravesando el cajón sin chocar
                // pero lanzamos una pequeña rutina para que se vuelva sólida en medio segundo
                StartCoroutine(VolverSolidaTrasCaida(piezaEnganchada));

                piezaEnganchada = null;
            }
        }
    }

    // Esta rutina hace que la pieza sea sólida poco después de soltarla
    IEnumerator VolverSolidaTrasCaida(Transform pieza)
    {
        // Esperamos 0.5 segundos (suficiente para salir del área del cajón/brazo)
        yield return new WaitForSeconds(0.5f);

        if (pieza != null && pieza.TryGetComponent<BoxCollider>(out BoxCollider col))
        {
            col.isTrigger = false; // Ahora ya puede chocar con el suelo
            Debug.Log("<color=green>Pieza ahora es sólida.</color>");
        }
    }

    // Mantén el OnTriggerExit si quieres una seguridad extra por si el brazo se mueve rápido
    private void OnTriggerExit(Collider other)
    {
        if (other.name.Contains("cajon"))
        {
            // Si por algún motivo la pieza sigue siendo trigger, la forzamos a sólida
            // Esto sirve de "red de seguridad"
        }
    }
}