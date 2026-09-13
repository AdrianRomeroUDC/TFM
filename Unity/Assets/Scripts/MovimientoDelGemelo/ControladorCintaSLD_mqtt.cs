using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Controla el gemelo digital de la cinta transportadora de la estación SLD (la cinta clasificadora
/// con detección de color). Esta cinta recibe piezas desde la cinta del MPO, las lleva hasta el
/// sensor de color, las "pinta" del color detectado por el cilindro correspondiente
/// (<see cref="ControladorCilindrosSLD_mqtt"/>) y, cuando el pistón empuja, traslada la pieza
/// físicamente hasta el final de la rampa de su color (blanca, roja o azul). También se encarga
/// de que, si por cualquier motivo no llega ninguna pieza real desde el MPO, se genere una pieza
/// de repuesto para no romper la simulación, y de destruir cualquier pieza que llegue al final de
/// la cinta sin haber sido recogida.
/// </summary>
public class ControladorCintaSLD_mqtt : MonoBehaviour
{
    [Header("Referencia a la Cinta")]
    public Transform objetoCintaPadre; // Objeto padre que contiene todos los eslabones (piezas de la cadena) de la cinta.

    [Header("Referencias de Sensores Físicos (Arrastra el objeto 3D aquí)")]
    public Transform sensorEntradaObjeto; // Posición del sensor de entrada real de la SLD (donde "aparecen" las piezas nuevas).

    [Header("Referencia para Auto-Destrucción (Expulsor Azul)")]
    [Tooltip("Arrastra aquí el objeto 3D del Pistón Azul (Cilindro_PiezasAzules).")]
    public Transform pistonAzul; // Se usa como referencia para calcular el punto final de la cinta, a partir del cual una pieza se considera "perdida".

    [Tooltip("Ajuste fino en X para desplazar el punto límite de corte respecto al pistón azul.")]
    public float offsetX_Autodestruccion = 0f;

    [Tooltip("Ajuste fino en Y para desplazar el punto límite de corte respecto al pistón azul.")]
    public float offsetY_Autodestruccion = 0f;

    [Tooltip("Ajuste fino en Z para desplazar el punto límite de corte respecto al pistón azul.")]
    public float offsetZ_Autodestruccion = 0.0003f;

    [Header("Prefabs de Auto-Sanación (Fallback Spawner)")]
    [Tooltip("Arrastra aquí el prefab de tu pieza base gris (el mismo que usa el DPS y el Horno).")]
    public GameObject prefabBaseGris; // Prefab de repuesto: se usa si no llega ninguna pieza real desde la cinta del MPO cuando se detecta que debería haber una.

    [Header("Ajuste Fino de Escaneo (¡Para el Gizmo!)")]
    [Tooltip("Desfase local desde el sensor para centrar la búsqueda en la superficie útil superior de la cinta.")]
    public Vector3 offsetBusqueda = Vector3.zero;
    public bool mostrarGizmos = true; // Activa/desactiva el dibujado de las ayudas visuales en el editor (ver OnDrawGizmos).

    [Header("Configuración de Movimiento Visual (Sincronizado con MPO)")]
    [Tooltip("Usa el mismo valor que en la CintaMPO (ej: 0.001) para que vayan a la par.")]
    public float multiplicadorVelocidad = 0.001f; // Convierte la velocidad "cruda" que llega por MQTT en un avance visual razonable de los eslabones.
    [SerializeField] private float velocidadActual = 0f; // Última velocidad de cinta recibida por MQTT (se ve en el Inspector para depurar).

    [Header("Monitoreo de Sensores (Lectura)")]
    public bool SensorEntrada = false; // Estado actual del sensor de entrada de la SLD (si hay o no una pieza justo ahí).
    public bool SensorCilindros = false; // Estado actual del sensor de cilindros (asociado al pistón que empuja hacia las rampas).

    [Header("Ajustes Anti-Duplicados (Filtro por Tiempo)")]
    [Tooltip("Tiempo mínimo en segundos entre dos flancos de bajada para permitir un nuevo spawn.")]
    public float cooldownFlancoBajada = 1.0f; // Tiempo mínimo entre dos "reapariciones" de pieza, para evitar duplicar por rebotes de señal.
    private float ultimoTiempoFlanco = -999f; // Marca de tiempo del último flanco de bajada aceptado.

    [Header("Ajuste de Posición Manual")]
    [Tooltip("Modifica estos tres valores (X, Y, Z) en el Inspector para centrar y elevar la pieza respecto al eslabón.")]
    public Vector3 offsetLocalPieza = new Vector3(0f, 0.000154f, -0.000238f);
    public Vector3 rotacionLocalPieza = new Vector3(-2.818f, -90f, 90f);

    [Header("Referencias de Rampas / Plataformas")]
    // Puntos finales de cada rampa de salida, hacia donde se traslada la pieza cuando el
    // pistón de su color la empuja fuera de la cinta.
    public Transform finRampaBlanca;
    public Transform finRampaRoja;
    public Transform finRampaAzul;

    [Header("Ajustes del Desplazamiento")]
    [Tooltip("Velocidad lineal a la que se desplazará la pieza hacia la rampa.")]
    public float velocidadTraslacion = 0.5f;

    [Tooltip("Posición Z local exacta que debe tener la pieza al llegar a la rampa para evitar traspasarla.")]
    public float posicionZLocalEnRampa = -0.0001335999f;

    // --- VARIABLES DE CONTROL INTERNO Y COLOR ---
    private Transform piezaActual = null; // Pieza que está actualmente viajando sobre la cinta (no en una rampa).
    private string ultimoColorCilindro = "WHITE"; // Último color de cilindro recibido por MQTT, usado para pintar la pieza.
    private bool solicitarReaparicion = false; // Bandera: "hay que hacer aparecer/reaparecer una pieza en el siguiente Update()".
    private bool flagCambiarColor = false; // Bandera: "hay que repintar la pieza actual en el siguiente Update()".

    // Banderas de hilos seguras para el empuje a la rampa
    private bool flagEmpujarARampa = false; // Bandera: "hay que iniciar el empuje hacia una rampa en el siguiente Update()".
    private string colorParaEmpuje = ""; // Color de la rampa hacia la que hay que empujar la pieza.

    // --- MEMORIA PARA EVITAR REBOTES EN MQTT ---
    private bool ultimoEstadoActive = false; // Recuerda si el cilindro estaba activo en el mensaje MQTT anterior, para detectar flancos de subida.

    // Control de traslación hacia las rampas
    private Transform piezaEnRampa = null; // Pieza que se está desplazando actualmente hacia el final de su rampa.
    private Vector3 posicionLocalObjetivo; // Posición local (dentro de la rampa) a la que debe llegar piezaEnRampa.

    // Lista de eslabones de la cadena de la cinta, ordenados en el orden real en que se
    // recorren (de uno al siguiente), junto con sus posiciones/rotaciones de referencia.
    private List<Transform> eslabonesOrdenados = new List<Transform>();
    private Vector3[] posRailes;
    private Quaternion[] rotRailes;
    private float progresoCiclo = 0f; // Progreso (0 a 1) del desplazamiento de los eslabones entre su posición actual y la siguiente.

    // Bloque de propiedades de material para evitar lag
    // Permite cambiar el color de una pieza sin crear un material nuevo cada vez (más eficiente).
    private MaterialPropertyBlock propBlock;

    void Start()
    {
        if (objetoCintaPadre == null)
        {
            Debug.LogError("<color=red><b>[CINTA SLD - ERROR]:</b> ¡Falta asignar el Objeto Cinta Padre en el Inspector!</color>");
            return;
        }

        propBlock = new MaterialPropertyBlock();

        // Ordenamos los eslabones de la cadena y nos suscribimos a los eventos MQTT de la cinta.
        ConfigurarEslabones();
        InvokeRepeating("IntentarSuscripcion", 0f, 1f);
    }

    void IntentarSuscripcion()
    {
        if (MQTTClient.Instance != null)
        {
            // Nos suscribimos a dos eventos: el movimiento/sensores de la propia cinta, y el
            // color/estado de los cilindros de la SLD (para saber de qué color pintar la pieza).
            MQTTClient.Instance.OnBeltUpdateEvent += ActualizarDatosCinta;
            MQTTClient.Instance.OnCylinderUpdateEvent += ActualizarColorDesdeCilindro;

            Debug.Log("<color=green><b>Cinta SLD:</b> Suscrito a eventos de movimiento y color de cilindros con éxito.</color>");
            CancelInvoke("IntentarSuscripcion");
        }
    }

    void OnDisable()
    {
        // Nos damos de baja de ambos eventos al desactivar el objeto, para no dejar suscripciones activas de más.
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnBeltUpdateEvent -= ActualizarDatosCinta;
            MQTTClient.Instance.OnCylinderUpdateEvent -= ActualizarColorDesdeCilindro;
        }
    }

    // Se llama cada vez que llega un mensaje MQTT sobre los cilindros de la SLD. Guarda el color
    // que debe llevar la pieza y, si detecta que el cilindro se acaba de activar (flanco de
    // subida), marca que hay que empujar la pieza hacia la rampa de ese color.
    void ActualizarColorDesdeCilindro(JSON_SLDCylinder data)
    {
        if (data == null) return;

        string colorLimpio = null;

        if (!string.IsNullOrEmpty(data.cyl_color))
        {
            colorLimpio = data.cyl_color.Replace("\"", "").Trim().ToUpper();
        }

        bool nuevoActive = data.active;
        // Un "flanco de subida" es el instante exacto en que el cilindro pasa de inactivo a activo.
        bool flancoSubidaActive = nuevoActive && !ultimoEstadoActive;
        ultimoEstadoActive = nuevoActive;

        if (string.IsNullOrEmpty(colorLimpio)) return;

        ultimoColorCilindro = colorLimpio;
        flagCambiarColor = true; // Pedimos que se repinte la pieza en el próximo Update().

        if (flancoSubidaActive)
        {
            // El cilindro se acaba de activar: pedimos que se empuje la pieza hacia su rampa.
            colorParaEmpuje = colorLimpio;
            flagEmpujarARampa = true;
        }
    }

    /// <summary>
    /// Se llama cada vez que llega un mensaje MQTT con el estado físico de la cinta (velocidad
    /// y sensores). Detecta el instante en que una pieza deja de tapar el sensor de entrada
    /// mientras la cinta está en marcha (un "flanco de bajada" con movimiento), que es la señal
    /// de que ha entrado una pieza nueva a la cinta y hay que hacerla aparecer en Unity.
    /// </summary>
    /// <param name="data">Datos de la cinta: velocidad actual y estado de los sensores de entrada y de cilindros.</param>
    void ActualizarDatosCinta(SLDBeltPayload data)
    {
        velocidadActual = data.velocidad;
        SensorCilindros = (data.SensorCilindros == 1);

        bool nuevoSensorEntrada = (data.SensorEntrada == 1);

        // DETECCIÓN DE FLANCO DE BAJADA CON CINTA EN MOVIMIENTO + FILTRO COOLDOWN
        if (SensorEntrada && !nuevoSensorEntrada)
        {
            bool cintaEstaEnMovimiento = velocidadActual > 0f;

            if (cintaEstaEnMovimiento)
            {
                // Solo aceptamos el flanco si ha pasado suficiente tiempo desde el último, para
                // evitar generar piezas duplicadas por pequeños rebotes en la señal del sensor.
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
                // Si la cinta está parada, el cambio de sensor no puede deberse a que una pieza
                // avance de verdad, así que lo ignoramos.
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
        // Simulamos el movimiento de la cadena desplazando cada eslabón hacia la posición del
        // siguiente, en un ciclo continuo (como una cinta transportadora real).
        if (velocidadActual > 0 && eslabonesOrdenados.Count > 0)
        {
            float deltaProgresoMPOStyle = velocidadActual * multiplicadorVelocidad * Time.deltaTime;
            progresoCiclo += deltaProgresoMPOStyle;

            // Cuando el progreso llega a 1 (un eslabón completo), "rotamos" la lista: el último
            // eslabón pasa a ser el primero, simulando que la cadena da una vuelta completa.
            while (progresoCiclo >= 1f)
            {
                Transform ultimo = eslabonesOrdenados[eslabonesOrdenados.Count - 1];
                eslabonesOrdenados.RemoveAt(eslabonesOrdenados.Count - 1);
                eslabonesOrdenados.Insert(0, ultimo);

                progresoCiclo -= 1f;
            }

            // Interpolamos la posición y rotación de cada eslabón entre su lugar y el del siguiente.
            for (int i = 0; i < eslabonesOrdenados.Count; i++)
            {
                int sigIdx = (i + 1) % eslabonesOrdenados.Count;
                eslabonesOrdenados[i].localPosition = Vector3.Lerp(posRailes[i], posRailes[sigIdx], progresoCiclo);
                eslabonesOrdenados[i].localRotation = Quaternion.Slerp(rotRailes[i], rotRailes[sigIdx], progresoCiclo);
            }
        }

        // 4. Traslación física a rampa
        // Si hay una pieza siendo empujada hacia una rampa, la desplazamos poco a poco hacia su destino.
        if (piezaEnRampa != null)
        {
            piezaEnRampa.localPosition = Vector3.MoveTowards(
                piezaEnRampa.localPosition,
                posicionLocalObjetivo,
                velocidadTraslacion * Time.deltaTime
            );

            // Cuando la pieza está prácticamente en su posición final, terminamos el desplazamiento
            // y reactivamos su física normal (para que ya no siga "pegada" artificialmente).
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

    // Calcula, en coordenadas del mundo, el punto a partir del cual se considera que una pieza
    // ha "rebasado" el final útil de la cinta (cerca del pistón azul) y debe ser eliminada.
    private Vector3 ObtenerPuntoLimiteEliminacion()
    {
        if (pistonAzul == null) return Vector3.zero;

        Vector3 offsetLocal = new Vector3(offsetX_Autodestruccion, offsetY_Autodestruccion, offsetZ_Autodestruccion);
        return pistonAzul.TransformPoint(offsetLocal);
    }

    // Comprueba si la pieza que hay sobre la cinta ha avanzado más allá del punto límite de
    // eliminación (es decir, se ha "perdido" al final de la cinta sin ser recogida) y, si es así,
    // la destruye para no dejar piezas fantasma en la escena.
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

    // Recorre los eslabones de la cinta buscando cuál de ellos lleva encima una pieza (por su
    // nombre, etiqueta, etc.) y la devuelve.
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

    // Devuelve la pieza "de referencia" más fiable en cada momento: primero busca si hay una
    // pieza físicamente sobre la cinta; si no, usa la que está viajando hacia una rampa; y si
    // tampoco, la última pieza conocida (piezaActual).
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

    /// <summary>
    /// Inicia el desplazamiento físico de la pieza actual desde la cinta hasta el final de la
    /// rampa de salida del color indicado, calculando la posición local exacta a la que debe
    /// llegar para quedar bien centrada sobre la rampa (sin traspasarla ni quedar descentrada).
    /// </summary>
    /// <param name="color">Color de la rampa de destino ("WHITE", "RED" o "BLUE").</param>
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

            // Calculamos el centro real (geométrico) tanto de la rampa como de la pieza, para que
            // el punto de destino deje la pieza perfectamente centrada, sea cual sea su forma.
            Vector3 centroRampaLocal = ObtenerCentroLocal(rampaDestino);
            Vector3 centroPiezaLocal = ObtenerCentroLocal(piezaEnRampa);

            piezaEnRampa.SetParent(rampaDestino, true);

            posicionLocalObjetivo = centroRampaLocal - (piezaEnRampa.localRotation * centroPiezaLocal);
            posicionLocalObjetivo.z = posicionZLocalEnRampa;

            // Mientras viaja hacia la rampa, hacemos la pieza cinemática para que no le afecte la
            // gravedad ni los choques físicos hasta que llegue a su sitio.
            Rigidbody rb = piezaEnRampa.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            Debug.Log($"<color=orange><b>[CINTA SLD]:</b> Iniciando empuje de rampa hacia {rampaDestino.name}.</color>");
        }
    }

    // Calcula el centro geométrico real de un objeto (usando su malla o, si no tiene, su
    // renderer) expresado en coordenadas locales respecto a ese mismo objeto.
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

    // Repinta la pieza actual del color indicado por el último mensaje MQTT de los cilindros,
    // usando un MaterialPropertyBlock para cambiar el color sin generar lag ni crear materiales nuevos.
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

    /// <summary>
    /// Hace aparecer una pieza al principio de la cinta SLD cuando el sensor de entrada detecta
    /// que ha llegado una nueva. Primero intenta usar la pieza que ya venía viajando desde la
    /// cinta del MPO (<see cref="ControladorCintaMPO_mqtt.piezaEnTransito"/>); si no hay ninguna
    /// (por ejemplo, porque se perdió en algún punto), genera una pieza de repuesto a partir del
    /// prefab gris para que la simulación no se quede "coja".
    /// </summary>
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

        // Buscamos cuál es el eslabón de la cadena más cercano al sensor de entrada, para colocar
        // la pieza justo ahí (como si acabara de entrar por ese punto de la cinta real).
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
            // La pieza viaja "pegada" al eslabón (cinemática, sin gravedad) hasta que algo la mueva de ahí.
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
                // Si veníamos usando la pieza real del MPO, avisamos a ese script de que ya se ha
                // "entregado" y ha dejado de estar en tránsito.
                ControladorCintaMPO_mqtt.piezaEnTransito = null;
            }
        }
        else
        {
            // No se encontró ningún eslabón cercano (la cinta no está bien configurada): si habíamos
            // creado una pieza de repuesto, la destruimos para no dejarla flotando sin sitio.
            if (esNuevaPiezaSpawneada && pieza != null)
            {
                Destroy(pieza.gameObject);
            }
        }
    }

    // Ordena los eslabones de la cadena de la cinta formando una cadena continua: empieza por
    // el primero y va encadenando siempre el más cercano al anterior, hasta completar el círculo.
    // Guarda también la posición y rotación de referencia de cada eslabón para poder interpolar
    // su movimiento en Update().
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

    // Dibuja ayudas visuales en el editor de Unity (solo visibles en la vista de Escena) para
    // comprobar dónde está el punto de búsqueda de piezas nuevas y dónde está el límite a
    // partir del cual una pieza se considera perdida y se destruye.
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
