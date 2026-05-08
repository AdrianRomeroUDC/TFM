using UnityEngine;
using System.Collections;

public class ControladorTurntableMPO_mqtt : MonoBehaviour
{
    [Header("Componentes")]
    // Arrastra aquí el objeto "PivotTurntable"
    public Transform pivotMesaGiratoria;
    public Transform ejector;
    public Transform sierraDisco;

    [Header("Calibración Unity (Eje Y Local)")]
    // Ahora capturamos el eje Y del pivote virtual
    [ContextMenuItem("Capturar", "CapturarRef7")] public float unityRot_Ref7;
    [ContextMenuItem("Capturar", "CapturarRef9")] public float unityRot_Ref9;
    [ContextMenuItem("Capturar", "CapturarRef10")] public float unityRot_Ref10;

    [Header("Posiciones Ejector")]
    [ContextMenuItem("Capturar", "CapturarEject0")] public Vector3 posEjectRetraido;
    [ContextMenuItem("Capturar", "CapturarEject1")] public Vector3 posEjectExtendido;

    [Header("Configuración")]
    public float velocidadSierraRpm = 300f;
    public float lerpSpeed = 5f;

    private float targetAngleY;
    private Vector3 targetPosEjector;
    private float sawDir = 0f;

    // --- MÉTODOS DE CAPTURA ---
    // Capturan el eje Y local del Pivote
    void CapturarRef7() => unityRot_Ref7 = pivotMesaGiratoria.localEulerAngles.y;
    void CapturarRef9() => unityRot_Ref9 = pivotMesaGiratoria.localEulerAngles.y;
    void CapturarRef10() => unityRot_Ref10 = pivotMesaGiratoria.localEulerAngles.y;

    void CapturarEject0() => posEjectRetraido = ejector.localPosition;
    void CapturarEject1() => posEjectExtendido = ejector.localPosition;

    void Start()
    {
        if (pivotMesaGiratoria) targetAngleY = pivotMesaGiratoria.localEulerAngles.y;
        if (ejector) targetPosEjector = ejector.localPosition;

        StartCoroutine(SuscripcionSegura());
    }

    IEnumerator SuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;
        MQTTClient.Instance.OnTurntableUpdateEvent += (data) =>
        {
            // Asignación de objetivos según MQTT
            if (data.move2Ref7 == 1) targetAngleY = unityRot_Ref7;
            else if (data.move2Ref9 == 1) targetAngleY = unityRot_Ref9;
            else if (data.move2Ref10 == 1) targetAngleY = unityRot_Ref10;

            targetPosEjector = (data.eject == 1) ? posEjectExtendido : posEjectRetraido;
            sawDir = (float)data.saw;
        };
    }

    void Update()
    {
        // 1. Rotación de la Mesa (Usa el eje Y del Pivote)
        if (pivotMesaGiratoria)
        {
            float currentY = pivotMesaGiratoria.localEulerAngles.y;
            float nextY = Mathf.LerpAngle(currentY, targetAngleY, lerpSpeed * Time.deltaTime);
            pivotMesaGiratoria.localRotation = Quaternion.Euler(0, nextY, 0);
        }

        // 2. Movimiento del Ejector
        if (ejector)
        {
            ejector.localPosition = Vector3.Lerp(ejector.localPosition, targetPosEjector, lerpSpeed * Time.deltaTime);
        }

        // 3. Rotación de la Sierra
        if (sawDir != 0f && sierraDisco != null)
        {
            sierraDisco.Rotate(0, sawDir * (velocidadSierraRpm * 6f) * Time.deltaTime, 0, Space.Self);
        }
    }
}