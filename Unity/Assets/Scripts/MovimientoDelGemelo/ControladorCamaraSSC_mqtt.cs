using UnityEngine;
using System.Collections;

/// <summary>
/// Controla el gemelo digital de la cámara Pan-Tilt de la estación SSC (Sensor Station Camera),
/// la cámara que vigila la fábrica y que puede girar en horizontal ("pan") y en vertical ("tilt").
/// Este script recibe por MQTT los valores "crudos" que manda el PLC (el autómata real) para
/// cada uno de los dos motores de la cámara, los convierte a los ángulos de rotación que usa el
/// modelo 3D de Unity, y mueve de forma suave los dos ejes (el soporte de giro horizontal y el
/// soporte de cabeceo vertical) para que la cámara virtual apunte exactamente hacia donde apunta
/// la cámara física real.
/// </summary>
public class ControladorCamaraSSC : MonoBehaviour
{
    [Header("Estructura de la Cámara")]
    [Tooltip("El soporte que SOLO gira horizontalmente (Eje Y)")]
    public Transform ejePan;
    [Tooltip("El soporte que SOLO cabecea verticalmente (Eje Z)")]
    public Transform ejeTilt;

    [Header("Rango de Entrada del PLC")]
    // Rango de valores "crudos" (unidades del autómata real) que puede enviar el PLC para
    // cada eje: el mínimo y el máximo que puede llegar a marcar el motor real.
    public float plcPanMin = 0f;
    public float plcPanMax = 2000f;
    public float plcTiltMin = 0f;
    public float plcTiltMax = 1000f;

    [Header("Captura de Ángulo PAN (Solo importa el valor Y)")]
    // Ángulos del modelo 3D de Unity que corresponden a los extremos del movimiento real del
    // pan. Se rellenan a mano desde el Inspector, pulsando el botón de contexto que llama a los
    // métodos CapturarPanMinimo/CapturarPanMaximo con la cámara colocada físicamente en cada extremo.
    [ContextMenuItem("Capturar PAN Mínimo", "CapturarPanMinimo")]
    public float unityPanMinY;
    [ContextMenuItem("Capturar PAN Máximo", "CapturarPanMaximo")]
    public float unityPanMaxY;

    [Header("Captura de Ángulo TILT (Solo importa el valor Z)")]
    // Lo mismo que arriba, pero para el eje de tilt (cabeceo vertical).
    [ContextMenuItem("Capturar TILT Mínimo", "CapturarTiltMinimo")]
    public float unityTiltMinZ;
    [ContextMenuItem("Capturar TILT Máximo", "CapturarTiltMaximo")]
    public float unityTiltMaxZ;

    [Header("Suavizado de Movimiento")]
    public float suavizado = 5f; // Cuanto más alto, más rápido "alcanza" la cámara virtual el ángulo real recibido por MQTT.

    // Objetivos flotantes a los que deben llegar los ángulos específicos
    // Guardan el ángulo (en grados, escala de Unity) hacia el que debe moverse cada eje; se
    // actualizan al recibir un mensaje MQTT y se persiguen suavemente en Update().
    private float targetAnguloPanY;
    private float targetAnguloTiltZ;

    // --- MÉTODOS DE CAPTURA CON CLIC DERECHO ---
    // Extraen exclusivamente el eje que te interesa independientemente de lo que marque el Inspector
    // Estos 4 métodos se ejecutan desde el menú contextual (clic derecho) del Inspector: leen el
    // ángulo actual del modelo 3D y lo guardan como referencia de calibración para ese extremo.
    public void CapturarPanMinimo() { if (ejePan) unityPanMinY = ejePan.localEulerAngles.y; }
    public void CapturarPanMaximo() { if (ejePan) unityPanMaxY = ejePan.localEulerAngles.y; }
    public void CapturarTiltMinimo() { if (ejeTilt) unityTiltMinZ = ejeTilt.localEulerAngles.z; }
    public void CapturarTiltMaximo() { if (ejeTilt) unityTiltMaxZ = ejeTilt.localEulerAngles.z; }

    void Start()
    {
        // Esperamos de forma segura a que exista el cliente MQTT antes de suscribirnos.
        StartCoroutine(IntentarSuscripcionSegura());

        // Inicializamos los objetivos con los valores actuales que tengan al arrancar
        // (para que la cámara no dé un salto brusco nada más empezar la escena).
        if (ejePan) targetAnguloPanY = ejePan.localEulerAngles.y;
        if (ejeTilt) targetAnguloTiltZ = ejeTilt.localEulerAngles.z;
    }

    // Corrutina que espera, frame a frame, a que el cliente MQTT central esté listo en la
    // escena, y entonces se suscribe al evento de telemetría de la cámara de la SSC.
    IEnumerator IntentarSuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;
        MQTTClient.Instance.OnSSCCamaraUpdateEvent += ProcesarTelemetriaCamara;
        Debug.Log("<color=green><b>Cámara SSC:</b> Suscripción con restricción de ejes activada.</color>");
    }

    private void OnDisable()
    {
        // Nos desuscribimos al desactivar el objeto para evitar llamadas a un objeto ya inactivo.
        if (MQTTClient.Instance != null) MQTTClient.Instance.OnSSCCamaraUpdateEvent -= ProcesarTelemetriaCamara;
    }

    /// <summary>
    /// Recibe los valores "crudos" de pan y tilt que manda el PLC real de la cámara y los
    /// convierte a los ángulos equivalentes del modelo 3D de Unity, guardándolos como el nuevo
    /// objetivo hacia el que se moverán los ejes en <see cref="Update"/>.
    /// </summary>
    /// <param name="plcPan">Valor bruto del eje de giro horizontal (pan) tal y como lo manda el PLC real.</param>
    /// <param name="plcTilt">Valor bruto del eje de cabeceo vertical (tilt) tal y como lo manda el PLC real.</param>
    private void ProcesarTelemetriaCamara(float plcPan, float plcTilt)
    {
        // 1. Mapear el valor analógico del PLC al ángulo Y del Pan
        // Primero calculamos qué porcentaje (0 a 1) representa el valor del PLC dentro de su
        // rango real, y luego aplicamos ese mismo porcentaje al rango de ángulos de Unity.
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
            // (para que el soporte de pan nunca se incline ni gire en un eje que no le corresponde).
            ejePan.localRotation = Quaternion.Euler(0f, currentY, 0f);
        }

        // --- CONTROL ESTRICTO DEL TILT (Solo Z, X=0, Y=0) ---
        if (ejeTilt != null)
        {
            // Calculamos el suavizado únicamente en el ángulo Z
            float currentZ = Mathf.LerpAngle(ejeTilt.localEulerAngles.z, targetAnguloTiltZ, Time.deltaTime * suavizado);

            // Forzamos explícitamente a que X e Y sean 0 en cada frame
            // (el 180 en Y es un ajuste fijo de orientación del modelo, no algo que cambie con MQTT).
            ejeTilt.localRotation = Quaternion.Euler(0f, 180f, currentZ);
        }
    }
}
