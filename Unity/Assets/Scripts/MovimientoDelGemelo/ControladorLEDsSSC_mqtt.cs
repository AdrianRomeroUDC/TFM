using UnityEngine;
using System.Collections;

/// <summary>
/// Controla el gemelo digital del semáforo de LEDs y el indicador de conexión de la estación
/// SSC (Sensor Station Camera). La fábrica real enciende o apaga tres luces (verde, amarilla y
/// roja) para mostrar el estado general de la planta, y además tiene un LED independiente que
/// indica si la estación está "online" (conectada). Este script recibe por MQTT esos estados y
/// enciende/apaga los componentes <see cref="Light"/> correspondientes en la escena de Unity
/// para que el semáforo virtual coincida siempre con el real.
/// </summary>
public class ControladorSSC_LEDs : MonoBehaviour
{
    [Header("Referencias a componentes Light")]
    public Light ledVerde;      // Bit 0 (LSB) -> Valor decimal 1
    public Light ledAmarillo;   // Bit 1       -> Valor decimal 2
    public Light ledRojo;       // Bit 2 (MSB) -> Valor decimal 4
    public Light ledOnline;     // Indicador independiente de estado Online

    [Header("Ajustes de Estado Lógico")]
    // Guardan el último valor recibido por MQTT para los LEDs del semáforo y para el LED online.
    // Empiezan en -1 porque ese valor nunca puede llegar por MQTT: así sabemos si "aún no ha
    // llegado ningún dato nuevo que aplicar" (ver Update() y ProcesarEstadosLuces()).
    private int ultimoValorLEDs = -1;
    private int ultimoValorOnline = -1;

    void Start()
    {
        // Esperamos de forma segura a que el cliente MQTT central exista antes de suscribirnos.
        StartCoroutine(IntentarSuscripcionSegura());
    }

    // Corrutina que espera, frame a frame, a que MQTTClient.Instance esté disponible, y entonces
    // se suscribe al evento que informa del estado de los LEDs de la SSC.
    IEnumerator IntentarSuscripcionSegura()
    {
        while (MQTTClient.Instance == null)
        {
            yield return null;
        }

        MQTTClient.Instance.OnSSCLEDsUpdateEvent += ActualizarLuces;
        Debug.Log("<color=green><b>SSC LEDs:</b> Suscripción completada con éxito.</color>");
    }

    private void OnDisable()
    {
        // Nos desuscribimos al desactivar el objeto, para no dejar una suscripción activa de más.
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnSSCLEDsUpdateEvent -= ActualizarLuces;
        }
    }

    /// <summary>
    /// Se llama cada vez que llega un mensaje MQTT con el estado de los LEDs de la SSC. Solo
    /// guarda los valores recibidos; el trabajo real de encender/apagar las luces se hace en
    /// <see cref="Update"/>, para mantener todo el cambio visual dentro del hilo principal de Unity.
    /// </summary>
    /// <param name="ledOnlineEstado">Estado del LED de conexión: 1 si la estación está online, 0 si no.</param>
    /// <param name="ledsValor">Valor numérico que representa, en binario, qué LEDs del semáforo (verde/amarillo/rojo) están encendidos.</param>
    private void ActualizarLuces(int ledOnlineEstado, int ledsValor)
    {
        ultimoValorOnline = ledOnlineEstado;
        ultimoValorLEDs = ledsValor;
    }

    void Update()
    {
        // Si hay algún dato nuevo pendiente de aplicar (distinto de -1), lo procesamos ahora.
        if (ultimoValorLEDs != -1 || ultimoValorOnline != -1)
        {
            ProcesarEstadosLuces();
        }
    }

    /// <summary>
    /// Aplica de verdad los últimos valores recibidos por MQTT: enciende o apaga el LED de
    /// conexión y, usando el valor numérico del semáforo como una máscara de bits de 3 posiciones
    /// (uno por cada LED: verde, amarillo y rojo), enciende o apaga cada luz según corresponda.
    /// </summary>
    void ProcesarEstadosLuces()
    {
        // 1. CONTROL INDEPENDIENTE LED ONLINE
        if (ultimoValorOnline != -1)
        {
            if (ledOnline != null) ledOnline.enabled = (ultimoValorOnline == 1);
            ultimoValorOnline = -1; // Marcamos como "ya aplicado" hasta que llegue un valor nuevo.
        }

        // 2. CONTROL POR MÁSCARA DE BITS (3 BITS)
        if (ultimoValorLEDs != -1)
        {
            // Usamos .enabled en lugar de .SetActive para no ocultar el objeto 3D
            // Comprobamos cada bit por separado con una operación AND a nivel de bits:
            // bit 0 (valor 1) = LED verde, bit 1 (valor 2) = LED amarillo, bit 2 (valor 4) = LED rojo.
            if (ledVerde != null)
                ledVerde.enabled = ((ultimoValorLEDs & 1) != 0);

            if (ledAmarillo != null)
                ledAmarillo.enabled = ((ultimoValorLEDs & 2) != 0);

            if (ledRojo != null)
                ledRojo.enabled = ((ultimoValorLEDs & 4) != 0);

            ultimoValorLEDs = -1; // Marcamos como "ya aplicado" hasta que llegue un valor nuevo.
        }
    }
}
