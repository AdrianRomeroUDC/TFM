using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    private BrazoMPO_proxy proxyFisico;

    // --- MEMORIA PARA DETECTAR EL FLANCO DE BAJADA DE LOWERING ---
    private bool ultimoEstadoLowering = false;

    // Almacena únicamente el ÚLTIMO estado absoluto enviado por el PLC
    private MPOBrazoPayload estadoObjetivo = null;
    private readonly object lockObj = new object();

    void Start()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnBrazoUpdateEvent += RecibirEstadoDesdeMQTT;

        if (puntoAgarre != null)
        {
            proxyFisico = puntoAgarre.GetComponent<BrazoMPO_proxy>();
        }

        if (proxyFisico == null)
        {
            Debug.LogError("<color=red><b>[MPO]:</b> Falta el script 'BrazoMPO_proxy' en el objeto de la casilla 'Punto Agarre'.</color>");
        }
    }

    void RecibirEstadoDesdeMQTT(MPOBrazoPayload data)
    {
        lock (lockObj)
        {
            estadoObjetivo = data;
        }
    }

    void Update()
    {
        MPOBrazoPayload estadoActual = null;
        lock (lockObj)
        {
            estadoActual = estadoObjetivo;
        }

        if (estadoActual == null) return;

        // =======================================================================
        // 1. SEGUIMIENTO CONTINUO DEL EJE HORIZONTAL (Z)
        // =======================================================================
        if (ejeHorizontal != null)
        {
            float targetZ = ejeHorizontal.localPosition.z;
            if (estadoActual.move2Ref4) targetZ = zHorno;
            else if (estadoActual.move2Ref3) targetZ = zTurntable;

            float distanciaTotalH = Mathf.Abs(zHorno - zTurntable);
            float velocidadH = distanciaTotalH / Mathf.Max(0.01f, tiempoRecorridoHorizontal);

            Vector3 posH = ejeHorizontal.localPosition;
            posH.z = Mathf.MoveTowards(posH.z, targetZ, velocidadH * Time.deltaTime);
            ejeHorizontal.localPosition = posH;
        }

        // =======================================================================
        // 2. SEGUIMIENTO CONTINUO DEL EJE VERTICAL (X)
        // =======================================================================
        if (ejeVertical != null)
        {
            float targetX = estadoActual.lowering ? xPickup : xReposo;

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
            if (ultimoEstadoLowering && !estadoActual.lowering && estadoActual.vacuum)
            {
                // Disparamos la verificación diferida con tiempo de asentamiento MQTT
                StartCoroutine(VerificarFalloAgarreHornoDelay(delayVerificacionHorno));
            }

            ultimoEstadoLowering = estadoActual.lowering;
        }
    }

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

    private void OnDestroy()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnBrazoUpdateEvent -= RecibirEstadoDesdeMQTT;
    }
}