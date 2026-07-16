using UnityEngine;

public class ControladorCilindrosSLD_mqtt : MonoBehaviour
{
    [Header("Referencias de Pistones")]
    public Transform pistonBlanco;
    public Transform pistonRojo;
    public Transform pistonAzul;

    [Header("Configuración de Spawning")]
    [Tooltip("Arrastra aquí tu Prefab de la pieza base (piezaBase)")]
    public GameObject piezaBasePrefab;

    [Tooltip("Punto de aparición para la pieza blanca")]
    public Transform spawnPointBlanco;
    [Tooltip("Punto de aparición para la pieza roja")]
    public Transform spawnPointRojo;
    [Tooltip("Punto de aparición para la pieza azul")]
    public Transform spawnPointAzul;

    // Coordenadas Estándar (Rojo y Azul)
    private float xReposoEstandar = 0.001122198f;
    private float xEstiradoEstandar = 0.000826f;

    // Coordenadas Especiales (Blanco)
    private float xReposoBlanco = 0.0002811983f;
    private float xEstiradoBlanco = -0.0000149997f;

    [Header("Ajustes")]
    public float velocidadPiston = 0.01f;
    [Tooltip("Tiempo mínimo en segundos entre spawns para evitar rebotes de señal.")]
    public float cooldownSpawn = 1.5f;

    private float targetBlanco, targetRojo, targetAzul;

    // Control de estado y seguridad
    private bool piezaSpawned = false;
    private float ultimoTiempoActivo = -99f;

    // --- PUENTE SEGURO PARA EVITAR CAÍDAS DE HILOS ---
    private JSON_SLDCylinder datosPendientes = null;

    // --- LÓGICA DE SUSCRIPCIÓN ROBUSTA ---
    void Start()
    {
        Debug.Log("<b>[SLD System]</b> Iniciando script y buscando cliente MQTT...");
        InvokeRepeating("IntentarSuscripcion", 0f, 1f);
    }

    void IntentarSuscripcion()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnCylinderUpdateEvent += ProcesarComandoCilindro;
            Debug.Log("<color=green><b>Cilindros SLD:</b> Conectado con éxito al sistema central.</color>");
            CancelInvoke("IntentarSuscripcion");
        }
    }

    void OnDisable()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnCylinderUpdateEvent -= ProcesarComandoCilindro;
    }

    // --- RECIBIR DATOS DE MQTT (Inmune a caídas de hilos) ---
    void ProcesarComandoCilindro(JSON_SLDCylinder data)
    {
        if (data == null) return;
        datosPendientes = data; // Guardamos los datos de forma segura
    }

    // --- LÓGICA DE SPAWN SEGURA (Hilo principal de Unity) ---
    void ProcesarDatosMQTTSeguro(JSON_SLDCylinder data)
    {
        // --- 1. DETECCIÓN DEL SENSOR ACTIVO EN ESTE INSTANTE ---
        string colorSensor = "";
        if (data.is_white) colorSensor = "WHITE";
        else if (data.is_red) colorSensor = "RED";
        else if (data.is_blue) colorSensor = "BLUE";

        // --- 2. DETECCIÓN DEL COLOR REGISTRADO EN MQTT ---
        string cylColorLimpio = "";
        if (!string.IsNullOrEmpty(data.cyl_color))
        {
            cylColorLimpio = data.cyl_color.Replace("\"", "").Trim().ToUpper();
        }

        // --- 3. DETERMINACIÓN DEL COLOR FINAL PARA EL MOVIMIENTO DEL PISTÓN ---
        string colorFinal = "WHITE";
        if (!string.IsNullOrEmpty(cylColorLimpio)) colorFinal = cylColorLimpio;
        else if (!string.IsNullOrEmpty(colorSensor)) colorFinal = colorSensor;

        // Controlar el movimiento físico de los pistones
        float valor = data.active ? 1f : 0f;
        if (colorFinal == "WHITE") targetBlanco = valor;
        else if (colorFinal == "RED") targetRojo = valor;
        else if (colorFinal == "BLUE") targetAzul = valor;

        // Registrar si el cilindro se activa físicamente
        if (data.active)
        {
            ultimoTiempoActivo = Time.time;
        }

        // --- 4. LÓGICA DE SPAWNING CON FILTRADO DE COINCIDENCIAS ---
        bool algunSensorDetecta = !string.IsNullOrEmpty(colorSensor);

        if (!algunSensorDetecta)
        {
            if (piezaSpawned)
            {
                Debug.Log("<b>[SLD Spawn]</b> Sensores despejados. Candado de spawn reseteado.");
                piezaSpawned = false;
            }
        }
        else if (algunSensorDetecta && !piezaSpawned)
        {
            bool coincideConCylColor = (cylColorLimpio == colorSensor);

            // Tiempos de seguridad
            float tiempoDesdeUltimoActive = Time.time - ultimoTiempoActivo;
            bool activoRecientemente = tiempoDesdeUltimoActive < cooldownSpawn;

            if (coincideConCylColor)
            {
                Debug.Log($"<b>[SLD Spawn Omitido]</b> El sensor activo ({colorSensor}) coincide con cyl_color ({cylColorLimpio}). Evitando duplicado.");
            }
            else if (data.active)
            {
                Debug.LogWarning("<b>[SLD Spawn Cancelado]</b> El cilindro está empujando (active = true).");
            }
            else if (activoRecientemente)
            {
                Debug.LogWarning($"<b>[SLD Spawn Cancelado]</b> Cooldown de seguridad activo ({tiempoDesdeUltimoActive:F2}s).");
            }
            else
            {
                Debug.Log($"<color=orange><b>[SLD Spawn Aprobado]</b> Cambio detectado. cyl_color: '{cylColorLimpio}' | Sensor activo: '{colorSensor}'. Spawneando pieza...</color>");
                SpawnPieza(colorSensor);
            }
        }
    }

    void SpawnPieza(string color)
    {
        if (piezaBasePrefab == null)
        {
            Debug.LogError("<b>[SLD ERROR]</b> Falta asignar el Prefab (piezaBasePrefab) en el Inspector.");
            return;
        }

        Transform puntoElegido = null;

        switch (color)
        {
            case "WHITE": puntoElegido = spawnPointBlanco; break;
            case "RED": puntoElegido = spawnPointRojo; break;
            case "BLUE": puntoElegido = spawnPointAzul; break;
        }

        if (puntoElegido != null)
        {
            // 1. Instanciamos la pieza en la escena
            GameObject nuevaPieza = Instantiate(piezaBasePrefab);

            // 2. Guardamos la escala real de tu prefab original
            Vector3 escalaPrefabOriginal = piezaBasePrefab.transform.localScale;

            // 3. La hacemos hija de la rampa (pasamos 'false' para resetear posición y rotación local a 0 temporalmente)
            nuevaPieza.transform.SetParent(puntoElegido, false);

            // 4. FORZAMOS POSICIÓN LOCAL CALIBRADA (De tu Imagen 2)
            nuevaPieza.transform.localPosition = new Vector3(-8.7e-05f, 0.000151f, 0f);

            // 5. FORZAMOS ROTACIÓN LOCAL CALIBRADA (De tu Imagen 2)
            nuevaPieza.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            // 6. MATEMÁTICA ANTI-DEFORMACIÓN
            // Corregimos la escala para que el tamaño del padre CAD no aplaste o estire la pieza
            Vector3 escalaPadre = puntoElegido.lossyScale;
            nuevaPieza.transform.localScale = new Vector3(
                escalaPrefabOriginal.x / (escalaPadre.x != 0 ? escalaPadre.x : 1f),
                escalaPrefabOriginal.y / (escalaPadre.y != 0 ? escalaPadre.y : 1f),
                escalaPrefabOriginal.z / (escalaPadre.z != 0 ? escalaPadre.z : 1f)
            );

            piezaSpawned = true; // Bloqueamos el candado para evitar duplicados
            Debug.Log($"<color=green><b>[SLD Spawn ÉXITO]</b> Nueva pieza {color} creada recta en {puntoElegido.name} con escala e inclinación corregidas.</color>");
        }
        else
        {
            Debug.LogError($"<b>[SLD ERROR]</b> No se ha asignado el Spawn Point para el color: {color} en el Inspector.");
        }
    }

    void Update()
    {
        // 1. Mover pistones
        MoverPiston(pistonBlanco, targetBlanco, xReposoBlanco, xEstiradoBlanco);
        MoverPiston(pistonRojo, targetRojo, xReposoEstandar, xEstiradoEstandar);
        MoverPiston(pistonAzul, targetAzul, xReposoEstandar, xEstiradoEstandar);

        // 2. Procesar datos de forma segura en el hilo principal
        if (datosPendientes != null)
        {
            ProcesarDatosMQTTSeguro(datosPendientes);
            datosPendientes = null; // Consumimos los datos
        }
    }

    void MoverPiston(Transform piston, float estadoActual, float reposo, float estirado)
    {
        if (piston == null) return;

        float xObjetivo = Mathf.Lerp(reposo, estirado, estadoActual);
        Vector3 pos = piston.localPosition;
        pos.x = Mathf.MoveTowards(pos.x, xObjetivo, velocidadPiston * Time.deltaTime);
        piston.localPosition = pos;
    }
}