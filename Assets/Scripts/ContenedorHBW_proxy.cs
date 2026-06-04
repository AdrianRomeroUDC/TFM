using UnityEngine;

public class ContenedorHBW_proxy : MonoBehaviour
{
    // Variables para almacenar magnéticamente la coordenada de nacimiento en la estantería
    private Vector3 posicionInicialGlobal;
    private Quaternion rotacionInicialGlobal;

    void Start()
    {
        // Guardamos la posición exacta del contenedor tal y como está colocado al iniciar la simulación
        posicionInicialGlobal = this.transform.position;
        rotacionInicialGlobal = this.transform.rotation;
    }

    // Función pública que llamará el soporte para "teletransportar" el contenedor a su sitio exacto
    public void RetornarAPosicionInicial()
    {
        // Cortamos el parentesco con el brazo para que se quede libre en el mundo
        this.transform.SetParent(null);

        // Forzamos las coordenadas idénticas a las del inicio del juego
        this.transform.position = posicionInicialGlobal;
        this.transform.rotation = rotacionInicialGlobal;

        Debug.Log($"<color=lime><b>[Memoria Contenedor]:</b> Contenedor {gameObject.name} devuelto milimétricamente a su posición de origen.</color>");
    }

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
}