using UnityEngine;

public class PlataformaHorno_proxy : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        // Solo se acopla si el impacto pertenece a la pieza y no tiene un padre activo
        if (collision.gameObject.name.ToLower().Contains("pieza") && collision.transform.parent == null)
        {
            AcoplarPiezaEnPuntoDeContacto(collision.transform);
        }
    }

    public void AcoplarPiezaEnPuntoDeContacto(Transform pieza)
    {
        // Inmovilización del Rigidbody al impactar la superficie física
        Rigidbody rb = pieza.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Emparentado inicial respetando la posición global de la colisión
        pieza.SetParent(this.transform, true);

        // Desfase milimétrico de calibración en el eje Y local preservando X y Z del impacto
        Vector3 posicionLocalActual = pieza.localPosition;
        pieza.localPosition = new Vector3(posicionLocalActual.x, -0.000152f, posicionLocalActual.z);

        Debug.Log($"[Horno]: Pieza {pieza.name} registrada en plataforma. Altura local Y fijada en -0.000152.");
    }
}