using UnityEngine;

public class ContenedorHBW_proxy : MonoBehaviour
{
    // Método que gestiona únicamente la entrada de piezas de colores dentro del volumen del contenedor
    private void OnTriggerEnter(Collider other)
    {
        if (other.name.Contains("pieza"))
        {
            if (other.transform.parent != null && other.transform.parent.name.ToLower().Contains("ventosa"))
            {
                return;
            }
            if (other.transform.parent == this.transform)
            {
                return;
            }

            Rigidbody rbPieza = other.GetComponent<Rigidbody>();
            if (rbPieza != null) Destroy(rbPieza);

            other.transform.SetParent(this.transform);

            if (other.TryGetComponent<BoxCollider>(out BoxCollider col))
            {
                col.isTrigger = true;
            }

            Debug.Log($"<color=green>Pieza almacenada con éxito en: {this.name}</color>");
        }
    }

    // El método OnCollisionEnter viejo ha sido eliminado de aquí, moviendo la responsabilidad al soporte
}