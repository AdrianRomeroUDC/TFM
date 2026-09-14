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
    // Últimos valores recibidos por MQTT para cada eje: Horizontal, Vertical y Extensión (en unidades del PLC real).
    private float lastH, lastV, lastE;

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
    public float lerpSpeed = 5f;        // Velocidad de suavizado del movimiento de los carros de cada eje.
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

    // Guarda la posición real que acaba de reportar el carro del almacén; el movimiento suave del
    // modelo 3D hacia esa posición se hace después, en Update().
    private void ActualizarPosicionDesdeMQTT(float hor, float vert, float ext)
    {
        lastH = hor;   // Nueva posición objetivo del eje Horizontal (columna A/B/C).
        lastV = vert;  // Nueva posición objetivo del eje Vertical (fila 1/2/3).

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

        float dt = Time.deltaTime;

        // --- MOVIMIENTO SUAVE DEL EJE HORIZONTAL (columna A/B/C del almacén) ---
        if (ejeHorizontal)
        {
            float tH = Mathf.InverseLerp(plcH_Min, plcH_Max, lastH); // Convertimos la posición real del PLC a un valor entre 0 y 1.
            float targetZ = Mathf.Lerp(unityH_Min, unityH_Max, tH);  // Traducimos ese 0-1 a la posición equivalente en el modelo 3D.
            Vector3 p = ejeHorizontal.localPosition;
            p.z = Mathf.Lerp(p.z, targetZ, lerpSpeed * dt);          // Desplazamos el carro suavemente hacia esa posición en el eje Z.
            ejeHorizontal.localPosition = p;
        }

        // --- MOVIMIENTO SUAVE DEL EJE VERTICAL (fila 1/2/3 del almacén) ---
        if (ejeVertical)
        {
            float tV = Mathf.InverseLerp(plcV_Min, plcV_Max, lastV); // Convertimos la posición real del PLC a un valor entre 0 y 1.
            float targetY = Mathf.Lerp(unityV_Min, unityV_Max, tV);  // Traducimos ese 0-1 a la posición equivalente en el modelo 3D.
            Vector3 p = ejeVertical.localPosition;
            p.y = Mathf.Lerp(p.y, targetY, lerpSpeed * dt);          // Desplazamos el carro suavemente hacia esa posición en el eje Y.
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
