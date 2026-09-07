using UnityEngine;
using System.Collections;

public class ControladorCamaraSSC : MonoBehaviour
{
    [Header("Estructura de la Cámara")]
    [Tooltip("El soporte que SOLO gira horizontalmente (Eje Y)")]
    public Transform ejePan;
    [Tooltip("El soporte que SOLO cabecea verticalmente (Eje Z)")]
    public Transform ejeTilt;

    [Header("Rango de Entrada del PLC")]
    public float plcPanMin = 0f;
    public float plcPanMax = 2000f;
    public float plcTiltMin = 0f;
    public float plcTiltMax = 1000f;

    [Header("Captura de Ángulo PAN (Solo importa el valor Y)")]
    [ContextMenuItem("Capturar PAN Mínimo", "CapturarPanMinimo")]
    public float unityPanMinY;
    [ContextMenuItem("Capturar PAN Máximo", "CapturarPanMaximo")]
    public float unityPanMaxY;

    [Header("Captura de Ángulo TILT (Solo importa el valor Z)")]
    [ContextMenuItem("Capturar TILT Mínimo", "CapturarTiltMinimo")]
    public float unityTiltMinZ;
    [ContextMenuItem("Capturar TILT Máximo", "CapturarTiltMaximo")]
    public float unityTiltMaxZ;

    [Header("Suavizado de Movimiento")]
    public float suavizado = 5f;

    // Objetivos flotantes a los que deben llegar los ángulos específicos
    private float targetAnguloPanY;
    private float targetAnguloTiltZ;

    // --- MÉTODOS DE CAPTURA CON CLIC DERECHO ---
    // Extraen exclusivamente el eje que te interesa independientemente de lo que marque el Inspector
    public void CapturarPanMinimo() { if (ejePan) unityPanMinY = ejePan.localEulerAngles.y; }
    public void CapturarPanMaximo() { if (ejePan) unityPanMaxY = ejePan.localEulerAngles.y; }
    public void CapturarTiltMinimo() { if (ejeTilt) unityTiltMinZ = ejeTilt.localEulerAngles.z; }
    public void CapturarTiltMaximo() { if (ejeTilt) unityTiltMaxZ = ejeTilt.localEulerAngles.z; }

    void Start()
    {
        StartCoroutine(IntentarSuscripcionSegura());

        // Inicializamos los objetivos con los valores actuales que tengan al arrancar
        if (ejePan) targetAnguloPanY = ejePan.localEulerAngles.y;
        if (ejeTilt) targetAnguloTiltZ = ejeTilt.localEulerAngles.z;
    }

    IEnumerator IntentarSuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;
        MQTTClient.Instance.OnSSCCamaraUpdateEvent += ProcesarTelemetriaCamara;
        Debug.Log("<color=green><b>Cámara SSC:</b> Suscripción con restricción de ejes activada.</color>");
    }

    private void OnDisable()
    {
        if (MQTTClient.Instance != null) MQTTClient.Instance.OnSSCCamaraUpdateEvent -= ProcesarTelemetriaCamara;
    }

    private void ProcesarTelemetriaCamara(float plcPan, float plcTilt)
    {
        // 1. Mapear el valor analógico del PLC al ángulo Y del Pan
        float porcentajePan = Mathf.InverseLerp(plcPanMin, plcPanMax, plcPan);
        targetAnguloPanY = Mathf.LerpAngle(unityPanMinY, unityPanMaxY, porcentajePan);

        // 2. Mapear el valor analógico del PLC al ángulo Z del Tilt
        float porcentajeTilt = Mathf.InverseLerp(plcTiltMin, plcTiltMax, plcTilt);
        targetAnguloTiltZ = Mathf.LerpAngle(unityTiltMinZ, unityTiltMaxZ, porcentajeTilt);
    }

    void Update()
    {
        // --- CONTROL ESTRICTO DEL PAN (Solo Y, X=0, Z=0) ---
        if (ejePan != null)
        {
            // Calculamos el suavizado únicamente en el ángulo Y usando LerpAngle para transiciones limpias
            float currentY = Mathf.LerpAngle(ejePan.localEulerAngles.y, targetAnguloPanY, Time.deltaTime * suavizado);

            // Forzamos explícitamente a que X y Z sean 0 en cada frame
            ejePan.localRotation = Quaternion.Euler(0f, currentY, 0f);
        }

        // --- CONTROL ESTRICTO DEL TILT (Solo Z, X=0, Y=0) ---
        if (ejeTilt != null)
        {
            // Calculamos el suavizado únicamente en el ángulo Z
            float currentZ = Mathf.LerpAngle(ejeTilt.localEulerAngles.z, targetAnguloTiltZ, Time.deltaTime * suavizado);

            // Forzamos explícitamente a que X e Y sean 0 en cada frame
            ejeTilt.localRotation = Quaternion.Euler(0f, 180f, currentZ);
        }
    }
}