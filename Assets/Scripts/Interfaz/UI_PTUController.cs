using UnityEngine;
using TMPro;
using System.Text.RegularExpressions;

public class UI_PTUController : MonoBehaviour
{
    [Header("Componentes de la Interfaz")]
    public TMP_Dropdown dropdownGrados;

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
    // FUNCIONES DE MOVIMIENTO (AHORA CON BLOQUEO DE SEGURIDAD SI ESTÁ EN OFF)
    // =======================================================================

    public void MoverArriba()
    {
        // Si la cámara está apagada, abortamos y no enviamos nada por MQTT
        if (!UI_CameraController.IsCameraOn)
        {
            Debug.LogWarning("[PTU] Comando 'MoverArriba' ignorado: La cámara está apagada.");
            return;
        }

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendPtuCommand("relmove_up", ObtenerGrados());
        }
    }

    public void MoverAbajo()
    {
        if (!UI_CameraController.IsCameraOn)
        {
            Debug.LogWarning("[PTU] Comando 'MoverAbajo' ignorado: La cámara está apagada.");
            return;
        }

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendPtuCommand("relmove_down", ObtenerGrados());
        }
    }

    public void MoverIzquierda()
    {
        if (!UI_CameraController.IsCameraOn)
        {
            Debug.LogWarning("[PTU] Comando 'MoverIzquierda' ignorado: La cámara está apagada.");
            return;
        }

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendPtuCommand("relmove_left", ObtenerGrados());
        }
    }

    public void MoverDerecha()
    {
        if (!UI_CameraController.IsCameraOn)
        {
            Debug.LogWarning("[PTU] Comando 'MoverDerecha' ignorado: La cámara está apagada.");
            return;
        }

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendPtuCommand("relmove_right", ObtenerGrados());
        }
    }

    public void BotonCentralHome()
    {
        if (!UI_CameraController.IsCameraOn)
        {
            Debug.LogWarning("[PTU] Comando 'Home' ignorado: La cámara está apagada.");
            return;
        }

        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendPtuCommand("home");
        }
    }
}