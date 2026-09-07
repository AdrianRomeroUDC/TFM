using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;
using System.Collections;

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
    public TMP_Dropdown dropdownPeriodo;   // Dropdown del tiempo de muestreo (05s, 10s, etc.)

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
            dropdownPeriodo.onValueChanged.RemoveListener(CambiarPeriodoSensores);
            dropdownPeriodo.onValueChanged.AddListener(CambiarPeriodoSensores);
        }

        // 3. Configuración inicial del Toggle
        if (toggleSensores != null)
        {
            toggleSensores.onValueChanged.RemoveListener(ToggleMostrarPanel);
            toggleSensores.onValueChanged.AddListener(ToggleMostrarPanel);

            UI_ToggleSwitch switchComp = toggleSensores.GetComponent<UI_ToggleSwitch>();
            if (switchComp != null)
            {
                switchComp.ActualizarEstadoInstantaneo(toggleSensores.isOn);
            }

            ToggleMostrarPanel(toggleSensores.isOn);
        }

        // 🟢 4. SUSCRIPCIÓN CONTINUA A EVENTOS MQTT (En Start para que no se desconecte al cerrar UI)
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnLdrLightEvent += ActualizarLDR;
            MQTT_InterfaceClient.Instance.OnBmeEnvironmentEvent += ActualizarBME680;
        }

        // 5. Enviar el período inicial a MQTT
        StartCoroutine(EnviarPeriodoInicialMqtt());
    }

    private void OnDestroy()
    {
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnLdrLightEvent -= ActualizarLDR;
            MQTT_InterfaceClient.Instance.OnBmeEnvironmentEvent -= ActualizarBME680;
        }
    }

    private IEnumerator EnviarPeriodoInicialMqtt()
    {
        yield return new WaitForSeconds(0.2f);
        int indiceInicial = (dropdownPeriodo != null) ? dropdownPeriodo.value : 0;
        CambiarPeriodoSensores(indiceInicial);
    }

    // ====================================================================
    // CONTROL DEL PANEL E INTERACTIVIDAD
    // ====================================================================

    public void ToggleMostrarPanel(bool estaActivo)
    {
        if (panelSensores != null)
        {
            panelSensores.SetActive(estaActivo);
        }

        if (imagenFondoToggle != null)
        {
            imagenFondoToggle.color = estaActivo ? colorVerdeEncendido : colorRojoApagado;
        }

        if (dropdownPeriodo != null)
        {
            dropdownPeriodo.interactable = estaActivo;
        }
    }

    public void CambiarPeriodoSensores(int index)
    {
        int segundos = 5;

        if (dropdownPeriodo != null && dropdownPeriodo.options.Count > index)
        {
            string textoOpcion = dropdownPeriodo.options[index].text;
            string soloNumeros = Regex.Replace(textoOpcion, @"[^\d]", "");

            if (!int.TryParse(soloNumeros, out segundos))
            {
                segundos = 5;
            }
        }

        EnviarPeriodosAMqtt(segundos);
    }

    private void EnviarPeriodosAMqtt(int segundos)
    {
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendLdrPeriod(segundos);
            MQTT_InterfaceClient.Instance.SendBme680Period(segundos);
            Debug.Log($"<color=yellow>[MQTT] Periodo enviado a 'c/ldr' y 'c/bme680': {segundos}s</color>");
        }
    }

    // ====================================================================
    // LECTURA DE TELEMETRÍA (LDR Y BME680)
    // ====================================================================

    private void ActualizarLDR(LdrPayload datos)
    {
        // Se procesa siempre que el toggle de sensores esté activo
        if (toggleSensores != null && !toggleSensores.isOn) return;
        if (txtLuminosidad != null) txtLuminosidad.text = $"{datos.br:F1} %";
    }

    private void ActualizarBME680(Bme680Payload datos)
    {
        // 🟢 Se procesa siempre que el toggle de sensores esté activo
        if (toggleSensores != null && !toggleSensores.isOn) return;

        if (txtTemperatura != null) txtTemperatura.text = $"{datos.t:F1} °C";
        if (txtHumedad != null) txtHumedad.text = $"{datos.h:F1} %";
        if (txtPresion != null) txtPresion.text = $"{datos.p:F1} hPa";

        if (txtCalidadAire != null)
        {
            Color colorLED = Color.white;

            if (datos.iaq <= 50) colorLED = new Color(0f, 0.75f, 0.1f);
            else if (datos.iaq <= 100) colorLED = new Color(0.5f, 0.85f, 0f);
            else if (datos.iaq <= 150) colorLED = new Color(1f, 0.75f, 0f);
            else if (datos.iaq <= 200) colorLED = new Color(1f, 0.4f, 0f);
            else if (datos.iaq <= 300) colorLED = Color.red;
            else colorLED = new Color(0.5f, 0f, 0.5f);

            txtCalidadAire.text = $"{datos.iaq}";

            if (imgCalidadAireLED != null)
            {
                imgCalidadAireLED.color = colorLED;
            }
        }
    }
}