using UnityEngine;
using UnityEngine.UI;
using TMPro; // Necesario para modificar el texto de TextMesh Pro

/// <summary>
/// Controla el deslizador (Slider) de la interfaz que permite elegir a cuántos
/// fotogramas por segundo (FPS) queremos recibir el vídeo en directo de la cámara
/// de la fábrica (estación SSC). Actualiza el texto en pantalla mientras el usuario
/// mueve el slider, y puede enviar el valor elegido a la fábrica por MQTT a través
/// de <see cref="MQTT_InterfaceClient"/>.
/// </summary>
public class UI_SliderFPSController : MonoBehaviour
{
    [Header("Referencias UI")]
    public Slider fpsSlider;           // Arrastra aquí tu Slider
    public TextMeshProUGUI textoFPS;   // Arrastra aquí el texto "FPS VIDEO (1-15)"

    void Start()
    {
        if (fpsSlider != null)
        {
            // Escuchamos el evento nativo del Slider cuando el usuario lo mueve
            fpsSlider.onValueChanged.AddListener(OnSliderValueChanged);

            // Forzamos la primera actualización con el valor inicial al arrancar
            OnSliderValueChanged(fpsSlider.value);
        }
    }

    // Esta función se ejecuta automáticamente cada vez que se mueve el Slider
    /// <summary>
    /// Se ejecuta cada vez que el usuario arrastra el slider. Solo actualiza el texto
    /// que se ve en pantalla (todavía no envía nada a la fábrica).
    /// </summary>
    /// <param name="valor">Valor actual del slider, en formato decimal (float).</param>
    void OnSliderValueChanged(float valor)
    {
        // Convertimos el float a un número entero de forma segura
        int valorEntero = Mathf.RoundToInt(valor);

        // Actualizamos el texto en pantalla
        if (textoFPS != null)
        {
            textoFPS.text = $"FPS VIDEO: {valorEntero}";
        }
    }

    // =======================================================================
    // OPCIONAL: FUNCIÓN PARA EL BOTÓN DE ENVIAR (O AL SOLTAR EL SLIDER)
    // =======================================================================
    // Puedes llamar a esta función para enviar el nuevo valor por MQTT
    /// <summary>
    /// Envía a la fábrica, por MQTT, la configuración actual de la cámara: si debe
    /// estar encendida o apagada y a cuántos FPS debe transmitir el vídeo. El valor
    /// de FPS se lee directamente del slider en el momento de la llamada.
    /// </summary>
    /// <param name="camaraEncendida">true si la cámara debe quedar encendida, false si debe apagarse.</param>
    public void EnviarConfiguracionMqtt(bool camaraEncendida)
    {
        int fpsActuales = Mathf.RoundToInt(fpsSlider.value);

        // Usamos tu magnífico Singleton para enviar el dato al broker
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendCameraConfig(camaraEncendida, fpsActuales);
        }
    }
}
