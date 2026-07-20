using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class UI_StockController : MonoBehaviour
{
    [Header("--- Contenedores de Sprites ---")]
    [SerializeField] private Sprite spriteVacio;
    [SerializeField] private Sprite spriteAzul;
    [SerializeField] private Sprite spriteRojo;
    [SerializeField] private Sprite spriteBlanco;

    [Serializable]
    public class SlotUI
    {
        public string idSlot;
        public Image imagenComponente;
        [HideInInspector] public Workpiece piezaActual;
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

    [Header("--- UI de Información (Tooltip Flotante) ---")]
    [SerializeField] private GameObject panelTooltip;
    [SerializeField] private TextMeshProUGUI txtTooltipContenido;
    [SerializeField] private Vector2 tooltipOffset = new Vector2(15f, -15f);

    private SlotUI slotBajoElCursor = null;

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

        // 3. AUTOMATIZACIÓN: Añadir detectores de Mouse-Over a cada imagen del almacén
        ConfigurarDetectoresHover();

        // Aseguramos que el tooltip empiece oculto
        if (panelTooltip != null) panelTooltip.SetActive(false);
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
        // Si el tooltip está encendido, hacemos que siga la posición del ratón
        if (panelTooltip != null && panelTooltip.activeSelf)
        {
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                Vector2 posicionRaton = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                panelTooltip.transform.position = (Vector3)posicionRaton + (Vector3)tooltipOffset;
            }
        }
    }

    // ====================================================================
    // GESTIÓN DEL TOOLTIP (HOVER)
    // ====================================================================
    private void ConfigurarDetectoresHover()
    {
        foreach (SlotUI slot in listaSlots)
        {
            if (slot.imagenComponente != null)
            {
                UI_SlotMouseDetector detector = slot.imagenComponente.gameObject.AddComponent<UI_SlotMouseDetector>();
                detector.OnMouseOverSlot = () => AlEntrarCursorEnSlot(slot);
                detector.OnMouseExitSlot = () => AlSalirCursorDeSlot(slot);
            }
        }
    }

    private void AlEntrarCursorEnSlot(SlotUI slot)
    {
        slotBajoElCursor = slot;
        MostrarDatosTooltip();
    }

    private void AlSalirCursorDeSlot(SlotUI slot)
    {
        if (slotBajoElCursor == slot)
        {
            slotBajoElCursor = null;
            if (panelTooltip != null) panelTooltip.SetActive(false);
        }
    }

    private void MostrarDatosTooltip()
    {
        if (slotBajoElCursor == null || panelTooltip == null || txtTooltipContenido == null) return;

        if (slotBajoElCursor.piezaActual == null || string.IsNullOrEmpty(slotBajoElCursor.piezaActual.id))
        {
            panelTooltip.SetActive(false);
            return;
        }

        Workpiece wp = slotBajoElCursor.piezaActual;
        txtTooltipContenido.text = $"<b>Ubicación:</b> {slotBajoElCursor.idSlot}\n" +
                                   $"<b>ID:</b> {wp.id}\n" +
                                   $"<b>Color:</b> {wp.type}\n" +
                                   $"<b>Estado:</b> {wp.state}";

        panelTooltip.SetActive(true);
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

            if (slotVisual != null && slotVisual.imagenComponente != null)
            {
                slotVisual.piezaActual = item.workpiece;

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

        if (slotBajoElCursor != null)
        {
            MostrarDatosTooltip();
        }

        ActualizarUIElementoPedido(txtStockAzul, btnPedirAzul, contadorAzul);
        ActualizarUIElementoPedido(txtStockRojo, btnPedirRojo, contadorRojo);
        ActualizarUIElementoPedido(txtStockBlanco, btnPedirBlanco, contadorBlanco);
    }

    private void EnviarPedidoA_MQTT(string colorPieza)
    {
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.SendOrder(colorPieza);
            Debug.Log($"<color=cyan>[MQTT] Orden de pieza enviada: {colorPieza}</color>");
        }
    }

    private void ActualizarUIElementoPedido(TextMeshProUGUI textoStock, Button botonPedir, int cantidad)
    {
        if (textoStock != null) textoStock.text = $"Stock: {cantidad}";
        if (botonPedir != null) botonPedir.interactable = (cantidad > 0);
    }
}