using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ControladorCintaSLD_mqtt : MonoBehaviour
{
    [Header("Referencia a la Cinta")]
    public Transform objetoCintaPadre;

    [Header("Configuración de Movimiento")]
    public float multiplicadorVelocidad = 0.001f;
    [SerializeField] private float velocidadActual = 0f;

    [Header("Monitoreo de Sensores (Lectura)")]
    public bool sensorEntrada = false;
    public bool sensorCilindros = false;

    private List<Transform> eslabonesOrdenados = new List<Transform>();
    private Vector3[] posRailes;
    private Quaternion[] rotRailes;
    private float progresoCiclo = 0f;

    void Start()
    {
        if (objetoCintaPadre == null)
        {
            Debug.LogError("¡Falta asignar el Objeto Cinta Padre en el Inspector!");
            return;
        }

        ConfigurarEslabones();

        // Intentamos suscribirnos cada segundo hasta que el MQTTClient esté listo
        InvokeRepeating("IntentarSuscripcion", 0f, 1f);
    }

    void IntentarSuscripcion()
    {
        if (MQTTClient.Instance != null)
        {
            // Nos suscribimos al nuevo método que acepta el Payload completo
            MQTTClient.Instance.OnBeltUpdateEvent += ActualizarDatosCinta;
            Debug.Log("<color=green><b>Cinta SLD:</b> Conectado con éxito al sistema central (Velocidad y Sensores habilitados).</color>");
            CancelInvoke("IntentarSuscripcion");
        }
    }

    void OnDisable()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnBeltUpdateEvent -= ActualizarDatosCinta;
    }

    // --- RECEPCIÓN DE DATOS ---
    void ActualizarDatosCinta(SLDBeltPayload data)
    {
        // 1. Actualizamos la velocidad para el movimiento de los eslabones
        velocidadActual = data.velocidad;

        // 2. Convertimos los enteros (0 o 1) de Python a booleanos de Unity
        sensorEntrada = (data.SensorEntrada == 1);
        sensorCilindros = (data.SensorCilindros == 1);

        // Aquí puedes añadir lógica inmediata si un sensor se activa, por ejemplo:
        // if (sensorEntrada) { DoSomething(); }
    }

    // --- LÓGICA DE LA CADENA ---
    void ConfigurarEslabones()
    {
        List<Transform> sinOrdenar = new List<Transform>();
        foreach (Transform t in objetoCintaPadre) sinOrdenar.Add(t);

        if (sinOrdenar.Count == 0) return;

        eslabonesOrdenados.Clear();
        Transform actual = sinOrdenar[0];
        eslabonesOrdenados.Add(actual);
        sinOrdenar.RemoveAt(0);

        while (sinOrdenar.Count > 0)
        {
            Transform masCercano = sinOrdenar
                .OrderBy(t => Vector3.Distance(t.localPosition, actual.localPosition))
                .First();

            eslabonesOrdenados.Add(masCercano);
            sinOrdenar.Remove(masCercano);
            actual = masCercano;
        }

        posRailes = new Vector3[eslabonesOrdenados.Count];
        rotRailes = new Quaternion[eslabonesOrdenados.Count];

        for (int i = 0; i < eslabonesOrdenados.Count; i++)
        {
            posRailes[i] = eslabonesOrdenados[i].localPosition;
            rotRailes[i] = eslabonesOrdenados[i].localRotation;
        }
    }

    void Update()
    {
        if (velocidadActual > 0 && eslabonesOrdenados.Count > 0)
        {
            progresoCiclo += velocidadActual * multiplicadorVelocidad * Time.deltaTime;

            if (progresoCiclo >= 1f) progresoCiclo -= 1f;

            for (int i = 0; i < eslabonesOrdenados.Count; i++)
            {
                int sigIdx = (i + 1) % eslabonesOrdenados.Count;
                eslabonesOrdenados[i].localPosition = Vector3.Lerp(posRailes[i], posRailes[sigIdx], progresoCiclo);
                eslabonesOrdenados[i].localRotation = Quaternion.Slerp(rotRailes[i], rotRailes[sigIdx], progresoCiclo);
            }
        }
    }
}