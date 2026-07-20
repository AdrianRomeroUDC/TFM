using UnityEngine;
using UnityEngine.UI;   // Necesario para Toggle, Slider y Button
using TMPro;            // Necesario para el Dropdown de TextMeshPro
using System;

public class UI_CameraController : MonoBehaviour
{
    // Cambiado a false por defecto para que empiece apagado
    public static bool IsCameraOn { get; private set; } = false;

    [Header("Componentes de Renderizado Video")]
    public RawImage rawImageVideo;

    [Header("Nuevos Ajustes Visuales y Paneles")]
    public Image imagenFondoToggle;       // Arrastra aquí el 'Background' del botón ON-OFF
    public GameObject panelVideoIzquierda; // Arrastra aquí el Panel de la cámara entero

    [Header("Componentes de Control (Se arrastran aquí)")]
    public Toggle toggleCamara;       // Tu botón ON-OFF (LED)
    public Slider sliderFPS;          // Tu barra de FPS
    public TMP_Dropdown dropdownGrados; // El dropdown de los grados

    [Header("Botones de Movimiento a bloquear")]
    public Button[] botonesPTU;       // Lista donde meterás las flechas y el botón Home

    private Texture2D texturaVideo;
    private string proximaBase64 = "";
    private bool hayNuevaImagen = false;
    private readonly object bloqueoHilo = new object();

    // Colores industriales personalizados
    private readonly Color colorVerdeEncendido = new Color(0.2f, 0.75f, 0.2f, 1f);
    private readonly Color colorRojoApagado = new Color(0.85f, 0.2f, 0.2f, 1f);

    void Start()
    {
        texturaVideo = new Texture2D(2, 2);

        // Forzamos el estado a APAGADO al arrancar
        IsCameraOn = false;

        if (toggleCamara != null)
        {
            toggleCamara.isOn = false; // Desmarcamos el Toggle visualmente
        }

        // Aplicamos la interfaz en estado apagado (oculta panel, pone LED rojo y deshabilita botones)
        ActualizarInteractividadUI();
        ActualizarVisualesCamara();

        // Asegurar pantalla en negro al arrancar
        if (rawImageVideo != null)
        {
            rawImageVideo.texture = null;
            rawImageVideo.color = Color.black;
        }

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnCameraImageEvent += AlRecibirImagenBase64;
        }
    }

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

    public void EnviarConfiguracionActual()
    {
        if (toggleCamara == null || sliderFPS == null) return;

        IsCameraOn = toggleCamara.isOn;

        // Cambiar el estado de los botones, el color del botón y la visibilidad del panel
        ActualizarInteractividadUI();
        ActualizarVisualesCamara();

        // =======================================================================
        // LÓGICA DE TEXTURAS: Si se apaga, limpiamos la pantalla y la ponemos en negro
        // =======================================================================
        if (!IsCameraOn)
        {
            if (rawImageVideo != null)
            {
                rawImageVideo.texture = null;
                rawImageVideo.color = Color.black;
            }
        }
        else
        {
            if (rawImageVideo != null)
            {
                rawImageVideo.color = Color.white;
            }
        }

        int fpsSeleccionados = Mathf.RoundToInt(sliderFPS.value);

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendCameraConfig(IsCameraOn, fpsSeleccionados);
        }
    }

    /// <summary>
    /// Activa o desactiva la interacción de todos los mandos según el estado de la cámara
    /// </summary>
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

    /// <summary>
    /// Controla de forma centralizada el color del Toggle y la visibilidad del panel
    /// </summary>
    private void ActualizarVisualesCamara()
    {
        // 1. Cambiar el color del botón (Verde si está ON, Rojo si está OFF)
        if (imagenFondoToggle != null)
        {
            imagenFondoToggle.color = IsCameraOn ? colorVerdeEncendido : colorRojoApagado;
        }

        // 2. Mostrar u ocultar el panel entero
        if (panelVideoIzquierda != null)
        {
            panelVideoIzquierda.SetActive(IsCameraOn);
        }
    }

    private void OnDestroy()
    {
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnCameraImageEvent -= AlRecibirImagenBase64;
        }
    }
}