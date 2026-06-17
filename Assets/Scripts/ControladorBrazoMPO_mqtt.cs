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

    private float targetZH;
    private bool estaOcupado = false;
    private BrazoMPO_proxy proxyFisico;
    private Queue<MPOBrazoPayload> colaComandos = new Queue<MPOBrazoPayload>();

    void Start()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnBrazoUpdateEvent += EncolarComando;

        if (ejeHorizontal) targetZH = ejeHorizontal.localPosition.z;

        // Extraemos automáticamente el proxy del punto de agarre asignado
        if (puntoAgarre != null)
        {
            proxyFisico = puntoAgarre.GetComponent<BrazoMPO_proxy>();
        }

        if (proxyFisico == null)
        {
            Debug.LogError("<color=red><b>[MPO]:</b> Falta el script 'BrazoMPO_proxy' en el objeto de la casilla 'Punto Agarre'.</color>");
        }
    }

    void EncolarComando(MPOBrazoPayload data)
    {
        lock (colaComandos) { colaComandos.Enqueue(data); }
    }

    void Update()
    {
        if (!estaOcupado)
        {
            lock (colaComandos)
            {
                if (colaComandos.Count > 0)
                {
                    MPOBrazoPayload proximoComando = colaComandos.Dequeue();
                    StartCoroutine(EjecutarSecuencia(proximoComando));
                }
            }
        }
    }

    IEnumerator EjecutarSecuencia(MPOBrazoPayload data)
    {
        estaOcupado = true;

        // 1. DETERMINAR DESTINO HORIZONTAL SEGÚN TELEMETRÍA MQTT
        float inicioZ = ejeHorizontal.localPosition.z;
        float destinoZ = inicioZ;

        if (data.move2Ref4 == 1) destinoZ = zHorno;
        else if (data.move2Ref3 == 1) destinoZ = zTurntable;

        // Mover horizontalmente si es necesario
        if (Mathf.Abs(inicioZ - destinoZ) > 0.001f)
        {
            float tiempoPasadoH = 0;
            while (tiempoPasadoH < tiempoRecorridoHorizontal)
            {
                tiempoPasadoH += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, tiempoPasadoH / tiempoRecorridoHorizontal);
                float nz = Mathf.Lerp(inicioZ, destinoZ, t); // <-- CORREGIDO AQUÍ
                ejeHorizontal.localPosition = new Vector3(ejeHorizontal.localPosition.x, ejeHorizontal.localPosition.y, nz);
                yield return null;
            }
            ejeHorizontal.localPosition = new Vector3(ejeHorizontal.localPosition.x, ejeHorizontal.localPosition.y, destinoZ);
        }

        // =======================================================================================
        // ¡DETECCIÓN UNIVERSAL!: Medimos la distancia hacia ambas estaciones para saber exactamente
        // sobre cuál estamos parados en este milisegundo (independientemente de qué comando llegó).
        // =======================================================================================
        float distanciaAlHorno = Mathf.Abs(ejeHorizontal.localPosition.z - zHorno);
        float distanciaALaTurntable = Mathf.Abs(ejeHorizontal.localPosition.z - zTurntable);

        Transform plataformaActual = (distanciaAlHorno < distanciaALaTurntable) ? plataformaHorno : plataformaTurntable;
        string nombreEstacion = (distanciaAlHorno < distanciaALaTurntable) ? "HORNO" : "TURNTABLE";

        // 2. EJECUCIÓN AG NÓSTICA DE COMANDOS
        bool ventosaTienePiezaReal = proxyFisico != null && proxyFisico.TienePieza();

        if (data.pickup == 1 && !ventosaTienePiezaReal)
        {
            Debug.Log($"<color=yellow><b>[MPO]:</b> Ejecutando Pickup Universal en <b>{nombreEstacion}</b>...</color>");
            yield return StartCoroutine(SecuenciaFisicaVertical(true, null));
        }
        else if (data.release == 1 && ventosaTienePiezaReal)
        {
            Debug.Log($"<color=yellow><b>[MPO]:</b> Ejecutando Release Universal en <b>{nombreEstacion}</b>...</color>");
            yield return StartCoroutine(SecuenciaFisicaVertical(false, plataformaActual));
        }

        estaOcupado = false;
    }

    IEnumerator SecuenciaFisicaVertical(bool agarrar, Transform destinoRelease)
    {
        float inicioX = ejeVertical.localPosition.x;

        // BAJAR
        float tiempoPasadoV = 0;
        while (tiempoPasadoV < tiempoRecorridoVertical)
        {
            tiempoPasadoV += Time.deltaTime;
            float t = tiempoPasadoV / tiempoRecorridoVertical;
            float nx = Mathf.Lerp(inicioX, xPickup, t);
            ejeVertical.localPosition = new Vector3(nx, ejeVertical.localPosition.y, ejeVertical.localPosition.z);
            yield return null;
        }
        ejeVertical.localPosition = new Vector3(xPickup, ejeVertical.localPosition.y, ejeVertical.localPosition.z);

        // INTERACCIÓN FÍSICA DIRECTA
        if (agarrar)
        {
            if (proxyFisico != null) proxyFisico.ForzarEscaneoInmediato();
        }
        else
        {
            if (proxyFisico != null && destinoRelease != null) proxyFisico.EjecutarRelease(destinoRelease);
        }

        yield return new WaitForSeconds(0.3f);

        // SUBIR
        inicioX = ejeVertical.localPosition.x;
        float tiempoPasadoSubir = 0;
        while (tiempoPasadoSubir < tiempoRecorridoVertical)
        {
            tiempoPasadoSubir += Time.deltaTime;
            float t = tiempoPasadoSubir / tiempoRecorridoVertical;
            float nx = Mathf.Lerp(inicioX, xReposo, t);
            ejeVertical.localPosition = new Vector3(nx, ejeVertical.localPosition.y, ejeVertical.localPosition.z);
            yield return null;
        }
        ejeVertical.localPosition = new Vector3(xReposo, ejeVertical.localPosition.y, ejeVertical.localPosition.z);
    }

    private void OnDestroy()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnBrazoUpdateEvent -= EncolarComando;
    }
}