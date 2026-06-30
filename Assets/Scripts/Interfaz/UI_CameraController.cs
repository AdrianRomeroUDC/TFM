using UnityEngine;
using UnityEngine.UI;
using System;

public class UI_CameraController : MonoBehaviour
{
    [Header("Componente UI")]
    public RawImage rawImageVideo; // Tu cuadro blanco de la interfaz

    private Texture2D texturaVideo;
    private string proximaBase64 = "";
    private bool hayNuevaImagen = false;
    private readonly object bloqueoHilo = new object();

    void Start()
    {
        // Inicializamos la textura de renderizado
        texturaVideo = new Texture2D(2, 2);

        // ESCUCHAR TU EVENTO: Nos suscribimos de forma limpia al evento de tu Singleton
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnCameraImageEvent += AlRecibirImagenBase64;
        }
    }

    // Este método se ejecutará automáticamente cada vez que tu MQTT reciba un mensaje en "i/cam"
    private void AlRecibirImagenBase64(string base64Data)
    {
        // Guardamos los datos bloqueando el hilo un milisegundo por seguridad
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

        // Comprobamos en el hilo principal de Unity si llegó algo
        lock (bloqueoHilo)
        {
            if (hayNuevaImagen)
            {
                base64ParaProcesar = proximaBase64;
                hayNuevaImagen = false;
                procesar = true;
            }
        }

        // Si hay datos nuevos, los pintamos de forma segura en la pantalla
        if (procesar && !string.IsNullOrEmpty(base64ParaProcesar))
        {
            PintarTexturaEnUI(base64ParaProcesar);
        }
    }

    private void PintarTexturaEnUI(string base64String)
    {
        try
        {
            // Limpiamos el prefijo "data:image/jpeg;base64," si viene incluido en el JSON
            if (base64String.Contains(","))
            {
                base64String = base64String.Substring(base64String.IndexOf(",") + 1);
            }

            // Convertimos el texto Base64 purificado a bytes de imagen
            byte[] imageBytes = Convert.FromBase64String(base64String);

            // Cargamos los bytes en la textura y la asignamos a tu RawImage
            texturaVideo.LoadImage(imageBytes);

            if (rawImageVideo != null)
            {
                rawImageVideo.texture = texturaVideo;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error al renderizar la imagen MQTT en la interfaz: " + e.Message);
        }
    }

    private void OnDestroy()
    {
        // Buenas prácticas: nos desuscribimos al destruir el objeto para evitar fugas de memoria
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnCameraImageEvent -= AlRecibirImagenBase64;
        }
    }
}