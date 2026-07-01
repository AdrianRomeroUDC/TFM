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

        // Lógica de rangos para el IAQ y el color del cuadrado LED
        if (txtCalidadAire != null)
        {
            string estadoAire = "";
            Color colorLED = Color.white;

            if (datos.iaq <= 50)
            {
                estadoAire = "Excelente";
                colorLED = new Color(0f, 0.75f, 0.1f); // Verde brillante
            }
            else if (datos.iaq <= 100)
            {
                estadoAire = "Buena";
                colorLED = new Color(0.5f, 0.85f, 0f); // Verde claro / Lima
            }
            else if (datos.iaq <= 150)
            {
                estadoAire = "Moderada";
                colorLED = new Color(1f, 0.75f, 0f); // Amarillo / Ámbar
            }
            else if (datos.iaq <= 200)
            {
                estadoAire = "Mala";
                colorLED = new Color(1f, 0.4f, 0f); // Naranja
            }
            else if (datos.iaq <= 300)
            {
                estadoAire = "Muy mala";
                colorLED = Color.red; // Rojo
            }
            else // Más de 300
            {
                estadoAire = "Severa";
                colorLED = new Color(0.5f, 0f, 0.5f); // Morado / Púrpura
            }

            // Mostramos el valor numérico del IAQ seguido del estado entre paréntesis
            txtCalidadAire.text = $"{datos.iaq} ({estadoAire})";

            // Cambiamos el color del cuadrado pequeño (Image) asignado en el inspector
            if (imgCalidadAireLED != null)
            {
                imgCalidadAireLED.color = colorLED;
            }
        }
    }
}