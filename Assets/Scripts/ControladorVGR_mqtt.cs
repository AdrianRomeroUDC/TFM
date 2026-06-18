using UnityEngine;
using System.Collections;

public class ControladorVGR_mqtt : MonoBehaviour
{
    private float lastRot, lastVert, lastExt;
    private bool estadoGripPendiente = false;
    private bool cambioGripDetectado = false;

    private Transform piezaCercana;
    private Transform piezaEnganchada;
    private ContenedorHBW_proxy contenedorActual;

    [Header("Referencias")]
    public Transform ejeRotacion;
    public Transform ejeVertical;
    public Transform ejeExtension;
    public Transform puntoAnclajeVentosa;

    [Header("Ajustes Agarre (Hijo en Ventosa)")]
    public Vector3 posicionEnPinza = new Vector3(0f, 0.05f, 0f);
    public Vector3 rotacionEnPinza = Vector3.zero;

    [Header("Ajuste Fino de Escaneo (¡Para el Radar Ventosa!)")]
    [Tooltip("Desfase local desde la ventosa hacia abajo (ej: -0.05 en Y o Z según orientación) para detectar la pieza en la rampa sin tocarla.")]
    public Vector3 offsetBusquedaVentosa = new Vector3(0f, -0.02f, 0f);
    [Tooltip("Tamaño de la esfera del radar de agarre.")]
    public float radioBusquedaVentosa = 0.05f;
    public bool mostrarGizmosVentosa = true;

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

    public void RegistrarContenedorBajoVentosa(ContenedorHBW_proxy contenedor)
    {
        contenedorActual = contenedor;
    }

    public ContenedorHBW_proxy ObtenerContenedorActual() => contenedorActual;
    public Transform ObtenerPiezaEnganchada() => piezaEnganchada;
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
                // ¡CORRECCIÓN AQUÍ!: Calculamos el centro usando el offset dinámico
                Vector3 centroBusquedaMundial = puntoAnclajeVentosa.TransformPoint(offsetBusquedaVentosa);

                Collider[] collidersEnVentosa = Physics.OverlapSphere(centroBusquedaMundial, radioBusquedaVentosa);
                foreach (Collider col in collidersEnVentosa)
                {
                    if (col.name.ToLower().Contains("pieza"))
                    {
                        piezaEnganchada = col.transform;
                        Debug.Log("<color=cyan><b>[VGR]:</b> Pieza detectada en rango del radar y fijada: </color>" + piezaEnganchada.name);
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
        }
        else
        {
            if (piezaEnganchada != null)
            {
                ContenedorHBW_proxy destinoFinal = contenedorActual;

                if (piezaEnganchada.parent == puntoAnclajeVentosa)
                {
                    piezaEnganchada.SetParent(null);
                }

                if (destinoFinal != null)
                {
                    destinoFinal.AcoplarPiezaDirecto(piezaEnganchada);
                    Debug.Log($"<color=orange><b>[VGR MQTT]:</b> Entrega confirmada en [{destinoFinal.name}].</color>");
                }
                else
                {
                    if (piezaEnganchada.parent == null)
                    {
                        piezaEnganchada.position += new Vector3(0f, 0.025f, 0f);

                        BoxCollider[] allCols = piezaEnganchada.GetComponentsInChildren<BoxCollider>();
                        foreach (BoxCollider c in allCols) if (c != null) c.isTrigger = false;

                        Rigidbody rb = piezaEnganchada.GetComponent<Rigidbody>();
                        if (rb == null) rb = piezaEnganchada.gameObject.AddComponent<Rigidbody>();

                        rb.isKinematic = false;
                        rb.useGravity = true;
                        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }

                contenedorActual = null;
                piezaEnganchada = null;
                piezaCercana = null;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.name.ToLower().Contains("pieza")) SetPiezaCercana(other.transform);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.name.ToLower().Contains("pieza") && piezaEnganchada == null) SetPiezaCercana(null);
    }

    // --- EL GIZMO VISUAL PARA TU ESCENA ---
    void OnDrawGizmos()
    {
        if (!mostrarGizmosVentosa || puntoAnclajeVentosa == null) return;

        // Convierte el offset local a coordenadas del mundo real basado en la rotación de la ventosa
        Vector3 centroBusquedaMundial = puntoAnclajeVentosa.TransformPoint(offsetBusquedaVentosa);

        // 1. Dibujar esfera de alambre del radar
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(centroBusquedaMundial, radioBusquedaVentosa);

        // 2. Dibujar una pequeña esfera sólida en el centro del radar
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f); // Cian con transparencia
        Gizmos.DrawSphere(centroBusquedaMundial, radioBusquedaVentosa * 0.2f);

        // 3. Línea guía que une la punta de la ventosa con el centro de búsqueda
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(puntoAnclajeVentosa.position, centroBusquedaMundial);
    }
}