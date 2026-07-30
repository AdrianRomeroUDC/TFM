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
    private Vector3 posicionLocalOriginalDSI;
    private Quaternion rotacionLocalOriginalDSI;

    // --- VARIABLES PARA MONITOREAR CONTROL DE CALIDAD SLD (RAMPAS BLANCA/ROJA/AZUL) ---
    private bool verificarFalloAgarreSLD = false;
    private string colorRampaMonitoreada = "";
    private Vector3 posicionLocalOriginalSLD;
    private Quaternion rotacionLocalOriginalSLD;
    private Transform padreOriginalSLD;
    private float yMundialAlAgarrarSLD = 0f;

    // --- VARIABLES PARA MONITOREAR CONTROL DE CALIDAD ENTREGA EN HORNO ---
    private bool verificarFalloEntregaHorno = false;
    private float yMundialAlSoltarHorno = 0f;
    private Transform piezaMonitoreadaHorno = null;

    // --- 🎯 VARIABLES PARA MONITOREAR CONTROL DE CALIDAD ENTREGA EN DSO ---
    private bool verificarFalloEntregaDSO = false;
    private float yMundialAlSoltarDSO = 0f;
    private Transform piezaMonitoreadaDSO = null;

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

        // --- 1. SISTEMA ANTIFALLO DSI EN ESPACIO MUNDIAL ---
        if (verificarFalloAgarreDSI)
        {
            if (piezaEnganchada == null)
            {
                verificarFalloAgarreDSI = false;
            }
            else
            {
                float deltaYMundial = Mathf.Abs(ejeVertical.position.y - yMundialAlAgarrar);

                if (deltaYMundial > 0.015f) // 1.5 cm reales en el espacio 3D
                {
                    Debug.Log($"<color=yellow><b>[VGR CHEQUEO DSI]:</b> Altura límite superada. Delta: {deltaYMundial:F4}m. dsi_sensor = {dsiSensorActivo}</color>");

                    if (dsiSensorActivo)
                    {
                        Debug.Log("<color=red><b>[VGR FALLO AGARRE DSI]:</b> ¡FALLO! dsi_sensor = True. Devolviendo pieza virtual a la posición de spawn exacta.</color>");

                        ControladorDPS_mqtt dps = Object.FindFirstObjectByType<ControladorDPS_mqtt>();
                        if (dps != null && dps.plataformaDSI != null)
                        {
                            piezaEnganchada.SetParent(dps.plataformaDSI);
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
                        Debug.Log("<color=green><b>[VGR AGARRE ÉXITO DSI]:</b> dsi_sensor = False. Agarre confirmado.</color>");
                    }

                    verificarFalloAgarreDSI = false;
                }
            }
        }

        // --- 2. SISTEMA ANTIFALLO SLD (RAMPAS BLANCA/ROJA/AZUL) EN ESPACIO MUNDIAL ---
        if (verificarFalloAgarreSLD)
        {
            if (piezaEnganchada == null)
            {
                verificarFalloAgarreSLD = false;
            }
            else
            {
                float deltaYMundialSLD = Mathf.Abs(ejeVertical.position.y - yMundialAlAgarrarSLD);

                if (deltaYMundialSLD > 0.015f) // 1.5 cm reales
                {
                    ControladorCilindrosSLD_mqtt sld = Object.FindFirstObjectByType<ControladorCilindrosSLD_mqtt>();
                    bool sensorRampaActivo = false;

                    if (sld != null)
                    {
                        switch (colorRampaMonitoreada)
                        {
                            case "WHITE": sensorRampaActivo = sld.IsWhiteSensorActivo; break;
                            case "RED": sensorRampaActivo = sld.IsRedSensorActivo; break;
                            case "BLUE": sensorRampaActivo = sld.IsBlueSensorActivo; break;
                        }
                    }

                    Debug.Log($"<color=yellow><b>[VGR CHEQUEO SLD]:</b> Altura límite superada. Delta: {deltaYMundialSLD:F4}m. Sensor {colorRampaMonitoreada} = {sensorRampaActivo}</color>");

                    if (sensorRampaActivo)
                    {
                        Debug.Log($"<color=red><b>[VGR FALLO AGARRE SLD]:</b> ¡FALLO! Sensor {colorRampaMonitoreada} = True. Devolviendo pieza a la rampa de origen.</color>");

                        if (padreOriginalSLD != null)
                        {
                            piezaEnganchada.SetParent(padreOriginalSLD);
                            piezaEnganchada.localPosition = posicionLocalOriginalSLD;
                            piezaEnganchada.localRotation = rotacionLocalOriginalSLD;

                            Rigidbody rb = piezaEnganchada.GetComponent<Rigidbody>();
                            if (rb != null) rb.isKinematic = true;

                            BoxCollider[] colliders = piezaEnganchada.GetComponentsInChildren<BoxCollider>();
                            foreach (BoxCollider col in colliders) if (col != null) col.isTrigger = false;
                        }

                        piezaEnganchada = null;
                    }
                    else
                    {
                        Debug.Log($"<color=green><b>[VGR AGARRE ÉXITO SLD]:</b> Sensor {colorRampaMonitoreada} = False. Agarre confirmado en la ventosa.</color>");
                    }

                    verificarFalloAgarreSLD = false;
                }
            }
        }

        // --- 3. SISTEMA ANTIFALLO ENTREGA EN HORNO (AL SUBIR EL VGR) ---
        if (verificarFalloEntregaHorno)
        {
            float deltaYMundialHorno = Mathf.Abs(ejeVertical.position.y - yMundialAlSoltarHorno);

            if (deltaYMundialHorno > 0.015f)
            {
                ControladorHorno_mqtt horno = Object.FindFirstObjectByType<ControladorHorno_mqtt>();
                bool sensorHornoActivo = (horno != null && horno.SensorHornoActivo);

                Debug.Log($"<color=yellow><b>[VGR CHEQUEO HORNO]:</b> VGR subió. Delta: {deltaYMundialHorno:F4}m. Sensor Horno real = {sensorHornoActivo}</color>");

                if (!sensorHornoActivo)
                {
                    Debug.Log("<color=red><b>[VGR FALLO ENTREGA HORNO]:</b> ¡FALLO! Sensor Horno = False (no hay pieza real). Eliminando pieza fantasma de la simulación.</color>");

                    if (piezaMonitoreadaHorno != null)
                    {
                        Destroy(piezaMonitoreadaHorno.gameObject);
                    }
                    else if (horno != null)
                    {
                        Transform platReal = horno.BuscarPlataformaRealHijo();
                        if (platReal != null)
                        {
                            foreach (Transform h in platReal)
                            {
                                if (h.name.ToLower().Contains("pieza")) Destroy(h.gameObject);
                            }
                        }
                    }
                }
                else
                {
                    Debug.Log("<color=green><b>[VGR ENTREGA ÉXITO HORNO]:</b> Sensor Horno = True. Entrega confirmada en el horno real.</color>");
                }

                verificarFalloEntregaHorno = false;
                piezaMonitoreadaHorno = null;
            }
        }

        // --- 🎯 4. SISTEMA ANTIFALLO ENTREGA EN PLATAFORMA DSO (AL SUBIR EL VGR) ---
        if (verificarFalloEntregaDSO)
        {
            float deltaYMundialDSO = Mathf.Abs(ejeVertical.position.y - yMundialAlSoltarDSO);

            if (deltaYMundialDSO > 0.02f)
            {
                // Solo dejamos constancia en consola, la física de la plataforma DSO decide si la borra o no
                Debug.Log("<color=green><b>[VGR ENTREGA DSO]:</b> Brazo VGR elevado tras la entrega.</color>");

                verificarFalloEntregaDSO = false;
                piezaMonitoreadaDSO = null;
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
    public ContenedorHBW_proxy ObtenerContenedorActual() => contenedorActual;
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
                // A. VERIFICACIÓN DSI
                bool esDeDSI = piezaEnganchada.name.ToLower().Contains("dsi") ||
                               (piezaEnganchada.parent != null && piezaEnganchada.parent.name.ToLower().Contains("dsi"));

                if (esDeDSI)
                {
                    posicionLocalOriginalDSI = piezaEnganchada.localPosition;
                    rotacionLocalOriginalDSI = piezaEnganchada.localRotation;

                    verificarFalloAgarreDSI = true;
                    yMundialAlAgarrar = ejeVertical.position.y;

                    Debug.Log($"<color=orange><b>[VGR MONITOREO DSI]:</b> Altura inicial: {yMundialAlAgarrar:F6}. Posición original memorizada: {posicionLocalOriginalDSI}.</color>");
                }

                // B. VERIFICACIÓN SLD (RAMPAS BLANCA, ROJA, AZUL)
                ControladorCilindrosSLD_mqtt sldScript = Object.FindFirstObjectByType<ControladorCilindrosSLD_mqtt>();
                Transform padreActual = piezaEnganchada.parent;

                if (sldScript != null && padreActual != null)
                {
                    string colorRampa = "";
                    if (padreActual == sldScript.spawnPointBlanco || padreActual.name.ToLower().Contains("blanco") || padreActual.name.ToLower().Contains("white"))
                        colorRampa = "WHITE";
                    else if (padreActual == sldScript.spawnPointRojo || padreActual.name.ToLower().Contains("rojo") || padreActual.name.ToLower().Contains("red"))
                        colorRampa = "RED";
                    else if (padreActual == sldScript.spawnPointAzul || padreActual.name.ToLower().Contains("azul") || padreActual.name.ToLower().Contains("blue"))
                        colorRampa = "BLUE";

                    if (!string.IsNullOrEmpty(colorRampa))
                    {
                        posicionLocalOriginalSLD = piezaEnganchada.localPosition;
                        rotacionLocalOriginalSLD = piezaEnganchada.localRotation;
                        padreOriginalSLD = padreActual;
                        colorRampaMonitoreada = colorRampa;

                        verificarFalloAgarreSLD = true;
                        yMundialAlAgarrarSLD = ejeVertical.position.y;

                        Debug.Log($"<color=orange><b>[VGR MONITOREO SLD]:</b> Pieza atrapada en rampa {colorRampa}. Guardada posición local de rampa: {posicionLocalOriginalSLD}.</color>");
                    }
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
                Transform piezaASoltar = piezaEnganchada;

                // C. DETECCIÓN Y MONITOREO DE ENTREGA EN EL HORNO
                ControladorHorno_mqtt hornoScript = Object.FindFirstObjectByType<ControladorHorno_mqtt>();
                if (hornoScript != null)
                {
                    Transform platReal = hornoScript.BuscarPlataformaRealHijo();
                    if (platReal != null)
                    {
                        float distanciaAlHorno = Vector3.Distance(puntoAnclajeVentosa.position, platReal.position);
                        if (distanciaAlHorno < 0.25f)
                        {
                            piezaMonitoreadaHorno = piezaASoltar;
                            verificarFalloEntregaHorno = true;
                            yMundialAlSoltarHorno = ejeVertical.position.y;

                            Debug.Log("<color=orange><b>[VGR MONITOREO HORNO]:</b> Pieza soltada en el Horno. Iniciando monitoreo al subir el VGR...</color>");
                        }
                    }
                }

                // 🎯 D. DETECCIÓN Y MONITOREO DE ENTREGA EN LA PLATAFORMA DSO
                ControladorDPS_mqtt dpsScript = Object.FindFirstObjectByType<ControladorDPS_mqtt>();
                if (dpsScript != null && dpsScript.plataformaDSO != null)
                {
                    float distanciaADSO = Vector3.Distance(puntoAnclajeVentosa.position, dpsScript.plataformaDSO.position);
                    if (distanciaADSO < 0.25f)
                    {
                        piezaMonitoreadaDSO = piezaASoltar;
                        verificarFalloEntregaDSO = true;
                        yMundialAlSoltarDSO = ejeVertical.position.y;

                        Debug.Log("<color=orange><b>[VGR MONITOREO DSO]:</b> Pieza soltada cerca de DSO. Iniciando monitoreo al subir el VGR...</color>");
                    }
                }

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
                verificarFalloAgarreSLD = false;
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
        }

        if (verificarFalloAgarreSLD && ejeVertical != null)
        {
            Vector3 baseAgarreSLD = new Vector3(ejeVertical.position.x, yMundialAlAgarrarSLD, ejeVertical.position.z);
            Vector3 posicionEjeActual = ejeVertical.position;

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(baseAgarreSLD, posicionEjeActual);
            Gizmos.DrawWireSphere(baseAgarreSLD + Vector3.up * 0.015f, 0.004f);
        }

        if (verificarFalloEntregaHorno && ejeVertical != null)
        {
            Vector3 baseSoltarHorno = new Vector3(ejeVertical.position.x, yMundialAlSoltarHorno, ejeVertical.position.z);
            Vector3 posicionEjeActual = ejeVertical.position;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(baseSoltarHorno, posicionEjeActual);
            Gizmos.DrawWireSphere(baseSoltarHorno + Vector3.up * 0.015f, 0.004f);
        }

        if (verificarFalloEntregaDSO && ejeVertical != null)
        {
            Vector3 baseSoltarDSO = new Vector3(ejeVertical.position.x, yMundialAlSoltarDSO, ejeVertical.position.z);
            Vector3 posicionEjeActual = ejeVertical.position;

            Gizmos.color = Color.green;
            Gizmos.DrawLine(baseSoltarDSO, posicionEjeActual);
            Gizmos.DrawWireSphere(baseSoltarDSO + Vector3.up * 0.015f, 0.004f);
        }
    }
}