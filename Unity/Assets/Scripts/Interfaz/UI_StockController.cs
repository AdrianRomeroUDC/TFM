using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controla el panel visual del almacén (estación HBW) en la interfaz: pinta en cada
/// hueco de la cuadrícula 3x3 el sprite de la pieza que hay guardada (azul, roja, blanca
/// o vacío), mantiene el contador de stock de cada color, gestiona los botones de "Pedir
/// pieza" y muestra un pequeño tooltip flotante con los detalles de la pieza cuando el
/// ratón pasa por encima de un hueco.
///
/// Los datos del almacén llegan desde la fábrica real a través de
/// <see cref="MQTT_InterfaceClient"/> (evento <c>OnStockUpdateEvent</c>); este script no
/// habla directamente con la fábrica, solo escucha esos eventos y actualiza lo que se ve
/// en pantalla. Los pedidos de piezas, en cambio, sí se envían directamente por MQTT
/// mediante <see cref="MQTT_InterfaceClient.SendOrder"/>.
/// </summary>
public class UI_StockController : MonoBehaviour
{
    [Header("--- Contenedores de Sprites ---")]
    [SerializeField] private Sprite spriteVacio;
    [SerializeField] private Sprite spriteAzul;
    [SerializeField] private Sprite spriteRojo;
    [SerializeField] private Sprite spriteBlanco;

    /// <summary>
    /// Representa, en la interfaz, un único hueco (slot) del almacén HBW: su identificador
    /// de ubicación (por ejemplo "A1"), la imagen que lo dibuja en pantalla y la pieza que
    /// tiene actualmente guardada (si la hay).
    /// </summary>
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

    [Header("--- UI de Pedidos (Botones Almacén Principal) ---")]
    [SerializeField] private Button btnPedirAzul;
    [SerializeField] private Button btnPedirRojo;
    [SerializeField] private Button btnPedirBlanco;

    [Header("--- UI de Información (Tooltip Flotante) ---")]
    [SerializeField] private GameObject panelTooltip;
    [SerializeField] private TextMeshProUGUI txtTooltipContenido;
    [SerializeField] private Vector2 tooltipOffset = new Vector2(15f, -15f);

    // Guarda sobre qué slot está el cursor del ratón en este momento (null si no está sobre ninguno)
    private SlotUI slotBajoElCursor = null;

    // Contadores de cuántas piezas de cada color hay guardadas ahora mismo en el almacén
    private int stockActualAzul = 0;
    private int stockActualRojo = 0;
    private int stockActualBlanco = 0;

    // Indica si hay una simulación offline en marcha (en ese caso no se permite pedir piezas)
    private bool simulacionEnCurso = false;

    /// <summary>
    /// Al activarse este componente, nos suscribimos a los eventos que nos avisan si hay una
    /// simulación offline en marcha o si el menú ha cambiado el permiso para pedir piezas.
    /// </summary>
    private void OnEnable()
    {
        SimuladorOffline.OnEstadoSimulacionOfflineCambiado += OnEstadoSimulacionOfflineCambiado;
        UI_ControladorMenu.OnEstadoPermisoPedidoCambiado += OnEstadoPermisoPedidoCambiado;
    }

    /// <summary>
    /// Al desactivarse, nos desuscribimos de esos mismos eventos para no dejar
    /// suscripciones "colgadas" cuando este objeto ya no está activo.
    /// </summary>
    private void OnDisable()
    {
        SimuladorOffline.OnEstadoSimulacionOfflineCambiado -= OnEstadoSimulacionOfflineCambiado;
        UI_ControladorMenu.OnEstadoPermisoPedidoCambiado -= OnEstadoPermisoPedidoCambiado;
    }

    /// <summary>
    /// Configura, al arrancar, la suscripción al evento de stock del almacén, los botones
    /// de pedido, los detectores de ratón sobre cada hueco y el estado inicial del tooltip.
    /// </summary>
    void Start()
    {
        if (MQTT_InterfaceClient.Instance != null)
        {
            // Nos suscribimos para que, cada vez que llegue una actualización de stock desde
            // la fábrica real, se repinte automáticamente el panel del almacén
            MQTT_InterfaceClient.Instance.OnStockUpdateEvent += ActualizarPanelAlmacen;

            // Si ya había datos de stock guardados de antes (por ejemplo, si nos conectamos
            // tarde), pintamos el almacén con esos datos inmediatamente
            if (MQTT_InterfaceClient.Instance.UltimoStock != null)
            {
                ActualizarPanelAlmacen(MQTT_InterfaceClient.Instance.UltimoStock);
            }
        }

        // Enganchamos cada botón de "Pedir pieza" para que envíe el pedido del color correspondiente
        if (btnPedirAzul != null) btnPedirAzul.onClick.AddListener(() => EnviarPedidoA_MQTT("BLUE"));
        if (btnPedirRojo != null) btnPedirRojo.onClick.AddListener(() => EnviarPedidoA_MQTT("RED"));
        if (btnPedirBlanco != null) btnPedirBlanco.onClick.AddListener(() => EnviarPedidoA_MQTT("WHITE"));

        ConfigurarDetectoresHover();

        if (panelTooltip != null) panelTooltip.SetActive(false);
        ReevaluarTodosLosBotones();
    }

    /// <summary>
    /// Al destruirse este objeto, nos desuscribimos del evento de stock para evitar
    /// que se intente actualizar un panel que ya no existe.
    /// </summary>
    void OnDestroy()
    {
        if (MQTT_InterfaceClient.Instance != null)
        {
            MQTT_InterfaceClient.Instance.OnStockUpdateEvent -= ActualizarPanelAlmacen;
        }
    }

    /// <summary>
    /// Cada frame, si el tooltip está visible, lo movemos para que siga la posición
    /// actual del cursor del ratón (con un pequeño desplazamiento para no taparlo).
    /// </summary>
    void Update()
    {
        if (panelTooltip != null && panelTooltip.activeSelf)
        {
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                Vector2 posicionRaton = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                panelTooltip.transform.position = (Vector3)posicionRaton + (Vector3)tooltipOffset;
            }
        }
    }

    /// <summary>
    /// Se llama cuando cambia el estado de la simulación offline (empieza o termina).
    /// Mientras haya una simulación en marcha, no tiene sentido permitir pedir piezas
    /// a la fábrica real, así que se vuelve a comprobar si los botones deben bloquearse.
    /// </summary>
    /// <param name="enEjecucion">true si la simulación offline está corriendo.</param>
    private void OnEstadoSimulacionOfflineCambiado(bool enEjecucion)
    {
        simulacionEnCurso = enEjecucion;
        ReevaluarTodosLosBotones();
    }

    /// <summary>
    /// Se llama cuando el menú principal cambia el permiso general para pedir piezas
    /// (por ejemplo, según el modo de funcionamiento seleccionado). Vuelve a comprobar
    /// el estado de los botones de pedido.
    /// </summary>
    /// <param name="permitido">true si en este momento está permitido pedir piezas.</param>
    private void OnEstadoPermisoPedidoCambiado(bool permitido)
    {
        ReevaluarTodosLosBotones();
    }

    /// <summary>
    /// Vuelve a comprobar, para los tres colores, si su botón de pedido debe estar
    /// activo o desactivado (según el stock disponible, la simulación y los permisos).
    /// </summary>
    private void ReevaluarTodosLosBotones()
    {
        ActualizarEstadoBoton(btnPedirAzul, stockActualAzul);
        ActualizarEstadoBoton(btnPedirRojo, stockActualRojo);
        ActualizarEstadoBoton(btnPedirBlanco, stockActualBlanco);
    }

    /// <summary>
    /// Activa o desactiva un botón de pedido según tres condiciones: que el menú lo
    /// permita, que no haya una simulación en marcha, y que quede stock de ese color.
    /// </summary>
    /// <param name="boton">Botón de pedido a comprobar.</param>
    /// <param name="stockDisponible">Cuántas piezas de ese color hay ahora mismo en el almacén.</param>
    private void ActualizarEstadoBoton(Button boton, int stockDisponible)
    {
        if (boton != null)
        {
            bool permitidoPorMenu = (UI_ControladorMenu.Instance == null) || UI_ControladorMenu.Instance.PuedePedirPieza;
            boton.interactable = permitidoPorMenu && !simulacionEnCurso && (stockDisponible > 0);
        }
    }

    /// <summary>
    /// Añade (si no lo tiene ya) el componente <see cref="UI_SlotMouseDetector"/> a la
    /// imagen de cada hueco del almacén, y conecta sus eventos de "ratón encima" y
    /// "ratón fuera" con los métodos que muestran/ocultan el tooltip de ese hueco.
    /// </summary>
    private void ConfigurarDetectoresHover()
    {
        foreach (SlotUI slot in listaSlots)
        {
            if (slot.imagenComponente != null)
            {
                UI_SlotMouseDetector detector = slot.imagenComponente.GetComponent<UI_SlotMouseDetector>();
                if (detector == null)
                {
                    detector = slot.imagenComponente.gameObject.AddComponent<UI_SlotMouseDetector>();
                }

                detector.OnMouseOverSlot = () => AlEntrarCursorEnSlot(slot);
                detector.OnMouseExitSlot = () => AlSalirCursorDeSlot(slot);
            }
        }
    }

    /// <summary>
    /// Se ejecuta cuando el cursor entra en el área de un hueco del almacén: recuerda
    /// cuál es el hueco activo y muestra su tooltip con la información de la pieza.
    /// </summary>
    /// <param name="slot">Hueco del almacén sobre el que ha entrado el cursor.</param>
    private void AlEntrarCursorEnSlot(SlotUI slot)
    {
        slotBajoElCursor = slot;
        MostrarDatosTooltip();
    }

    /// <summary>
    /// Se ejecuta cuando el cursor sale del área de un hueco del almacén: si era el hueco
    /// que teníamos guardado como "activo", lo olvidamos y ocultamos el tooltip.
    /// </summary>
    /// <param name="slot">Hueco del almacén del que ha salido el cursor.</param>
    private void AlSalirCursorDeSlot(SlotUI slot)
    {
        if (slotBajoElCursor == slot)
        {
            slotBajoElCursor = null;
            if (panelTooltip != null) panelTooltip.SetActive(false);
        }
    }

    /// <summary>
    /// Rellena y muestra el panel del tooltip con los datos de la pieza que hay en el
    /// hueco sobre el que está el cursor. Si el hueco está vacío, oculta el tooltip.
    /// </summary>
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

    /// <summary>
    /// Se ejecuta automáticamente cada vez que llega una actualización de inventario del
    /// almacén HBW desde la fábrica real. Recorre todos los huecos recibidos, actualiza el
    /// sprite de cada uno según el color de pieza que contenga (o el sprite vacío si no hay
    /// nada), recalcula los contadores de stock por color y refresca los textos y botones
    /// de pedido.
    /// </summary>
    /// <param name="datosStock">Estado completo del almacén recibido por MQTT.</param>
    private void ActualizarPanelAlmacen(StockPayload datosStock)
    {
        if (datosStock == null || datosStock.stockItems == null) return;

        stockActualAzul = 0;
        stockActualRojo = 0;
        stockActualBlanco = 0;

        foreach (StockItem item in datosStock.stockItems)
        {
            // Buscamos, entre todos los huecos visuales, el que corresponde a esta ubicación
            SlotUI slotVisual = listaSlots.Find(s => s.idSlot.Equals(item.location, StringComparison.OrdinalIgnoreCase));

            if (slotVisual != null && slotVisual.imagenComponente != null)
            {
                slotVisual.piezaActual = item.workpiece;

                if (item.workpiece == null || string.IsNullOrEmpty(item.workpiece.type))
                {
                    // Hueco vacío: se pinta el sprite de "vacío"
                    slotVisual.imagenComponente.sprite = spriteVacio;
                }
                else
                {
                    string tipoPieza = item.workpiece.type.ToUpper();
                    switch (tipoPieza)
                    {
                        case "BLUE":
                            slotVisual.imagenComponente.sprite = spriteAzul;
                            stockActualAzul++;
                            break;
                        case "RED":
                            slotVisual.imagenComponente.sprite = spriteRojo;
                            stockActualRojo++;
                            break;
                        case "WHITE":
                            slotVisual.imagenComponente.sprite = spriteBlanco;
                            stockActualBlanco++;
                            break;
                        default:
                            slotVisual.imagenComponente.sprite = spriteVacio;
                            break;
                    }
                }
            }
        }

        // Si el ratón está sobre algún hueco justo cuando llega esta actualización,
        // refrescamos también el contenido del tooltip para que no se quede desactualizado
        if (slotBajoElCursor != null)
        {
            MostrarDatosTooltip();
        }

        ActualizarUIElementoPedido(txtStockAzul, btnPedirAzul, stockActualAzul);
        ActualizarUIElementoPedido(txtStockRojo, btnPedirRojo, stockActualRojo);
        ActualizarUIElementoPedido(txtStockBlanco, btnPedirBlanco, stockActualBlanco);
    }

    /// <summary>
    /// Envía directamente a la fábrica real, por MQTT, una orden de pedido de una pieza
    /// de un color concreto (esto hace que la fábrica física prepare y entregue la pieza).
    /// </summary>
    /// <param name="colorPieza">Color de la pieza a pedir ("BLUE", "RED" o "WHITE").</param>
    private void EnviarPedidoA_MQTT(string colorPieza)
    {
        if (MQTT_InterfaceClient.Instance != null && MQTT_InterfaceClient.Instance.IsConnected)
        {
            MQTT_InterfaceClient.Instance.SendOrder(colorPieza);
            Debug.Log($"<color=cyan>[MQTT Directo] Orden enviada a la fábrica real: {colorPieza}</color>");
        }
    }

    /// <summary>
    /// Actualiza el texto de stock de un color concreto y vuelve a evaluar si su botón
    /// de pedido debe estar activo o no, según la nueva cantidad disponible.
    /// </summary>
    /// <param name="textoStock">Texto donde se muestra la cantidad en stock.</param>
    /// <param name="botonPedir">Botón de pedido asociado a ese color.</param>
    /// <param name="cantidad">Cantidad actual de piezas de ese color en el almacén.</param>
    private void ActualizarUIElementoPedido(TextMeshProUGUI textoStock, Button botonPedir, int cantidad)
    {
        if (textoStock != null) textoStock.text = $"Stock: {cantidad}";
        ActualizarEstadoBoton(botonPedir, cantidad);
    }
}
