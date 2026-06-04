using UnityEngine;

public class ContenedorHBW_proxy : MonoBehaviour
{
    private Vector3 posicionInicialGlobal;
    private Quaternion rotacionInicialGlobal;

    void Start()
    {
        posicionInicialGlobal = this.transform.position;
        rotacionInicialGlobal = this.transform.rotation;
    }

    public void RetornarAPosicionInicial()
    {
        this.transform.SetParent(null);
        this.transform.position = posicionInicialGlobal;
        this.transform.rotation = rotacionInicialGlobal;
        Debug.Log($"<color=lime><b>[Memoria Contenedor]:</b> Contenedor {gameObject.name} devuelto a su posición de origen.</color>");
    }

    // Usamos OnTriggerEnter para detectar el momento exacto del impacto de la pieza
    private void OnTriggerEnter(Collider other)
    {
        if (other.name.Contains("pieza"))
        {
            // Si la pieza sigue sujeta por la grúa VGR (tiene de padre la ventosa), ignoramos para no interrumpir el viaje
            if (other.transform.parent != null && other.transform.parent.name.ToLower().Contains("ventosa"))
            {
                return;
            }
            if (other.transform.parent == this.transform)
            {
                return;
            }

            // 1. ELIMINACIÓN DE FÍSICAS INMEDIATA (Tu idea para evitar que atraviese)
            // Al destruir el Rigidbody en este frame exacto, la pieza pierde la gravedad, 
            // detiene su velocidad de caída en seco y se congela en la posición de contacto.
            Rigidbody rbPieza = other.GetComponent<Rigidbody>();
            if (rbPieza != null)
            {
                Destroy(rbPieza);
                Debug.Log($"<color=yellow><b>[Contenedor]:</b> Rigidbody eliminado de {other.name} para evitar traspaso.</color>");
            }

            // 2. REEMPARENTAR
            // Ahora que la pieza es estática y no tiene físicas que la empujen hacia abajo,
            // la unimos de forma segura a la jerarquía del contenedor.
            other.transform.SetParent(this.transform);

            // 3. PASAR COLLIDER A TRIGGER (Opcional)
            // Volvemos su BoxCollider un trigger para que no choque con las paredes internas del propio cajón
            if (other.TryGetComponent<BoxCollider>(out BoxCollider col))
            {
                col.isTrigger = true;
            }

            Debug.Log($"<color=green><b>[Contenedor]:</b> Pieza bloqueada con éxito y almacenada en: {this.name}</color>");
        }
    }
}