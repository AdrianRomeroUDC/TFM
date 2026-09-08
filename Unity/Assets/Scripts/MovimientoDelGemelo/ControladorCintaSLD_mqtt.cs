using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ControladorCintaSLD_mqtt : MonoBehaviour
{
    [Header("Referencia a la Cinta")]
    public Transform objetoCintaPadre;

    [Header("Referencias de Sensores Físicos (Arrastra el objeto 3D aquí)")]
    public Transform sensorEntradaObjeto;

    [Header("Referencia para Auto-Destrucción (Expulsor Azul)")]
    [Tooltip("Arrastra aquí el objeto 3D del Pistón Azul (Cilindro_PiezasAzules).")]
    public Transform pistonAzul;

    [Tooltip("Ajuste fino en X para desplazar el punto límite de corte respecto al pistón azul.")]
    public float offsetX_Autodestruccion = 0f;

    [Tooltip("Ajuste fino en Y para desplazar el punto límite de corte respecto al pistón azul.")]
    public float offsetY_Autodestruccion = 0f;

    [Tooltip("Ajuste fino en Z para desplazar el punto límite de corte respecto al pistón azul.")]
    public float offsetZ_Autodestruccion = 0.0003f;

    [Header("Prefabs de Auto-Sanación (Fallback Spawner)")]
    [Tooltip("Arrastra aquí el prefab de tu pieza base gris (el mismo que usa el DPS y el Horno).")]
    public GameObject prefabBaseGris;

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

    [Header("Ajustes Anti-Duplicados (Filtro por Tiempo)")]
    [Tooltip("Tiempo mínimo en segundos entre dos flancos de bajada para permitir un nuevo spawn.")]
    public float cooldownFlancoBajada = 1.0f;
    private float ultimoTiempoFlanco = -999f;

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
    private Transform piezaActual = null;
    private string ultimoColorCilindro = "WHITE";
    private bool solicitarReaparicion = false;
    private bool flagCambiarColor = false;

    // Banderas de hilos seguras para el empuje a la rampa
    private bool flagEmpujarARampa = false;
    private string colorParaEmpuje = "";

    // --- MEMORIA PARA EVITAR REBOTES EN MQTT ---
    private bool ultimoEstadoActive = false;

    // Control de traslación hacia las rampas
    private Transform piezaEnRampa = null;
    private Vector3 posicionLocalObjetivo;

    private List<Transform> eslabonesOrdenados = new List<Transform>();
    private Vector3[] posRailes;
    private Quaternion[] rotRailes;
    private float progresoCiclo = 0f;

    // Bloque de propiedades de material para evitar lag
    private MaterialPropertyBlock propBlock;

    void Start()
    {
        if (objetoCintaPadre == null)
        {
            Debug.LogError("<color=red><b>[CINTA SLD - ERROR]:</b> ¡Falta asignar el Objeto Cinta Padre en el Inspector!</color>");
            return;
        }

        propBlock = new MaterialPropertyBlock();

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

    void ActualizarColorDesdeCilindro(JSON_SLDCylinder data)
    {
        if (data == null) return;

        string colorLimpio = null;

        if (!string.IsNullOrEmpty(data.cyl_color))
        {
            colorLimpio = data.cyl_color.Replace("\"", "").Trim().ToUpper();
        }

        bool nuevoActive = data.active;
        bool flancoSubidaActive = nuevoActive && !ultimoEstadoActive;
        ultimoEstadoActive = nuevoActive;

        if (string.IsNullOrEmpty(colorLimpio)) return;

        ultimoColorCilindro = colorLimpio;
        flagCambiarColor = true;

        if (flancoSubidaActive)
        {
            colorParaEmpuje = colorLimpio;
            flagEmpujarARampa = true;
        }
    }

    void ActualizarDatosCinta(SLDBeltPayload data)
    {
        velocidadActual = data.velocidad;
        SensorCilindros = (data.SensorCilindros == 1);

        bool nuevoSensorEntrada = (data.SensorEntrada == 1);

        // 🎯 DETECCIÓN DE FLANCO DE BAJADA CON CINTA EN MOVIMIENTO + FILTRO COOLDOWN
        if (SensorEntrada && !nuevoSensorEntrada)
        {
            bool cintaEstaEnMovimiento = velocidadActual > 0f;

            if (cintaEstaEnMovimiento)
            {
                if (Time.time - ultimoTiempoFlanco >= cooldownFlancoBajada)
                {
                    solicitarReaparicion = true;
                    ultimoTiempoFlanco = Time.time;
                    Debug.Log("<color=green><b>[CINTA SLD]:</b> Flanco de bajada detectado con cinta en movimiento. Pieza solicitada.</color>");
                }
                else
                {
                    Debug.LogWarning($"<color=yellow><b>[CINTA SLD]:</b> Flanco de bajada ignorado por filtro de tiempo (pasaron menos de {cooldownFlancoBajada}s).</color>");
                }
            }
            else
            {
                Debug.LogWarning("<color=orange><b>[CINTA SLD]:</b> Flanco de bajada detectado, pero IGNORADO porque la cinta está detenida (velocidad = 0).</color>");
            }
        }

        SensorEntrada = nuevoSensorEntrada;
    }

    void Update()
    {
        // 1. Cambio de color inmediato tras publicación MQTT
        if (flagCambiarColor)
        {
            flagCambiarColor = false;
            EjecutarCambioColorPieza();
        }

        // 2. Ejecutar el empuje de rampa
        if (flagEmpujarARampa)
        {
            flagEmpujarARampa = false;
            EjecutarEmpujeHaciaRampa(colorParaEmpuje);
        }

        // 3. Mover los eslabones de la cinta
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

        // 4. Traslación física a rampa
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

                Debug.Log($"<color=lime><b>[CINTA SLD]:</b> Traslación a rampa finalizada.</color>");
                piezaEnRampa = null;
            }
        }

        // 5. Reaparición segura
        if (solicitarReaparicion)
        {
            solicitarReaparicion = false;
            EjecutarReaparicionPieza();
        }

        // 6. Comprobación de auto-destrucción tras rebasar pistón azul
        ComprobarDestruccionPiezaFinCinta();
    }

    private Vector3 ObtenerPuntoLimiteEliminacion()
    {
        if (pistonAzul == null) return Vector3.zero;

        Vector3 offsetLocal = new Vector3(offsetX_Autodestruccion, offsetY_Autodestruccion, offsetZ_Autodestruccion);
        return pistonAzul.TransformPoint(offsetLocal);
    }

    private void ComprobarDestruccionPiezaFinCinta()
    {
        Transform piezaAChequear = ObtenerPiezaEnCinta();
        if (piezaAChequear == null || pistonAzul == null || sensorEntradaObjeto == null) return;

        Vector3 puntoLimite = ObtenerPuntoLimiteEliminacion();

        // Distancia desde la entrada hasta el punto límite y hasta la pieza
        float distanciaLimite = Vector3.Distance(sensorEntradaObjeto.position, puntoLimite);
        float distanciaPieza = Vector3.Distance(sensorEntradaObjeto.position, piezaAChequear.position);

        if (distanciaPieza > distanciaLimite)
        {
            Debug.Log($"<color=red><b>[CINTA SLD]:</b> Pieza '{piezaAChequear.name}' rebasó el Gizmo de Eliminación. Destruyendo pieza.</color>");

            if (piezaActual == piezaAChequear) piezaActual = null;
            Destroy(piezaAChequear.gameObject);
        }
    }

    private Transform ObtenerPiezaEnCinta()
    {
        if (objetoCintaPadre == null) return null;

        foreach (Transform eslabon in objetoCintaPadre)
        {
            foreach (Transform hijo in eslabon)
            {
                string nombre = hijo.name.ToLower();
                if (nombre.Contains("pieza") || nombre.Contains("workpiece") || nombre.Contains("clone") || hijo.CompareTag("Pieza"))
                {
                    return hijo;
                }
            }
        }
        return null;
    }

    private Transform ObtenerPiezaSegura()
    {
        Transform piezaEnCinta = ObtenerPiezaEnCinta();
        if (piezaEnCinta != null)
        {
            piezaActual = piezaEnCinta;
            return piezaEnCinta;
        }

        if (piezaEnRampa != null) return piezaEnRampa;
        return piezaActual;
    }

    private void EjecutarEmpujeHaciaRampa(string color)
    {
        Transform piezaParaEmpujar = ObtenerPiezaSegura();
        if (piezaParaEmpujar == null)
        {
            Debug.LogWarning("<color=red><b>[CINTA SLD]:</b> Intento de empujar, pero no se encontró la pieza en la cinta.</color>");
            return;
        }

        Transform rampaDestino = null;
        switch (color.ToUpper())
        {
            case "WHITE": rampaDestino = finRampaBlanca; break;
            case "RED": rampaDestino = finRampaRoja; break;
            case "BLUE": rampaDestino = finRampaAzul; break;
        }

        if (rampaDestino != null)
        {
            piezaEnRampa = piezaParaEmpujar;
            if (piezaParaEmpujar == piezaActual)
            {
                piezaActual = null;
            }

            Vector3 centroRampaLocal = ObtenerCentroLocal(rampaDestino);
            Vector3 centroPiezaLocal = ObtenerCentroLocal(piezaEnRampa);

            piezaEnRampa.SetParent(rampaDestino, true);

            posicionLocalObjetivo = centroRampaLocal - (piezaEnRampa.localRotation * centroPiezaLocal);
            posicionLocalObjetivo.z = posicionZLocalEnRampa;

            Rigidbody rb = piezaEnRampa.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            Debug.Log($"<color=orange><b>[CINTA SLD]:</b> Iniciando empuje de rampa hacia {rampaDestino.name}.</color>");
        }
    }

    private Vector3 ObtenerCentroLocal(Transform objetivo)
    {
        if (objetivo == null) return Vector3.zero;

        MeshFilter mf = objetivo.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Vector3 centroMundo = mf.transform.TransformPoint(mf.sharedMesh.bounds.center);
            return objetivo.InverseTransformPoint(centroMundo);
        }

        Renderer renderer = objetivo.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            return objetivo.InverseTransformPoint(renderer.bounds.center);
        }

        return Vector3.zero;
    }

    private void EjecutarCambioColorPieza()
    {
        Transform piezaAColorear = ObtenerPiezaSegura();
        if (piezaAColorear == null)
        {
            Debug.Log($"<b>[CINTA SLD]:</b> Intento de cambiar color a {ultimoColorCilindro}, pero la pieza no está en la cinta (puede estar en la rampa).");
            return;
        }

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

            renderizador.GetPropertyBlock(propBlock);

            propBlock.SetColor("_Color", colorObjetivo);
            propBlock.SetColor("_BaseColor", colorObjetivo);

            renderizador.SetPropertyBlock(propBlock);

            Debug.Log($"<color=cyan><b>[CINTA SLD]:</b> ¡PINTADO AL INSTANTE! Pieza '{piezaAColorear.name}' pintada de <b>{ultimoColorCilindro}</b> de forma ultra fluida.</color>");
        }
        else
        {
            Debug.LogWarning($"<color=yellow><b>[CINTA SLD]:</b> Se encontró la pieza '{piezaAColorear.name}', pero no tiene Renderer en sus hijos.</color>");
        }
    }

    private void EjecutarReaparicionPieza()
    {
        if (sensorEntradaObjeto == null) return;

        Transform pieza = ControladorCintaMPO_mqtt.piezaEnTransito;
        bool esNuevaPiezaSpawneada = false;

        if (pieza == null)
        {
            if (prefabBaseGris == null)
            {
                Debug.LogError("<color=red><b>[CINTA SLD - ERROR]:</b> ¡Falta asignar el Prefab Base Gris en el Inspector de la Cinta SLD!</color>");
                return;
            }

            GameObject nuevaPieza = Instantiate(prefabBaseGris);
            nuevaPieza.name = prefabBaseGris.name + "(Clone)";
            nuevaPieza.transform.localScale = prefabBaseGris.transform.localScale;

            pieza = nuevaPieza.transform;
            esNuevaPiezaSpawneada = true;

            Debug.Log("<color=yellow><b>[CINTA SLD]:</b> No venía pieza de la Cinta MPO. Generando pieza de respaldo.</color>");
        }

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
            if (rb == null) rb = pieza.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            pieza.SetParent(eslabonMasCercano, true);

            piezaActual = pieza;

            pieza.localPosition = offsetLocalPieza;
            pieza.localRotation = Quaternion.Euler(rotacionLocalPieza);

            pieza.gameObject.SetActive(true);

            if (!esNuevaPiezaSpawneada)
            {
                ControladorCintaMPO_mqtt.piezaEnTransito = null;
            }
        }
        else
        {
            if (esNuevaPiezaSpawneada && pieza != null)
            {
                Destroy(pieza.gameObject);
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

    void OnDrawGizmos()
    {
        if (!mostrarGizmos) return;

        // 1. Gizmo de Búsqueda / Spawn (Verde y Amarillo)
        if (sensorEntradaObjeto != null)
        {
            Vector3 puntoDeBusqueda = sensorEntradaObjeto.TransformPoint(offsetBusqueda);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(puntoDeBusqueda, 0.012f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(sensorEntradaObjeto.position, puntoDeBusqueda);
        }

        // 2. Gizmo de Punto Límite de Eliminación (Esfera Roja)
        if (pistonAzul != null)
        {
            Vector3 puntoLimite = ObtenerPuntoLimiteEliminacion();
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(puntoLimite, 0.012f);
        }
    }
}