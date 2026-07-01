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

    void Start()
    {
        // Al arrancar, el LED es completamente transparente hasta recibir el primer valor
        if (imgCalidadAireLED != null)
        {
            imgCalidadAireLED.color = Color.clear;
        }
    }

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

        // Lógica de rangos para el color del cuadrado LED
        if (txtCalidadAire != null)
        {
            Color colorLED = Color.white;

            if (datos.iaq <= 50)
            {
                colorLED = new Color(0f, 0.75f, 0.1f); // Verde brillante
            }
            else if (datos.iaq <= 100)
            {
                colorLED = new Color(0.5f, 0.85f, 0f); // Verde claro / Lima
            }
            else if (datos.iaq <= 150)
            {
                colorLED = new Color(1f, 0.75f, 0f); // Amarillo / Ámbar
            }
            else if (datos.iaq <= 200)
            {
                colorLED = new Color(1f, 0.4f, 0f); // Naranja
            }
            else if (datos.iaq <= 300)
            {
                colorLED = Color.red; // Rojo
            }
            else // Más de 300
            {
                colorLED = new Color(0.5f, 0f, 0.5f); // Morado / Púrpura
            }

            // Mostramos ÚNICAMENTE el valor numérico bruto del IAQ
            txtCalidadAire.text = $"{datos.iaq}";

            // Cambiamos el color del cuadrado pequeño (vuelve a ser opaco con su color correspondiente)
            if (imgCalidadAireLED != null)
            {
                imgCalidadAireLED.color = colorLED;
            }
        }
    }
}