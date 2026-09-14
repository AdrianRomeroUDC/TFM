using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;

/// <summary>
/// Controla los botones y el desplegable de la interfaz que mueven la PTU
/// (la "Pan-Tilt-Unit", es decir, la cámara orientable con motores de la estación SSC)
/// de la fábrica física. Este script no mueve la cámara directamente: solo traduce
/// los clics del usuario en comandos MQTT que se envían a la fábrica a través de
/// <see cref="MQTT_InterfaceClient"/>. Además, se encarga de bloquear todos estos
/// controles cuando la cámara de vídeo está apagada, para que no se puedan enviar
/// movimientos "a ciegas".
/// </summary>
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

    // Guarda el último estado conocido de la cámara (encendida/apagada) para poder
    // detectar cuándo cambia y así no estar recalculando la interactividad cada frame sin motivo.
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

    /// <summary>
    /// Lee el desplegable de grados (por ejemplo "10º", "20º"...) y extrae solo el número,
    /// para saber cuántos grados hay que mover la cámara en cada pulsación de botón.
    /// Si no hay desplegable asignado o no se puede leer el número, se usa 10 grados por defecto.
    /// </summary>
    private int ObtenerGrados()
    {
        if (dropdownGrados != null)
        {
            string textoSeleccionado = dropdownGrados.options[dropdownGrados.value].text;
            // Quitamos todo lo que no sea un dígito (por ejemplo el símbolo "º") para quedarnos solo con el número
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

    /// <summary>
    /// Se llama al pulsar el botón "Arriba". Pide a la fábrica que incline la cámara
    /// hacia arriba los grados indicados en el desplegable.
    /// </summary>
    public void MoverArriba()
    {
        if (!UI_CameraController.IsCameraOn) return;

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendPtuCommand("relmove_up", ObtenerGrados());
        }
    }

    /// <summary>
    /// Se llama al pulsar el botón "Abajo". Pide a la fábrica que incline la cámara
    /// hacia abajo los grados indicados en el desplegable.
    /// </summary>
    public void MoverAbajo()
    {
        if (!UI_CameraController.IsCameraOn) return;

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendPtuCommand("relmove_down", ObtenerGrados());
        }
    }

    /// <summary>
    /// Se llama al pulsar el botón "Izquierda". Pide a la fábrica que gire la cámara
    /// hacia la izquierda los grados indicados en el desplegable.
    /// </summary>
    public void MoverIzquierda()
    {
        if (!UI_CameraController.IsCameraOn) return;

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendPtuCommand("relmove_left", ObtenerGrados());
        }
    }

    /// <summary>
    /// Se llama al pulsar el botón "Derecha". Pide a la fábrica que gire la cámara
    /// hacia la derecha los grados indicados en el desplegable.
    /// </summary>
    public void MoverDerecha()
    {
        if (!UI_CameraController.IsCameraOn) return;

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendPtuCommand("relmove_right", ObtenerGrados());
        }
    }

    /// <summary>
    /// Se llama al pulsar el botón central "Home". Pide a la fábrica que devuelva
    /// la cámara a su posición de origen (sin necesitar el número de grados).
    /// </summary>
    public void BotonCentralHome()
    {
        if (!UI_CameraController.IsCameraOn) return;

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendPtuCommand("home");
        }
    }
}
