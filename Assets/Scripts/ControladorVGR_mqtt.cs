using UnityEngine;
using System.Collections;

public class ControladorVGR_mqtt : MonoBehaviour
{
    private float lastRot, lastVert, lastExt;

    // Variables para manejar el hilo de Unity
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

    private void ActualizarPosicionDesdeMQTT(float rot, float vert, float ext)
    {
        lastRot = rot;
        lastVert = vert;
        lastExt = ext;
    }

    private void RecibirGripMQTT(bool activo)
    {
        estadoGripPendiente = activo;
        cambioGripDetectado = true;
    }

    public void SetPiezaCercana(Transform pieza) => piezaCercana = pieza;

    void Update()
    {
        if (cambioGripDetectado)
        {
            ProcesarLogicaGrip(estadoGripPendiente);
            cambioGripDetectado = false;
        }

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

    private void ProcesarLogicaGrip(bool activo)
    {
        if (activo)
        {
            if (puntoAnclajeVentosa != null && piezaEnganchada == null)
            {
                Collider[] collidersEnVentosa = Physics.OverlapSphere(puntoAnclajeVentosa.position, 0.15f);
                foreach (Collider col in collidersEnVentosa)
                {
                    if (col.name.ToLower().Contains("pieza"))
                    {
                        Transform raizPieza = col.transform;
                        while (raizPieza.parent != null && raizPieza.parent != puntoAnclajeVentosa && raizPieza.parent.name.ToLower().Contains("pieza"))
                        {
                            raizPieza = raizPieza.parent;
                        }

                        piezaEnganchada = raizPieza;
                        Debug.Log("<color=cyan><b>[VGR Vacío]:</b> Pieza succionada con éxito: </color>" + piezaEnganchada.name);
                        break;
                    }
                }
            }

            if (piezaEnganchada != null)
            {
                Rigidbody rb = piezaEnganchada.GetComponent<Rigidbody>();
                if (rb == null) rb = piezaEnganchada.gameObject.AddComponent<Rigidbody>();

                rb.isKinematic = true;
                rb.useGravity = false;

                BoxCollider[] colliders = piezaEnganchada.GetComponentsInChildren<BoxCollider>();
                foreach (BoxCollider col in colliders)
                {
                    col.isTrigger = true;
                }

                piezaEnganchada.SetParent(puntoAnclajeVentosa);
                piezaEnganchada.position = puntoAnclajeVentosa.position;
                piezaEnganchada.rotation = Quaternion.Euler(rotacionEnPinza);
                piezaEnganchada.localScale = Vector3.one;
                piezaEnganchada.Translate(posicionEnPinza, Space.Self);
            }
            else
            {
                Debug.LogWarning("<color=red><b>[VGR]:</b> El PLC ordenó GRIP, pero no hay pieza bajo la ventosa.</color>");
            }
        }
        else
        {
            // --- LÓGICA DE SUELTA BLINDADA CONTRA FILTRACIONES ---
            if (piezaEnganchada != null)
            {
                // 1. La desvinculamos del robot
                piezaEnganchada.SetParent(null);

                // 2. TRUCO DE SEGURIDAD: La subimos 2.5 centímetros en el espacio del mundo 
                // para evitar que el desfase del Trigger la haga nacer empotrada en el suelo de la cesta
                piezaEnganchada.position += new Vector3(0f, 0.025f, 0f);

                // 3. Forzamos a apagar el Trigger inmediatamente
                BoxCollider[] allCols = piezaEnganchada.GetComponentsInChildren<BoxCollider>();
                foreach (BoxCollider c in allCols)
                {
                    if (c != null) c.isTrigger = false;
                }

                // 4. Activamos físicas dinámicas en modo continuo
                Rigidbody rb = piezaEnganchada.GetComponent<Rigidbody>();
                if (rb == null) rb = piezaEnganchada.gameObject.AddComponent<Rigidbody>();

                rb.isKinematic = false;
                rb.useGravity = true;

                // Esto obliga a Unity a escanear milímetro a milímetro la caída para evitar el "efecto túnel"
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                Debug.Log("<color=lime><b>[VGR BLINDAJE]:</b> Pieza reposicionada y soltada sólidamente de forma nativa.</color>");

                piezaEnganchada = null;
                piezaCercana = null;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.name.ToLower().Contains("pieza"))
        {
            SetPiezaCercana(other.transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.name.ToLower().Contains("pieza"))
        {
            if (piezaEnganchada == null) SetPiezaCercana(null);
        }
    }
}