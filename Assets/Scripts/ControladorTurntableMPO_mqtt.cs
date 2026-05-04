using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ControladorTurntable_mqtt : MonoBehaviour
{
    [Header("Componentes")]
    public Transform mesaGiratoria; // Rota sobre Y
    public Transform ejector;       // Movimiento local
    public Transform sierraDisco;   // Rota sobre Y

    [Header("Ángulos Y (Grados) - Clic derecho para capturar")]
    [ContextMenuItem("Capturar Horno (Ref7)", "CapturarRef7")] public float rotY_Ref7;
    [ContextMenuItem("Capturar Cinta (Ref9)", "CapturarRef9")] public float rotY_Ref9;
    [ContextMenuItem("Capturar Sierra (Ref10)", "CapturarRef10")] public float rotY_Ref10;

    [Header("Posiciones Ejector")]
    [ContextMenuItem("Capturar Retraído", "CapturarEject0")] public Vector3 posEjectRetraido;
    [ContextMenuItem("Capturar Extendido", "CapturarEject1")] public Vector3 posEjectExtendido;

    [Header("Configuración")]
    public float velocidadSierraRpm = 300f;
    public float lerpSpeed = 5f;

    private Queue<MPOTurntablePayload> colaMensajes = new Queue<MPOTurntablePayload>();
    private float sawDir = 0f;

    void CapturarRef7() => rotY_Ref7 = mesaGiratoria.localEulerAngles.y;
    void CapturarRef9() => rotY_Ref9 = mesaGiratoria.localEulerAngles.y;
    void CapturarRef10() => rotY_Ref10 = mesaGiratoria.localEulerAngles.y;
    void CapturarEject0() => posEjectRetraido = ejector.localPosition;
    void CapturarEject1() => posEjectExtendido = ejector.localPosition;

    private void Start() => StartCoroutine(SuscripcionSegura());

    private IEnumerator SuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;
        MQTTClient.Instance.OnTurntableUpdateEvent += (data) => {
            lock (colaMensajes) { colaMensajes.Enqueue(data); }
        };
    }

    private void Update()
    {
        lock (colaMensajes) { while (colaMensajes.Count > 0) ProcesarMensaje(colaMensajes.Dequeue()); }

        float step = lerpSpeed * Time.deltaTime;

        // 1. ROTACIÓN SIERRA (Giro continuo sobre eje Y local)
        if (sawDir != 0f && sierraDisco != null)
        {
            // Rotación pura sobre el eje Y local
            sierraDisco.Rotate(0, sawDir * (velocidadSierraRpm * 6f) * Time.deltaTime, 0, Space.Self);
        }
    }

    private void ProcesarMensaje(MPOTurntablePayload data)
    {
        // 2. ROTACIÓN MESA (Interpolación al ángulo objetivo en Y)
        float targetY = mesaGiratoria.localEulerAngles.y;
        if (data.move2Ref7 == 1) targetY = rotY_Ref7;
        else if (data.move2Ref9 == 1) targetY = rotY_Ref9;
        else if (data.move2Ref10 == 1) targetY = rotY_Ref10;

        mesaGiratoria.localRotation = Quaternion.Slerp(mesaGiratoria.localRotation,
                                     Quaternion.Euler(0, targetY, 0),
                                     lerpSpeed * Time.deltaTime);

        // 3. EJECTOR
        Vector3 targetPos = (data.eject == 1) ? posEjectExtendido : posEjectRetraido;
        ejector.localPosition = Vector3.Lerp(ejector.localPosition, targetPos, lerpSpeed * Time.deltaTime);

        sawDir = (float)data.saw;
    }
}