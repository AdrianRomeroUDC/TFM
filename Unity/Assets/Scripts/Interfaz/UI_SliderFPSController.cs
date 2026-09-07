using UnityEngine;
using UnityEngine.UI;
using TMPro; // Necesario para modificar el texto de TextMesh Pro

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
