using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ControladorCintaSLD_mqtt : MonoBehaviour
{
    [Header("Referencia a la Cinta")]
    public Transform objetoCintaPadre;

    [Header("Referencias de Sensores Físicos (Arrastra el objeto 3D aquí)")]
    public Transform sensorEntradaObjeto;

    [Header("Configuración de Movimiento")]
    public float multiplicadorVelocidad = 0.001f;
    [SerializeField] private float velocidadActual = 0f;

    [Header("Monitoreo de Sensores (Lectura)")]
    public bool sensorEntrada = false;
    public bool sensorCilindros = false;

    // Hilo seguro: Bandera para avisarle a Update() que debe procesar la pieza
    private bool solicitarReaparicion = false;

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
        InvokeRepeating("IntentarSuscripcion", 0f, 1f);
    }

    void IntentarSuscripcion()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnBeltUpdateEvent += ActualizarDatosCinta;
            Debug.Log("<color=green><b>Cinta SLD:</b> Conectado con éxito al sistema central.</color>");
            CancelInvoke("IntentarSuscripcion");
        }
    }

    void OnDisable()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnBeltUpdateEvent -= ActualizarDatosCinta;
    }

    void ActualizarDatosCinta(SLDBeltPayload data)
    {
        velocidadActual = data.velocidad;
        sensorCilindros = (data.SensorCilindros == 1);

        bool nuevoSensorEntrada = (data.SensorEntrada == 1);

        // Detección de flanco de bajada (Cambio de 1 a 0)
        if (sensorEntrada && !nuevoSensorEntrada)
        {
            // Levantamos la bandera de forma segura. El Update se encargará del resto.
            solicitarReaparicion = true;
        }

        sensorEntrada = nuevoSensorEntrada;
    }

    void Update()
    {
        // 1. Mover los eslabones si la cinta SLD está activa
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

        // 2. Ejecución segura en el Main Thread para hacer reaparecer la pieza
        if (solicitarReaparicion)
        {
            solicitarReaparicion = false; // Consumimos el evento
            EjecutarReaparicionPieza();
        }
    }

    private void EjecutarReaparicionPieza()
    {
        Transform pieza = ControladorCintaMPO_mqtt.piezaEnTransito;

        if (pieza != null)
        {
            if (sensorEntradaObjeto == null)
            {
                Debug.LogError("[CINTA SLD]: No se ha asignado el 'sensorEntradaObjeto' en el Inspector para calcular la cercanía.");
                return;
            }

            Transform eslabonMasCercano = null;
            float distanciaMinima = float.MaxValue;

            // Buscamos el eslabón de SLD más cercano a la posición de la fotocélula de entrada
            foreach (Transform eslabon in eslabonesOrdenados)
            {
                float distancia = Vector3.Distance(eslabon.position, sensorEntradaObjeto.position);
                if (distancia < distanciaMinima)
                {
                    distanciaMinima = distancia;
                    eslabonMasCercano = eslabon;
                }
            }

            if (eslabonMasCercano != null)
            {
                // Asignamos el nuevo eslabón de la cinta SLD como padre de la pieza
                pieza.SetParent(eslabonMasCercano, true);

                // Ubicación milimétrica relativa al eslabón (Cifras del Inspector)
                pieza.localPosition = new Vector3(0f, 0.000154f, -0.000238f);
                pieza.localRotation = Quaternion.Euler(-2.818f, -90f, 90f);

                // Hacemos visible la pieza de nuevo
                pieza.gameObject.SetActive(true);

                // Vaciamos el tránsito para dejarlo disponible para la siguiente pieza
                ControladorCintaMPO_mqtt.piezaEnTransito = null;

                Debug.Log($"<color=green><b>[CINTA SLD]:</b> Pieza acoplada con éxito al eslabón ({eslabonMasCercano.name}) tras flanco de bajada.</color>");

                // Sincroniza las físicas inmediatamente para evitar desfases de colisión
                Physics.SyncTransforms();
            }
        }
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
}