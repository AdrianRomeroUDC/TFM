using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;

public class UI_SensorsMonitor : MonoBehaviour
{
    [Header("--- Referencias de Texto ---")]
    public TextMeshProUGUI txtTemperatura;
    public TextMeshProUGUI txtHumedad;
    public TextMeshProUGUI txtPresion;
    public TextMeshProUGUI txtLuminosidad;
    public TextMeshProUGUI txtCalidadAire;

    [Header("--- Indicador Visual IAQ ---")]
    public Image imgCalidadAireLED;

    [Header("--- Control de Panel y Toggle ---")]
    public Toggle toggleSensores;          // El botón ON/OFF de los sensores
    public Image imagenFondoToggle;        // El 'Background' del botón ON/OFF
    public GameObject panelSensores;       // El panel con la información gráfica de los sensores
    public TMP_Dropdown dropdownPeriodo;   // Dropdown del tiempo de muestreo

    // Colores de estado para el botón Toggle (Verde / Rojo)
    private readonly Color colorVerdeEncendido = new Color(0.2f, 0.75f, 0.2f, 1f);
    private readonly Color colorRojoApagado = new Color(0.85f, 0.2f, 0.2f, 1f);

    void Start()
    {
        // 1. Al arrancar, el LED de IAQ es transparente hasta recibir la primera lectura
        if (imgCalidadAireLED != null)
        {
            imgCalidadAireLED.color = Color.clear;
        }

        // 2. Configuración inicial del Dropdown
        if (dropdownPeriodo != null)
        {
            dropdownPeriodo.onValueChanged.AddListener(CambiarPeriodoSensores);
            CambiarPeriodoSensores(dropdownPeriodo.value);
        }

        // 3. Configuración inicial del Toggle
        if (toggleSensores != null)
        {
            toggleSensores.onValueChanged.AddListener(ToggleMostrarPanel);
            ToggleMostrarPanel(toggleSensores.isOn); // Aplicar estado inicial
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

    // ====================================================================
    // CONTROL DEL PANEL Y INTERACTIVIDAD
    // ====================================================================

    /// <summary>
    /// Activa/desactiva el panel flotante y actualiza los indicadores visuales
    /// </summary>
    public void ToggleMostrarPanel(bool estaActivo)
    {
        // 1. Mostrar u ocultar el panel entero de la interfaz
        if (panelSensores != null)
        {
            panelSensores.SetActive(estaActivo);
        }

        // 2. Cambiar el color del botón Toggle (Verde si está activo, Rojo si está apagado)
        if (imagenFondoToggle != null)
        {
            imagenFondoToggle.color = estaActivo ? colorVerdeEncendido : colorRojoApagado;
        }

        // 3. Bloquear el dropdown si los sensores están desactivados
        if (dropdownPeriodo != null)
        {
            dropdownPeriodo.interactable = estaActivo;
        }
    }

    /// <summary>
    /// Envia por MQTT el periodo de muestreo seleccionado en el Dropdown
    /// </summary>
    public void CambiarPeriodoSensores(int index)
    {
        if (dropdownPeriodo == null) return;

        string textoOpcion = dropdownPeriodo.options[index].text;
        string soloNumeros = Regex.Replace(textoOpcion, @"[^\d]", "");

        if (int.TryParse(soloNumeros, out int segundos))
        {
            if (MQTT_InterfaceClient.Instance != null)
            {
                MQTT_InterfaceClient.Instance.SendLdrPeriod(segundos);
                MQTT_InterfaceClient.Instance.SendBme680Period(segundos);
                Debug.Log($"<color=yellow>[MQTT] Periodo de sensores actualizado a {segundos}s</color>");
            }
        }
    }

    // ====================================================================
    // LECTURA DE TELEMETRÍA (LDR Y BME680)
    // ====================================================================

    private void ActualizarLDR(LdrPayload datos)
    {
        if (txtLuminosidad != null) txtLuminosidad.text = $"{datos.br:F1} %";
    }

    private void ActualizarBME680(Bme680Payload datos)
    {
        if (txtTemperatura != null) txtTemperatura.text = $"{datos.t:F1} °C";
        if (txtHumedad != null) txtHumedad.text = $"{datos.h:F1} %";
        if (txtPresion != null) txtPresion.text = $"{datos.p:F1} hPa";

        // Lógica de rangos para el color del cuadrado LED de IAQ
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

            txtCalidadAire.text = $"{datos.iaq}";

            if (imgCalidadAireLED != null)
            {
                imgCalidadAireLED.color = colorLED;
            }
        }
    }
}