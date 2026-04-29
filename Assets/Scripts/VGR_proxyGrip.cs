using UnityEngine;

public class VGR_Proxy_Grip : MonoBehaviour
{
    public ControladorVGR_mqtt controlador;

    private void OnTriggerEnter(Collider other)
    {
        // Importante: Detectamos la pieza, no el contenedor
        if (other.name.Contains("piezaBlanca"))
        {
            controlador.SetPiezaCercana(other.transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.name.Contains("piezaBlanca"))
        {
            controlador.SetPiezaCercana(null);
        }
    }
}