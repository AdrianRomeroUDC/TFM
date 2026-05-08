using UnityEngine;
using System.Collections;

public class ControladorTurntableMPO_mqtt : MonoBehaviour
{
    [Header("Componentes")]
    public Transform pivotMesaGiratoria;
    public Transform ejector;
    public Transform sierraDisco;

    [Header("Calibración Unity (Eje Y Local)")]
    [ContextMenuItem("Capturar", "CapturarRef7")] public float unityRot_Ref7;    // Brazo (Horno)
    [ContextMenuItem("Capturar", "CapturarRef9")] public float unityRot_Ref9;    // Cinta
    [ContextMenuItem("Capturar", "CapturarRef10")] public float unityRot_Ref10; // Sierra

    [Header("Posiciones Ejector")]
    [ContextMenuItem("Capturar", "CapturarEject0")] public Vector3 posEjectRetraido;
    [ContextMenuItem("Capturar", "CapturarEject1")] public Vector3 posEjectExtendido;

    [Header("Configuración")]
    public float lerpSpeed = 5f;
    public float velocidadSierraRpm = 512f;

    // --- VARIABLES DE CONTROL DE TRAYECTORIA ---
    private float targetAngleY;       // El ángulo al que se mueve la mesa AHORA
    private float finalTargetAngleY;  // El destino final real (Brazo)
    private int ultimoRef = -1;       // Guarda la última posición confirmada (7, 9 o 10)
    private bool pasoIntermedioActivo = false;

    private Vector3 targetPosEjector;
    private float sawDir = 0f;

    // Métodos de captura (Context Menu)
    void CapturarRef7() => unityRot_Ref7 = pivotMesaGiratoria.localEulerAngles.y;
    void CapturarRef9() => unityRot_Ref9 = pivotMesaGiratoria.localEulerAngles.y;
    void CapturarRef10() => unityRot_Ref10 = pivotMesaGiratoria.localEulerAngles.y;
    void CapturarEject0() => posEjectRetraido = ejector.localPosition;
    void CapturarEject1() => posEjectExtendido = ejector.localPosition;

    void Start()
    {
        if (pivotMesaGiratoria)
        {
            targetAngleY = pivotMesaGiratoria.localEulerAngles.y;
            finalTargetAngleY = targetAngleY;
        }
        if (ejector) targetPosEjector = ejector.localPosition;

        StartCoroutine(SuscripcionSegura());
    }

    IEnumerator SuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;
        MQTTClient.Instance.OnTurntableUpdateEvent += (data) =>
        {
            int nuevoRef = -1;
            float nuevoAngulo = 0f;

            // Identificamos a qué referencia nos mandan
            if (data.move2Ref7 == 1) { nuevoRef = 7; nuevoAngulo = unityRot_Ref7; }
            else if (data.move2Ref9 == 1) { nuevoRef = 9; nuevoAngulo = unityRot_Ref9; }
            else if (data.move2Ref10 == 1) { nuevoRef = 10; nuevoAngulo = unityRot_Ref10; }

            if (nuevoRef != -1)
            {
                // LÓGICA ESPECIAL: De Cinta (9) a Brazo (7)
                if (ultimoRef == 9 && nuevoRef == 7)
                {
                    pasoIntermedioActivo = true;
                    targetAngleY = unityRot_Ref10;  // Primero ve a la Sierra
                    finalTargetAngleY = unityRot_Ref7; // Destino final guardado
                }
                else
                {
                    pasoIntermedioActivo = false;
                    targetAngleY = nuevoAngulo;
                    finalTargetAngleY = nuevoAngulo;
                }

                ultimoRef = nuevoRef; // Actualizamos nuestra posición lógica
            }

            targetPosEjector = (data.eject == 1) ? posEjectExtendido : posEjectRetraido;
            sawDir = (float)data.saw;
        };
    }

    void Update()
    {
        if (pivotMesaGiratoria)
        {
            float currentY = pivotMesaGiratoria.localEulerAngles.y;

            // Si estamos haciendo el desvío por la Sierra
            if (pasoIntermedioActivo)
            {
                // Comprobamos si ya casi llegamos a la Sierra (margen de 1 grado)
                if (Mathf.Abs(Mathf.DeltaAngle(currentY, unityRot_Ref10)) < 1.0f)
                {
                    targetAngleY = finalTargetAngleY; // Cambiamos objetivo al Brazo
                    pasoIntermedioActivo = false;     // Fin del desvío
                }
            }

            float nextY = Mathf.LerpAngle(currentY, targetAngleY, lerpSpeed * Time.deltaTime);
            pivotMesaGiratoria.localRotation = Quaternion.Euler(0, nextY, 0);
        }

        if (ejector)
            ejector.localPosition = Vector3.Lerp(ejector.localPosition, targetPosEjector, lerpSpeed * Time.deltaTime);

        if (sawDir != 0f && sierraDisco != null)
            sierraDisco.Rotate(0, sawDir * (velocidadSierraRpm * 6f) * Time.deltaTime, 0, Space.Self);
    }
}