using UnityEngine;
using System.Collections;

public class ControladorTurntableMPO_mqtt : MonoBehaviour
{
    [Header("Componentes")]
    public Transform pivotMesaGiratoria;
    public Transform ejector;
    public Transform sierraDisco;

    [Tooltip("Objeto de la cinta transportadora a la que se entregará la pieza (CintaTransportadora_EntradaSLD).")]
    public Transform cintaMPO;

    [Header("Calibración")]
    [ContextMenuItem("Capturar", "CapturarRef7")] public float TurntableBrazo;
    [ContextMenuItem("Capturar", "CapturarRef9")] public float TurntableCinta;
    [ContextMenuItem("Capturar", "CapturarRef10")] public float TurntableSierra;
    [ContextMenuItem("Capturar Eject Extendido", "CapturarEjectExtendido")] public Vector3 posEjectExtendido;
    [ContextMenuItem("Capturar Eject Retraido", "CapturarEjectRetraido")] public Vector3 posEjectRetraido;

    float Normalizar(float angulo) => (angulo % 360 + 360) % 360;
    void CapturarRef7() => TurntableBrazo = Normalizar(pivotMesaGiratoria.localEulerAngles.y);
    void CapturarRef9() => TurntableCinta = Normalizar(pivotMesaGiratoria.localEulerAngles.y);
    void CapturarRef10() => TurntableSierra = Normalizar(pivotMesaGiratoria.localEulerAngles.y);
    void CapturarEjectExtendido() => posEjectExtendido = ejector.localPosition;
    void CapturarEjectRetraido() => posEjectRetraido = ejector.localPosition;

    [Header("Configuración")]
    public float SpeedTurntable = 100f;
    public float VelocidadSierra = 512f;

    [Tooltip("Velocidad del pusher.")]
    public float velocidadEjector = 0.001f;

    private float targetAngleY;
    private float anguloVirtualActual;
    private float targetVirtual;
    private int sentidoGiro = 0;
    private bool estaOcupado = false;
    private float sawDir = 0f;

    // --- AUTOMATIZACIÓN POR CICLO AUTO-MANTENIDO ---
    private bool _pusherMoviendoseHaciaAfuera = false;
    private bool piezaLiberadaEnEsteCiclo = false;

    // El eyector se considera activo si va hacia afuera o si aún no ha regresado a su base de reposo
    public bool EjectorEstaActivo
    {
        get
        {
            if (ejector == null) return false;
            return _pusherMoviendoseHaciaAfuera || (Vector3.Distance(ejector.localPosition, posEjectRetraido) > 0.00001f);
        }
    }

    void Start()
    {
        anguloVirtualActual = Normalizar(pivotMesaGiratoria.localEulerAngles.y);
        targetAngleY = anguloVirtualActual;
        piezaLiberadaEnEsteCiclo = false;
        _pusherMoviendoseHaciaAfuera = false;
    }

    void Update()
    {
        // 1. MQTT - PROCESAMIENTO DE COLA
        if (MQTTClient.Instance != null)
        {
            lock (MQTTClient.Instance.colaMensajes)
            {
                if (MQTTClient.Instance.colaMensajes.Count > 0)
                {
                    var data = MQTTClient.Instance.colaMensajes.Peek();
                    bool esSoloActuador = (data.move2Ref7 == 0 && data.move2Ref8 == 0 && data.move2Ref9 == 0 && data.move2Ref10 == 0);

                    if (!estaOcupado || esSoloActuador)
                    {
                        ProcesarComando(MQTTClient.Instance.colaMensajes.Dequeue());
                    }
                }
            }
        }

        // 2. Movimiento de Mesa Giratoria
        if (estaOcupado)
        {
            anguloVirtualActual = Mathf.MoveTowards(anguloVirtualActual, targetVirtual, SpeedTurntable * Time.deltaTime);
            pivotMesaGiratoria.localRotation = Quaternion.Euler(0, anguloVirtualActual, 0);

            if (Mathf.Abs(anguloVirtualActual - targetVirtual) < 0.01f)
            {
                float anguloFinal = Normalizar(targetAngleY);
                pivotMesaGiratoria.localRotation = Quaternion.Euler(0, anguloFinal, 0);
                anguloVirtualActual = anguloFinal;
                estaOcupado = false;
                sentidoGiro = 0;
            }
        }

        // 3. Control Cinemático Automático del Ejector (Pusher)
        if (ejector)
        {
            // Determinamos el objetivo dependiendo de la fase del ciclo en la que nos encontremos
            Vector3 targetPosEjector = _pusherMoviendoseHaciaAfuera ? posEjectExtendido : posEjectRetraido;

            ejector.localPosition = Vector3.MoveTowards(ejector.localPosition, targetPosEjector, velocidadEjector * Time.deltaTime);

            // Fase de extensión y entrega
            if (_pusherMoviendoseHaciaAfuera && !piezaLiberadaEnEsteCiclo)
            {
                float distanciaAlObjetivo = Vector3.Distance(ejector.localPosition, posEjectExtendido);

                if (distanciaAlObjetivo < 0.00005f) // Margen de llegada seguro
                {
                    Debug.Log("<color=green><b>[Pusher]:</b> Límite de carrera alcanzado. Entregando pieza a la cinta...</color>");
                    LiberarPiezaEnCinta();
                    piezaLiberadaEnEsteCiclo = true;

                    // Conmutación automática: Iniciamos el retorno inmediato a casa
                    _pusherMoviendoseHaciaAfuera = false;
                }
            }
        }

        // 4. SIERRA
        if (sierraDisco != null && sawDir != 0f)
        {
            sierraDisco.Rotate(Vector3.up, sawDir * VelocidadSierra * Time.deltaTime, Space.Self);
        }
    }

    void ProcesarComando(MPOTurntablePayload data)
    {
        // El pulso de activación inicia el ciclo blindado
        if (data.eject == 1)
        {
            // Solo disparamos si el pusher está en reposo para evitar re-disparos buclados
            if (!_pusherMoviendoseHaciaAfuera && Vector3.Distance(ejector.localPosition, posEjectRetraido) < 0.0001f)
            {
                _pusherMoviendoseHaciaAfuera = true;
                piezaLiberadaEnEsteCiclo = false;
                Debug.Log("<color=lime><b>[Pusher]:</b> Pulso EJECT detectado (30ms). Ejecutando carrera completa auto-mantenida.</color>");
            }
        }
        // NOTA DE INGENIERÍA: Ignoramos 'data.eject == 0' para evitar que los flancos de bajada 
        // rápidos destruyan el avance mecánico del gemelo digital.

        sawDir = (float)data.saw;

        bool tieneOrdenDeReferencia = (data.move2Ref7 == 1 || data.move2Ref8 == 1 || data.move2Ref9 == 1 || data.move2Ref10 == 1);

        if (tieneOrdenDeReferencia)
        {
            float nuevoAngulo = -1;
            if (data.move2Ref7 == 1) nuevoAngulo = TurntableBrazo;
            else if (data.move2Ref9 == 1) nuevoAngulo = TurntableCinta;
            else if (data.move2Ref10 == 1 || data.move2Ref8 == 1) nuevoAngulo = TurntableSierra;

            if (nuevoAngulo != -1)
            {
                targetAngleY = Normalizar(nuevoAngulo);
                anguloVirtualActual = Normalizar(pivotMesaGiratoria.localEulerAngles.y);

                if (data.rotation != 0)
                {
                    sentidoGiro = data.rotation;
                }
                else
                {
                    if (data.move2Ref7 == 1 || data.move2Ref8 == 1) sentidoGiro = -1; // Antihorario
                    else if (data.move2Ref9 == 1 || data.move2Ref10 == 1) sentidoGiro = 1;  // Horario
                    else sentidoGiro = (Mathf.DeltaAngle(anguloVirtualActual, targetAngleY) > 0) ? 1 : -1;
                }

                float diff = Mathf.DeltaAngle(anguloVirtualActual, targetAngleY);

                if (sentidoGiro == 1 && diff < 0) targetVirtual = anguloVirtualActual + (diff + 360);
                else if (sentidoGiro == -1 && diff > 0) targetVirtual = anguloVirtualActual + (diff - 360);
                else targetVirtual = anguloVirtualActual + diff;

                estaOcupado = true;
            }
        }
    }

    private void LiberarPiezaEnCinta()
    {
        Transform piezaSujeta = null;

        foreach (Transform hijo in ejector.GetComponentsInChildren<Transform>())
        {
            if (hijo != ejector && hijo.name.ToLower().Contains("pieza"))
            {
                piezaSujeta = hijo;
                break;
            }
        }

        if (piezaSujeta == null)
        {
            foreach (Transform hijo in transform.GetComponentsInChildren<Transform>())
            {
                if (hijo.name.ToLower().Contains("pieza"))
                {
                    piezaSujeta = hijo;
                    break;
                }
            }
        }

        if (piezaSujeta != null)
        {
            Transform realCinta = cintaMPO;

            if (realCinta != null)
            {
                ControladorCintaMPO_mqtt scriptCinta = realCinta.GetComponent<ControladorCintaMPO_mqtt>();
                if (scriptCinta != null && scriptCinta.objetoCintaPadre != null)
                {
                    realCinta = scriptCinta.objetoCintaPadre;
                }

                Transform eslabonMasCercano = null;
                float distanciaMinima = float.MaxValue;

                foreach (Transform eslabon in realCinta.GetComponentsInChildren<Transform>())
                {
                    if (eslabon == realCinta) continue;

                    float distancia = Vector3.Distance(piezaSujeta.position, eslabon.position);
                    if (distancia < distanciaMinima)
                    {
                        distanciaMinima = distancia;
                        eslabonMasCercano = eslabon;
                    }
                }

                if (eslabonMasCercano != null)
                {
                    Debug.Log($"<color=green>[CINTA]: Transferida con éxito la pieza '{piezaSujeta.name}' al eslabón '{eslabonMasCercano.name}' (Distancia: {distanciaMinima:F5}).</color>");

                    piezaSujeta.SetParent(eslabonMasCercano, true);

                    Vector3 posDeseada = new Vector3(0f, 0.000154f, 0.000178f);
                    piezaSujeta.localPosition = posDeseada;

                    Rigidbody rb = piezaSujeta.GetComponent<Rigidbody>();
                    if (rb == null) rb = piezaSujeta.gameObject.AddComponent<Rigidbody>();

                    rb.isKinematic = true;
                    rb.useGravity = false;

                    Physics.SyncTransforms();
                }
                else
                {
                    Debug.LogWarning("[CINTA - ERROR]: No se encontraron sub-objetos (eslabones) válidos dentro de la cinta.");
                }
            }
            else
            {
                Debug.LogError("[CINTA - ERROR]: No se ha asignado la cinta en la variable 'cintaMPO' del Inspector.");
            }
        }
        else
        {
            Debug.LogWarning("[Pusher]: Se intentó liberar la pieza pero no se detectó ninguna pieza sujeta.");
        }
    }
}