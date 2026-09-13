using UnityEngine;
using System.Collections;

/// <summary>
/// Controla el gemelo digital del VGR (Vacuum Gripper Robot): el robot cartesiano de 3 ejes con
/// ventosa que traslada piezas entre todas las estaciones de la fábrica (HBW, DPS, SLD, MPO).
/// Se suscribe a los eventos de <see cref="MQTTClient"/> para mover sus 3 ejes (rotación, altura,
/// extensión) exactamente igual que el robot real, y para agarrar/soltar piezas 3D en Unity cuando
/// la ventosa real se activa o desactiva. Además, incluye varios "sistemas antifallo" que comprueban,
/// usando los sensores reales de otras estaciones (DSI de la <see cref="ControladorDPS_mqtt"/>, los
/// sensores de color de <see cref="ControladorCilindrosSLD_mqtt"/> y el sensor del horno de
/// <see cref="ControladorHorno_mqtt"/>), si el agarre o la entrega de una pieza ha funcionado de
/// verdad en la máquina física, para que el gemelo digital nunca se desincronice de la realidad.
/// </summary>
public class ControladorVGR_mqtt : MonoBehaviour
{
    // Valores "anterior" y "nuevo" (en unidades del PLC) de los 3 ejes, usados para interpolar el
    // movimiento en el tiempo real transcurrido entre dos mensajes MQTT consecutivos, en vez de
    // perseguir el objetivo con una velocidad de suavizado fija: así el movimiento se adapta solo a
    // la cadencia real de red (100ms en condiciones normales) y se estira en vez de saltar si algún
    // mensaje llega tarde.
    private float prevRot, prevVert, prevExt;
    private float targetRot, targetVert, targetExt;
    private float tInicioInterpolacion = -1f; // Instante (Time.time) en que llegó el último mensaje de posición; -1 = aún no ha llegado ninguno.
    private float duracionInterpolacion = 0.1f; // Tiempo real que debe durar la interpolación hasta el próximo mensaje; se recalcula con cada mensaje nuevo.
    private bool estadoGripPendiente = false; // Nuevo estado de la ventosa recibido, pendiente de aplicar en Update().
    private bool cambioGripDetectado = false; // Aviso de que ha llegado un cambio de ventosa que aún no se ha procesado.

    private Transform piezaCercana; // Pieza que está tocando el radar de la ventosa ahora mismo (pero no necesariamente agarrada).
    private Transform piezaEnganchada; // Pieza que está actualmente "pegada" a la ventosa (agarrada de verdad).
    private ContenedorHBW_proxy contenedorActual; // Contenedor del HBW que hay justo debajo de la ventosa (si lo hay).

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
    // Valores mínimo/máximo que envía el PLC (autómata) real para cada eje: sirven para convertir
    // esas unidades "crudas" del robot físico a la escala de metros/grados que usa Unity.
    public float plcRot_Min = 1395; public float plcRot_Max = 21;
    public float plcVert_Min = 20; public float plcVert_Max = 1272;
    public float plcExt_Min = 40; public float plcExt_Max = 1210;

    [Header("Calibración Unity")]
    // Valores equivalentes pero en el mundo de Unity, capturados a mano desde el editor con los
    // botones de contexto de abajo (por eso son [ContextMenuItem]: aparecen como botón en el Inspector).
    [ContextMenuItem("Capturar", "CapturarRotMin")] public float unityRot_Min;
    [ContextMenuItem("Capturar", "CapturarRotMax")] public float unityRot_Max;
    [ContextMenuItem("Capturar", "CapturarVertMin")] public float unityVert_Min;
    [ContextMenuItem("Capturar", "CapturarVertMax")] public float unityVert_Max;
    [ContextMenuItem("Capturar", "CapturarExtMin")] public float unityExt_Min;
    [ContextMenuItem("Capturar", "CapturarExtMax")] public float unityExt_Max;

    [Header("Ajustes")]
    [Tooltip("Recorte mínimo y máximo (en segundos) para la duración de cada interpolación, por si un mensaje tarda demasiado o llega duplicado al instante.")]
    public float duracionInterpolacionMin = 0.02f;
    public float duracionInterpolacionMax = 0.6f;

    // --- VARIABLES PARA MONITOREAR CONTROL DE CALIDAD DSI (MUNDIAL) ---
    // Tras agarrar una pieza que viene de la entrada DSI de la DPS, vigilamos si el sensor real
    // dsi_sensor sigue activo cuando el brazo ya se ha alejado: si sigue activo es que el agarre
    // ha fallado en la máquina real (la pieza se quedó atrás) y hay que corregir el gemelo digital.
    private bool dsiSensorActivo = false;
    private bool verificarFalloAgarreDSI = false;
    private float yMundialAlAgarrar = 0f;
    private Vector3 posicionLocalOriginalDSI;
    private Quaternion rotacionLocalOriginalDSI;

    // --- VARIABLES PARA MONITOREAR CONTROL DE CALIDAD SLD (RAMPAS BLANCA/ROJA/AZUL) ---
    // Mismo principio que el bloque DSI, pero para piezas agarradas desde las rampas de salida
    // de la estación clasificadora SLD (una por cada color: blanco, rojo, azul).
    private bool verificarFalloAgarreSLD = false;
    private string colorRampaMonitoreada = "";
    private Vector3 posicionLocalOriginalSLD;
    private Quaternion rotacionLocalOriginalSLD;
    private Transform padreOriginalSLD;
    private float yMundialAlAgarrarSLD = 0f;

    // --- VARIABLES PARA MONITOREAR CONTROL DE CALIDAD ENTREGA EN HORNO ---
    // Al soltar una pieza dentro del horno del MPO, comprobamos que el sensor real del horno
    // detecte la pieza; si no la detecta, es que la entrega ha fallado y quitamos la pieza fantasma.
    private bool verificarFalloEntregaHorno = false;
    private float yMundialAlSoltarHorno = 0f;
    private Transform piezaMonitoreadaHorno = null;

    // --- VARIABLES PARA MONITOREAR CONTROL DE CALIDAD ENTREGA EN DSO ---
    private bool verificarFalloEntregaDSO = false;
    private float yMundialAlSoltarDSO = 0f;
    private Transform piezaMonitoreadaDSO = null;

    // Métodos auxiliares que se ejecutan desde el botón del Inspector para "fotografiar" la posición
    // actual del modelo 3D en Unity y guardarla como referencia de calibración (unityRot_Min, etc.).
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

    // Espera a que MQTTClient exista en la escena antes de suscribirse a sus eventos, para evitar
    // errores de referencia nula si este script se inicializa antes que el cliente MQTT.
    IEnumerator SuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;
        MQTTClient.Instance.OnVGRPositionUpdateEvent += ActualizarPosicionDesdeMQTT;
        MQTTClient.Instance.OnVGRGripEvent += RecibirGripMQTT;
        MQTTClient.Instance.OnDPSPiezaDSIEvent += ActualizarSensorDSI;

        Debug.Log("<color=green><b>[VGR SUSCRIPCIÓN]:</b> VGR Suscrito correctamente.</color>");
    }

    // Nos damos de baja de todos los eventos al desactivar el objeto, para no dejar suscripciones
    // "fantasma" que sigan intentando llamar a métodos de un objeto ya inactivo.
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
        // Si ha llegado un cambio de ventosa (agarrar/soltar) desde MQTT, lo procesamos ahora,
        // ya en el hilo principal de Unity y de forma controlada.
        if (cambioGripDetectado)
        {
            ProcesarLogicaGrip(estadoGripPendiente);
            cambioGripDetectado = false;
        }

        // En qué punto de la interpolación estamos entre el mensaje anterior y el más reciente,
        // repartido sobre el tiempo real que tardó en llegar el mensaje nuevo (ver ActualizarPosicionDesdeMQTT).
        float frac = (tInicioInterpolacion >= 0f && duracionInterpolacion > 0f)
            ? Mathf.Clamp01((Time.time - tInicioInterpolacion) / duracionInterpolacion)
            : 1f;

        // Movemos cada eje del robot interpolando entre la posición anterior y la nueva, convirtiendo
        // primero las unidades del PLC a la escala del modelo 3D de Unity.
        if (ejeRotacion)
        {
            float tPrev = Mathf.InverseLerp(plcRot_Min, plcRot_Max, prevRot);
            float tTarget = Mathf.InverseLerp(plcRot_Min, plcRot_Max, targetRot);
            float anguloPrev = unityRot_Min + (unityRot_Max - unityRot_Min) * tPrev;
            float anguloTarget = unityRot_Min + (unityRot_Max - unityRot_Min) * tTarget;
            float anguloInterpolado = Mathf.LerpAngle(anguloPrev, anguloTarget, frac);
            ejeRotacion.localRotation = Quaternion.Euler(0, anguloInterpolado, 0);
        }

        if (ejeVertical)
        {
            float tPrevV = Mathf.InverseLerp(plcVert_Min, plcVert_Max, prevVert);
            float tTargetV = Mathf.InverseLerp(plcVert_Min, plcVert_Max, targetVert);
            float yPrev = Mathf.Lerp(unityVert_Min, unityVert_Max, tPrevV);
            float yTarget = Mathf.Lerp(unityVert_Min, unityVert_Max, tTargetV);
            float yInterpolado = Mathf.Lerp(yPrev, yTarget, frac);
            ejeVertical.localPosition = new Vector3(ejeVertical.localPosition.x, yInterpolado, ejeVertical.localPosition.z);
        }

        if (ejeExtension)
        {
            float tPrevE = Mathf.InverseLerp(plcExt_Min, plcExt_Max, prevExt);
            float tTargetE = Mathf.InverseLerp(plcExt_Min, plcExt_Max, targetExt);
            float xPrev = Mathf.Lerp(unityExt_Min, unityExt_Max, tPrevE);
            float xTarget = Mathf.Lerp(unityExt_Min, unityExt_Max, tTargetE);
            float xInterpolado = Mathf.Lerp(xPrev, xTarget, frac);
            ejeExtension.localPosition = new Vector3(xInterpolado, ejeExtension.localPosition.y, ejeExtension.localPosition.z);
        }

        // --- 1. SISTEMA ANTIFALLO DSI EN ESPACIO MUNDIAL ---
        // Comprobamos si el brazo ya se ha alejado lo suficiente de la posición donde agarró
        // la pieza (más de 2 cm reales) para poder confirmar si el agarre salió bien o mal.
        if (verificarFalloAgarreDSI)
        {
            if (piezaEnganchada == null)
            {
                verificarFalloAgarreDSI = false;
            }
            else
            {
                float deltaYMundial = Mathf.Abs(ejeVertical.position.y - yMundialAlAgarrar);

                if (deltaYMundial > 0.02f) // 2 cm reales en el espacio 3D
                {
                    Debug.Log($"<color=yellow><b>[VGR CHEQUEO DSI]:</b> Altura límite superada. Delta: {deltaYMundial:F4}m. dsi_sensor = {dsiSensorActivo}</color>");

                    if (dsiSensorActivo)
                    {
                        // Si el sensor real dsi_sensor sigue activo, significa que la pieza física
                        // NO llegó a subir con la ventosa real: el gemelo digital debe deshacer el
                        // agarre y devolver la pieza virtual exactamente a su posición original.
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
                        // Sensor inactivo = la pieza real subió con la ventosa: el agarre fue correcto.
                        Debug.Log("<color=green><b>[VGR AGARRE ÉXITO DSI]:</b> dsi_sensor = False. Agarre confirmado.</color>");
                    }

                    verificarFalloAgarreDSI = false;
                }
            }
        }

        // --- 2. SISTEMA ANTIFALLO SLD (RAMPAS BLANCA/ROJA/AZUL) EN ESPACIO MUNDIAL ---
        // Igual que el bloque anterior, pero comprobando el sensor de color correspondiente
        // de la rampa de la SLD desde la que se agarró la pieza.
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
                        // El sensor de la rampa sigue viendo la pieza: el agarre real falló, así que
                        // devolvemos la pieza virtual a su rampa de origen exacta.
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
        // Tras soltar una pieza en el horno del MPO, esperamos a que el brazo suba y comprobamos
        // si el sensor real del horno la detecta; si no, la pieza virtual se elimina (nunca llegó).
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
                        // Por si perdimos la referencia directa, buscamos manualmente cualquier
                        // pieza que haya quedado colgando dentro de la plataforma real del horno.
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

        // --- 4. SISTEMA ANTIFALLO ENTREGA EN PLATAFORMA DSO (AL SUBIR EL VGR) ---
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

    // Guarda la nueva posición de los 3 ejes recibida por MQTT y prepara la interpolación hacia
    // ella; el movimiento real ocurre en Update(), repartido sobre el tiempo real que tarde en
    // llegar el próximo mensaje.
    private void ActualizarPosicionDesdeMQTT(float rot, float vert, float ext)
    {
        // Antes de sustituir los valores objetivo, guardamos como "punto de partida" el valor que
        // el eje tiene ahora mismo (ya interpolado), no el antiguo objetivo en bruto, para que el
        // siguiente tramo de interpolación arranque sin ningún salto visual.
        float fracActual = (tInicioInterpolacion >= 0f && duracionInterpolacion > 0f)
            ? Mathf.Clamp01((Time.time - tInicioInterpolacion) / duracionInterpolacion)
            : 1f;
        prevRot = Mathf.Lerp(prevRot, targetRot, fracActual);
        prevVert = Mathf.Lerp(prevVert, targetVert, fracActual);
        prevExt = Mathf.Lerp(prevExt, targetExt, fracActual);

        targetRot = rot; targetVert = vert; targetExt = ext;

        // Medimos cuánto ha tardado en llegar este mensaje desde el anterior (normalmente ~100ms) y
        // usamos ese mismo intervalo real para repartir la interpolación del próximo tramo, recortado
        // a un rango razonable por si hay un corte de red o un mensaje duplicado instantáneo.
        float ahora = Time.time;
        if (tInicioInterpolacion >= 0f)
        {
            duracionInterpolacion = Mathf.Clamp(ahora - tInicioInterpolacion, duracionInterpolacionMin, duracionInterpolacionMax);
        }
        tInicioInterpolacion = ahora;
    }

    // Marca que ha llegado un cambio de estado de la ventosa (agarrar/soltar), para procesarlo
    // de forma segura en el siguiente Update().
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

    /// <summary>
    /// Aplica de verdad el cambio de estado de la ventosa (llamado desde Update() cuando llega un
    /// mensaje MQTT nuevo): si se activa, busca y "engancha" físicamente la pieza más cercana bajo
    /// la ventosa; si se desactiva, suelta la pieza y decide dónde debe quedar en la escena 3D
    /// (dentro de un hueco del HBW, cayendo por gravedad, etc.), además de arrancar los controles
    /// de calidad correspondientes según de dónde venga o hacia dónde vaya la pieza.
    /// </summary>
    private void ProcesarLogicaGrip(bool activo)
    {
        if (activo)
        {
            // --- AGARRAR PIEZA ---
            if (puntoAnclajeVentosa != null && piezaEnganchada == null)
            {
                // Buscamos con una esfera de físicas (radar) si hay alguna pieza justo debajo de la ventosa.
                Vector3 centroBusquedaMundial = puntoAnclajeVentosa.TransformPoint(offsetBusquedaVentosa);

                Collider[] collidersEnVentosa = Physics.OverlapSphere(centroBusquedaMundial, radioBusquedaVentosa);
                foreach (Collider col in collidersEnVentosa)
                {
                    Transform objetoActual = col.transform;
                    Transform piezaReal = null;

                    // Subimos por la jerarquía de objetos hasta encontrar el que representa la "pieza"
                    // en sí (y no, por ejemplo, el cajón o contenedor que la sostiene).
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
                // A. VERIFICACIÓN DSI: si la pieza venía de la plataforma de entrada de la DPS,
                // activamos el control de calidad que vigilará el sensor dsi_sensor real.
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

                // B. VERIFICACIÓN SLD (RAMPAS BLANCA, ROJA, AZUL): si la pieza venía de una de las
                // rampas de salida de la clasificadora, activamos el control de calidad correspondiente.
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

                // Convertimos la pieza en cinemática (no le afecta la física) para que se mueva
                // pegada a la ventosa en vez de caer por gravedad, y activamos sus colliders como
                // "trigger" para que no choque físicamente contra otros objetos mientras viaja.
                Rigidbody rb = piezaEnganchada.GetComponent<Rigidbody>();
                if (rb == null) rb = piezaEnganchada.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                BoxCollider[] colliders = piezaEnganchada.GetComponentsInChildren<BoxCollider>();
                foreach (BoxCollider col in colliders) if (col != null) col.isTrigger = true;

                // "Enganchamos" la pieza a la ventosa: la hacemos hija del punto de anclaje y la
                // colocamos en la posición/rotación exactas configuradas para el agarre.
                piezaEnganchada.SetParent(puntoAnclajeVentosa);
                piezaEnganchada.position = puntoAnclajeVentosa.position;
                piezaEnganchada.rotation = Quaternion.Euler(rotacionEnPinza);
                piezaEnganchada.localScale = Vector3.one;
                piezaEnganchada.Translate(posicionEnPinza, Space.Self);
            }
        }
        else
        {
            // --- SOLTAR PIEZA ---
            if (piezaEnganchada == null && puntoAnclajeVentosa != null)
            {
                // Por si acaso perdimos la referencia, buscamos entre los hijos directos de la
                // ventosa alguno que sea una pieza (para no dejar piezas "huérfanas" sin soltar bien).
                foreach (Transform hijo in puntoAnclajeVentosa)
                {
                    if (hijo.name.ToLower().Contains("pieza")) { piezaEnganchada = hijo; break; }
                }
            }

            if (piezaEnganchada != null)
            {
                Transform piezaASoltar = piezaEnganchada;

                // C. DETECCIÓN Y MONITOREO DE ENTREGA EN EL HORNO: si soltamos la pieza cerca de la
                // plataforma real del horno, activamos el control de calidad de entrega en el horno.
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

                // D. DETECCIÓN Y MONITOREO DE ENTREGA EN LA PLATAFORMA DSO: igual que el bloque
                // anterior, pero para la plataforma de salida de piezas terminadas de la DPS.
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

                // Buscamos si justo debajo de la pieza hay un hueco/contenedor del HBW donde deba encajar.
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
                    // Hay un hueco del HBW debajo: encajamos la pieza directamente ahí.
                    destinoFinal.AcoplarPiezaDirecto(piezaEnganchada);
                }
                else
                {
                    // No hay ningún contenedor debajo: dejamos que la pieza caiga con física normal
                    // (gravedad activada) en el lugar donde se soltó.
                    piezaEnganchada.position += new Vector3(0f, 0.025f, 0f);
                    BoxCollider[] allCols = piezaEnganchada.GetComponentsInChildren<BoxCollider>();
                    foreach (BoxCollider c in allCols) if (c != null) c.isTrigger = false;

                    Rigidbody rb = piezaEnganchada.GetComponent<Rigidbody>() ?? piezaEnganchada.gameObject.AddComponent<Rigidbody>();
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }

                // Reiniciamos todo el estado del agarre para quedar listos para el siguiente ciclo.
                contenedorActual = null;
                piezaEnganchada = null;
                piezaCercana = null;
                verificarFalloAgarreDSI = false;
                verificarFalloAgarreSLD = false;
            }
        }
    }

    // Detecta cuándo una pieza entra en el radar de la ventosa (útil para depuración/gizmos).
    private void OnTriggerEnter(Collider other)
    {
        if (other.name.ToLower().Contains("pieza")) SetPiezaCercana(other.transform);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.name.ToLower().Contains("pieza") && piezaEnganchada == null) SetPiezaCercana(null);
    }

    // Dibuja ayudas visuales en el editor de Unity (solo se ven en la vista de Escena, no en el
    // juego real) para poder calibrar y depurar el radar de agarre y los sistemas antifallo.
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
