using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ControladorTurntable_mqtt : MonoBehaviour
{
    [Header("Componentes")]
    public Transform mesaGiratoria;  // El objeto que rotará (PivotRotacion)
    public Transform referenciaEje; // El objeto con el centro geométrico (Engranaje)
    public Transform ejector;
    public Transform sierraDisco;

    [Header("Ángulos Y (Grados)")]
    [ContextMenuItem("Capturar Horno (Ref7)", "CapturarRef7")] public float PosBrazo;
    [ContextMenuItem("Capturar Cinta (Ref9)", "CapturarRef9")] public float PosCinta;
    [ContextMenuItem("Capturar Sierra (Ref10)", "CapturarRef10")] public float PosSierra;

    [Header("Posiciones Ejector")]
    [ContextMenuItem("Capturar Retraído", "CapturarEject0")] public Vector3 posEjectRetraido;
    [ContextMenuItem("Capturar Extendido", "CapturarEject1")] public Vector3 posEjectExtendido;

    [Header("Configuración")]
    public float velocidadSierraRpm = 300f;
    public float lerpSpeed = 5f;

    private Queue<MPOTurntablePayload> colaMensajes = new Queue<MPOTurntablePayload>();
    private float sawDir = 0f;

    // Métodos para el ContextMenu (Clic derecho en el inspector)
    void CapturarRef7() => PosBrazo = mesaGiratoria.localEulerAngles.y;
    void CapturarRef9() => PosCinta = mesaGiratoria.localEulerAngles.y;
    void CapturarRef10() => PosSierra = mesaGiratoria.localEulerAngles.y;
    void CapturarEject0() => posEjectRetraido = ejector.localPosition;
    void CapturarEject1() => posEjectExtendido = ejector.localPosition;

    [ContextMenu("Centrar Pivote en Eje")]
    public void CentrarPivoteEnEje()
    {
        if (referenciaEje != null && mesaGiratoria != null)
        {
            // Alineamos posición y rotación al centro geométrico del engranaje
            mesaGiratoria.position = referenciaEje.position;
            mesaGiratoria.rotation = referenciaEje.rotation;
            Debug.Log("Pivote alineado correctamente con: " + referenciaEje.name);
        }
        else
        {
            Debug.LogWarning("Asigna Mesa Giratoria y Referencia Eje en el inspector primero.");
        }
    }

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

        // 1. ROTACIÓN SIERRA (Giro continuo sobre eje Y local)
        if (sawDir != 0f && sierraDisco != null)
        {
            float gradosPorSegundo = velocidadSierraRpm * 6f;
            sierraDisco.Rotate(0, sawDir * gradosPorSegundo * Time.deltaTime, 0, Space.Self);
        }
    }

    private void ProcesarMensaje(MPOTurntablePayload data)
    {
        // 2. ROTACIÓN MESA (Interpolación al ángulo objetivo en Y local)
        float targetY = mesaGiratoria.localEulerAngles.y;
        if (data.move2Ref7 == 1) targetY = PosBrazo;
        else if (data.move2Ref9 == 1) targetY = PosCinta;
        else if (data.move2Ref10 == 1) targetY = PosSierra;

        float currentY = mesaGiratoria.localEulerAngles.y;
        float newY = Mathf.LerpAngle(currentY, targetY, lerpSpeed * Time.deltaTime);

        mesaGiratoria.localEulerAngles = new Vector3(mesaGiratoria.localEulerAngles.x, newY, mesaGiratoria.localEulerAngles.z);

        // 3. EJECTOR
        if (ejector != null)
        {
            Vector3 targetPos = (data.eject == 1) ? posEjectExtendido : posEjectRetraido;
            ejector.localPosition = Vector3.Lerp(ejector.localPosition, targetPos, lerpSpeed * Time.deltaTime);
        }

        sawDir = (float)data.saw;
    }
}