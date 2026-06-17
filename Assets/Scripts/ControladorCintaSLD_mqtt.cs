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

    [Header("Configuración de Movimiento Visual (Sincronizado con MPO)")]
    [Tooltip("Usa el mismo valor que en la CintaMPO (ej: 0.001) para que vayan a la par.")]
    public float multiplicadorVelocidad = 0.001f;
    [SerializeField] private float velocidadActual = 0f;

    [Header("Monitoreo de Sensores (Lectura)")]
    public bool SensorEntrada = false;
    public bool SensorCilindros = false;

    [Header("Ajuste de Posición Manual")]
    [Tooltip("Modifica estos tres valores (X, Y, Z) en el Inspector para centrar y elevar la pieza respecto al eslabón.")]
    public Vector3 offsetLocalPieza = new Vector3(0f, 0.000154f, -0.000238f);
    public Vector3 rotacionLocalPieza = new Vector3(-2.818f, -90f, 90f);

    [Header("Referencias de Rampas / Plataformas")]
    public Transform finRampaBlanca;
    public Transform finRampaRoja;
    public Transform finRampaAzul;

    [Header("Ajustes del Desplazamiento")]
    [Tooltip("Velocidad lineal a la que se desplazará la pieza hacia la rampa.")]
    public float velocidadTraslacion = 0.5f;

    [Tooltip("Posición Z local exacta que debe tener la pieza al llegar a la rampa para evitar traspasarla.")]
    public float posicionZLocalEnRampa = -0.0001335999f;

    // --- VARIABLES DE CONTROL INTERNO Y COLOR ---
    private Transform piezaActual = null;          // Guarda la pieza que viaja actualmente por esta cinta
    private string ultimoColorCilindro = "WHITE"; // Almacena el último color enviado por el topic de cilindros
    private bool solicitarReaparicion = false;    // Bandera de hilos para reaparición
    private bool flagCambiarColor = false;        // Bandera de hilos para cambio de color

    // Banderas de hilos seguras para el empuje a la rampa
    private bool flagEmpujarARampa = false;
    private string colorParaEmpuje = "";

    // Control de traslación hacia las rampas
    private Transform piezaEnRampa = null;
    private Vector3 posicionLocalObjetivo;

    private List<Transform> eslabonesOrdenados = new List<Transform>();
    private Vector3[] posRailes;
    private Quaternion[] rotRailes;
    private float progresoCiclo = 0f;

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
            MQTTClient.Instance.OnCylinderUpdateEvent += ActualizarColorDesdeCilindro;

            Debug.Log("<color=green><b>Cinta SLD:</b> Suscrito a eventos de movimiento y color de cilindros con éxito.</color>");
            CancelInvoke("IntentarSuscripcion");
        }
    }

    void OnDisable()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnBeltUpdateEvent -= ActualizarDatosCinta;
            MQTTClient.Instance.OnCylinderUpdateEvent -= ActualizarColorDesdeCilindro;
        }
    }

    // Callback del topic f/sld/cylinder (Ocurre en el hilo de MQTT)
    void ActualizarColorDesdeCilindro(string color, int estado)
    {
        ultimoColorCilindro = color;

        if (estado == 1)
        {
            if (SensorCilindros)
            {
                flagCambiarColor = true;
            }

            colorParaEmpuje = color;
            flagEmpujarARampa = true;
        }
    }

    void ActualizarDatosCinta(SLDBeltPayload data)
    {
        velocidadActual = data.velocidad;

        bool nuevoSensorCilindros = (data.SensorCilindros == 1);
        if (!SensorCilindros && nuevoSensorCilindros)
        {
            Debug.Log($"<color=orange><b>[DEBUG SLD]:</b> ¡Pieza detectada en Sensor! Color objetivo actual: {ultimoColorCilindro}</color>");
            flagCambiarColor = true;
        }
        SensorCilindros = nuevoSensorCilindros;

        bool nuevoSensorEntrada = (data.SensorEntrada == 1);
        if (SensorEntrada && !nuevoSensorEntrada)
        {
            solicitarReaparicion = true;
        }
        SensorEntrada = nuevoSensorEntrada;
    }

    void Update()
    {
        // 1. Cambio de color seguro
        if (flagCambiarColor)
        {
            flagCambiarColor = false;
            EjecutarCambioColorPieza();
        }

        // 2. Ejecutar el empuje de rampa de forma segura en el Hilo Principal
        if (flagEmpujarARampa)
        {
            flagEmpujarARampa = false;
            EjecutarEmpujeHaciaRampa(colorParaEmpuje);
        }

        // 3. Mover los eslabones (Sincronizado visualmente con MPO)
        if (velocidadActual > 0 && eslabonesOrdenados.Count > 0)
        {
            float deltaProgresoMPOStyle = velocidadActual * multiplicadorVelocidad * Time.deltaTime;
            progresoCiclo += deltaProgresoMPOStyle;

            while (progresoCiclo >= 1f)
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

        // 4. Traslación pura hacia el centro local de la rampa activa (Sin alterar rotaciones)
        if (piezaEnRampa != null)
        {
            piezaEnRampa.localPosition = Vector3.MoveTowards(
                piezaEnRampa.localPosition,
                posicionLocalObjetivo,
                velocidadTraslacion * Time.deltaTime
            );

            if (Vector3.Distance(piezaEnRampa.localPosition, posicionLocalObjetivo) < 0.0001f)
            {
                Rigidbody rb = piezaEnRampa.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = false;

                Debug.Log($"<color=lime><b>[CINTA SLD]:</b> Centros perfectamente alineados en {piezaEnRampa.parent.name}. Traslación finalizada.</color>");
                piezaEnRampa = null;
            }
        }

        // 5. Reaparición segura en el hilo principal
        if (solicitarReaparicion)
        {
            solicitarReaparicion = false;
            EjecutarReaparicionPieza();
        }
    }

    private void EjecutarEmpujeHaciaRampa(string color)
    {
        if (piezaActual == null) return;

        Transform rampaDestino = null;
        switch (color.ToUpper())
        {
            case "WHITE": rampaDestino = finRampaBlanca; break;
            case "RED": rampaDestino = finRampaRoja; break;
            case "BLUE": rampaDestino = finRampaAzul; break;
        }

        if (rampaDestino != null)
        {
            piezaEnRampa = piezaActual;
            piezaActual = null;

            Vector3 centroRampaLocal = ObtenerCentroLocal(rampaDestino);
            Vector3 centroPiezaLocal = ObtenerCentroLocal(piezaEnRampa);

            piezaEnRampa.SetParent(rampaDestino, true);

            // Calculamos la posición objetivo basada en centros geométricos
            posicionLocalObjetivo = centroRampaLocal - (piezaEnRampa.localRotation * centroPiezaLocal);

            // ¡SOLUCIÓN!: Sobrescribimos el eje Z calculado con el tope manual exacto para que no traspase
            posicionLocalObjetivo.z = posicionZLocalEnRampa;

            Rigidbody rb = piezaEnRampa.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            Debug.Log($"<color=orange><b>[CINTA SLD]:</b> Iniciando traslación corregida en eje Z hacia {rampaDestino.name}.</color>");
        }
    }

    private Vector3 ObtenerCentroLocal(Transform objetivo)
    {
        Renderer[] renderers = objetivo.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return Vector3.zero;

        Bounds encapsulada = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            encapsulada.Encapsulate(renderers[i].bounds);
        }
        return objetivo.InverseTransformPoint(encapsulada.center);
    }

    private void EjecutarCambioColorPieza()
    {
        Transform piezaAColorear = piezaActual != null ? piezaActual : piezaEnRampa;
        if (piezaAColorear == null) return;

        Renderer renderizador = piezaAColorear.GetComponentInChildren<Renderer>();
        if (renderizador != null)
        {
            Color colorObjetivo = Color.white;
            switch (ultimoColorCilindro.ToUpper())
            {
                case "WHITE": colorObjetivo = Color.white; break;
                case "RED": colorObjetivo = Color.red; break;
                case "BLUE": colorObjetivo = Color.blue; break;
                default: colorObjetivo = Color.white; break;
            }

            renderizador.material.color = colorObjetivo;
            Debug.Log($"<color=cyan><b>[CINTA SLD]:</b> Color de la pieza cambiado a <b>{ultimoColorCilindro}</b>.</color>");
        }
    }

    private void EjecutarReaparicionPieza()
    {
        Transform pieza = ControladorCintaMPO_mqtt.piezaEnTransito;
        if (pieza == null || sensorEntradaObjeto == null) return;

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
            Rigidbody rb = pieza.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            pieza.SetParent(eslabonMasCercano, false);
            piezaActual = pieza;

            pieza.localPosition = offsetLocalPieza;
            pieza.localRotation = Quaternion.Euler(rotacionLocalPieza);

            pieza.gameObject.SetActive(true);
            ControladorCintaMPO_mqtt.piezaEnTransito = null;
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
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(sensorEntradaObjeto.position, puntoDeBusqueda);
    }
}