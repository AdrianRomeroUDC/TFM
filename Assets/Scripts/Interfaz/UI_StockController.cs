using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class UI_StockController : MonoBehaviour
{
    [Header("--- Componente Reloj ---")]
    [SerializeField] private TextMeshProUGUI textoReloj;

    [Header("--- Contenedores de Sprites ---")]
    [SerializeField] private Sprite spriteVacio;
    [SerializeField] private Sprite spriteAzul;
    [SerializeField] private Sprite spriteRojo;
    [SerializeField] private Sprite spriteBlanco;

    // Cambiado a CLASS para poder guardar los datos dinámicos de las piezas (Workpiece) por referencia
    [Serializable]
    public class SlotUI
    {
        public string idSlot;
        public Image imagenComponente;
        [HideInInspector] public Workpiece piezaActual; // Guardará los datos del último JSON MQTT
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
    [SerializeField] private TMP_Dropdown dropdownPeriodo;

    [Header("--- UI de Información (Tooltip Flotante) ---")]
    [SerializeField] private GameObject panelTooltip; // El contenedor/fondo del Tooltip
    [SerializeField] private TextMeshProUGUI txtTooltipContenido; // El texto TMP interno del Tooltip
    [SerializeField] private Vector2 tooltipOffset = new Vector2(15f, -15f); // Desplazamiento respecto al cursor

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

        // 3. Vinculación del evento del Dropdown
        if (dropdownPeriodo != null)
        {
            dropdownPeriodo.onValueChanged.AddListener(CambiarPeriodoSensores);
            CambiarPeriodoSensores(dropdownPeriodo.value);
        }

        // 4. AUTOMATIZACIÓN: Añadir detectores de Mouse-Over a cada imagen del almacén
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
        // Actualizar Reloj
        if (textoReloj != null)
        {
            textoReloj.text = DateTime.Now.ToString("dd/MM/yyyy   HH:mm:ss");
        }

        // Si el tooltip está encendido, hacemos que siga la posición del ratón
        if (panelTooltip != null && panelTooltip.activeSelf)
        {
            // Verificamos si el ratón existe y está activo en el nuevo Input System
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                // Leemos la posición del ratón con la nueva API
                Vector2 posicionRaton = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

                // Asignamos la posición al Tooltip
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
                // Añadimos el script detector dinámicamente por código para ahorrar trabajo manual
                UI_SlotMouseDetector detector = slot.imagenComponente.gameObject.AddComponent<UI_SlotMouseDetector>();

                // Nos suscribimos a sus acciones usando expresiones lambda pasándole el slot correspondiente
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

        // Eliminamos las etiquetas <color=#FFEA00> y </color> de la línea del ID
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
                // GUARDAMOS la información de la pieza en nuestro mapa para que el Tooltip pueda leerla
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

        // Si el ratón ya estaba encima de un slot mientras llegó una actualización de datos por MQTT, refrescamos el texto visible
        if (slotBajoElCursor != null)
        {
            MostrarDatosTooltip();
        }

        ActualizarUIElementoPedido(txtStockAzul, btnPedirAzul, contadorAzul);
        ActualizarUIElementoPedido(txtStockRojo, btnPedirRojo, contadorRojo);
        ActualizarUIElementoPedido(txtStockBlanco, btnPedirBlanco, contadorBlanco);
    }

    private void CambiarPeriodoSensores(int index)
    {
        if (dropdownPeriodo == null) return;
        string textoOpcion = dropdownPeriodo.options[index].text;
        string soloNumeros = Regex.Replace(textoOpcion, @"[^\d]", "");

        if (int.TryParse(soloNumeros, out int segundos))
        {
            if (MQTT_InterfaceClient.Instance != null)
            {
                MQTT_InterfaceClient.Instance.SendLdrPeriod(segundos);
                MQTT_InterfaceClient.Instance.SendBme680Period(segundos);
                Debug.Log($"<color=yellow>[MQTT] Configuración enviada. LDR y BME680 actualizados a {segundos}s</color>");
            }
        }
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