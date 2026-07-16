using UnityEngine;
using System.Collections.Generic;

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
    private float ultimoTiempoActivo = -99f;

    // --- REFERENCIAS INDEPENDIENTES DE PIEZAS ---
    private GameObject piezaBlanca = null;
    private GameObject piezaRoja = null;
    private GameObject piezaAzul = null;

    // --- NUEVO: MEMORIA DE ESTADO DEL SENSOR (Evita borrados antes de activarse) ---
    private bool sensorBlancoFueActivo = false;
    private bool sensorRojoFueActivo = false;
    private bool sensorAzulFueActivo = false;

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
        // --- 1. DETERMINACIÓN DEL COLOR PARA EL MOVIMIENTO DEL PISTÓN ---
        string colorSensor = "";
        if (data.is_white) colorSensor = "WHITE";
        else if (data.is_red) colorSensor = "RED";
        else if (data.is_blue) colorSensor = "BLUE";

        string cylColorLimpio = "";
        if (!string.IsNullOrEmpty(data.cyl_color))
        {
            cylColorLimpio = data.cyl_color.Replace("\"", "").Trim().ToUpper();
        }

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

        // --- 2. CONTROL INDEPENDIENTE POR RAMPA (Con paso de referencia del estado de memoria) ---
        ControlarRampa("WHITE", data.is_white, cylColorLimpio, data.active, ref piezaBlanca, ref sensorBlancoFueActivo);
        ControlarRampa("RED", data.is_red, cylColorLimpio, data.active, ref piezaRoja, ref sensorRojoFueActivo);
        ControlarRampa("BLUE", data.is_blue, cylColorLimpio, data.active, ref piezaAzul, ref sensorAzulFueActivo);
    }

    // --- MÉTODO MODULAR DE CONTROL DE RAMPA ---
    void ControlarRampa(string colorRampa, bool sensorActivo, string cylColorLimpio, bool cilindroActivo, ref GameObject piezaReferencia, ref bool sensorFueActivo)
    {
        Transform puntoElegido = null;
        switch (colorRampa)
        {
            case "WHITE": puntoElegido = spawnPointBlanco; break;
            case "RED": puntoElegido = spawnPointRojo; break;
            case "BLUE": puntoElegido = spawnPointAzul; break;
        }

        if (puntoElegido == null) return;

        if (sensorActivo)
        {
            // 🛡️ Guardamos en la memoria que el sensor se ha activado al menos una vez
            sensorFueActivo = true;

            // Si en el script no tenemos registrada una pieza...
            if (piezaReferencia == null)
            {
                // CAPA 1: Buscamos si YA existe físicamente una pieza huérfana bajo esa rampa
                GameObject piezaExistente = EncontrarPiezaHija(puntoElegido);
                if (piezaExistente != null)
                {
                    piezaReferencia = piezaExistente;
                    Debug.Log($"<b>[SLD Rampa]</b> Se enlazó una pieza física ya existente en {puntoElegido.name}. Evitando duplicados.");
                }
                else
                {
                    // Si realmente está vacía, hacemos el spawn de seguridad normal
                    bool coincideConCylColor = (cylColorLimpio == colorRampa);
                    float tiempoDesdeUltimoActive = Time.time - ultimoTiempoActivo;
                    bool activoRecientemente = tiempoDesdeUltimoActive < cooldownSpawn;

                    if (coincideConCylColor)
                    {
                        Debug.Log($"<b>[SLD Spawn Omitido]</b> El sensor {colorRampa} coincide con cyl_color ({cylColorLimpio}).");
                    }
                    else if (cilindroActivo)
                    {
                        Debug.LogWarning($"<b>[SLD Spawn Cancelado]</b> El cilindro está empujando para {colorRampa}.");
                    }
                    else if (activoRecientemente)
                    {
                        Debug.LogWarning($"<b>[SLD Spawn Cancelado]</b> Cooldown activo para {colorRampa} ({tiempoDesdeUltimoActive:F2}s).");
                    }
                    else
                    {
                        Debug.Log($"<color=orange><b>[SLD Spawn Aprobado]</b> Cambio detectado en {colorRampa}. Spawneando...</color>");
                        piezaReferencia = SpawnPieza(colorRampa, puntoElegido);
                    }
                }
            }
        }
        else
        {
            // 🛡️ CANDADO CLAVE: El sensor está a false. Solo actuamos si el sensor estuvo a TRUE previamente.
            if (sensorFueActivo)
            {
                // Limpieza de la pieza registrada
                if (piezaReferencia != null)
                {
                    // Si el objeto ya no tiene de padre la rampa, es porque el VGR se la ha llevado
                    if (piezaReferencia.transform.parent != puntoElegido)
                    {
                        Debug.Log($"<b>[SLD Clear]</b> El sensor {colorRampa} se apagó, pero la pieza fue trasladada (nuevo padre: {piezaReferencia.transform.parent.name}). Manteniendo pieza.");
                        piezaReferencia = null;
                    }
                    else
                    {
                        // Si sigue en la rampa, la destruimos
                        Debug.Log($"<color=red><b>[SLD Clear]</b> El sensor {colorRampa} se ha apagado. Destruyendo pieza registrada.</color>");
                        Destroy(piezaReferencia);
                        piezaReferencia = null;
                    }
                }

                // CAPA 2: BARRENDERO DE SEGURIDAD (Solo se ejecuta si el sensor estuvo activo primero)
                List<GameObject> piezasHuerfanas = new List<GameObject>();
                foreach (Transform hijo in puntoElegido)
                {
                    string nombreHijo = hijo.name.ToLower();
                    if (nombreHijo.Contains("pieza") || nombreHijo.Contains("workpiece") || hijo.CompareTag("Pieza") || nombreHijo.Contains("clone"))
                    {
                        piezasHuerfanas.Add(hijo.gameObject);
                    }
                }

                if (piezasHuerfanas.Count > 0)
                {
                    Debug.Log($"<color=red><b>[SLD Sweep]</b> Se encontraron {piezasHuerfanas.Count} pieza(s) huérfana(s) en {puntoElegido.name} tras apagarse el sensor. Eliminando...</color>");
                    foreach (GameObject go in piezasHuerfanas)
                    {
                        Destroy(go);
                    }
                }

                // Reseteamos el estado de memoria de activación
                sensorFueActivo = false;
            }
        }
    }

    // Método auxiliar para detectar si una pieza física ya existe bajo un punto de spawn
    GameObject EncontrarPiezaHija(Transform punto)
    {
        if (punto == null) return null;
        foreach (Transform hijo in punto)
        {
            string nombreHijo = hijo.name.ToLower();
            if (nombreHijo.Contains("pieza") || nombreHijo.Contains("workpiece") || hijo.CompareTag("Pieza") || nombreHijo.Contains("clone"))
            {
                return hijo.gameObject;
            }
        }
        return null;
    }

    GameObject SpawnPieza(string color, Transform puntoElegido)
    {
        if (piezaBasePrefab == null)
        {
            Debug.LogError("<b>[SLD ERROR]</b> Falta asignar el Prefab (piezaBasePrefab) en el Inspector.");
            return null;
        }

        GameObject nuevaPieza = Instantiate(piezaBasePrefab);
        Vector3 escalaPrefabOriginal = piezaBasePrefab.transform.localScale;

        nuevaPieza.transform.SetParent(puntoElegido, false);

        // Posición y rotación calibradas (Imagen 2)
        nuevaPieza.transform.localPosition = new Vector3(-8.7e-05f, 0.000151f, 0f);
        nuevaPieza.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        // Matemática anti-deformación CAD
        Vector3 escalaPadre = puntoElegido.lossyScale;
        nuevaPieza.transform.localScale = new Vector3(
            escalaPrefabOriginal.x / (escalaPadre.x != 0 ? escalaPadre.x : 1f),
            escalaPrefabOriginal.y / (escalaPadre.y != 0 ? escalaPadre.y : 1f),
            escalaPrefabOriginal.z / (escalaPadre.z != 0 ? escalaPadre.z : 1f)
        );

        Debug.Log($"<color=green><b>[SLD Spawn ÉXITO]</b> Nueva pieza {color} creada en {puntoElegido.name} con escala corregida y color nativo.</color>");
        return nuevaPieza;
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