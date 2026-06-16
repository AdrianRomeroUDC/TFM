using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ControladorCintaMPO_mqtt : MonoBehaviour
{
    [Header("Referencia a la Cinta")]
    public Transform objetoCintaPadre;

    [Header("Configuración de Movimiento")]
    public float multiplicadorVelocidad = 0.001f;
    [SerializeField] private float velocidadActual = 0f;

    [Header("Monitoreo de Sensores (Lectura)")]
    public bool sensorSalida = false;

    private List<Transform> eslabonesOrdenados = new List<Transform>();
    private Vector3[] posRailes;
    private Quaternion[] rotRailes;
    private float progresoCiclo = 0f;

    void Start()
    {
        if (objetoCintaPadre == null)
        {
            Debug.LogError("¡Falta asignar el Objeto Cinta Padre!");
            return;
        }

        ConfigurarEslabones();
        InvokeRepeating("IntentarSuscripcion", 0f, 1f);
    }

    void IntentarSuscripcion()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnMPOBeltUpdateEvent += ActualizarDatosCintaMPO;
            Debug.Log("<color=cyan><b>Cinta MPO:</b> Conectado con éxito (Estado y Sensor habilitados).</color>");
            CancelInvoke("IntentarSuscripcion");
        }
    }

    void OnDisable()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnMPOBeltUpdateEvent -= ActualizarDatosCintaMPO;
    }

    void ActualizarDatosCintaMPO(MPOBeltPayload data)
    {
        velocidadActual = (data.estado == 1) ? 512f : 0f;
        sensorSalida = (data.sensorSalida == 1);
    }

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

            // CUANDO SE COMPLETA EL TRAMO ENTRE UN ESLABÓN Y EL SIGUIENTE
            if (progresoCiclo >= 1f)
            {
                // ROTACIÓN INTERNA DE LA LISTA:
                // Como cada eslabón ha llegado físicamente a la posición del siguiente,
                // sacamos el último elemento de la lista y lo metemos en la primera posición (índice 0).
                // De esta forma, en el siguiente frame todos los eslabones avanzarán al siguiente raíl de destino.
                Transform ultimo = eslabonesOrdenados[eslabonesOrdenados.Count - 1];
                eslabonesOrdenados.RemoveAt(eslabonesOrdenados.Count - 1);
                eslabonesOrdenados.Insert(0, ultimo);

                progresoCiclo -= 1f; // Restamos el ciclo de forma fluida
            }

            // Aplicar el movimiento continuo utilizando las posiciones de raíl fijas
            for (int i = 0; i < eslabonesOrdenados.Count; i++)
            {
                int sigIdx = (i + 1) % eslabonesOrdenados.Count;
                eslabonesOrdenados[i].localPosition = Vector3.Lerp(posRailes[i], posRailes[sigIdx], progresoCiclo);
                eslabonesOrdenados[i].localRotation = Quaternion.Slerp(rotRailes[i], rotRailes[sigIdx], progresoCiclo);
            }
        }
    }
}