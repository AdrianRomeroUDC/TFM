using UnityEngine;
using System.Collections;

/// <summary>
/// Controla el carro (transelevador) del almacén HBW: la pieza mecánica que se mueve por dentro de
/// la estantería 3x3 para guardar o sacar cajones. Tiene 3 movimientos independientes -horizontal
/// (columna A/B/C), vertical (fila 1/2/3) y extensión (el brazo que se estira para meter/sacar el
/// cajón del hueco)- y este script se suscribe al evento de <see cref="MQTTClient"/> que informa de
/// esos 3 valores reales para mover el modelo 3D exactamente igual que la máquina física, además de
/// gestionar cuándo el carro "coge" o "suelta" un cajón mientras viaja.
/// </summary>
public class ControladorHBWposition_mqtt : MonoBehaviour
{
    // Valor "anterior" y "nuevo" (en unidades del PLC) de los ejes Horizontal y Vertical, usados
    // para interpolar el movimiento en el tiempo real transcurrido entre dos mensajes MQTT
    // consecutivos, en vez de perseguir el objetivo con una velocidad de suavizado fija.
    private float prevH, targetH, prevV, targetV;
    private float tInicioInterpolacion = -1f; // Instante (Time.time) del último mensaje de posición; -1 = aún no ha llegado ninguno.
    private float duracionInterpolacion = 0.1f; // Tiempo real que debe durar la interpolación hasta el próximo mensaje; se recalcula con cada mensaje nuevo.

    // Último valor recibido por MQTT para el eje de Extensión (en unidades del PLC real): es una
    // orden discreta (-512/0/512), no una posición continua, así que no se interpola como H/V.
    private float lastE;

    // Aviso de que ha llegado una orden nueva de estirar/recoger el brazo, para procesarla en el siguiente Update().
    private bool hayNuevaOrdenEstirar = false;

    [Header("Referencias de los Ejes")]
    public Transform ejeHorizontal;
    public Transform ejeVertical;
    public Transform ejeExtension;

    [Header("Calibración PLC")]
    public float plcH_Min = 0;
    public float plcH_Max = 1985;
    public float plcV_Min = 0;
    public float plcV_Max = 845;

    [Header("Calibración Unity (Click Derecho -> Capturar)")]
    [ContextMenuItem("Capturar", "CapturarHMin")] public float unityH_Min;
    [ContextMenuItem("Capturar", "CapturarHMax")] public float unityH_Max;
    [ContextMenuItem("Capturar", "CapturarVMin")] public float unityV_Min;
    [ContextMenuItem("Capturar", "CapturarVMax")] public float unityV_Max;
    [ContextMenuItem("Capturar", "CapturarExtEst")] public float unityE_Estirado;
    [ContextMenuItem("Capturar", "CapturarExtRec")] public float unityE_Recogido;

    [Header("Ajustes de Animación")]
    [Tooltip("Recorte mínimo y máximo (en segundos) para la duración de cada interpolación de H/V, por si un mensaje tarda demasiado o llega duplicado al instante.")]
    public float duracionInterpolacionMin = 0.02f;
    public float duracionInterpolacionMax = 0.6f;
    public float tiempoAnimacion = 4f;   // Segundos que tarda el brazo en estirarse o recogerse del todo.

    [Header("Estado del Agarre")]
    public Transform objetoCogido = null;      // Cajón que el carro lleva agarrado ahora mismo mientras se desplaza (si lleva alguno).
    public bool esOperacionDeEntrega = false;
    private Transform padreOriginalEstante = null;   // Guarda dónde estaba colocado el cajón en la estantería, por si hay que devolverlo.
    private Coroutine corrutinaExtension;          // Referencia a la animación del brazo en marcha, para poder cancelarla si llega una orden nueva.

    // --- BOTONES DE CAPTURA PARA EL INSPECTOR (sirven para calibrar a mano los límites de cada eje) ---
    void CapturarHMin() => unityH_Min = ejeHorizontal.localPosition.z;
    void CapturarHMax() => unityH_Max = ejeHorizontal.localPosition.z;
    void CapturarVMin() => unityV_Min = ejeVertical.localPosition.y;
    void CapturarVMax() => unityV_Max = ejeVertical.localPosition.y;
    void CapturarExtEst() => unityE_Estirado = ejeExtension.localPosition.x;
    void CapturarExtRec() => unityE_Recogido = ejeExtension.localPosition.x;

    void Start() { StartCoroutine(SuscripcionSegura()); }

    // Espera a que el cliente MQTT ya exista antes de suscribirse, para no engancharse a un evento que todavía no está disponible.
    IEnumerator SuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;

        // A partir de aquí, cada vez que el almacén real reporte una nueva posición del carro, se llama a ActualizarPosicionDesdeMQTT.
        MQTTClient.Instance.OnHBWPositionUpdateEvent += ActualizarPosicionDesdeMQTT;
        Debug.Log("<color=green>HBW suscrito correctamente</color>");
    }

    // Guarda la nueva posición real que acaba de reportar el carro del almacén y prepara la
    // interpolación de los ejes Horizontal/Vertical hacia ella; el movimiento real ocurre después,
    // en Update(), repartido sobre el tiempo real que tarde en llegar el próximo mensaje.
    private void ActualizarPosicionDesdeMQTT(float hor, float vert, float ext)
    {
        // Antes de sustituir los valores objetivo, guardamos como "punto de partida" el valor que
        // cada eje tiene ahora mismo (ya interpolado), no el antiguo objetivo en bruto, para que el
        // siguiente tramo de interpolación arranque sin ningún salto visual.
        float fracActual = (tInicioInterpolacion >= 0f && duracionInterpolacion > 0f)
            ? Mathf.Clamp01((Time.time - tInicioInterpolacion) / duracionInterpolacion)
            : 1f;
        prevH = Mathf.Lerp(prevH, targetH, fracActual);
        prevV = Mathf.Lerp(prevV, targetV, fracActual);

        targetH = hor;   // Nueva posición objetivo del eje Horizontal (columna A/B/C).
        targetV = vert;  // Nueva posición objetivo del eje Vertical (fila 1/2/3).

        // Medimos cuánto ha tardado en llegar este mensaje desde el anterior (normalmente ~100ms) y
        // usamos ese mismo intervalo real para repartir la interpolación del próximo tramo, recortado
        // a un rango razonable por si hay un corte de red o un mensaje duplicado instantáneo.
        float ahora = Time.time;
        if (tInicioInterpolacion >= 0f)
        {
            duracionInterpolacion = Mathf.Clamp(ahora - tInicioInterpolacion, duracionInterpolacionMin, duracionInterpolacionMax);
        }
        tInicioInterpolacion = ahora;

        // El brazo extractor real no manda una posición continua, sino dos órdenes discretas:
        // -512 significa "estirar el brazo" y 512 significa "recoger el brazo". Solo reaccionamos
        // cuando llega una de esas dos órdenes y es distinta de la que ya teníamos guardada.
        if (ext != lastE && (ext == -512 || ext == 512))
        {
            lastE = ext;
            hayNuevaOrdenEstirar = true; // Marcamos que hay que animar el brazo en el próximo Update().
        }
        else if (ext == 0) lastE = 0;
    }

    void Update()
    {
        // Si ha llegado una orden nueva de estirar/recoger el brazo desde la fábrica real, la procesamos ahora.
        if (hayNuevaOrdenEstirar)
        {
            hayNuevaOrdenEstirar = false;

            // Si la orden es "ESTIRAR": cuando el carro ya llevaba un cajón agarrado, significa que lo
            // está DEJANDO en el hueco (entrega); si no llevaba nada, significa que está a punto de COGER uno.
            if (lastE == -512)
            {
                esOperacionDeEntrega = (objetoCogido != null);
            }

            IniciarAnimacionExtension(lastE == -512 ? unityE_Estirado : unityE_Recogido);
        }

        // En qué punto de la interpolación estamos entre el mensaje anterior y el más reciente,
        // repartido sobre el tiempo real que tardó en llegar el mensaje nuevo (ver ActualizarPosicionDesdeMQTT).
        float frac = (tInicioInterpolacion >= 0f && duracionInterpolacion > 0f)
            ? Mathf.Clamp01((Time.time - tInicioInterpolacion) / duracionInterpolacion)
            : 1f;

        // --- MOVIMIENTO DEL EJE HORIZONTAL (columna A/B/C del almacén) ---
        if (ejeHorizontal)
        {
            float tPrevH = Mathf.InverseLerp(plcH_Min, plcH_Max, prevH); // Convertimos la posición anterior del PLC a un valor entre 0 y 1.
            float tTargetH = Mathf.InverseLerp(plcH_Min, plcH_Max, targetH); // Lo mismo para la posición nueva.
            float zPrev = Mathf.Lerp(unityH_Min, unityH_Max, tPrevH);  // Traducimos ambos 0-1 a la posición equivalente en el modelo 3D.
            float zTarget = Mathf.Lerp(unityH_Min, unityH_Max, tTargetH);
            float zInterpolado = Mathf.Lerp(zPrev, zTarget, frac);
            Vector3 p = ejeHorizontal.localPosition;
            p.z = zInterpolado; // Desplazamos el carro hacia esa posición interpolada en el eje Z.
            ejeHorizontal.localPosition = p;
        }

        // --- MOVIMIENTO DEL EJE VERTICAL (fila 1/2/3 del almacén) ---
        if (ejeVertical)
        {
            float tPrevV = Mathf.InverseLerp(plcV_Min, plcV_Max, prevV); // Convertimos la posición anterior del PLC a un valor entre 0 y 1.
            float tTargetV = Mathf.InverseLerp(plcV_Min, plcV_Max, targetV); // Lo mismo para la posición nueva.
            float yPrev = Mathf.Lerp(unityV_Min, unityV_Max, tPrevV);  // Traducimos ambos 0-1 a la posición equivalente en el modelo 3D.
            float yTarget = Mathf.Lerp(unityV_Min, unityV_Max, tTargetV);
            float yInterpolado = Mathf.Lerp(yPrev, yTarget, frac);
            Vector3 p = ejeVertical.localPosition;
            p.y = yInterpolado; // Desplazamos el carro hacia esa posición interpolada en el eje Y.
            ejeVertical.localPosition = p;

            // Bloque original comentado para prevenir caídas accidentales basándose puramente en altura:
            // if (objetoEnganchado != null && lastV < plcV_Min + 5) SoltarCajon();
        }
    }

    // --- MÉTODOS DE AGARRE Y ANIMACIÓN DEL BRAZO ---

    /// <summary>
    /// Se llama cuando el carro real acaba de coger un cajón de la estantería: hace que el cajón 3D
    /// pase a moverse junto con la plataforma del carro (como si estuviera agarrado de verdad) y
    /// apaga su física para que no se caiga durante el viaje.
    /// </summary>
    public void ProcesarCaptura(Transform contenedor, Transform plataforma)
    {
        // Guardamos qué cajón lleva el carro agarrado ahora mismo.
        objetoCogido = contenedor;

        // El cajón pasa a depender del carro (su plataforma), para que se mueva pegado a él por la estantería.
        contenedor.SetParent(plataforma);

        // Apagamos la física normal del cajón y anulamos cualquier velocidad previa, para que viaje sin temblores ni caídas.
        if (contenedor.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    // El propio cajón (su proxy) avisa aquí al carro cuando ya ha quedado bien colocado en su hueco,
    // para que el carro sepa que puede retirarse sin llevárselo.
    public void NotificarCajonLiberado()
    {
        objetoCogido = null; // El carro deja de llevar ningún cajón agarrado.
        Debug.Log("<color=yellow><b>[Controlador HBW]:</b> El transelevador registra que ya no lleva ningún cajón y se retirará solo.</color>");
    }

    // Método de emergencia para forzar la suelta del cajón, devolviéndole su física normal (por si algo falla en el proceso habitual).
    private void SoltarCajon()
    {
        if (objetoCogido != null)
        {
            objetoCogido.SetParent(padreOriginalEstante, true);
            if (objetoCogido.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
            objetoCogido = null;
        }
    }

    // Arranca la animación de estirar/recoger el brazo, cancelando primero cualquier animación anterior que siguiera en marcha.
    void IniciarAnimacionExtension(float d)
    {
        if (corrutinaExtension != null) StopCoroutine(corrutinaExtension);
        corrutinaExtension = StartCoroutine(AnimarBrazo(d));
    }

    // Corrutina que mueve el brazo extractor poco a poco, frame a frame, imitando el movimiento del pistón telescópico real.
    IEnumerator AnimarBrazo(float d)
    {
        float t = 0;
        float inicioX = ejeExtension.localPosition.x;
        while (t < tiempoAnimacion)
        {
            t += Time.deltaTime;
            float progreso = t / tiempoAnimacion;
            // Usamos SmoothStep para que el brazo acelere al empezar y frene al llegar, en vez de moverse a velocidad constante.
            float vX = Mathf.Lerp(inicioX, d, Mathf.SmoothStep(0, 1, progreso));
            ejeExtension.localPosition = new Vector3(vX, ejeExtension.localPosition.y, ejeExtension.localPosition.z);
            yield return null;
        }
        // Al terminar la animación, fijamos la posición final exacta para que no quede ningún desajuste por redondeo.
        ejeExtension.localPosition = new Vector3(d, ejeExtension.localPosition.y, ejeExtension.localPosition.z);
    }
}
