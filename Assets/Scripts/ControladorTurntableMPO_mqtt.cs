using UnityEngine;
using System.Collections;

public class ControladorTurntableMPO_mqtt : MonoBehaviour
{
    [Header("Componentes")]
    public Transform pivotMesaGiratoria;
    public Transform ejector;
    public Transform sierraDisco;

    [Header("Calibración")]
    [ContextMenuItem("Capturar", "CapturarRef7")] public float TurntableBrazo;
    [ContextMenuItem("Capturar", "CapturarRef9")] public float TurntableCinta;
    [ContextMenuItem("Capturar", "CapturarRef10")] public float TurntableSierra;
    [ContextMenuItem("Capturar Eject Extendido", "CapturarEjectExtendido")] public Vector3 posEjectExtendido;
    [ContextMenuItem("Capturar Eject Retraido", "CapturarEjectRetraido")] public Vector3 posEjectRetraido;

    // --- MÉTODOS DE CALIBRACIÓN ---
    float Normalizar(float angulo) => (angulo % 360 + 360) % 360;

    void CapturarRef7() => TurntableBrazo = Normalizar(pivotMesaGiratoria.localEulerAngles.y);
    void CapturarRef9() => TurntableCinta = Normalizar(pivotMesaGiratoria.localEulerAngles.y);
    void CapturarRef10() => TurntableSierra = Normalizar(pivotMesaGiratoria.localEulerAngles.y);

    void CapturarEjectExtendido() => posEjectExtendido = ejector.localPosition;
    void CapturarEjectRetraido() => posEjectRetraido = ejector.localPosition;

    [Header("Configuración")]
    public float SpeedTurntable = 100f;
    public float VelocidadSierra = 512f;

    private float targetAngleY;
    private float anguloVirtualActual;
    private float targetVirtual;
    private int sentidoGiro = 0;
    private bool estaOcupado = false;
    private float sawDir = 0f;
    private Vector3 targetPosEjector;

    void Start()
    {
        anguloVirtualActual = Normalizar(pivotMesaGiratoria.localEulerAngles.y);
        targetAngleY = anguloVirtualActual;
        targetPosEjector = (ejector != null) ? ejector.localPosition : Vector3.zero;
    }

    void Update()
    {
        // 1. MQTT - PROCESAMIENTO MEJORADO
        if (MQTTClient.Instance != null)
        {
            lock (MQTTClient.Instance.colaMensajes)
            {
                // Leemos los mensajes siempre para actualizar la sierra/ejector al instante
                if (MQTTClient.Instance.colaMensajes.Count > 0)
                {
                    // Si estamos moviendo la mesa, solo procesamos si NO es una orden de movimiento
                    // o si queremos que la nueva orden interrumpa la actual.
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

        // 3. Ejector
        if (ejector)
            ejector.localPosition = Vector3.Lerp(ejector.localPosition, targetPosEjector, 5f * Time.deltaTime);

        // 4. SIERRA - Giro sobre eje Y local (Flecha Verde)
        if (sierraDisco != null && sawDir != 0f)
        {
            // Multiplicamos por sawDir (1 o -1) para el sentido
            sierraDisco.Rotate(Vector3.up, sawDir * VelocidadSierra * Time.deltaTime, Space.Self);
        }
    }

    void ProcesarComando(MPOTurntablePayload data)
    {
        // Esto se actualiza SIEMPRE, incluso con la mesa moviéndose
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
}