using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ControladorHorno_mqtt : MonoBehaviour
{
    [Header("Componentes")]
    public Transform puerta;
    public Transform plataformaPieza;
    public Light luzHorno;

    [Header("Posiciones (Usar clic derecho para capturar)")]
    [ContextMenuItem("Capturar Cerrada", "CapturarPuertaCerrada")] public Vector3 posPuertaCerrada;
    [ContextMenuItem("Capturar Abierta", "CapturarPuertaAbierta")] public Vector3 posPuertaAbierta;
    [ContextMenuItem("Capturar Fuera", "CapturarPlataformaFuera")] public Vector3 posPlataformaFuera;
    [ContextMenuItem("Capturar Dentro", "CapturarPlataformaDentro")] public Vector3 posPlataformaDentro;

    [Header("Configuración de Velocidad (Duración en segundos)")]
    public float duracionMovimientoPuerta = 1.5f;
    public float duracionMovimientoPlataforma = 1.0f;

    // Cola para sincronizar mensajes de MQTT con el hilo principal de Unity
    private Queue<MPOHornoPayload> colaMensajes = new Queue<MPOHornoPayload>();
    private Coroutine movimientoPuerta;
    private Coroutine movimientoPlataforma;

    // Métodos de captura
    void CapturarPuertaCerrada() => posPuertaCerrada = puerta.position;
    void CapturarPuertaAbierta() => posPuertaAbierta = puerta.position;
    void CapturarPlataformaFuera() => posPlataformaFuera = plataformaPieza.position;
    void CapturarPlataformaDentro() => posPlataformaDentro = plataformaPieza.position;

    private void Start() => StartCoroutine(SuscripcionSegura());

    private IEnumerator SuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;

        MQTTClient.Instance.OnHornoUpdateEvent += (data) => {
            lock (colaMensajes) { colaMensajes.Enqueue(data); }
        };
        Debug.Log("<color=cyan>Controlador Horno suscrito correctamente</color>");
    }

    private void Update()
    {
        lock (colaMensajes)
        {
            while (colaMensajes.Count > 0)
            {
                ProcesarHorno(colaMensajes.Dequeue());
            }
        }
    }

    private void ProcesarHorno(MPOHornoPayload data)
    {
        // 1. LUZ
        if (luzHorno) luzHorno.enabled = (data.lights == 1);

        // 2. PUERTA
        if (data.openDoor == 1)
        {
            if (movimientoPuerta != null) StopCoroutine(movimientoPuerta);
            movimientoPuerta = StartCoroutine(MoverObjeto(puerta, posPuertaAbierta, duracionMovimientoPuerta));
        }
        else if (data.closeDoor == 1)
        {
            if (movimientoPuerta != null) StopCoroutine(movimientoPuerta);
            movimientoPuerta = StartCoroutine(MoverObjeto(puerta, posPuertaCerrada, duracionMovimientoPuerta));
        }

        // 3. PLATAFORMA
        if (data.move2Ref5 == 1)
        {
            if (movimientoPlataforma != null) StopCoroutine(movimientoPlataforma);
            movimientoPlataforma = StartCoroutine(MoverObjeto(plataformaPieza, posPlataformaDentro, duracionMovimientoPlataforma));
        }
        else if (data.move2Ref6 == 1)
        {
            if (movimientoPlataforma != null) StopCoroutine(movimientoPlataforma);
            movimientoPlataforma = StartCoroutine(MoverObjeto(plataformaPieza, posPlataformaFuera, duracionMovimientoPlataforma));
        }
    }

    private IEnumerator MoverObjeto(Transform objeto, Vector3 destino, float duracion)
    {
        Vector3 inicio = objeto.position;
        float tiempo = 0f;

        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            float t = tiempo / duracion;

            // Suavizado tipo SmoothStep
            t = Mathf.SmoothStep(0f, 1f, t);

            objeto.position = Vector3.Lerp(inicio, destino, t);
            yield return null;
        }
        objeto.position = destino;
    }
}