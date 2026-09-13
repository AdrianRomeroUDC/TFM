using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;
using System.Collections;

/// <summary>
/// Muestra en la interfaz, en tiempo real, las lecturas de los sensores ambientales de la
/// estación SSC de la fábrica: el sensor BME680 (temperatura, humedad, presión y calidad
/// del aire IAQ) y el sensor LDR (luminosidad).
///
/// Este script NO lee los sensores directamente: se limita a escuchar los eventos que
/// dispara <see cref="MQTT_InterfaceClient"/> cuando llegan datos nuevos desde la fábrica
/// real por MQTT, y a pintar esos valores en los textos correspondientes de la pantalla.
///
/// También controla un interruptor (Toggle) que permite mostrar u ocultar el panel de
/// sensores, y un desplegable (Dropdown) para elegir cada cuánto tiempo queremos que la
/// fábrica nos envíe nuevas lecturas (por ejemplo, cada 5 o cada 10 segundos).
/// </summary>
public class UI_SensorsMonitor : MonoBehaviour
{
    [Header("--- Referencias de Texto ---")]
    public TextMeshProUGUI txtTemperatura;
    public TextMeshProUGUI txtHumedad;
    public TextMeshProUGUI txtPresion;
    public TextMeshProUGUI txtLuminosidad;
    public TextMeshProUGUI txtCalidadAire;

    [Header("--- Indicador Visual IAQ ---")]
    public Image imgCalidadAireLED; // Círculo/LED que cambia de color según la calidad del aire (IAQ)

    [Header("--- Control de Panel y Toggle ---")]
    public Toggle toggleSensores;          // El botón ON/OFF de los sensores
    public Image imagenFondoToggle;        // El 'Background' del botón ON/OFF
    public GameObject panelSensores;       // El panel con la información gráfica de los sensores
    public TMP_Dropdown dropdownPeriodo;   // Dropdown del tiempo de muestreo (05s, 10s, etc.)

    // Colores de estado para el botón Toggle (Verde / Rojo)
    private readonly Color colorVerdeEncendido = new Color(0.2f, 0.75f, 0.2f, 1f);
    private readonly Color colorRojoApagado = new Color(0.85f, 0.2f, 0.2f, 1f);

    /// <summary>
    /// Configura el panel al arrancar la escena: deja el LED de calidad de aire transparente
    /// hasta que llegue el primer dato, engancha los eventos de los controles (Toggle y
    /// Dropdown), se suscribe a los eventos MQTT de sensores, y envía a la fábrica el
    /// período de muestreo inicial.
    /// </summary>
    void Start()
    {
        // 1. Al arrancar, el LED de IAQ es transparente hasta recibir la primera lectura
        if (imgCalidadAireLED != null)
        {
            imgCalidadAireLED.color = Color.clear;
        }

        // 2. Configuración inicial del Dropdown
        // Quitamos primero el listener (por si ya estuviera puesto) y lo volvemos a añadir,
        // así evitamos que se duplique la suscripción si Start() se llamara más de una vez
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

            // Si el interruptor visual (UI_ToggleSwitch) está en el mismo objeto que el Toggle,
            // le decimos que se coloque de inmediato en la posición correcta (sin animación)
            UI_ToggleSwitch switchComp = toggleSensores.GetComponent<UI_ToggleSwitch>();
            if (switchComp != null)
            {
                switchComp.ActualizarEstadoInstantaneo(toggleSensores.isOn);
            }

            ToggleMostrarPanel(toggleSensores.isOn);
        }

        // 4. SUSCRIPCIÓN CONTINUA A EVENTOS MQTT (En Start para que no se desconecte al cerrar UI)
        // Nos suscribimos a los eventos que MQTT_InterfaceClient dispara cada vez que llega
        // una lectura nueva del sensor LDR (luz) o del sensor BME680 (ambiente) desde la fábrica real
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnLdrLightEvent += ActualizarLDR;
            MQTT_InterfaceClient.Instance.OnBmeEnvironmentEvent += ActualizarBME680;
        }

        // 5. Enviar el período inicial a MQTT
        StartCoroutine(EnviarPeriodoInicialMqtt());
    }

    /// <summary>
    /// Al destruirse este objeto, nos desuscribimos de los eventos MQTT para evitar que el
    /// evento intente llamar a un método de un objeto que ya no existe (lo que provocaría errores).
    /// </summary>
    private void OnDestroy()
    {
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnLdrLightEvent -= ActualizarLDR;
            MQTT_InterfaceClient.Instance.OnBmeEnvironmentEvent -= ActualizarBME680;
        }
    }

    /// <summary>
    /// Corrutina que espera una pequeña fracción de segundo antes de enviar el período de
    /// muestreo inicial. La espera da tiempo a que el cliente MQTT termine de conectarse
    /// antes de intentar mandarle el mensaje.
    /// </summary>
    private IEnumerator EnviarPeriodoInicialMqtt()
    {
        yield return new WaitForSeconds(0.2f);
        int indiceInicial = (dropdownPeriodo != null) ? dropdownPeriodo.value : 0;
        CambiarPeriodoSensores(indiceInicial);
    }

    // ====================================================================
    // CONTROL DEL PANEL E INTERACTIVIDAD
    // ====================================================================

    /// <summary>
    /// Muestra u oculta el panel de sensores según el estado del interruptor, y actualiza
    /// el color de fondo del propio interruptor (verde si está activo, rojo si no).
    /// También activa o desactiva el desplegable de período, ya que no tiene sentido poder
    /// cambiarlo si los sensores están apagados.
    /// </summary>
    /// <param name="estaActivo">true si el interruptor de sensores está encendido.</param>
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

    /// <summary>
    /// Se llama cuando el usuario cambia la opción seleccionada en el desplegable de período.
    /// Extrae el número de segundos del texto de la opción elegida (por ejemplo, de "10s"
    /// se queda solo con "10") y lo envía a la fábrica por MQTT.
    /// </summary>
    /// <param name="index">Índice de la opción seleccionada dentro del Dropdown.</param>
    public void CambiarPeriodoSensores(int index)
    {
        int segundos = 5;

        if (dropdownPeriodo != null && dropdownPeriodo.options.Count > index)
        {
            string textoOpcion = dropdownPeriodo.options[index].text;
            // Nos quedamos solo con los dígitos del texto (quitamos la "s" de segundos, espacios, etc.)
            string soloNumeros = Regex.Replace(textoOpcion, @"[^\d]", "");

            if (!int.TryParse(soloNumeros, out segundos))
            {
                segundos = 5;
            }
        }

        EnviarPeriodosAMqtt(segundos);
    }

    /// <summary>
    /// Envía a la fábrica, por MQTT, el nuevo período de muestreo tanto para el sensor LDR
    /// como para el sensor BME680, de forma que ambos empiecen a mandar datos con esa frecuencia.
    /// </summary>
    /// <param name="segundos">Segundos que deben pasar entre lectura y lectura.</param>
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

    /// <summary>
    /// Se ejecuta automáticamente cada vez que llega una lectura nueva del sensor LDR
    /// (luminosidad) desde la fábrica real. Actualiza el texto de luminosidad en pantalla.
    /// </summary>
    /// <param name="datos">Datos recibidos del sensor LDR (incluye el porcentaje de brillo).</param>
    private void ActualizarLDR(LdrPayload datos)
    {
        // Se procesa siempre que el toggle de sensores esté activo
        if (toggleSensores != null && !toggleSensores.isOn) return;
        if (txtLuminosidad != null) txtLuminosidad.text = $"{datos.br:F1} %";
    }

    /// <summary>
    /// Se ejecuta automáticamente cada vez que llega una lectura nueva del sensor BME680
    /// (temperatura, humedad, presión y calidad del aire) desde la fábrica real.
    /// Actualiza los textos correspondientes y el color del LED de calidad de aire (IAQ).
    /// </summary>
    /// <param name="datos">Datos recibidos del sensor BME680.</param>
    private void ActualizarBME680(Bme680Payload datos)
    {
        // Se procesa siempre que el toggle de sensores esté activo
        if (toggleSensores != null && !toggleSensores.isOn) return;

        if (txtTemperatura != null) txtTemperatura.text = $"{datos.t:F1} °C";
        if (txtHumedad != null) txtHumedad.text = $"{datos.h:F1} %";
        if (txtPresion != null) txtPresion.text = $"{datos.p:F1} hPa";

        if (txtCalidadAire != null)
        {
            Color colorLED = Color.white;

            // El índice IAQ (Índice de Calidad del Aire) es un número: cuanto más bajo, mejor
            // es el aire. Aquí traducimos ese número a un color, como si fuera un semáforo:
            // verde (aire bueno) -> amarillo/naranja (aire moderado) -> rojo/morado (aire malo)
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
