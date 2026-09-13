using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel muy pequeño de la interfaz que muestra tres botones (pieza blanca, roja y azul) para pedir
/// una pieza cuando se está en modo "Simulación Offline" (sin conexión real a la fábrica física).
/// Al pulsar un botón, delega en <see cref="UI_ControladorMenu"/> para que sea él quien decida
/// cómo tramitar el pedido (arrancando el modo simulación si hace falta y pasándoselo al
/// <c>SimuladorOffline</c>). Este panel no contiene lógica de fábrica: solo traduce clics de botón
/// en llamadas al controlador central del menú.
/// </summary>
public class UI_SeccionSimulacion : MonoBehaviour
{
    [Header("UI Sección Simulación (Botones de Pedido)")]
    public Button btnSimPedirBlanca;
    public Button btnSimPedirRoja;
    public Button btnSimPedirAzul;

    /// <summary>
    /// Conecta cada botón de pedido con su color correspondiente. La llama
    /// <see cref="UI_ControladorMenu"/> al arrancar la escena, igual que hace con <c>UI_SeccionBBDD</c>.
    /// </summary>
    public void Inicializar()
    {
        if (btnSimPedirBlanca != null) btnSimPedirBlanca.onClick.AddListener(() => PedirPieza("WHITE"));
        if (btnSimPedirRoja != null) btnSimPedirRoja.onClick.AddListener(() => PedirPieza("RED"));
        if (btnSimPedirAzul != null) btnSimPedirAzul.onClick.AddListener(() => PedirPieza("BLUE"));
    }

    /// <summary>
    /// Activa o desactiva los tres botones de pedido a la vez, según si en este momento tiene
    /// sentido permitir un nuevo pedido (por ejemplo, se bloquean mientras ya hay un pedido en curso).
    /// </summary>
    /// <param name="sePuedePedir">true para permitir pulsar los botones, false para bloquearlos.</param>
    public void ActualizarEstadoBotones(bool sePuedePedir)
    {
        if (btnSimPedirBlanca != null) btnSimPedirBlanca.interactable = sePuedePedir;
        if (btnSimPedirRoja != null) btnSimPedirRoja.interactable = sePuedePedir;
        if (btnSimPedirAzul != null) btnSimPedirAzul.interactable = sePuedePedir;
    }

    // Reenvía el color pedido al controlador central del menú, que es quien sabe cómo iniciar
    // o continuar la simulación offline con esa pieza.
    private void PedirPieza(string color)
    {
        if (UI_ControladorMenu.Instance != null)
        {
            UI_ControladorMenu.Instance.PedirPiezaSimulacion(color);
        }
    }
}
