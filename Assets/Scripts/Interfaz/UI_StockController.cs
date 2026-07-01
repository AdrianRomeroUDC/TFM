using UnityEngine;
using UnityEngine.UI; // Obligatorio para componentes Image y Button
using TMPro; // Obligatorio para TextMeshPro y TMP_Dropdown
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions; // Para limpiar el texto del dropdown

public class UI_StockController : MonoBehaviour
{
    [Header("--- Componente Reloj ---")]
    [SerializeField] private TextMeshProUGUI textoReloj;

    [Header("--- Contenedores de Sprites ---")]
    [SerializeField] private Sprite spriteVacio;
    [SerializeField] private Sprite spriteAzul;
    [SerializeField] private Sprite spriteRojo;
    [SerializeField] private Sprite spriteBlanco;

    [Serializable]
    public struct SlotUI
    {
        public string idSlot;
        public Image imagenComponente;
    }

    [Header("--- Mapeo del Almacén ---")]
    [SerializeField] private List<SlotUI> listaSlots = new List<SlotUI>();

    [Header("--- UI de Pedidos (Textos de Stock) ---")]
    [SerializeField] private TextMeshProUGUI txtStockAzul;
    [SerializeField] private TextMeshProUGUI txtStockRojo;
    [SerializeField] private TextMeshProUGUI txtStockBlanco;

    [Header("--- UI de Pedidos (Botones) ---")]
    [SerializeField] private Button btnPedirAzul;
    [SerializeField] private Button btnPedirRojo;
    [SerializeField] private Button btnPedirBlanco;

    [Header("--- UI de Configuración (Sensores) ---")]
    [SerializeField] private TMP_Dropdown dropdownPeriodo; // <-- NUEVO DROPDOWN

    void Start()
    {
        // 1. Suscripción al evento de telemetría del stock MQTT
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnStockUpdateEvent += ActualizarPanelAlmacen;
        }

        // 2. Vinculación de los eventos Click de los botones de pedido
        if (btnPedirAzul != null) btnPedirAzul.onClick.AddListener(() => EnviarPedidoA_MQTT("BLUE"));
        if (btnPedirRojo != null) btnPedirRojo.onClick.AddListener(() => EnviarPedidoA_MQTT("RED"));
        if (btnPedirBlanco != null) btnPedirBlanco.onClick.AddListener(() => EnviarPedidoA_MQTT("WHITE"));

        // 3. Vinculación del evento del Dropdown al cambiar de valor
        if (dropdownPeriodo != null)
        {
            dropdownPeriodo.onValueChanged.AddListener(CambiarPeriodoSensores);

            // Opcional: Sincroniza los sensores con el valor inicial del dropdown al dar a Play
            CambiarPeriodoSensores(dropdownPeriodo.value);
        }
    }

    void OnDestroy()
    {
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnStockUpdateEvent -= ActualizarPanelAlmacen;
        }
    }

    void Update()
    {
        if (textoReloj != null)
        {
            textoReloj.text = DateTime.Now.ToString("dd/MM/yyyy   HH:mm:ss");
        }
    }

    // ====================================================================
    // ACCIÓN DEL DROPDOWN: Cambia el período de envío de los sensores
    // ====================================================================
    private void CambiarPeriodoSensores(int index)
    {
        if (dropdownPeriodo == null) return;

        // Obtenemos el texto de la opción seleccionada (Ej: "5s", "10 s", "60 segundos")
        string textoOpcion = dropdownPeriodo.options[index].text;

        // Usamos una expresión regular para eliminar cualquier letra/espacio y quedarnos SOLO con los números
        string soloNumeros = Regex.Replace(textoOpcion, @"[^\d]", "");

        // Convertimos el texto numérico a un entero (int)
        if (int.TryParse(soloNumeros, out int segundos))
        {
            if (MQTT_InterfaceClient.Instance != null)
            {
                // Enviamos de forma síncrona el valor a ambos topics de configuración
                MQTT_InterfaceClient.Instance.SendLdrPeriod(segundos);
                MQTT_InterfaceClient.Instance.SendBme680Period(segundos);

                Debug.Log($"<color=yellow>[MQTT] Configuración enviada. LDR y BME680 actualizados a {segundos}s</color>");
            }
        }
        else
        {
            Debug.LogError($"No se pudo extraer un número válido de la opción del Dropdown: '{textoOpcion}'");
        }
    }

    // ====================================================================
    // ACCIÓN DE LOS BOTONES: Publica la orden al Broker
    // ====================================================================
    private void EnviarPedidoA_MQTT(string colorPieza)
    {
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendOrder(colorPieza);
            Debug.Log($"<color=cyan>[MQTT] Orden de pieza enviada: {colorPieza}</color>");
        }
    }

    // ====================================================================
    // LECTURA DE TELEMETRÍA: Procesa el stock recibido del Broker
    // ====================================================================
    private void ActualizarPanelAlmacen(StockPayload datosStock)
    {
        if (datosStock == null || datosStock.stockItems == null) return;

        int contadorAzul = 0;
        int contadorRojo = 0;
        int contadorBlanco = 0;

        foreach (StockItem item in datosStock.stockItems)
        {
            SlotUI slotVisual = listaSlots.Find(s => s.idSlot.Equals(item.location, StringComparison.OrdinalIgnoreCase));

            if (slotVisual.imagenComponente != null)
            {
                if (item.workpiece == null || string.IsNullOrEmpty(item.workpiece.type))
                {
                    slotVisual.imagenComponente.sprite = spriteVacio;
                }
                else
                {
                    string tipoPieza = item.workpiece.type.ToUpper();
                    switch (tipoPieza)
                    {
                        case "BLUE":
                            slotVisual.imagenComponente.sprite = spriteAzul;
                            contadorAzul++;
                            break;
                        case "RED":
                            slotVisual.imagenComponente.sprite = spriteRojo;
                            contadorRojo++;
                            break;
                        case "WHITE":
                            slotVisual.imagenComponente.sprite = spriteBlanco;
                            contadorBlanco++;
                            break;
                        default:
                            slotVisual.imagenComponente.sprite = spriteVacio;
                            break;
                    }
                }
            }
        }

        ActualizarUIElementoPedido(txtStockAzul, btnPedirAzul, contadorAzul);
        ActualizarUIElementoPedido(txtStockRojo, btnPedirRojo, contadorRojo);
        ActualizarUIElementoPedido(txtStockBlanco, btnPedirBlanco, contadorBlanco);
    }

    private void ActualizarUIElementoPedido(TextMeshProUGUI textoStock, Button botonPedir, int cantidad)
    {
        if (textoStock != null) textoStock.text = $"Stock: {cantidad}";
        if (botonPedir != null) botonPedir.interactable = (cantidad > 0);
    }
}