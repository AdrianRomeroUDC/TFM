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

        if (pieza == null)
        {
            Debug.LogError("<color=red><b>[CINTA SLD - ALERTA CRÍTICA]:</b> 'piezaEnTransito' es NULL.</color>");
            return;
        }

        if (sensorEntradaObjeto == null)
        {
            Debug.LogError("<color=red><b>[CINTA SLD - ERROR]:</b> Falta asignar 'sensorEntradaObjeto' en el Inspector.</color>");
            return;
        }

        // 1. Encontrar la posición del haz de entrada en el mundo físico
        Vector3 puntoDeBusquedaMundial = sensorEntradaObjeto.TransformPoint(offsetBusqueda);

        // 2. Buscar el eslabón de la cinta SLD más cercano para calibrar la altura (Y) de asentamiento
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
            // --- TRUCO MATEMÁTICO DE ESPACIO DE ALINEACIÓN ---
            // Primero la hacemos hija directa de la estructura global para aislarla de las rotaciones raras de los eslabones
            pieza.SetParent(objetoCintaPadre, false);

            // Colocamos la pieza en la posición horizontal del haz, pero con la altura (Y) física exacta de la superficie del eslabón
            Vector3 posicionAlineadaMundo = puntoDeBusquedaMundial;
            posicionAlineadaMundo.y = eslabonMasCercano.position.y + 0.000154f; // Mantiene el desfase de altura útil que tenías
            pieza.position = posicionAlineadaMundo;

            // CALIBRACIÓN DE ROTACIÓN RELATIVA SÍNCRONA:
            // Forzamos a la pieza a mirar exactamente con la misma orientación relativa que tenía la cinta original, 
            // pero alineada a los ejes estructurales de la nueva cinta SLD, compensando el desfase de 90 grados.
            pieza.localRotation = Quaternion.Euler(0f, 0f, 0f);

            // Activamos el objeto en la escena (Imagen 1 muestra que se clona correctamente en la jerarquía)
            pieza.gameObject.SetActive(true);

            // Asignamos la referencia para el movimiento lineal continuo del Update
            piezaActivaEnCinta = pieza;

            // Vaciamos el canal de tránsito
            ControladorCintaMPO_mqtt.piezaEnTransito = null;

            Debug.Log($"<color=green><b>[CINTA SLD]:</b> ¡Pieza reposicionada y reorientada con éxito! Adaptada de MPO a SLD.</color>");
            Physics.SyncTransforms();
        }
        else
        {
            Debug.LogError("<color=red><b>[CINTA SLD - ERROR]:</b> No se encontró eslabón de apoyo.</color>");
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