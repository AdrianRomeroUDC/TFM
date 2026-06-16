using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ControladorCintaMPO_mqtt : MonoBehaviour
{
    [Header("Referencia a la Cinta")]
    public Transform objetoCintaPadre;

    // --- REFERENCIAS PARA PASO POR SENSOR ---
    [Header("Referencias de Sensores Físicos")]
    public Transform sensorSalidaObjeto; // Arrastra aquí el objeto 3D del sensor de salida

    [Tooltip("Longitud en metros del haz de luz verde para cruzar la cinta.")]
    public float rangoSensor = 0.15f;

    [Header("Configuración de Movimiento")]
    public float multiplicadorVelocidad = 0.001f;
    [SerializeField] private float velocidadActual = 0f;

    [Header("Monitoreo de Sensores (Lectura)")]
    public bool sensorSalida = false;

    // Variable global temporal para transferir la pieza a la otra cinta
    public static Transform piezaEnTransito = null;

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
        // 1. Mover los eslabones si la cinta está activa
        if (velocidadActual > 0 && eslabonesOrdenados.Count > 0)
        {
            progresoCiclo += velocidadActual * multiplicadorVelocidad * Time.deltaTime;

            if (progresoCiclo >= 1f)
            {
                Transform ultimo = eslabonesOrdenados[eslabonesOrdenados.Count - 1];
                eslabonesOrdenados.RemoveAt(eslabonesOrdenados.Count - 1);
                eslabonesOrdenados.Insert(0, ultimo);

                progresoCiclo -= 1f;
            }

            for (int i = 0; i < eslabonesOrdenados.Count; i++)
            {
                int sigIdx = (i + 1) % eslabonesOrdenados.Count;
                eslabonesOrdenados[i].localPosition = Vector3.Lerp(posRailes[i], posRailes[sigIdx], progresoCiclo);
                eslabonesOrdenados[i].localRotation = Quaternion.Slerp(rotRailes[i], rotRailes[sigIdx], progresoCiclo);
            }
        }

        // 2. --- DETECCIÓN POR HAZ DE LUZ LÁSER (LÍNEA VERDE) ---
        if (sensorSalidaObjeto != null)
        {
            // Calculamos el origen exacto desde el centro del sensor
            Vector3 centroSensorMundo = sensorSalidaObjeto.position;
            Renderer renderizador = sensorSalidaObjeto.GetComponent<Renderer>();
            if (renderizador != null)
            {
                centroSensorMundo = renderizador.bounds.center;
            }

            // Lanzamos un rayo físico invisible que sigue exactamente la dirección de la línea verde
            RaycastHit hit;
            if (Physics.Raycast(centroSensorMundo, sensorSalidaObjeto.forward, out hit, rangoSensor))
            {
                // Filtramos para que solo reaccione si lo que toca contiene la palabra "pieza"
                if (hit.transform.name.ToLower().Contains("pieza") && hit.transform.gameObject.activeSelf)
                {
                    piezaEnTransito = hit.transform;
                    piezaEnTransito.gameObject.SetActive(false); // Desaparece instantáneamente al tocar la línea

                    Debug.Log($"<color=green><b>[CINTA MPO]:</b> ¡Pieza detectada al interrumpir la línea verde! Ocultada.</color>");
                }
            }
        }
    }

    // --- GIZMO: DIBUJA EXCLUSIVAMENTE LA LÍNEA VERDE DEL HAZ ---
    private void OnDrawGizmos()
    {
        if (sensorSalidaObjeto != null)
        {
            Matrix4x4 matrizOriginal = Gizmos.matrix;

            // Buscamos el centro geométrico real del Mesh del sensor
            Vector3 centroSensorMundo = sensorSalidaObjeto.position;
            Renderer renderizador = sensorSalidaObjeto.GetComponent<Renderer>();
            if (renderizador != null)
            {
                centroSensorMundo = renderizador.bounds.center;
            }

            // Aplicamos posición del centro y rotación del objeto
            Matrix4x4 matrizCentro = Matrix4x4.TRS(centroSensorMundo, sensorSalidaObjeto.rotation, Vector3.one);
            Gizmos.matrix = matrizCentro;

            // Dibujamos únicamente la línea VERDE que apunta hacia adelante en su eje azul (+Z)
            Gizmos.color = Color.green;
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * rangoSensor);

            Gizmos.matrix = matrizOriginal;
        }
    }
}