using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controla el gemelo digital del brazo interno del MPO (Multi-Processing Oven): el bracito pequeño
/// que vive dentro de la estación MPO y se encarga de mover piezas entre el horno y el plato giratorio
/// (turntable). Tiene dos ejes: uno horizontal (Z) que va de la posición del horno a la posición del
/// turntable, y uno vertical (X) que baja para "picar" la pieza con la ventosa y sube para llevarla
/// colgada. Este script se suscribe a <see cref="MQTTClient.OnBrazoUpdateEvent"/> para recibir en
/// tiempo real el estado que manda el PLC (autómata) real del brazo físico, mueve los ejes del modelo
/// 3D hacia ese estado, y delega el agarre/suelta de piezas en <see cref="BrazoMPO_proxy"/>. Además
/// incluye un sistema antifallo que, tras cada bajada con succión activa, comprueba el sensor real
/// del horno (a través de <see cref="ControladorHorno_mqtt"/>) para asegurarse de que el brazo físico
/// consiguió agarrar la pieza de verdad, y corrige el gemelo digital si no fue así.
/// </summary>
public class ControladorBrazoMPO : MonoBehaviour
{
    [Header("Componentes")]
    public Transform ejeHorizontal;
    public Transform ejeVertical;
    public Transform puntoAgarre; // Objeto Ventosa con el script BrazoMPO_proxy

    [Header("Posiciones Guardadas (Eje Horizontal Z)")]
    public float zHorno;
    public float zTurntable;

    [Header("Ajustes Verticales (Local X)")]
    public float xReposo = 0f;
    public float xPickup = -0.05f;

    [Header("Configuración de Tiempos")]
    public float tiempoRecorridoHorizontal = 2.0f;
    public float tiempoRecorridoVertical = 1.0f;

    [Header("Ajuste Antifallo Horno")]
    [Tooltip("Tiempo en segundos tras terminar la bajada para comprobar si el sensor del horno sigue activo.")]
    public float delayVerificacionHorno = 0.5f;

    [Header("Referencias de Destinos Reales (Arrastra aquí)")]
    public Transform plataformaHorno;
    public Transform plataformaTurntable;

    // Script que ejecuta de verdad el agarre/suelta físico de la pieza en la ventosa de este brazo.
    private BrazoMPO_proxy proxyFisico;

    // --- MEMORIA PARA DETECTAR EL FLANCO DE BAJADA DE LOWERING ---
    // Guarda si en el frame anterior el brazo estaba bajando, para poder detectar el instante exacto
    // en que "lowering" pasa de true a false (el brazo terminó de bajar a por la pieza).
    private bool ultimoEstadoLowering = false;

    // Almacena únicamente el ÚLTIMO estado absoluto enviado por el PLC
    private MPOBrazoPayload estadoObjetivo = null;
    private readonly object lockObj = new object(); // Candado para leer/escribir estadoObjetivo sin líos entre hilos.

    void Start()
    {
        // Nos suscribimos al evento del brazo del MPO para recibir cada nuevo estado que manda el PLC real.
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnBrazoUpdateEvent += RecibirEstadoDesdeMQTT;

        if (puntoAgarre != null)
        {
            // Buscamos en el objeto de la ventosa el script que sabe agarrar/soltar piezas de verdad.
            proxyFisico = puntoAgarre.GetComponent<BrazoMPO_proxy>();
        }

        if (proxyFisico == null)
        {
            Debug.LogError("<color=red><b>[MPO]:</b> Falta el script 'BrazoMPO_proxy' en el objeto de la casilla 'Punto Agarre'.</color>");
        }
    }

    // Se llama cada vez que llega un mensaje MQTT nuevo con el estado del brazo (puede llegar desde
    // un hilo distinto al de Unity, por eso usamos el candado antes de guardar el dato).
    void RecibirEstadoDesdeMQTT(MPOBrazoPayload data)
    {
        lock (lockObj)
        {
            estadoObjetivo = data;
        }
    }

    void Update()
    {
        // Copiamos el último estado recibido de forma seguro (con el candado) para trabajar con él
        // durante el resto del frame sin que otro hilo lo cambie a medias.
        MPOBrazoPayload estadoActual = null;
        lock (lockObj)
        {
            estadoActual = estadoObjetivo;
        }

        // Si todavía no ha llegado ningún mensaje del PLC, no hay nada que mover.
        if (estadoActual == null) return;

        // =======================================================================
        // 1. SEGUIMIENTO CONTINUO DEL EJE HORIZONTAL (Z)
        // =======================================================================
        // Decide hacia dónde debe ir el eje horizontal: hacia el horno si el PLC pide "move2Ref4",
        // hacia el turntable si pide "move2Ref3", o se queda donde está si no pide ninguno de los dos.
        if (ejeHorizontal != null)
        {
            float targetZ = ejeHorizontal.localPosition.z;
            if (estadoActual.move2Ref4) targetZ = zHorno;
            else if (estadoActual.move2Ref3) targetZ = zTurntable;

            // Calculamos una velocidad constante para que el trayecto completo (horno-turntable)
            // siempre tarde "tiempoRecorridoHorizontal" segundos, sea cual sea la distancia real.
            float distanciaTotalH = Mathf.Abs(zHorno - zTurntable);
            float velocidadH = distanciaTotalH / Mathf.Max(0.01f, tiempoRecorridoHorizontal);

            Vector3 posH = ejeHorizontal.localPosition;
            posH.z = Mathf.MoveTowards(posH.z, targetZ, velocidadH * Time.deltaTime);
            ejeHorizontal.localPosition = posH;
        }

        // =======================================================================
        // 2. SEGUIMIENTO CONTINUO DEL EJE VERTICAL (X)
        // =======================================================================
        // Si el PLC dice que el brazo está "lowering" (bajando a recoger/dejar pieza), el objetivo
        // es la posición de picking; si no, se queda en la posición de reposo (arriba).
        if (ejeVertical != null)
        {
            float targetX = estadoActual.lowering ? xPickup : xReposo;

            // Igual que en el eje horizontal: velocidad calculada para que el recorrido completo
            // dure siempre "tiempoRecorridoVertical" segundos.
            float distanciaTotalV = Mathf.Abs(xReposo - xPickup);
            float velocidadV = distanciaTotalV / Mathf.Max(0.01f, tiempoRecorridoVertical);

            Vector3 posV = ejeVertical.localPosition;
            posV.x = Mathf.MoveTowards(posV.x, targetX, velocidadV * Time.deltaTime);
            ejeVertical.localPosition = posV;
        }

        // =======================================================================
        // 3. CONTROL REACTIVO DE LA VENTOSA (Vacuum)
        // =======================================================================
        if (proxyFisico != null)
        {
            bool ventosaTienePiezaReal = proxyFisico.TienePieza();

            // CASO A: El PLC exige succión y no la tenemos atrapada todavía
            // Si el brazo real ya ha llegado del todo abajo (a la posición exacta de pickup),
            // forzamos que el proxy físico compruebe ahora mismo si hay una pieza para agarrar.
            if (estadoActual.vacuum && !ventosaTienePiezaReal)
            {
                float distanciaAlSuelo = Mathf.Abs(ejeVertical.localPosition.x - xPickup);
                if (distanciaAlSuelo < 0.0001f)
                {
                    Physics.SyncTransforms();
                    proxyFisico.ForzarEscaneoInmediato();
                }
            }
            // CASO B: El PLC corta la succión pero la ventosa registra que tiene la pieza sujeta
            // Decidimos si soltarla en el horno o en el turntable según de qué lado esté más cerca
            // el eje horizontal en este momento, y le pedimos al proxy que suelte la pieza allí.
            else if (!estadoActual.vacuum && ventosaTienePiezaReal)
            {
                float distanciaAlHorno = Mathf.Abs(ejeHorizontal.localPosition.z - zHorno);
                float distanciaALaTurntable = Mathf.Abs(ejeHorizontal.localPosition.z - zTurntable);
                Transform plataformaActual = (distanciaAlHorno < distanciaALaTurntable) ? plataformaHorno : plataformaTurntable;

                proxyFisico.EjecutarRelease(plataformaActual);
            }

            // =======================================================================
            // 4. DETECCIÓN DE FLANCO DE BAJADA EN LOWERING + COLDELAY ANTIFALLO
            // =======================================================================
            // Si estaba bajando (lowering = True) y ahora deja de bajar (lowering = False) mientras vacuum = True
            // Esto marca el instante justo en el que el brazo real terminó de bajar con la ventosa
            // encendida: es el momento de comprobar, un poco más tarde, si consiguió agarrar la pieza.
            if (ultimoEstadoLowering && !estadoActual.lowering && estadoActual.vacuum)
            {
                // Disparamos la verificación diferida con tiempo de asentamiento MQTT
                StartCoroutine(VerificarFalloAgarreHornoDelay(delayVerificacionHorno));
            }

            ultimoEstadoLowering = estadoActual.lowering;
        }
    }

    /// <summary>
    /// Corrutina antifallo: espera unos instantes tras la bajada del brazo (para dar tiempo a que
    /// llegue el mensaje MQTT del sensor del horno) y comprueba si el sensor real del horno sigue
    /// detectando una pieza. Si la detecta pese a que el brazo debería habérsela llevado, significa
    /// que el agarre físico falló: en ese caso se deshace el agarre en el gemelo digital y la pieza
    /// virtual se devuelve fijada sobre la plataforma del horno, para que Unity no se desincronice
    /// de la fábrica real.
    /// </summary>
    /// <param name="delay">Segundos de margen a esperar antes de comprobar el sensor del horno.</param>
    private IEnumerator VerificarFalloAgarreHornoDelay(float delay)
    {
        // Esperamos el tiempo configurado (ej: 0.5s) para dar margen al retardo de red MQTT
        yield return new WaitForSeconds(delay);

        // Verificamos si estamos posicionados en la zona del horno (con margen de 2cm)
        float distanciaAlHorno = Mathf.Abs(ejeHorizontal.localPosition.z - zHorno);
        if (distanciaAlHorno < 0.02f)
        {
            ControladorHorno_mqtt hornoScript = Object.FindFirstObjectByType<ControladorHorno_mqtt>();

            // Si transcurridos 0.5s el sensor del horno SIGUE en True...
            if (hornoScript != null && hornoScript.SensorHornoActivo)
            {
                if (proxyFisico != null && proxyFisico.TienePieza())
                {
                    Debug.Log($"<color=red><b>[MPO FALLO AGARRE]:</b> Pasados {delay}s del flanco (lowering=False), ovenSensor = True. Devolviendo pieza al horno y quitándola del brazo.</color>");

                    // Buscamos la plataforma real del horno (o usamos la de respaldo si no la encontramos).
                    Transform plataformaDestino = hornoScript.BuscarPlataformaRealHijo();
                    if (plataformaDestino == null) plataformaDestino = plataformaHorno;

                    Vector3 posCalibrada = PlataformaHorno_proxy.PosicionCalibradaPieza;
                    Quaternion rotCalibrada = Quaternion.Euler(-90f, 0f, 0f);

                    // Quita la pieza del brazo MPO y la reubica fijada en la plataforma del horno
                    proxyFisico.CancelarAgarreYDevolver(plataformaDestino, posCalibrada, rotCalibrada);
                }
            }
            else
            {
                Debug.Log("<color=green><b>[MPO AGARRE ÉXITO]:</b> Pasados 0.5s, ovenSensor = False. Agarre confirmado en la ventosa.</color>");
            }
        }
    }

    // Nos damos de baja del evento del brazo al destruir este objeto, para no dejar una suscripción
    // "fantasma" apuntando a un script que ya no existe.
    private void OnDestroy()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnBrazoUpdateEvent -= RecibirEstadoDesdeMQTT;
    }
}
