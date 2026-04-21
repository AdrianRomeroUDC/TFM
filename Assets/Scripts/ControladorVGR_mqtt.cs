using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System;
using System.Text;
using System.Security.Authentication;

[Serializable]
public class VGRData
{
    public float estirar;
    public float rotacion;
    public float vertical;
}

public class ControladorVGR_mqtt : MonoBehaviour
{
    private MqttClient client;
    private float lastRot, lastVert, lastExt;

    [Header("Referencias")]
    public Transform ejeRotacion;
    public Transform ejeVertical;
    public Transform ejeExtension;

    [Header("Calibración PLC (Ajustado a tus Logs)")]
    public float plcRot_Min = 1395;
    public float plcRot_Max = 21;
    public float plcVert_Min = 20;
    public float plcVert_Max = 1272;
    public float plcExt_Min = 40;
    public float plcExt_Max = 1210;

    [Header("Calibración Unity (Capturar con Click Derecho)")]
    [ContextMenuItem("Capturar", "CapturarRotMin")] public float unityRot_Min;
    [ContextMenuItem("Capturar", "CapturarRotMax")] public float unityRot_Max;
    [ContextMenuItem("Capturar", "CapturarVertMin")] public float unityVert_Min;
    [ContextMenuItem("Capturar", "CapturarVertMax")] public float unityVert_Max;
    [ContextMenuItem("Capturar", "CapturarExtMin")] public float unityExt_Min;
    [ContextMenuItem("Capturar", "CapturarExtMax")] public float unityExt_Max;

    [Header("Ajustes")]
    public float lerpSpeed = 5f;

    void CapturarRotMin() => unityRot_Min = ejeRotacion.localEulerAngles.y;
    void CapturarRotMax() => unityRot_Max = ejeRotacion.localEulerAngles.y;
    void CapturarVertMin() => unityVert_Min = ejeVertical.localPosition.y;
    void CapturarVertMax() => unityVert_Max = ejeVertical.localPosition.y;
    void CapturarExtMin() => unityExt_Min = ejeExtension.localPosition.x;
    void CapturarExtMax() => unityExt_Max = ejeExtension.localPosition.x;

    void Start() => Connect();

    void Update()
    {
        float speed = lerpSpeed * Time.deltaTime;

        // ROTACIÓN: Cálculo lineal para evitar efecto espejo
        if (ejeRotacion)
        {
            float t = Mathf.InverseLerp(plcRot_Min, plcRot_Max, lastRot);
            // Forzamos el ángulo sin usar LerpAngle para que no busque el camino corto
            float targetAngle = unityRot_Min + (unityRot_Max - unityRot_Min) * t;
            ejeRotacion.localRotation = Quaternion.Slerp(ejeRotacion.localRotation, Quaternion.Euler(0, targetAngle, 0), speed);
        }

        // VERTICAL
        if (ejeVertical)
        {
            float tV = Mathf.InverseLerp(plcVert_Min, plcVert_Max, lastVert);
            float targetY = Mathf.Lerp(unityVert_Min, unityVert_Max, tV);
            ejeVertical.localPosition = Vector3.Lerp(ejeVertical.localPosition, new Vector3(ejeVertical.localPosition.x, targetY, ejeVertical.localPosition.z), speed);
        }

        // EXTENSIÓN
        if (ejeExtension)
        {
            float tE = Mathf.InverseLerp(plcExt_Min, plcExt_Max, lastExt);
            float targetX = Mathf.Lerp(unityExt_Min, unityExt_Max, tE);
            ejeExtension.localPosition = Vector3.Lerp(ejeExtension.localPosition, new Vector3(targetX, ejeExtension.localPosition.y, ejeExtension.localPosition.z), speed);
        }
    }

    void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        try
        {
            string json = Encoding.UTF8.GetString(e.Message);
            VGRData data = JsonUtility.FromJson<VGRData>(json);
            lastRot = data.rotacion; lastVert = data.vertical; lastExt = data.estirar;
        }
        catch { }
    }

    void Connect()
    {
        try
        {
            client = new MqttClient("4ca80baa3731405580bfa27dc37e6665.s1.eu.hivemq.cloud", 8883, true, null, null, MqttSslProtocols.TLSv1_2);
            client.Connect(Guid.NewGuid().ToString(), "LearningFactory", "Fischertechnik1");
            client.Subscribe(new string[] { "f/pos_vgr" }, new byte[] { 0 });
            client.MqttMsgPublishReceived += OnMessageReceived;
        }
        catch { }
    }
}