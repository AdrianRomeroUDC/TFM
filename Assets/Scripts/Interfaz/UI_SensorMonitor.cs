using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_SensorsMonitor : MonoBehaviour
{
    [Header("Referencias de Texto")]
    public TextMeshProUGUI txtTemperatura;
    public TextMeshProUGUI txtHumedad;
    public TextMeshProUGUI txtPresion;
    public TextMeshProUGUI txtLuminosidad;
    public TextMeshProUGUI txtCalidadAire;

    [Header("Indicador Visual")]
    public Image imgCalidadAireLED;

    void OnEnable()
    {
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnLdrLightEvent += ActualizarLDR;
            MQTT_InterfaceClient.Instance.OnBmeEnvironmentEvent += ActualizarBME680;
        }
    }

    void OnDisable()
    {
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnLdrLightEvent -= ActualizarLDR;
            MQTT_InterfaceClient.Instance.OnBmeEnvironmentEvent -= ActualizarBME680;
        }
    }

    private void ActualizarLDR(LdrPayload datos)
    {
        if (txtLuminosidad != null) txtLuminosidad.text = $"{datos.br:F1} %";
    }

    private void ActualizarBME680(Bme680Payload datos)
    {
        if (txtTemperatura != null) txtTemperatura.text = $"{datos.t:F1} °C";
        if (txtHumedad != null) txtHumedad.text = $"{datos.h:F1} %";
        if (txtPresion != null) txtPresion.text = $"{datos.p:F1} hPa";

        // Lógica de calidad del aire
        if (txtCalidadAire != null)
        {
            txtCalidadAire.text = datos.aq <= 2 ? "Óptima" : (datos.aq <= 4 ? "Media" : "Baja");
        }

        if (imgCalidadAireLED != null)
        {
            imgCalidadAireLED.color = datos.aq <= 2 ? Color.green : (datos.aq <= 4 ? new Color(1f, 0.75f, 0f) : Color.red);
        }
    }
}