using UnityEngine;
using UnityEngine.UI;   // Necesario para Toggle, Slider y Button
using TMPro;            // Necesario para el Dropdown de TextMeshPro
using System;

public class UI_CameraController : MonoBehaviour
{
    public static bool IsCameraOn { get; private set; } = true;

    [Header("Componentes de Renderizado Video")]
    public RawImage rawImageVideo;

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

    void Start()
    {
        texturaVideo = new Texture2D(2, 2);

        if (toggleCamara != null)
        {
            IsCameraOn = toggleCamara.isOn;
        }

        ActualizarInteractividadUI();

        // NUEVO: Asegurar negro al arrancar si el toggle está desactivado
        if (!IsCameraOn && rawImageVideo != null)
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

        // Cambiar el estado de los botones (interactivos o grises)
        ActualizarInteractividadUI();

        // =======================================================================
        // NUEVA LÓGICA: Si se apaga, limpiamos la pantalla y la ponemos en negro
        // =======================================================================
        if (!IsCameraOn)
        {
            if (rawImageVideo != null)
            {
                // Opción A: Dejar la textura vacía (se vuelve del color base, por defecto negro/gris)
                rawImageVideo.texture = null;

                // Opción B (Opcional): Si quieres asegurar un negro puro usando el color del componente:
                rawImageVideo.color = Color.black;
            }
        }
        else
        {
            if (rawImageVideo != null)
            {
                // Al encenderla, restauramos el color blanco base de la UI para que el video no se vea oscurecido
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
        // El slider y el dropdown se bloquean directamente
        if (sliderFPS != null) sliderFPS.interactable = IsCameraOn;
        if (dropdownGrados != null) dropdownGrados.interactable = IsCameraOn;

        // Recorremos la lista de botones de movimiento y los bloqueamos todos a la vez
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

    private void OnDestroy()
    {
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnCameraImageEvent -= AlRecibirImagenBase64;
        }
    }
}