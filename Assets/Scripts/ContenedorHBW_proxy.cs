using UnityEngine;

public class ContenedorHBW_proxy : MonoBehaviour
{
    // Variables para almacenar magnéticamente la coordenada y la jerarquía de origen
    private Vector3 posicionInicialGlobal;
    private Quaternion rotacionInicialGlobal;
    private Transform padreOriginalEstante; // <--- Nueva variable de memoria

    void Start()
    {
        // Guardamos la posición, rotación y el padre exacto (ej: Col1Fil1) al iniciar la simulación
        posicionInicialGlobal = this.transform.position;
        rotacionInicialGlobal = this.transform.rotation;
        padreOriginalEstante = this.transform.parent;
    }

    // Función pública que llama el soporte para regresar el contenedor a su sitio exacto
    public void RetornarAPosicionInicial()
    {
        // En lugar de SetParent(null), lo devolvemos bajo el ala de su nodo ColxFilx original
        if (padreOriginalEstante != null)
        {
            this.transform.SetParent(padreOriginalEstante, true);
            Debug.Log($"<color=lime><b>[Memoria Contenedor]:</b> Contenedor {gameObject.name} reemparentado con éxito a su nodo lógico: {padreOriginalEstante.name}</color>");
        }
        else
        {
            this.transform.SetParent(null, true);
            Debug.LogWarning($"<color=yellow><b>[Memoria Contenedor]:</b> {gameObject.name} no tenía padre al inicio del juego. Se queda en la raíz.</color>");
        }

        // Forzamos el regreso milimétrico a las coordenadas físicas de inicio
        this.transform.position = posicionInicialGlobal;
        this.transform.rotation = rotacionInicialGlobal;
    }

    // Usamos OnTriggerEnter para detectar el momento exacto del impacto de la pieza
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
            if (rbPieza != null)
            {
                Destroy(rbPieza);
                Debug.Log($"<color=yellow><b>[Contenedor]:</b> Rigidbody eliminado de {other.name} para evitar traspaso.</color>");
            }

            other.transform.SetParent(this.transform);

            if (other.TryGetComponent<BoxCollider>(out BoxCollider col))
            {
                col.isTrigger = true;
            }

            Debug.Log($"<color=green><b>[Contenedor]:</b> Pieza bloqueada con éxito y almacenada en: {this.name}</color>");
        }
    }
}