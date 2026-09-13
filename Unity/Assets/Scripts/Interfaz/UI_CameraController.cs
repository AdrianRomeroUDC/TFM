using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

/// <summary>
/// Controla el panel de la interfaz que muestra el streaming de la cámara Pan-Tilt de la estación
/// SSC: el interruptor de encendido/apagado, el slider de fotogramas por segundo (FPS), el dropdown
/// de grados de movimiento y los botones para mover la cámara. Recibe las imágenes en vivo (en Base64)
/// a través de <see cref="MQTT_InterfaceClient"/> y las pinta en un <see cref="RawImage"/> de la UI.
/// No confundir con <c>UI_CameraController</c> de la cámara virtual del usuario: este script no mueve
/// ninguna cámara de Unity, solo gestiona el panel de vídeo de la cámara física de la fábrica.
/// </summary>
public class UI_CameraController : MonoBehaviour
{
    // Indica a otros scripts (por ejemplo los que controlan los botones de movimiento de la cámara)
    // si la transmisión de vídeo está activada en este momento.
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

    private Texture2D texturaVideo; // Textura donde se va "dibujando" cada fotograma recibido de la cámara.
    private string proximaBase64 = ""; // Último fotograma pendiente de pintar, en texto Base64.
    private bool hayNuevaImagen = false; // Aviso de que ha llegado un fotograma nuevo que aún no se ha pintado.
    private readonly object bloqueoHilo = new object(); // Candado para proteger proximaBase64 entre el evento MQTT y Update().

    private RectTransform rectTransformPanelCamara;
    private Vector2 posicionInicialPanel;

    private readonly Color colorVerdeEncendido = new Color(0.2f, 0.75f, 0.2f, 1f);
    private readonly Color colorRojoApagado = new Color(0.85f, 0.2f, 0.2f, 1f);

    void Start()
    {
        // Creamos una textura mínima de partida; se sustituirá por el primer fotograma real que llegue.
        texturaVideo = new Texture2D(2, 2);
        IsCameraOn = false;

        // Guardamos la posición original del panel de vídeo para poder desplazarlo más tarde
        // cuando aparezca el reloj de simulación (ver OnRelojSimulacionVisibilidadCambiada).
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

        // Nos suscribimos al evento de MQTT_InterfaceClient que avisa cuando llega un fotograma nuevo de la cámara.
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnCameraImageEvent += AlRecibirImagenBase64;
        }

        // Nos suscribimos también al aviso de si el reloj de simulación está visible, para reubicar este panel.
        UI_ControladorMenu.OnRelojSimulacionVisibilidadCambiada += OnRelojSimulacionVisibilidadCambiada;

        StartCoroutine(EnviarEstadoInicialMqtt());
    }

    private void OnDestroy()
    {
        // Nos desuscribimos de los eventos al destruir este objeto, para evitar errores si el evento
        // se dispara cuando este script ya no existe.
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnCameraImageEvent -= AlRecibirImagenBase64;
        }

        UI_ControladorMenu.OnRelojSimulacionVisibilidadCambiada -= OnRelojSimulacionVisibilidadCambiada;
    }

    // Corrutina que espera a que la conexión MQTT de la interfaz esté lista antes de enviar
    // la configuración inicial de la cámara (evita mandar el mensaje al vacío si aún no hay conexión).
    private IEnumerator EnviarEstadoInicialMqtt()
    {
        // Esperamos activamente a que MQTT_InterfaceClient complete la conexión en segundo plano (máx 5 segundos).
        float tiempoEsperaMax = 5.0f;
        float transcurrido = 0f;

        while ((MQTT_InterfaceClient.Instance == null || !MQTT_InterfaceClient.Instance.IsConnected) && transcurrido < tiempoEsperaMax)
        {
            transcurrido += 0.2f;
            yield return new WaitForSeconds(0.2f);
        }

        // Si ya está conectado, enviamos la configuración inicial.
        if (MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.IsConnected)
        {
            EnviarConfiguracionMqtt();
        }
    }

    // Se llama cuando UI_ControladorMenu avisa de que el reloj de simulación (modo histórico) ha aparecido o desaparecido.
    private void OnRelojSimulacionVisibilidadCambiada(bool relojSimulacionVisible)
    {
        AjustarPosicionPanelCamara(relojSimulacionVisible);
    }

    // Desplaza el panel de vídeo hacia abajo cuando el reloj de simulación está visible,
    // para que no se solapen ambos elementos en pantalla, y lo devuelve a su sitio cuando no lo está.
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

    // Se ejecuta cuando el usuario pulsa el interruptor ON/OFF de la cámara.
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
                // Al apagar la cámara, limpiamos la imagen para no dejar el último fotograma "congelado" en pantalla.
                rawImageVideo.texture = null;
                rawImageVideo.color = Color.black;
            }
            else
            {
                rawImageVideo.color = Color.white;
            }
        }

        // Avisamos a la fábrica del nuevo estado (encendida/apagada) y de los FPS actuales.
        EnviarConfiguracionMqtt();
    }

    // Se ejecuta cuando el usuario mueve el slider de FPS; solo tiene efecto si la cámara está encendida.
    private void OnSliderFpsCambiado(float valorFps)
    {
        if (IsCameraOn)
        {
            EnviarConfiguracionMqtt();
        }
    }

    /// <summary>
    /// Envía a la fábrica, mediante <see cref="MQTT_InterfaceClient"/>, el estado actual de la cámara
    /// (encendida o apagada) junto con los fotogramas por segundo seleccionados en el slider.
    /// </summary>
    public void EnviarConfiguracionMqtt()
    {
        int fpsSeleccionados = (sliderFPS != null) ? Mathf.RoundToInt(sliderFPS.value) : 2;

        // Verificamos que el cliente exista y esté conectado antes de intentar publicar el mensaje.
        if (MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.IsConnected)
        {
            MQTT_InterfaceClient.Instance.SendCameraConfig(IsCameraOn, fpsSeleccionados);
            Debug.Log($"<color=cyan>[MQTT Cámara] Enviado a 'c/cam' -> Estado: {IsCameraOn}, FPS: {fpsSeleccionados}</color>");
        }
    }

    // Callback del evento OnCameraImageEvent: se ejecuta en el momento en que llega un fotograma
    // nuevo. Solo guardamos el dato (protegido por el candado) porque este método puede no correr
    // en el hilo principal de Unity; el pintado real ocurre en Update().
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

        // Recogemos el último fotograma pendiente (si lo hay) de forma segura entre hilos.
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

    // Decodifica el texto Base64 recibido a una imagen real y la asigna al RawImage de la interfaz.
    private void PintarTexturaEnUI(string base64String)
    {
        try
        {
            // Algunos formatos incluyen un prefijo antes de la coma (ej. "data:image/jpeg;base64,...");
            // si existe, lo recortamos para quedarnos solo con los datos de la imagen.
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

    // Activa o desactiva los controles de la cámara (slider FPS, dropdown de grados, botones PTU)
    // según si la cámara está encendida o apagada, para que no se puedan usar estando apagada.
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

    // Muestra u oculta el panel de vídeo y cambia el color del fondo del interruptor (verde = encendida, rojo = apagada).
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
