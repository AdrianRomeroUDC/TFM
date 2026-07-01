using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;

public class UI_PTUController : MonoBehaviour
{
    [Header("Componentes de la Interfaz")]
    public TMP_Dropdown dropdownGrados;

    [Header("Botones de Movimiento (Asignar en Inspector)")]
    public Button btnArriba;
    public Button btnAbajo;
    public Button btnIzquierda;
    public Button btnDerecha;
    public Button btnHome;

    private bool ultimaInteraccionEstado;

    void Start()
    {
        // Al arrancar, leemos cómo está la cámara y aplicamos el estado visual inmediatamente
        ultimaInteraccionEstado = UI_CameraController.IsCameraOn;
        ConfigurarInteractividad(ultimaInteraccionEstado);
    }

    void Update()
    {
        // Seguimos escuchando en el Update por si el usuario pulsa el Toggle ON/OFF en el juego
        if (UI_CameraController.IsCameraOn != ultimaInteraccionEstado)
        {
            ultimaInteraccionEstado = UI_CameraController.IsCameraOn;
            ConfigurarInteractividad(ultimaInteraccionEstado);
        }
    }

    /// <summary>
    /// Activa o desactiva por completo la interacción física y visual de los componentes
    /// </summary>
    private void ConfigurarInteractividad(bool estaActivo)
    {
        if (dropdownGrados != null) dropdownGrados.interactable = estaActivo;
        if (btnArriba != null) btnArriba.interactable = estaActivo;
        if (btnAbajo != null) btnAbajo.interactable = estaActivo;
        if (btnIzquierda != null) btnIzquierda.interactable = estaActivo;
        if (btnDerecha != null) btnDerecha.interactable = estaActivo;
        if (btnHome != null) btnHome.interactable = estaActivo;
    }

    private int ObtenerGrados()
    {
        if (dropdownGrados != null)
        {
            string textoSeleccionado = dropdownGrados.options[dropdownGrados.value].text;
            string numeroLimpio = Regex.Replace(textoSeleccionado, @"[^\d]", "");

            if (int.TryParse(numeroLimpio, out int grados))
            {
                return grados;
            }
        }
        return 10;
    }

    // =======================================================================
    // FUNCIONES DE MOVIMIENTO (Bloqueadas si IsCameraOn es false)
    // =======================================================================

    public void MoverArriba()
    {
        if (!UI_CameraController.IsCameraOn) return;

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendPtuCommand("relmove_up", ObtenerGrados());
        }
    }

    public void MoverAbajo()
    {
        if (!UI_CameraController.IsCameraOn) return;

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendPtuCommand("relmove_down", ObtenerGrados());
        }
    }

    public void MoverIzquierda()
    {
        if (!UI_CameraController.IsCameraOn) return;

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendPtuCommand("relmove_left", ObtenerGrados());
        }
    }

    public void MoverDerecha()
    {
        if (!UI_CameraController.IsCameraOn) return;

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendPtuCommand("relmove_right", ObtenerGrados());
        }
    }

    public void BotonCentralHome()
    {
        if (!UI_CameraController.IsCameraOn) return;

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendPtuCommand("home");
        }
    }
}