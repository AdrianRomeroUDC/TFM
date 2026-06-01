using UnityEngine;
using System.Collections;

public class ControladorHBWposition_mqtt : MonoBehaviour
{
    private float lastH, lastV, lastE;
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
    public float lerpSpeed = 5f;
    public float tiempoAnimacion = 4f;

    [Header("Estado del Agarre")]
    public Transform objetoEnganchado = null;
    private Transform padreOriginalEstante = null;
    private Coroutine corrutinaExtension;

    // --- FUNCIONES DE CAPTURA PARA EL INSPECTOR ---
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
        MQTTClient.Instance.OnHBWPositionUpdateEvent += ActualizarPosicionDesdeMQTT;
        Debug.Log("<color=green>HBW suscrito correctamente</color>");
    }

    private void ActualizarPosicionDesdeMQTT(float hor, float vert, float ext)
    {
        lastH = hor;
        lastV = vert;

        // Detección de movimiento para el brazo extractor
        if (ext != lastE && (ext == -512 || ext == 512))
        {
            lastE = ext;
            hayNuevaOrdenEstirar = true;
        }
        else if (ext == 0) lastE = 0;
    }

    void Update()
    {
        if (hayNuevaOrdenEstirar)
        {
            hayNuevaOrdenEstirar = false;
            IniciarAnimacionExtension(lastE == -512 ? unityE_Estirado : unityE_Recogido);
        }

        float dt = Time.deltaTime;

        if (ejeHorizontal)
        {
            float tH = Mathf.InverseLerp(plcH_Min, plcH_Max, lastH);
            float targetZ = Mathf.Lerp(unityH_Min, unityH_Max, tH);
            Vector3 p = ejeHorizontal.localPosition;
            p.z = Mathf.Lerp(p.z, targetZ, lerpSpeed * dt);
            ejeHorizontal.localPosition = p;
        }

        if (ejeVertical)
        {
            float tV = Mathf.InverseLerp(plcV_Min, plcV_Max, lastV);
            float targetY = Mathf.Lerp(unityV_Min, unityV_Max, tV);
            Vector3 p = ejeVertical.localPosition;
            p.y = Mathf.Lerp(p.y, targetY, lerpSpeed * dt);
            ejeVertical.localPosition = p;

            //if (objetoEnganchado != null && lastV < plcV_Min + 5) SoltarCajon();
        }
    }

    // --- MÉTODOS DE CAPTURA Y ANIMACIÓN ---
    public void ProcesarCaptura(Transform contenedor, Transform plataforma)
    {
        // ASIGNACIÓN CRUCIAL: Guardamos la referencia para saber qué tenemos cargado
        objetoEnganchado = contenedor;

        // Forzamos a que el contenedor sea hijo directo de la plataforma móvil
        contenedor.SetParent(plataforma);

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
        objetoEnganchado = null;
        Debug.Log("<color=yellow><b>[Controlador HBW]:</b> El transelevador registra que ya no lleva ningún cajón.</color>");
    }

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

    void IniciarAnimacionExtension(float d)
    {
        if (corrutinaExtension != null) StopCoroutine(corrutinaExtension);
        corrutinaExtension = StartCoroutine(AnimarBrazo(d));
    }

    IEnumerator AnimarBrazo(float d)
    {
        float t = 0;
        float inicioX = ejeExtension.localPosition.x;
        while (t < tiempoAnimacion)
        {
            t += Time.deltaTime;
            float progreso = t / tiempoAnimacion;
            float vX = Mathf.Lerp(inicioX, d, Mathf.SmoothStep(0, 1, progreso));
            ejeExtension.localPosition = new Vector3(vX, ejeExtension.localPosition.y, ejeExtension.localPosition.z);
            yield return null;
        }
        ejeExtension.localPosition = new Vector3(d, ejeExtension.localPosition.y, ejeExtension.localPosition.z);
    }
}