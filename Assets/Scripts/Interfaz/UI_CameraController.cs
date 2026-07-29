using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

public class UI_CameraController : MonoBehaviour
{
    public static bool IsCameraOn { get; private set; } = false;

    [Header("Componentes de Renderizado Video")]
    public RawImage rawImageVideo;

    [Header("Ajustes Visuales y Paneles")]
    public Image imagenFondoToggle;       // Background del botón ON-OFF
    public GameObject panelVideoIzquierda; // Panel contenedor del HUD de la cámara

    [Header("Ajustes de Posición Dinámica (Desplazamiento)")]
    public float desplazamientoY = 120f;

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

    private RectTransform rectTransformPanelCamara;
    private Vector2 posicionInicialPanel;

    private readonly Color colorVerdeEncendido = new Color(0.2f, 0.75f, 0.2f, 1f);
    private readonly Color colorRojoApagado = new Color(0.85f, 0.2f, 0.2f, 1f);

    void Start()
    {
        texturaVideo = new Texture2D(2, 2);
        IsCameraOn = false;

        if (panelVideoIzquierda != null)
        {
            rectTransformPanelCamara = panelVideoIzquierda.GetComponent<RectTransform>();
            if (rectTransformPanelCamara != null)
            {
                posicionInicialPanel = rectTransformPanelCamara.anchoredPosition;
            }
        }

        if (toggleCamara != null)
        {
            toggleCamara.isOn = false;
            toggleCamara.onValueChanged.AddListener(OnToggleCamaraCambiado);
        }

        if (sliderFPS != null)
        {
            sliderFPS.onValueChanged.RemoveAllListeners();
            sliderFPS.value = 2f;
            sliderFPS.onValueChanged.AddListener(OnSliderFpsCambiado);
        }

        ActualizarInteractividadUI();
        ActualizarVisualesCamara();

        if (rawImageVideo != null)
        {
            rawImageVideo.texture = null;
            rawImageVideo.color = Color.black;
        }

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnCameraImageEvent += AlRecibirImagenBase64;
        }

        UI_ControladorMenu.OnRelojSimulacionVisibilidadCambiada += OnRelojSimulacionVisibilidadCambiada;

        StartCoroutine(EnviarEstadoInicialMqtt());
    }

    private void OnDestroy()
    {
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnCameraImageEvent -= AlRecibirImagenBase64;
        }

        UI_ControladorMenu.OnRelojSimulacionVisibilidadCambiada -= OnRelojSimulacionVisibilidadCambiada;
    }

    private IEnumerator EnviarEstadoInicialMqtt()
    {
        yield return new WaitForSeconds(0.2f);
        EnviarConfiguracionMqtt();
    }

    private void OnRelojSimulacionVisibilidadCambiada(bool relojSimulacionVisible)
    {
        AjustarPosicionPanelCamara(relojSimulacionVisible);
    }

    private void AjustarPosicionPanelCamara(bool relojSimulacionVisible)
    {
        if (rectTransformPanelCamara == null) return;

        if (relojSimulacionVisible)
        {
            rectTransformPanelCamara.anchoredPosition = posicionInicialPanel + new Vector2(0, -desplazamientoY);
        }
        else
        {
            rectTransformPanelCamara.anchoredPosition = posicionInicialPanel;
        }
    }

    private void OnToggleCamaraCambiado(bool estadoEncendido)
    {
        IsCameraOn = estadoEncendido;

        ActualizarInteractividadUI();
        ActualizarVisualesCamara();

        if (IsCameraOn)
        {
            AjustarPosicionPanelCamara(UI_ControladorMenu.EsRelojSimulacionVisible);
        }

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

        EnviarConfiguracionMqtt();
    }

    private void OnSliderFpsCambiado(float valorFps)
    {
        if (IsCameraOn)
        {
            EnviarConfiguracionMqtt();
        }
    }

    public void EnviarConfiguracionMqtt()
    {
        int fpsSeleccionados = (sliderFPS != null) ? Mathf.RoundToInt(sliderFPS.value) : 2;

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendCameraConfig(IsCameraOn, fpsSeleccionados);
            Debug.Log($"<color=cyan>[MQTT Cámara] Enviado a 'c/cam' -> Estado: {IsCameraOn}, FPS: {fpsSeleccionados}</color>");
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

        // 🟢 Procesa la textura continuamente si IsCameraOn es true
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
        if (panelVideoIzquierda != null)
        {
            panelVideoIzquierda.SetActive(IsCameraOn);
        }

        if (imagenFondoToggle != null)
        {
            imagenFondoToggle.color = IsCameraOn ? colorVerdeEncendido : colorRojoApagado;
        }
    }
}