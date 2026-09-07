using UnityEngine;
using UnityEngine.UI;

public class UI_SeccionSimulacion : MonoBehaviour
{
    [Header("UI Sección Simulación (Botones de Pedido)")]
    public Button btnSimPedirBlanca;
    public Button btnSimPedirRoja;
    public Button btnSimPedirAzul;

    public void Inicializar()
    {
        if (btnSimPedirBlanca != null) btnSimPedirBlanca.onClick.AddListener(() => PedirPieza("WHITE"));
        if (btnSimPedirRoja != null) btnSimPedirRoja.onClick.AddListener(() => PedirPieza("RED"));
        if (btnSimPedirAzul != null) btnSimPedirAzul.onClick.AddListener(() => PedirPieza("BLUE"));
    }

    public void ActualizarEstadoBotones(bool sePuedePedir)
    {
        if (btnSimPedirBlanca != null) btnSimPedirBlanca.interactable = sePuedePedir;
        if (btnSimPedirRoja != null) btnSimPedirRoja.interactable = sePuedePedir;
        if (btnSimPedirAzul != null) btnSimPedirAzul.interactable = sePuedePedir;
    }

    private void PedirPieza(string color)
    {
        if (UI_ControladorMenu.Instance != null)
        {
            UI_ControladorMenu.Instance.PedirPiezaSimulacion(color);
        }
    }
}