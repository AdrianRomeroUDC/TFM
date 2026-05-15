using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ControladorBrazoMPO : MonoBehaviour
{
    [Header("Componentes")]
    public Transform ejeHorizontal;
    public Transform ejeVertical;
    public Transform puntoAgarre;

    [Header("Posiciones Guardadas (Eje Horizontal Z)")]
    public float zHorno;
    public float zTurntable;

    [Header("Ajustes Verticales (Local X)")]
    public float xReposo = 0f;
    public float xPickup = -0.05f;

    [Header("Configuración de Tiempos")]
    public float tiempoRecorridoHorizontal = 2.0f;
    public float tiempoRecorridoVertical = 1.0f;

    private float targetZH;
    private bool estaOcupado = false;
    private bool tienePieza = false;
    private Transform piezaAgarrada = null;

    private Queue<MPOBrazoPayload> colaComandos = new Queue<MPOBrazoPayload>();

    void Start()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnBrazoUpdateEvent += EncolarComando;

        if (ejeHorizontal) targetZH = ejeHorizontal.localPosition.z;
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
                    // Sacamos el último mensaje de la cola para tener el estado más reciente
                    MPOBrazoPayload proximoComando = colaComandos.Dequeue();
                    StartCoroutine(EjecutarSecuencia(proximoComando));
                }
            }
        }
    }

    IEnumerator EjecutarSecuencia(MPOBrazoPayload data)
    {
        estaOcupado = true;

        // 1. DETERMINAR DESTINO HORIZONTAL
        float inicioZ = ejeHorizontal.localPosition.z;
        float destinoZ = inicioZ;

        if (data.move2Ref4 == 1) destinoZ = zHorno;
        else if (data.move2Ref3 == 1) destinoZ = zTurntable;

        // Solo movemos si el destino es diferente a la posición actual
        if (Mathf.Abs(inicioZ - destinoZ) > 0.001f)
        {
            float tiempoPasadoH = 0;
            while (tiempoPasadoH < tiempoRecorridoHorizontal)
            {
                tiempoPasadoH += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, tiempoPasadoH / tiempoRecorridoHorizontal);
                float nz = Mathf.Lerp(inicioZ, destinoZ, t);
                ejeHorizontal.localPosition = new Vector3(ejeHorizontal.localPosition.x, ejeHorizontal.localPosition.y, nz);
                yield return null;
            }
            ejeHorizontal.localPosition = new Vector3(ejeHorizontal.localPosition.x, ejeHorizontal.localPosition.y, destinoZ);
        }

        // 2. LÓGICA DE MOVIMIENTO VERTICAL INMEDIATO
        // Si al llegar a la posición (o si ya estaba ahí) la señal de pickup o release está activa:

        if (data.pickup == 1 && !tienePieza)
        {
            Debug.Log("Ejecutando Pickup...");
            yield return StartCoroutine(SecuenciaFisicaVertical(true));
        }
        else if (data.release == 1 && tienePieza)
        {
            Debug.Log("Ejecutando Release...");
            yield return StartCoroutine(SecuenciaFisicaVertical(false));
        }

        estaOcupado = false;
    }

    IEnumerator SecuenciaFisicaVertical(bool agarrar)
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

        // ACCIÓN (Simulada para tus pruebas)
        if (agarrar) { tienePieza = true; Debug.Log("Pieza Agarrada (Simulado)"); }
        else { tienePieza = false; Debug.Log("Pieza Soltada (Simulado)"); }

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

    // Funciones de soltar físicas (comentadas como pediste)
    void AgarrarObjeto() { }
    void SoltarObjeto() { if (piezaAgarrada != null) { piezaAgarrada.SetParent(null); piezaAgarrada = null; } }

    private void OnDestroy()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnBrazoUpdateEvent -= EncolarComando;
    }
}