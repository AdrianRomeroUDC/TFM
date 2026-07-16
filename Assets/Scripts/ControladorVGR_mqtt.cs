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
    [Tooltip("Desfase local desde la ventosa hacia abajo para detectar la pieza en la rampa sin tocarla.")]
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

    // --- VARIABLES PARA MONITOREAR CONTROL DE CALIDAD DSI (MUNDIAL) ---
    private bool dsiSensorActivo = false;
    private bool verificarFalloAgarreDSI = false;
    private float yMundialAlAgarrar = 0f;

    // 🛡️ NUEVO: Memoria de transformación local de spawn
    private Vector3 posicionLocalOriginalDSI;
    private Quaternion rotacionLocalOriginalDSI;

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
        MQTTClient.Instance.OnDPSPiezaDSIEvent += ActualizarSensorDSI;

        Debug.Log("<color=green><b>[VGR SUSCRIPCIÓN]:</b> VGR Suscrito correctamente.</color>");
    }

    private void OnDisable()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnVGRPositionUpdateEvent -= ActualizarPosicionDesdeMQTT;
            MQTTClient.Instance.OnVGRGripEvent -= RecibirGripMQTT;
            MQTTClient.Instance.OnDPSPiezaDSIEvent -= ActualizarSensorDSI;
        }
    }

    private void Update()
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

        // --- SISTEMA ANTIFALLO EN ESPACIO MUNDIAL ---
        if (verificarFalloAgarreDSI)
        {
            if (piezaEnganchada == null)
            {
                verificarFalloAgarreDSI = false;
                return;
            }

            float deltaYMundial = Mathf.Abs(ejeVertical.position.y - yMundialAlAgarrar);

            if (deltaYMundial > 0.015f) // 1.5 cm reales en el espacio 3D
            {
                Debug.Log($"<color=yellow><b>[VGR CHEQUEO MUNDIAL]:</b> Altura límite superada. Delta: {deltaYMundial:F4}m. dsi_sensor = {dsiSensorActivo}</color>");

                if (dsiSensorActivo)
                {
                    // ¡FALLO! Devolvemos la pieza virtual a su posición exacta de spawn original
                    Debug.Log("<color=red><b>[VGR FALLO AGARRE DSI]:</b> ¡FALLO! dsi_sensor = True. Devolviendo pieza virtual a la posición de spawn exacta.</color>");

                    ControladorDPS_mqtt dps = Object.FindFirstObjectByType<ControladorDPS_mqtt>();
                    if (dps != null && dps.plataformaDSI != null)
                    {
                        piezaEnganchada.SetParent(dps.plataformaDSI);

                        // RESTAURACIÓN DE PRECISIÓN ABSOLUTA
                        piezaEnganchada.localPosition = posicionLocalOriginalDSI;
                        piezaEnganchada.localRotation = rotacionLocalOriginalDSI;

                        Rigidbody rb = piezaEnganchada.GetComponent<Rigidbody>();
                        if (rb != null) rb.isKinematic = true;

                        BoxCollider[] colliders = piezaEnganchada.GetComponentsInChildren<BoxCollider>();
                        foreach (BoxCollider col in colliders) if (col != null) col.isTrigger = false;
                    }
                    piezaEnganchada = null;
                }
                else
                {
                    Debug.Log("<color=green><b>[VGR AGARRE ÉXITO]:</b> dsi_sensor = False. Agarre confirmado.</color>");
                }

                verificarFalloAgarreDSI = false;
            }
        }
    }

    private void ActualizarPosicionDesdeMQTT(float rot, float vert, float ext)
    {
        lastRot = rot; lastVert = vert; lastExt = ext;
    }

    private void RecibirGripMQTT(bool activo)
    {
        estadoGripPendiente = activo;
        cambioGripDetectado = true;
    }

    private void ActualizarSensorDSI(bool detectado)
    {
        dsiSensorActivo = detectado;
    }

    public void RegistrarContenedorBajoVentosa(ContenedorHBW_proxy contenedor) => contenedorActual = contenedor;
    public ContenedorHBW_proxy @ObtenerContenedorActual() => contenedorActual;
    public Transform ObtenerPiezaEnganchada() => piezaEnganchada;
    public void AsignarPiezaEnganchada(Transform nuevaPieza)
    {
        piezaEnganchada = nuevaPieza;
    }
    public void SetPiezaCercana(Transform pieza) => piezaCercana = pieza;

    private void ProcesarLogicaGrip(bool activo)
    {
        if (activo)
        {
            if (puntoAnclajeVentosa != null && piezaEnganchada == null)
            {
                Vector3 centroBusquedaMundial = puntoAnclajeVentosa.TransformPoint(offsetBusquedaVentosa);

                Collider[] collidersEnVentosa = Physics.OverlapSphere(centroBusquedaMundial, radioBusquedaVentosa);
                foreach (Collider col in collidersEnVentosa)
                {
                    Transform objetoActual = col.transform;
                    Transform piezaReal = null;

                    while (objetoActual != null)
                    {
                        if (objetoActual.name.ToLower().StartsWith("pieza")) { piezaReal = objetoActual; break; }
                        if (objetoActual.name.ToLower().Contains("cajon") || objetoActual.name.ToLower().Contains("contenedor")) break;
                        objetoActual = objetoActual.parent;
                    }

                    if (piezaReal != null) { piezaEnganchada = piezaReal; break; }
                    else if (col.name.ToLower().Contains("pieza")) { piezaEnganchada = col.transform; break; }
                }
            }

            if (piezaEnganchada != null)
            {
                bool esDeDSI = piezaEnganchada.name.ToLower().Contains("dsi") ||
                               (piezaEnganchada.parent != null && piezaEnganchada.parent.name.ToLower().Contains("dsi"));

                // 💾 SALVAGUARDAMOS SU POSICIÓN DE SPAWN JUSTO ANTES DE DESVINCULARLA DE LA PLATAFORMA
                if (esDeDSI)
                {
                    posicionLocalOriginalDSI = piezaEnganchada.localPosition;
                    rotacionLocalOriginalDSI = piezaEnganchada.localRotation;

                    verificarFalloAgarreDSI = true;
                    yMundialAlAgarrar = ejeVertical.position.y;

                    Debug.Log($"<color=orange><b>[VGR MONITOREO MUNDIAL]:</b> Altura inicial: {yMundialAlAgarrar:F6}. Posición original de spawn memorizada: {posicionLocalOriginalDSI}.</color>");
                }

                Rigidbody rb = piezaEnganchada.GetComponent<Rigidbody>();
                if (rb == null) rb = piezaEnganchada.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                BoxCollider[] colliders = piezaEnganchada.GetComponentsInChildren<BoxCollider>();
                foreach (BoxCollider col in colliders) if (col != null) col.isTrigger = true;

                piezaEnganchada.SetParent(puntoAnclajeVentosa);
                piezaEnganchada.position = puntoAnclajeVentosa.position;
                piezaEnganchada.rotation = Quaternion.Euler(rotacionEnPinza);
                piezaEnganchada.localScale = Vector3.one;
                piezaEnganchada.Translate(posicionEnPinza, Space.Self);
            }
        }
        else
        {
            if (piezaEnganchada == null && puntoAnclajeVentosa != null)
            {
                foreach (Transform hijo in puntoAnclajeVentosa)
                {
                    if (hijo.name.ToLower().Contains("pieza")) { piezaEnganchada = hijo; break; }
                }
            }

            if (piezaEnganchada != null)
            {
                ContenedorHBW_proxy destinoFinal = contenedorActual;
                if (destinoFinal == null)
                {
                    Collider[] collidersAbajo = Physics.OverlapSphere(piezaEnganchada.position, 0.06f);
                    foreach (Collider col in collidersAbajo)
                    {
                        ContenedorHBW_proxy proxy = col.GetComponent<ContenedorHBW_proxy>() ?? col.GetComponentInParent<ContenedorHBW_proxy>();
                        if (proxy != null) { destinoFinal = proxy; break; }
                    }
                }

                if (piezaEnganchada.parent == puntoAnclajeVentosa) piezaEnganchada.SetParent(null);

                if (destinoFinal != null)
                {
                    destinoFinal.AcoplarPiezaDirecto(piezaEnganchada);
                }
                else
                {
                    piezaEnganchada.position += new Vector3(0f, 0.025f, 0f);
                    BoxCollider[] allCols = piezaEnganchada.GetComponentsInChildren<BoxCollider>();
                    foreach (BoxCollider c in allCols) if (c != null) c.isTrigger = false;

                    Rigidbody rb = piezaEnganchada.GetComponent<Rigidbody>() ?? piezaEnganchada.gameObject.AddComponent<Rigidbody>();
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }

                contenedorActual = null;
                piezaEnganchada = null;
                piezaCercana = null;
                verificarFalloAgarreDSI = false;
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

    void OnDrawGizmos()
    {
        if (mostrarGizmosVentosa && puntoAnclajeVentosa != null)
        {
            Vector3 centroBusquedaMundial = puntoAnclajeVentosa.TransformPoint(offsetBusquedaVentosa);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(centroBusquedaMundial, radioBusquedaVentosa);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(puntoAnclajeVentosa.position, centroBusquedaMundial);
        }

        if (verificarFalloAgarreDSI && ejeVertical != null)
        {
            Vector3 baseAgarre = new Vector3(ejeVertical.position.x, yMundialAlAgarrar, ejeVertical.position.z);
            Vector3 posicionEjeActual = ejeVertical.position;

            Vector3 limiteSuperior = baseAgarre + Vector3.up * 0.015f;
            Vector3 limiteInferior = baseAgarre + Vector3.down * 0.015f;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(baseAgarre, posicionEjeActual);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(limiteSuperior, 0.003f);
            Gizmos.DrawWireSphere(limiteInferior, 0.003f);

            float deltaActual = Mathf.Abs(posicionEjeActual.y - yMundialAlAgarrar);
            if (deltaActual > 0.015f)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(posicionEjeActual + Vector3.up * 0.01f, 0.006f);
            }
            else
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(posicionEjeActual + Vector3.up * 0.01f, 0.004f);
            }
        }
    }
}