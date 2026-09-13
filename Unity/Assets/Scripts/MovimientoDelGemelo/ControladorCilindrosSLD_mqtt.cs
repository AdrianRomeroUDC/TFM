using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controla el gemelo digital de los tres cilindros neumáticos (pistones) de la estación SLD
/// (la cinta clasificadora). Cuando una pieza pasa por el sensor de color de la SLD, la fábrica
/// real empuja un pistón (blanco, rojo o azul) para desviar la pieza hacia su rampa de salida
/// correspondiente. Este script escucha por MQTT esos mismos eventos y hace dos cosas a la vez:
/// 1) mueve visualmente el pistón 3D correcto en Unity, y 2) crea o destruye piezas 3D en las
/// rampas de salida según lo que digan los sensores reales, para que la escena de Unity refleje
/// siempre lo que está pasando en la máquina física.
///
/// Este script es muy importante porque <see cref="ControladorVGR_mqtt"/> (el robot que recoge
/// las piezas de las rampas) LEE desde fuera sus propiedades públicas <see cref="IsWhiteSensorActivo"/>,
/// <see cref="IsRedSensorActivo"/> e <see cref="IsBlueSensorActivo"/>, así como los puntos
/// <see cref="spawnPointBlanco"/>, <see cref="spawnPointRojo"/> y <see cref="spawnPointAzul"/>,
/// para saber si el brazo VGR ha agarrado bien una pieza o si el agarre ha fallado en la máquina real.
/// </summary>
public class ControladorCilindrosSLD_mqtt : MonoBehaviour
{
    [Header("Referencias de Pistones")]
    // Los 3 objetos 3D de los pistones que empujan la pieza hacia cada rampa de color.
    public Transform pistonBlanco;
    public Transform pistonRojo;
    public Transform pistonAzul;

    [Header("Configuración de Spawning")]
    [Tooltip("Arrastra aquí tu Prefab de la pieza base (piezaBase)")]
    public GameObject piezaBasePrefab; // Prefab genérico de pieza que se instancia cuando hace falta "crear" una pieza nueva en una rampa.

    [Tooltip("Punto de aparición para la pieza blanca")]
    // Posición 3D de la rampa de salida blanca. El VGR consulta este mismo Transform para
    // saber dónde debe ir a buscar una pieza blanca y para comprobar si el agarre fue correcto.
    public Transform spawnPointBlanco;
    [Tooltip("Punto de aparición para la pieza roja")]
    // Posición 3D de la rampa de salida roja. Usado también desde fuera por el VGR.
    public Transform spawnPointRojo;
    [Tooltip("Punto de aparición para la pieza azul")]
    // Posición 3D de la rampa de salida azul. Usado también desde fuera por el VGR.
    public Transform spawnPointAzul;

    // 🌐 ESTADOS PÚBLICOS DE LOS SENSORES PARA LECTURA DEL VGR
    /// <summary>
    /// Indica si el sensor de color real de la rampa blanca está detectando una pieza ahora mismo.
    /// El VGR consulta esta propiedad después de intentar agarrar una pieza de la rampa blanca:
    /// si sigue estando en <c>true</c> significa que la pieza física no se movió con la ventosa
    /// (el agarre falló) y el gemelo digital debe deshacer el agarre.
    /// </summary>
    public bool IsWhiteSensorActivo { get; private set; } = false;
    /// <summary>
    /// Igual que <see cref="IsWhiteSensorActivo"/> pero para el sensor de color de la rampa roja.
    /// </summary>
    public bool IsRedSensorActivo { get; private set; } = false;
    /// <summary>
    /// Igual que <see cref="IsWhiteSensorActivo"/> pero para el sensor de color de la rampa azul.
    /// </summary>
    public bool IsBlueSensorActivo { get; private set; } = false;

    // Coordenadas Estándar (Rojo y Azul)
    // Posición local en X del pistón cuando está recogido (reposo) y cuando está totalmente
    // estirado (empujando la pieza). Son valores calibrados a mano para que el pistón 3D
    // coincida con el recorrido real del pistón neumático físico.
    private float xReposoEstandar = 0.001122198f;
    private float xEstiradoEstandar = 0.000826f;

    // Coordenadas Especiales (Blanco)
    // El pistón blanco tiene un recorrido distinto en el modelo 3D, así que necesita sus propios
    // valores de reposo y estirado.
    private float xReposoBlanco = 0.0002811983f;
    private float xEstiradoBlanco = -0.0000149997f;

    [Header("Ajustes")]
    public float velocidadPiston = 0.01f; // Velocidad a la que se mueve visualmente cada pistón hacia su posición objetivo.
    [Tooltip("Tiempo mínimo en segundos entre spawns para evitar rebotes de señal.")]
    public float cooldownSpawn = 1.5f; // Tiempo de espera mínimo entre dos apariciones de pieza, para no duplicar piezas por señales MQTT con "rebotes".

    // Posiciones objetivo (0 = reposo, 1 = estirado) hacia las que se mueve cada pistón en Update().
    private float targetBlanco, targetRojo, targetAzul;

    // Control de estado y seguridad
    private float ultimoTiempoActivo = -99f; // Marca de tiempo (Time.time) de la última vez que CUALQUIER pistón se activó; se usa para el cooldown de spawn.

    // --- REFERENCIAS INDEPENDIENTES DE PIEZAS ---
    // Guardamos, por separado para cada rampa, qué pieza 3D concreta está actualmente colocada en ella.
    private GameObject piezaBlanca = null;
    private GameObject piezaRoja = null;
    private GameObject piezaAzul = null;

    // --- MEMORIA DE ESTADO DEL SENSOR (Evita borrados antes de activarse) ---
    // Recuerda si el sensor de cada rampa ha llegado a activarse alguna vez desde el último
    // "vaciado", para poder distinguir entre "nunca hubo pieza" y "la pieza se acaba de ir".
    private bool sensorBlancoFueActivo = false;
    private bool sensorRojoFueActivo = false;
    private bool sensorAzulFueActivo = false;

    // --- PUENTE SEGURO PARA EVITAR CAÍDAS DE HILOS ---
    // Los mensajes MQTT llegan en un hilo distinto al hilo principal de Unity, así que aquí solo
    // guardamos el último dato recibido; el trabajo real de aplicarlo se hace en Update().
    private JSON_SLDCylinder datosPendientes = null;

    void Start()
    {
        Debug.Log("<b>[SLD System]</b> Iniciando script y buscando cliente MQTT...");
        // Reintenta suscribirse cada segundo hasta que el cliente MQTT central exista en la escena.
        InvokeRepeating("IntentarSuscripcion", 0f, 1f);
    }

    void IntentarSuscripcion()
    {
        if (MQTTClient.Instance != null)
        {
            // Nos suscribimos al evento de cilindros: cada vez que la fábrica real mande un mensaje
            // sobre los pistones de la SLD, se llamará a ProcesarComandoCilindro.
            MQTTClient.Instance.OnCylinderUpdateEvent += ProcesarComandoCilindro;
            Debug.Log("<color=green><b>Cilindros SLD:</b> Conectado con éxito al sistema central.</color>");
            CancelInvoke("IntentarSuscripcion"); // Ya nos hemos suscrito, dejamos de reintentar.
        }
    }

    void OnDisable()
    {
        // Nos damos de baja del evento al desactivar el objeto, para no dejar una suscripción
        // "fantasma" intentando llamar a un método de un objeto ya inactivo.
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnCylinderUpdateEvent -= ProcesarComandoCilindro;
    }

    // Se ejecuta en el momento en que llega el mensaje MQTT (posiblemente en otro hilo):
    // solo guarda el dato para procesarlo de forma segura más tarde, en Update().
    void ProcesarComandoCilindro(JSON_SLDCylinder data)
    {
        if (data == null) return;
        datosPendientes = data;
    }

    /// <summary>
    /// Traduce el mensaje MQTT real de los cilindros de la SLD (qué sensores de color están
    /// activos y qué pistón está empujando) al estado del gemelo digital: actualiza las
    /// propiedades públicas que lee el VGR, decide hacia qué lado se mueve cada pistón 3D, y
    /// llama a <see cref="ControlarRampa"/> para crear o destruir piezas en cada rampa de salida.
    /// </summary>
    /// <param name="data">Datos recibidos por MQTT: qué sensores de color están activos, qué
    /// color tiene el cilindro que se está moviendo (<c>cyl_color</c>) y si está activo (empujando).</param>
    void ProcesarDatosMQTTSeguro(JSON_SLDCylinder data)
    {
        // 1. Guardar lectura pública de sensores de rampa para el VGR
        IsWhiteSensorActivo = data.is_white;
        IsRedSensorActivo = data.is_red;
        IsBlueSensorActivo = data.is_blue;

        // --- DETERMINACIÓN DEL COLOR PARA EL MOVIMIENTO DEL PISTÓN ---
        // Primero miramos qué sensor de color está activo (esto nos dice qué pieza hay delante).
        string colorSensor = "";
        if (data.is_white) colorSensor = "WHITE";
        else if (data.is_red) colorSensor = "RED";
        else if (data.is_blue) colorSensor = "BLUE";

        // Limpiamos el texto que indica qué cilindro está actuando según el PLC real.
        string cylColorLimpio = "";
        if (!string.IsNullOrEmpty(data.cyl_color))
        {
            cylColorLimpio = data.cyl_color.Replace("\"", "").Trim().ToUpper();
        }

        // El color "definitivo" prioriza el dato del cilindro (cyl_color); si no viene, usamos
        // el color detectado por el sensor; y si tampoco hay nada, por defecto blanco.
        string colorFinal = "WHITE";
        if (!string.IsNullOrEmpty(cylColorLimpio)) colorFinal = cylColorLimpio;
        else if (!string.IsNullOrEmpty(colorSensor)) colorFinal = colorSensor;

        // Controlar el movimiento físico de los pistones
        // valor = 1 significa "pistón estirado/empujando", 0 significa "pistón recogido".
        float valor = data.active ? 1f : 0f;
        if (colorFinal == "WHITE") targetBlanco = valor;
        else if (colorFinal == "RED") targetRojo = valor;
        else if (colorFinal == "BLUE") targetAzul = valor;

        if (data.active)
        {
            // Recordamos cuándo fue la última vez que algún pistón empujó, para el cooldown de spawn.
            ultimoTiempoActivo = Time.time;
        }

        // --- CONTROL INDEPENDIENTE POR RAMPA ---
        // Cada rampa (blanca, roja, azul) gestiona su propia pieza 3D de forma independiente,
        // según lo que diga su sensor de color correspondiente.
        ControlarRampa("WHITE", data.is_white, cylColorLimpio, data.active, ref piezaBlanca, ref sensorBlancoFueActivo);
        ControlarRampa("RED", data.is_red, cylColorLimpio, data.active, ref piezaRoja, ref sensorRojoFueActivo);
        ControlarRampa("BLUE", data.is_blue, cylColorLimpio, data.active, ref piezaAzul, ref sensorAzulFueActivo);
    }

    /// <summary>
    /// Gestiona la aparición y desaparición de la pieza 3D de UNA rampa concreta de la SLD,
    /// comparando el estado actual del sensor de color con lo que había antes. Si el sensor se
    /// enciende y no hay pieza todavía, crea una nueva (salvo que el propio cilindro esté
    /// empujando en ese instante, o estemos en pleno cooldown, casos en los que evitamos duplicar).
    /// Si el sensor se apaga, destruye la pieza (o la libera si ya fue movida a otro sitio, por
    /// ejemplo por el VGR).
    /// </summary>
    /// <param name="colorRampa">Color de la rampa que estamos gestionando ("WHITE", "RED" o "BLUE").</param>
    /// <param name="sensorActivo">Si el sensor de color real de esa rampa está detectando una pieza ahora mismo.</param>
    /// <param name="cylColorLimpio">Color del cilindro que está actuando según el último mensaje MQTT.</param>
    /// <param name="cilindroActivo">Si el cilindro (pistón) está empujando en este instante.</param>
    /// <param name="piezaReferencia">Referencia (por <c>ref</c>) a la pieza 3D actualmente asociada a esta rampa.</param>
    /// <param name="sensorFueActivo">Referencia (por <c>ref</c>) que recuerda si el sensor ya estuvo activo antes, para detectar cuándo se apaga.</param>
    void ControlarRampa(string colorRampa, bool sensorActivo, string cylColorLimpio, bool cilindroActivo, ref GameObject piezaReferencia, ref bool sensorFueActivo)
    {
        // Elegimos el punto de aparición (spawn point) según el color de rampa.
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
            sensorFueActivo = true;

            if (piezaReferencia == null)
            {
                // Puede que ya exista una pieza física colocada ahí (por ejemplo, dejada por una
                // ejecución anterior), así que primero comprobamos si hay alguna hija del punto de spawn.
                GameObject piezaExistente = EncontrarPiezaHija(puntoElegido);
                if (piezaExistente != null)
                {
                    piezaReferencia = piezaExistente;
                    Debug.Log($"<b>[SLD Rampa]</b> Se enlazó una pieza física ya existente en {puntoElegido.name}. Evitando duplicados.");
                }
                else
                {
                    // Antes de crear una pieza nueva, comprobamos varias condiciones para evitar
                    // duplicados o "falsos positivos" de la señal MQTT:
                    bool coincideConCylColor = (cylColorLimpio == colorRampa);
                    float tiempoDesdeUltimoActive = Time.time - ultimoTiempoActivo;
                    bool activoRecientemente = tiempoDesdeUltimoActive < cooldownSpawn;

                    if (coincideConCylColor)
                    {
                        // El cilindro de este mismo color está actuando: probablemente la pieza
                        // ya viene siendo empujada desde la cinta, así que no generamos una nueva aquí.
                        Debug.Log($"<b>[SLD Spawn Omitido]</b> El sensor {colorRampa} coincide con cyl_color ({cylColorLimpio}).");
                    }
                    else if (cilindroActivo)
                    {
                        // El pistón está empujando en este instante: mejor esperar a que termine antes de crear la pieza.
                        Debug.LogWarning($"<b>[SLD Spawn Cancelado]</b> El cilindro está empujando para {colorRampa}.");
                    }
                    else if (activoRecientemente)
                    {
                        // Ha pasado muy poco tiempo desde la última activación: evitamos duplicar por rebote de señal.
                        Debug.LogWarning($"<b>[SLD Spawn Cancelado]</b> Cooldown activo para {colorRampa} ({tiempoDesdeUltimoActive:F2}s).");
                    }
                    else
                    {
                        // Todas las comprobaciones pasan: creamos una pieza nueva de verdad en esta rampa.
                        Debug.Log($"<color=orange><b>[SLD Spawn Aprobado]</b> Cambio detectado en {colorRampa}. Spawneando...</color>");
                        piezaReferencia = SpawnPieza(colorRampa, puntoElegido);
                    }
                }
            }
        }
        else
        {
            // El sensor de esta rampa está apagado: si antes estuvo encendido, toca "limpiar".
            if (sensorFueActivo)
            {
                if (piezaReferencia != null)
                {
                    if (piezaReferencia.transform.parent != puntoElegido)
                    {
                        // La pieza ya no está en el punto de spawn (por ejemplo, el VGR la agarró y
                        // la movió a otro sitio): la dejamos existir, solo soltamos la referencia.
                        string nombrePadre = (piezaReferencia.transform.parent != null) ? piezaReferencia.transform.parent.name : "Ninguno (Raíz de la escena)";
                        Debug.Log($"<b>[SLD Clear]</b> El sensor {colorRampa} se apagó, pero la pieza fue trasladada (nuevo padre: {nombrePadre}). Manteniendo pieza.");
                        piezaReferencia = null;
                    }
                    else
                    {
                        // La pieza sigue en el punto de spawn pero el sensor dice que ya no hay
                        // nada: la destruimos, porque el gemelo digital debe reflejar la realidad.
                        Debug.Log($"<color=red><b>[SLD Clear]</b> El sensor {colorRampa} se ha apagado. Destruyendo pieza registrada.</color>");
                        Destroy(piezaReferencia);
                        piezaReferencia = null;
                    }
                }

                // Barrido de seguridad: por si quedó alguna pieza "huérfana" (sin referencia guardada)
                // colgando del punto de spawn, la buscamos y la eliminamos también.
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

                sensorFueActivo = false;
            }
        }
    }

    // Busca entre los hijos de un punto de spawn si ya hay una pieza colocada ahí (por su nombre o etiqueta).
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

    /// <summary>
    /// Crea (instancia) una pieza 3D nueva a partir del prefab base y la coloca correctamente
    /// encajada dentro del punto de spawn de la rampa indicada, ajustando su escala para que se
    /// vea del tamaño correcto sin importar la escala del objeto padre.
    /// </summary>
    /// <param name="color">Color de la rampa donde aparece la pieza (solo para el mensaje de log).</param>
    /// <param name="puntoElegido">Transform de la rampa donde debe aparecer la nueva pieza.</param>
    /// <returns>El GameObject de la pieza recién creada, o <c>null</c> si falta el prefab configurado.</returns>
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

        nuevaPieza.transform.localPosition = new Vector3(-8.7e-05f, 0.000151f, 0f);
        nuevaPieza.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        // Corregimos la escala para compensar la escala del padre (el punto de spawn), de forma
        // que la pieza siempre se vea con su tamaño real, sin deformarse.
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
        // Cada frame, movemos suavemente los 3 pistones hacia su posición objetivo (reposo o estirado).
        MoverPiston(pistonBlanco, targetBlanco, xReposoBlanco, xEstiradoBlanco);
        MoverPiston(pistonRojo, targetRojo, xReposoEstandar, xEstiradoEstandar);
        MoverPiston(pistonAzul, targetAzul, xReposoEstandar, xEstiradoEstandar);

        // Si llegó un mensaje MQTT nuevo desde el último frame, lo procesamos ahora de forma segura.
        if (datosPendientes != null)
        {
            ProcesarDatosMQTTSeguro(datosPendientes);
            datosPendientes = null;
        }
    }

    // Mueve el pistón indicado hacia su posición de reposo o estirado, según el valor 0-1 de estadoActual,
    // a velocidad constante (velocidadPiston), para que el movimiento se vea suave y no "de golpe".
    void MoverPiston(Transform piston, float estadoActual, float reposo, float estirado)
    {
        if (piston == null) return;

        float xObjetivo = Mathf.Lerp(reposo, estirado, estadoActual);
        Vector3 pos = piston.localPosition;
        pos.x = Mathf.MoveTowards(pos.x, xObjetivo, velocidadPiston * Time.deltaTime);
        piston.localPosition = pos;
    }
}
