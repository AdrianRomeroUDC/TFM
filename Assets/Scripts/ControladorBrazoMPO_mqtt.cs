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

    [Header("Referencias de Destinos Reales (Arrastra aquí)")]
    public Transform plataformaHorno;
    public Transform plataformaTurntable;

    private BrazoMPO_proxy proxyFisico;

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
        // Sobrescribimos el estado anterior de inmediato. Cero colas, cero acumulaciones.
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

        // Si no ha llegado telemetría todavía, esperamos
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
            // El objetivo físico cambia INSTANTÁNEAMENTE en cuanto el PLC cambia el booleano
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
                // FILTRO DE SEGURIDAD: Solo permitimos escanear si las milésimas del brazo confirman que ya llegó abajo
                float distanciaAlSuelo = Mathf.Abs(ejeVertical.localPosition.x - xPickup);
                if (distanciaAlSuelo < 0.0001f)
                {
                    Physics.SyncTransforms(); // Mantenemos las matrices de colisión de Unity al día
                    proxyFisico.ForzarEscaneoInmediato();
                }
            }
            // CASO B: El PLC corta la succión pero la ventosa registra que tiene la pieza sujeta (orden de soltar)
            else if (!estadoActual.vacuum && ventosaTienePiezaReal)
            {
                // Medimos la posición en tiempo real para saber dónde dejarla caer
                float distanciaAlHorno = Mathf.Abs(ejeHorizontal.localPosition.z - zHorno);
                float distanciaALaTurntable = Mathf.Abs(ejeHorizontal.localPosition.z - zTurntable);
                Transform plataformaActual = (distanciaAlHorno < distanciaALaTurntable) ? plataformaHorno : plataformaTurntable;

                proxyFisico.EjecutarRelease(plataformaActual);
            }
        }
    }

    private void OnDestroy()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnBrazoUpdateEvent -= RecibirEstadoDesdeMQTT;
    }
}