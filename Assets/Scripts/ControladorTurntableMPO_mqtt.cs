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
    private Vector3 targetPosEjector;

    // CONTROL DE FLUJO: Evita que el cambio de parentesco se ejecute repetidamente en cada frame
    private bool piezaLiberadaEnEsteCiclo = false;

    public bool EjectorEstaActivo
    {
        get
        {
            if (ejector == null) return false;
            return (targetPosEjector == posEjectExtendido) || (Vector3.Distance(ejector.localPosition, posEjectRetraido) > 0.000001f);
        }
    }

    void Start()
    {
        anguloVirtualActual = Normalizar(pivotMesaGiratoria.localEulerAngles.y);
        targetAngleY = anguloVirtualActual;
        targetPosEjector = (ejector != null) ? ejector.localPosition : Vector3.zero;
        piezaLiberadaEnEsteCiclo = false;
    }

    void Update()
    {
        // 1. MQTT - PROCESAMIENTO
        if (MQTTClient.Instance != null)
        {
            lock (MQTTClient.Instance.colaMensajes)
            {
                if (MQTTClient.Instance.colaMensajes.Count > 0)
                {
                    var data = MQTTClient.Instance.colaMensajes.Peek();
                    bool esSoloActuador = (data.move2Ref7 == 0 && data.move2Ref9 == 0 && data.move2Ref10 == 0);

                    if (!estaOcupado || esSoloActuador)
                    {
                        ProcesarComando(MQTTClient.Instance.colaMensajes.Dequeue());
                    }
                }
            }
        }

        // 2. Movimiento de Mesa
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

        // 3. Ejector (Pusher)
        if (ejector)
        {
            ejector.localPosition = Vector3.MoveTowards(ejector.localPosition, targetPosEjector, velocidadEjector * Time.deltaTime);

            // Solo intenta la liberación si el objetivo es extenderse Y no ha liberado aún en esta carrera
            if (targetPosEjector == posEjectExtendido && !piezaLiberadaEnEsteCiclo)
            {
                float distanciaAlObjetivo = Vector3.Distance(ejector.localPosition, posEjectExtendido);

                if (distanciaAlObjetivo < 0.000001f)
                {
                    LiberarPiezaEnCinta();
                    piezaLiberadaEnEsteCiclo = true; // Bloquea ejecuciones continuas en los siguientes frames
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
        if (data.eject == 0)
        {
            piezaLiberadaEnEsteCiclo = false;
        }

        targetPosEjector = (data.eject == 1) ? posEjectExtendido : posEjectRetraido;
        sawDir = (float)data.saw;

        bool tieneOrdenDeReferencia = (data.move2Ref7 == 1 || data.move2Ref9 == 1 || data.move2Ref10 == 1);

        if (tieneOrdenDeReferencia)
        {
            float nuevoAngulo = -1;
            if (data.move2Ref7 == 1) nuevoAngulo = TurntableBrazo;
            else if (data.move2Ref9 == 1) nuevoAngulo = TurntableCinta;
            else if (data.move2Ref10 == 1) nuevoAngulo = TurntableSierra;

            if (nuevoAngulo != -1)
            {
                targetAngleY = Normalizar(nuevoAngulo);
                anguloVirtualActual = Normalizar(pivotMesaGiratoria.localEulerAngles.y);

                if (data.rotation != 0)
                    sentidoGiro = data.rotation;
                else
                    sentidoGiro = (Mathf.DeltaAngle(anguloVirtualActual, targetAngleY) > 0) ? 1 : -1;

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

        if (piezaSujeta != null)
        {
            if (cintaMPO != null)
            {
                Transform eslabonMasCercano = null;
                float distanciaMinima = float.MaxValue;

                foreach (Transform eslabon in cintaMPO.GetComponentsInChildren<Transform>())
                {
                    if (eslabon == cintaMPO) continue;

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

                    // 1. Asignamos el nuevo padre (el eslabón físico de la cinta)
                    piezaSujeta.SetParent(eslabonMasCercano, true);

                    // 1. Definimos los valores exactos (Ajusta estos números según tu medición)
                    Vector3 posDeseada = new Vector3(0f, 0.000352f, -0.000027f); // X, Y, Z
                    Quaternion rotDeseada = Quaternion.Euler(0, 90, 0); // Ajusta los grados (X, Y, Z) según necesites

                    // 2. Aplicamos la posición y rotación relativas al nuevo padre (eslabón)
                    piezaSujeta.localPosition = posDeseada;
                    piezaSujeta.localRotation = rotDeseada;

                    // 3. Aseguramos físicas estables
                    Rigidbody rb = piezaSujeta.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = true;
                        rb.useGravity = false;
                    }

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
            Debug.LogWarning("[Pusher]: Se estiró al máximo pero no llevaba ninguna pieza como hija de forma directa.");
        }
    }
}