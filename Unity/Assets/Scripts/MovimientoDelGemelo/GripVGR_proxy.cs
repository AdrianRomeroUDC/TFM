using UnityEngine;

/// <summary>
/// Script sencillo de apoyo para la ventosa del VGR: solo vigila con un sensor de contacto si la
/// pieza blanca concreta está justo debajo de la ventosa o se ha alejado, y se lo comunica al
/// controlador principal del VGR. Sirve para que el controlador sepa en todo momento cuál es la
/// pieza "candidata" a ser agarrada, sin tener que calcular él mismo las distancias.
/// </summary>
public class GripVGR_proxy : MonoBehaviour
{
    public ControladorVGR_mqtt controlador;

    // La pieza blanca ha entrado en la zona de la ventosa: avisamos al controlador de que
    // ahora mismo es la pieza más cercana disponible para agarrar.
    private void OnTriggerEnter(Collider other)
    {
        // Importante: Detectamos la pieza, no el contenedor
        if (other.name.Contains("piezaBlanca"))
        {
            controlador.SetPiezaCercana(other.transform);
        }
    }

    // La pieza blanca ha salido de la zona de la ventosa: avisamos al controlador de que ya
    // no hay ninguna pieza cercana lista para agarrar.
    private void OnTriggerExit(Collider other)
    {
        if (other.name.Contains("piezaBlanca"))
        {
            controlador.SetPiezaCercana(null);
        }
    }
}
