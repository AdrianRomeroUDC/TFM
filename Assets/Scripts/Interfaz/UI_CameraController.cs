using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class UI_CameraController : MonoBehaviour
{
    // Propiedad global para saber en toda la app si la cámara está encendida
    public static bool IsCameraOn { get; private set; } = false;

    [Header("Componentes de Renderizado Video")]
    public RawImage rawImageVideo;

    [Header("Ajustes Visuales y Paneles")]
    public Image imagenFondoToggle;       // Background del botón ON-OFF (LED)
    public GameObject panelVideoIzquierda; // Panel contenedor de la cámara

    [Header("Componentes de Control")]
    public Toggle toggleCamara;          // Botón ON-OFF
    public Slider sliderFPS;             // Barra de FPS
    public TMP_Dropdown dropdownGrados;   // Dropdown de grados PTU

    [Header("Botones de Movimiento a bloquear")]
    public Button[] botonesPTU;          // Lista de botones PTU

    private Texture2D texturaVideo;
    private string proximaBase64 = "";
    private bool hayNuevaImagen = false;
    private readonly object bloqueoHilo = new object();

    // Colores industriales para el LED
    private readonly Color colorVerdeEncendido = new Color(0.2f, 0.75f, 0.2f, 1f);
    private readonly Color colorRojoApagado = new Color(0.85f, 0.2f, 0.2f, 1f);

    void Start()
    {
        texturaVideo = new Texture2D(2, 2);

        // Estado inicial: APAGADO
        IsCameraOn = false;

        // AUTOMATIZACIÓN: Escuchamos los cambios del Toggle y Slider por código
        if (toggleCamara != null)
        {
            toggleCamara.isOn = false;
            // toggleCamara.onValueChanged.RemoveAllListeners(); 

            toggleCamara.onValueChanged.AddListener(OnToggleCamaraCambiado);
        }

        if (sliderFPS != null)
        {
            sliderFPS.onValueChanged.RemoveAllListeners();
            sliderFPS.onValueChanged.AddListener(OnSliderFpsCambiado);
        }

        // Aplicamos el estado inicial apagado a la UI
        ActualizarInteractividadUI();
        ActualizarVisualesCamara();

        if (rawImageVideo != null)
        {
            rawImageVideo.texture = null;
            rawImageVideo.color = Color.black;
        }

        // Suscripción a eventos MQTT
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnCameraImageEvent += AlRecibirImagenBase64;
        }
    }

    private void OnDestroy()
    {
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnCameraImageEvent -= AlRecibirImagenBase64;
        }
    }

    // =======================================================================
    // GESTIÓN DE EVENTOS DE LA UI
    // =======================================================================

    /// <summary>
    /// Se ejecuta automáticamente cuando el usuario pulsa el Toggle ON/OFF
    /// </summary>
    private void OnToggleCamaraCambiado(bool estadoEncendido)
    {
        IsCameraOn = estadoEncendido;

        ActualizarInteractividadUI();
        ActualizarVisualesCamara();

        // Limpiar pantalla si se apaga
        if (rawImageVideo != null)
        {
            if (!IsCameraOn)
            {
                rawImageVideo.texture = null;
                rawImageVideo.color = Color.black;
            }
            else
            {
                rawImageVideo.color = Color.white;
            }
        }

        // Enviamos la orden por MQTT
        EnviarConfiguracionMqtt();
    }

    /// <summary>
    /// Se ejecuta automáticamente cuando el usuario mueve el Slider de FPS
    /// </summary>
    private void OnSliderFpsCambiado(float valorFps)
    {
        // LA CLAVE: Solo envía a MQTT si la cámara YA ESTÁ ENCENDIDA
        // Esto evita apagar la cámara o enviar comandos cuando está OFF.
        if (IsCameraOn)
        {
            EnviarConfiguracionMqtt();
        }
    }

    /// <summary>
    /// Construye y envía el JSON de configuración a Mosquitto respetando el estado actual
    /// </summary>
    public void EnviarConfiguracionMqtt()
    {
        int fpsSeleccionados = (sliderFPS != null) ? Mathf.RoundToInt(sliderFPS.value) : 15;

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendCameraConfig(IsCameraOn, fpsSeleccionados);
            Debug.Log($"<color=cyan>[MQTT Cámara] Enviado -> Estado: {IsCameraOn}, FPS: {fpsSeleccionados}</color>");
        }
    }

    // =======================================================================
    // PROCESAMIENTO DE IMAGEN BASE64
    // =======================================================================

    private void AlRecibirImagenBase64(string base64Data)
    {
        lock (bloqueoHilo)
        {
            proximaBase64 = base64Data;
            hayNuevaImagen = true;
        }
    }

    void Update()
    {
        string base64ParaProcesar = "";
        bool procesar = false;

        lock (bloqueoHilo)
        {
            if (hayNuevaImagen)
            {
                base64ParaProcesar = proximaBase64;
                hayNuevaImagen = false;
                procesar = true;
            }
        }

        if (procesar && !string.IsNullOrEmpty(base64ParaProcesar) && IsCameraOn)
        {
            PintarTexturaEnUI(base64ParaProcesar);
        }
    }

    private void PintarTexturaEnUI(string base64String)
    {
        try
        {
            if (base64String.Contains(","))
            {
                base64String = base64String.Substring(base64String.IndexOf(",") + 1);
            }

            byte[] imageBytes = Convert.FromBase64String(base64String);
            texturaVideo.LoadImage(imageBytes);

            if (rawImageVideo != null)
            {
                rawImageVideo.texture = texturaVideo;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error al renderizar la imagen: " + e.Message);
        }
    }

    // =======================================================================
    // ACTUALIZACIÓN DE ELEMENTOS VISUALES
    // =======================================================================

    private void ActualizarInteractividadUI()
    {
        if (sliderFPS != null) sliderFPS.interactable = IsCameraOn;
        if (dropdownGrados != null) dropdownGrados.interactable = IsCameraOn;

        if (botonesPTU != null)
        {
            foreach (Button boton in botonesPTU)
            {
                if (boton != null)
                {
                    boton.interactable = IsCameraOn;
                }
            }
        }
    }

    private void ActualizarVisualesCamara()
    {
        /*
        if (imagenFondoToggle != null)
        {
            imagenFondoToggle.color = IsCameraOn ? colorVerdeEncendido : colorRojoApagado;
        }
        */

        if (panelVideoIzquierda != null)
        {
            panelVideoIzquierda.SetActive(IsCameraOn);
        }
    }
}