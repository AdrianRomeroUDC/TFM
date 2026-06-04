using UnityEngine;
using System.Collections;

public class ControladorHBWposition_mqtt : MonoBehaviour
{
    // Historial y almacenamiento de los últimos valores recibidos para los ejes Horizontal, Vertical y Extensión
    private float lastH, lastV, lastE;

    // Control de flanco para disparar la animación del brazo telescópico de forma sincronizada
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
    public float lerpSpeed = 5f;        // Velocidad de suavizado para el movimiento de los carros de los ejes
    public float tiempoAnimacion = 4f;   // Tiempo en segundos que toma la extensión telescópica completa

    [Header("Estado del Agarre")]
    public Transform objetoEnganchado = null;      // Guarda la referencia del contenedor que se desplaza con la máquina
    private Transform padreOriginalEstante = null;   // Almacén de respaldo para el padre en la estantería
    private Coroutine corrutinaExtension;          // Mantiene la referencia de la corrutina activa para evitar duplicidades

    // --- FUNCIONES DE CAPTURA PARA EL INSPECTOR (Menús contextuales de conveniencia) ---
    void CapturarHMin() => unityH_Min = ejeHorizontal.localPosition.z;
    void CapturarHMax() => unityH_Max = ejeHorizontal.localPosition.z;
    void CapturarVMin() => unityV_Min = ejeVertical.localPosition.y;
    void CapturarVMax() => unityV_Max = ejeVertical.localPosition.y;
    void CapturarExtEst() => unityE_Estirado = ejeExtension.localPosition.x;
    void CapturarExtRec() => unityE_Recogido = ejeExtension.localPosition.x;

    void Start() { StartCoroutine(SuscripcionSegura()); }

    IEnumerator SuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;

        // Vinculación del método de actualización al evento de recepción de posiciones MQTT del HBW
        MQTTClient.Instance.OnHBWPositionUpdateEvent += ActualizarPosicionDesdeMQTT;
        Debug.Log("<color=green>HBW suscrito correctamente</color>");
    }

    private void ActualizarPosicionDesdeMQTT(float hor, float vert, float ext)
    {
        lastH = hor;   // Guardamos la nueva meta del eje Horizontal
        lastV = vert;  // Guardamos la nueva meta del eje Vertical

        // Detección de flanco o comandos discretos para el brazo extractor (-512 = Estirar, 512 = Recoger)
        if (ext != lastE && (ext == -512 || ext == 512))
        {
            lastE = ext;
            hayNuevaOrdenEstirar = true; // Izamos la bandera para procesarla en el siguiente frame del Update
        }
        else if (ext == 0) lastE = 0;
    }

    void Update()
    {
        // Si hay una orden pendiente de extensión o retracción del brazo, ejecutamos la rutina
        if (hayNuevaOrdenEstirar)
        {
            hayNuevaOrdenEstirar = false;
            IniciarAnimacionExtension(lastE == -512 ? unityE_Estirado : unityE_Recogido);
        }

        float dt = Time.deltaTime;

        // --- INTERPOLACIÓN SUAVE DEL EJE HORIZONTAL ---
        if (ejeHorizontal)
        {
            float tH = Mathf.InverseLerp(plcH_Min, plcH_Max, lastH); // Normalizamos valor PLC a rango [0,1]
            float targetZ = Mathf.Lerp(unityH_Min, unityH_Max, tH);  // Mapeamos el [0,1] al rango local de Unity
            Vector3 p = ejeHorizontal.localPosition;
            p.z = Mathf.Lerp(p.z, targetZ, lerpSpeed * dt);          // Aplicamos un suavizado Lerp en el eje Z
            ejeHorizontal.localPosition = p;
        }

        // --- INTERPOLACIÓN SUAVE DEL EJE VERTICAL ---
        if (ejeVertical)
        {
            float tV = Mathf.InverseLerp(plcV_Min, plcV_Max, lastV); // Normalizamos valor PLC a rango [0,1]
            float targetY = Mathf.Lerp(unityV_Min, unityV_Max, tV);  // Mapeamos el [0,1] al rango local de Unity
            Vector3 p = ejeVertical.localPosition;
            p.y = Mathf.Lerp(p.y, targetY, lerpSpeed * dt);          // Aplicamos un suavizado Lerp en el eje Y
            ejeVertical.localPosition = p;

            // Bloque original comentado para prevenir caídas accidentales basándose puramente en altura:
            // if (objetoEnganchado != null && lastV < plcV_Min + 5) SoltarCajon();
        }
    }

    // --- MÉTODOS DE CAPTURA Y ANIMACIÓN ---
    public void ProcesarCaptura(Transform contenedor, Transform plataforma)
    {
        // ASIGNACIÓN CRUCIAL: Guardamos la referencia para saber qué objeto tenemos cargado bajo custodia
        objetoEnganchado = contenedor;

        // Forzamos a que el contenedor cambie de jerarquía y pase a ser hijo directo de la plataforma móvil (el proxy)
        contenedor.SetParent(plataforma);

        // Volvemos el contenedor cinemático y anulamos inercias previas para evitar vibraciones en el viaje
        if (contenedor.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    // Nuevo método público para que el cajón le avise al controlador que ya llegó a su estante
    public void NotificarCajonLiberado()
    {
        objetoEnganchado = null; // Vaciamos la variable de custodia liberando el brazo mecánicamente
        Debug.Log("<color=yellow><b>[Controlador HBW]:</b> El transelevador registra que ya no lleva ningún cajón y se retirará solo.</color>");
    }

    // Método de seguridad para liberar forzadamente el cajón restableciendo sus componentes físicos nativos
    private void SoltarCajon()
    {
        if (objetoEnganchado != null)
        {
            objetoEnganchado.SetParent(padreOriginalEstante, true);
            if (objetoEnganchado.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
            objetoEnganchado = null;
        }
    }

    // Método puente para iniciar la corrutina de movimiento del brazo interrumpiendo cualquier proceso anterior
    void IniciarAnimacionExtension(float d)
    {
        if (corrutinaExtension != null) StopCoroutine(corrutinaExtension);
        corrutinaExtension = StartCoroutine(AnimarBrazo(d));
    }

    // Corrutina que traslada el brazo extractor frame a frame simulando el pistón telescópico
    IEnumerator AnimarBrazo(float d)
    {
        float t = 0;
        float inicioX = ejeExtension.localPosition.x;
        while (t < tiempoAnimacion)
        {
            t += Time.deltaTime;
            float progreso = t / tiempoAnimacion;
            // Interpolación suavizada usando SmoothStep para dar un efecto de aceleración y desaceleración elegante
            float vX = Mathf.Lerp(inicioX, d, Mathf.SmoothStep(0, 1, progreso));
            ejeExtension.localPosition = new Vector3(vX, ejeExtension.localPosition.y, ejeExtension.localPosition.z);
            yield return null;
        }
        // Aseguramos la asignación matemática exacta en la posición final al concluir el bucle
        ejeExtension.localPosition = new Vector3(d, ejeExtension.localPosition.y, ejeExtension.localPosition.z);
    }
}