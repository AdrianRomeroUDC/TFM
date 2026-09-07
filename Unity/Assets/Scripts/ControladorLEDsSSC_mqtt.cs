using UnityEngine;
using System.Collections;

public class ControladorSSC_LEDs : MonoBehaviour
{
    [Header("Referencias a componentes Light")]
    public Light ledVerde;      // Bit 0 (LSB) -> Valor decimal 1
    public Light ledAmarillo;   // Bit 1       -> Valor decimal 2
    public Light ledRojo;       // Bit 2 (MSB) -> Valor decimal 4
    public Light ledOnline;     // Indicador independiente de estado Online

    [Header("Ajustes de Estado Lógico")]
    private int ultimoValorLEDs = -1;
    private int ultimoValorOnline = -1;

    void Start()
    {
        StartCoroutine(IntentarSuscripcionSegura());
    }

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
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnSSCLEDsUpdateEvent -= ActualizarLuces;
        }
    }

    private void ActualizarLuces(int ledOnlineEstado, int ledsValor)
    {
        ultimoValorOnline = ledOnlineEstado;
        ultimoValorLEDs = ledsValor;
    }

    void Update()
    {
        if (ultimoValorLEDs != -1 || ultimoValorOnline != -1)
        {
            ProcesarEstadosLuces();
        }
    }

    void ProcesarEstadosLuces()
    {
        // 1. CONTROL INDEPENDIENTE LED ONLINE
        if (ultimoValorOnline != -1)
        {
            if (ledOnline != null) ledOnline.enabled = (ultimoValorOnline == 1);
            ultimoValorOnline = -1;
        }

        // 2. CONTROL POR MÁSCARA DE BITS (3 BITS)
        if (ultimoValorLEDs != -1)
        {
            // Usamos .enabled en lugar de .SetActive para no ocultar el objeto 3D
            if (ledVerde != null)
                ledVerde.enabled = ((ultimoValorLEDs & 1) != 0);

            if (ledAmarillo != null)
                ledAmarillo.enabled = ((ultimoValorLEDs & 2) != 0);

            if (ledRojo != null)
                ledRojo.enabled = ((ultimoValorLEDs & 4) != 0);

            ultimoValorLEDs = -1;
        }
    }
}