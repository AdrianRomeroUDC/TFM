using UnityEngine;

public class AlmacenContenedor : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 1. Filtramos: Solo nos interesa si entra una "pieza"
        if (other.name.Contains("pieza"))
        {
            // 2. Limpieza de físicas: Eliminamos el Rigidbody para que sea estática
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Destroy(rb);
            }

            // 3. Emparentar: La pieza se queda donde el VGR la dejó
            // Al ser hija del contenedor, se moverá con él si el almacén se desplaza
            other.transform.SetParent(this.transform);

            // 4. Configuración del Collider: 
            // La mantenemos como Trigger para que sea "atravesable" (Imagen 2)
            if (other.TryGetComponent<BoxCollider>(out BoxCollider col))
            {
                col.isTrigger = true;
            }

            Debug.Log($"<color=green>Pieza almacenada en: {this.name}</color>");
        }
    }
}