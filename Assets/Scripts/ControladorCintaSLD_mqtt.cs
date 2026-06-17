using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ControladorCintaSLD_mqtt : MonoBehaviour
{
    [Header("Referencia a la Cinta")]
    public Transform objetoCintaPadre;

    [Header("Referencias de Sensores Físicos (Arrastra el objeto 3D aquí)")]
    public Transform sensorEntradaObjeto;

    [Header("Ajuste Fino de Escaneo (¡Para el Gizmo!)")]
    [Tooltip("Desfase local desde el sensor para centrar la búsqueda en la superficie útil superior de la cinta.")]
    public Vector3 offsetBusqueda = Vector3.zero;
    public bool mostrarGizmos = true;

    [Header("Configuración de Movimiento Real de la Pieza")]
    public float multiplicadorVelocidad = 0.001f;
    [SerializeField] private float velocidadActual = 0f;

    [Tooltip("Dirección local (del Padre) en la que se desplazará la pieza a lo largo de la cinta.")]
    public Vector3 direccionAvanceLocal = new Vector3(0f, 0f, 1f);

    [Header("Monitoreo de Sensores (Lectura)")]
    public bool SensorEntrada = false;
    public bool SensorCilindros = false;

    [Header("Ajuste de Posición")]
    [Tooltip("Modifica estos tres valores (X, Y, Z) en el Inspector para centrar y elevar la pieza respecto al eslabón.")]
    public Vector3 offsetLocalPieza = new Vector3(0f, 0.000154f, -0.000238f);
    public Vector3 rotacionLocalPieza = new Vector3(-2.818f, -90f, 90f);

    // Hilo seguro: Bandera para avisarle a Update() que debe procesar la pieza
    private bool solicitarReaparicion = false;

    private List<Transform> eslabonesOrdenados = new List<Transform>();
    private Vector3[] posRailes;
    private Quaternion[] rotRailes;
    private float progresoCiclo = 0f;

    // Referencia interna para mover la pieza de extremo a extremo sin saltos
    private Transform piezaActivaEnCinta = null;

    void Start()
    {
        if (objetoCintaPadre == null)
        {
            Debug.LogError("<color=red><b>[CINTA SLD - ERROR]:</b> ¡Falta asignar el Objeto Cinta Padre en el Inspector!</color>");
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
        SensorCilindros = (data.SensorCilindros == 1);

        bool nuevoSensorEntrada = (data.SensorEntrada == 1);

        // Detección de flanco de bajada (Cambio de 1 a 0)
        if (SensorEntrada && !nuevoSensorEntrada)
        {
            Debug.Log("<color=orange><b>[DEBUG SLD]:</b> ¡Flanco de bajada detectado en Red! Levantando bandera de reaparición.</color>");
            solicitarReaparicion = true;
        }

        SensorEntrada = nuevoSensorEntrada;
    }

    void Update()
    {
        float deltaMovimiento = velocidadActual * multiplicadorVelocidad * Time.deltaTime;

        // 1. Mover los eslabones (Efecto visual continuo)
        if (velocidadActual > 0 && eslabonesOrdenados.Count > 0)
        {
            progresoCiclo += deltaMovimiento;

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

        // 2. Desplazar la pieza físicamente por encima de la cinta completa sin saltos
        if (velocidadActual > 0 && piezaActivaEnCinta != null)
        {
            piezaActivaEnCinta.localPosition += direccionAvanceLocal.normalized * deltaMovimiento;
        }

        // 3. Reaparición segura
        if (solicitarReaparicion)
        {
            solicitarReaparicion = false;
            EjecutarReaparicionPieza();
        }
    }

    private void EjecutarReaparicionPieza()
    {
        Transform pieza = ControladorCintaMPO_mqtt.piezaEnTransito;

        if (pieza == null) return;
        if (sensorEntradaObjeto == null) return;

        // 1. Localizar eslabón
        Vector3 puntoDeBusquedaMundial = sensorEntradaObjeto.TransformPoint(offsetBusqueda);
        Transform eslabonMasCercano = null;
        float distanciaMinima = float.MaxValue;

        foreach (Transform eslabon in eslabonesOrdenados)
        {
            float distancia = Vector3.Distance(eslabon.position, puntoDeBusquedaMundial);
            if (distancia < distanciaMinima)
            {
                distanciaMinima = distancia;
                eslabonMasCercano = eslabon;
            }
        }

        if (eslabonMasCercano != null)
        {
            // 2. Emparentamiento directo respetando la jerarquía local
            pieza.SetParent(eslabonMasCercano, false);

            // 3. Aplicamos el offset local del Inspector directamente
            pieza.localPosition = offsetLocalPieza;
            pieza.localRotation = Quaternion.Euler(rotacionLocalPieza);

            // Activamos visibilidad
            pieza.gameObject.SetActive(true);

            // Limpieza de buffers
            ControladorCintaMPO_mqtt.piezaEnTransito = null;
            piezaActivaEnCinta = null;

            Debug.Log($"<color=lime><b>[CINTA SLD]:</b> Pieza emparentada a '{eslabonMasCercano.name}'.</color>");
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

    void OnDrawGizmos()
    {
        if (!mostrarGizmos || sensorEntradaObjeto == null) return;

        Vector3 puntoDeBusqueda = sensorEntradaObjeto.TransformPoint(offsetBusqueda);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(puntoDeBusqueda, 0.012f);
        Gizmos.DrawSphere(puntoDeBusqueda, 0.003f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(sensorEntradaObjeto.position, puntoDeBusqueda);
    }
}