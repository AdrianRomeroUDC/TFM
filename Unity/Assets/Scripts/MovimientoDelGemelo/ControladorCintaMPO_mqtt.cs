using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Controla el gemelo digital de la cinta transportadora interna del MPO (Multi-Processing Oven):
/// la cinta que saca las piezas ya procesadas (horneadas y/o cortadas) hacia la siguiente estación
/// (la SLD). Este script hace dos cosas: por un lado anima visualmente los eslabones de la cinta en
/// Unity para que parezca que se mueven en bucle cuando la cinta real está encendida (usando el
/// estado que llega por <see cref="MQTTClient.OnMPOBeltUpdateEvent"/>); por otro, simula un sensor
/// óptico de salida mediante un rayo invisible (raycast) que imita el haz de luz real: cuando una
/// pieza 3D lo atraviesa, se considera "detectada" y se oculta, dejando su transform guardado en
/// <see cref="piezaEnTransito"/> para que la siguiente estación pueda recogerla.
/// </summary>
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
    // (la deja "aparcada" aquí para que el script de la siguiente cinta/estación la recoja).
    public static Transform piezaEnTransito = null;

    // Lista de eslabones (los trozos individuales de la cinta) ordenados formando el bucle cerrado.
    private List<Transform> eslabonesOrdenados = new List<Transform>();
    private Vector3[] posRailes; // Posición local de cada eslabón en su sitio de "raíl" original.
    private Quaternion[] rotRailes; // Rotación local de cada eslabón en su sitio de "raíl" original.
    private float progresoCiclo = 0f; // Progreso (0 a 1) del desplazamiento de un eslabón al siguiente.

    void Start()
    {
        if (objetoCintaPadre == null)
        {
            Debug.LogError("¡Falta asignar el Objeto Cinta Padre!");
            return;
        }

        // Calculamos el orden real de los eslabones antes de empezar a animarlos.
        ConfigurarEslabones();
        // Reintentamos la suscripción a MQTT una vez por segundo hasta que MQTTClient exista.
        InvokeRepeating("IntentarSuscripcion", 0f, 1f);
    }

    // Comprueba si el cliente MQTT ya está listo en la escena; en cuanto lo está, nos suscribimos
    // a su evento de estado de la cinta del MPO y dejamos de reintentar.
    void IntentarSuscripcion()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnMPOBeltUpdateEvent += ActualizarDatosCintaMPO;
            Debug.Log("<color=cyan><b>Cinta MPO:</b> Conectado con éxito (Estado y Sensor habilitados).</color>");
            CancelInvoke("IntentarSuscripcion");
        }
    }

    // Al desactivar este objeto nos damos de baja del evento MQTT, para no dejar una suscripción
    // "fantasma" activa sobre un componente que ya no está en uso.
    void OnDisable()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnMPOBeltUpdateEvent -= ActualizarDatosCintaMPO;
    }

    // Traduce el mensaje MQTT real de la cinta del MPO: si el motor real está encendido (estado == 1)
    // fijamos una velocidad de referencia para la animación, y guardamos si el sensor de salida real
    // está detectando una pieza en este instante.
    void ActualizarDatosCintaMPO(MPOBeltPayload data)
    {
        velocidadActual = (data.estado == 1) ? 512f : 0f;
        sensorSalida = (data.sensorSalida == 1);
    }

    // Ordena los eslabones sueltos de la cinta (que en el editor pueden estar en cualquier orden)
    // formando una cadena continua, uniendo siempre el más cercano al último añadido. Así se puede
    // luego animar el movimiento en bucle desplazando cada eslabón hacia la posición del siguiente.
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

        // Guardamos la posición y rotación "de raíl" de cada eslabón, en el mismo orden de la cadena,
        // para poder interpolar de una posición a la siguiente durante la animación.
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
        // Avanzamos el progreso del ciclo según la velocidad recibida por MQTT; cuando un eslabón
        // completa el hueco hasta el siguiente (progreso >= 1), lo "reciclamos": lo movemos del final
        // de la lista al principio, simulando así el bucle cerrado de la cinta real sin fin.
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

            // Interpolamos cada eslabón entre su posición de raíl y la del siguiente eslabón de la
            // cadena, dando la sensación visual de que toda la cinta se desplaza en bloque.
            for (int i = 0; i < eslabonesOrdenados.Count; i++)
            {
                int sigIdx = (i + 1) % eslabonesOrdenados.Count;
                eslabonesOrdenados[i].localPosition = Vector3.Lerp(posRailes[i], posRailes[sigIdx], progresoCiclo);
                eslabonesOrdenados[i].localRotation = Quaternion.Slerp(rotRailes[i], rotRailes[sigIdx], progresoCiclo);
            }
        }

        // 2. --- DETECCIÓN POR HAZ DE LUZ LÁSER (LÍNEA VERDE) ---
        // Simulamos la barrera óptica real de salida de la cinta lanzando un rayo invisible desde el
        // sensor, en la misma dirección que la línea verde que se dibuja en el editor (ver OnDrawGizmos).
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
                    // "Recogemos" la pieza de la cinta: la guardamos como pieza en tránsito y la
                    // ocultamos, como si hubiera pasado a la siguiente estación (la SLD).
                    piezaEnTransito = hit.transform;
                    piezaEnTransito.gameObject.SetActive(false); // Desaparece instantáneamente al tocar la línea

                    Debug.Log($"<color=green><b>[CINTA MPO]:</b> ¡Pieza detectada al interrumpir la línea verde! Ocultada.</color>");
                }
            }
        }
    }

    // --- GIZMO: DIBUJA EXCLUSIVAMENTE LA LÍNEA VERDE DEL HAZ ---
    // Ayuda visual que solo se ve en el editor de Unity (vista de Escena): dibuja la línea verde que
    // representa el haz del sensor óptico de salida, para poder colocarlo y calibrarlo a simple vista.
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
