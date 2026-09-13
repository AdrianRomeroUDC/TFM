using UnityEngine;

/// <summary>
/// Este script va colocado en cada cajón (contenedor) del almacén HBW, uno de los huecos de la
/// cuadrícula 3x3 donde se guardan las piezas. Se encarga de recordar dónde estaba el cajón en su
/// estante original, de "atrapar" dentro de sí una pieza cuando el VGR la suelta encima de él, y de
/// devolver el cajón a su sitio exacto cuando el transelevador termina de guardarlo. En resumen, es
/// el que decide cuándo una pieza pasa a formar parte "para siempre" de un hueco del almacén.
/// </summary>
public class ContenedorHBW_proxy : MonoBehaviour
{
    private Vector3 posicionInicialGlobal;
    private Quaternion rotacionInicialGlobal;
    private Transform padreOriginalEstante;

    [HideInInspector] public Vector3 offsetLocalPieza;
    [HideInInspector] public Quaternion offsetRotacionLocalPieza;
    [HideInInspector] public bool tieneOffsetRegistrado = false;

    // Al arrancar, memorizamos dónde está el cajón en su estante de origen (posición, rotación y
    // padre), para poder devolverlo aquí más adelante si el transelevador lo mueve y lo repone.
    void Start()
    {
        posicionInicialGlobal = this.transform.position;
        rotacionInicialGlobal = this.transform.rotation;
        padreOriginalEstante = this.transform.parent;
    }

    /// <summary>
    /// Guarda la posición y rotación exactas (dentro del propio cajón) en las que debe quedar
    /// colocada la pieza cuando se acople, para que encaje siempre igual de bien dentro del hueco.
    /// </summary>
    public void RegistrarOffsetTeorico(Vector3 localPos, Quaternion localRot)
    {
        offsetLocalPieza = localPos;
        offsetRotacionLocalPieza = localRot;
        tieneOffsetRegistrado = true;
    }

    /// <summary>
    /// Devuelve el cajón a su posición original en el estante del almacén, como si el
    /// transelevador real lo hubiera dejado de nuevo en su hueco de siempre.
    /// </summary>
    public void RetornarAPosicionInicial()
    {
        if (padreOriginalEstante != null) this.transform.SetParent(padreOriginalEstante, true);
        else this.transform.SetParent(null, true);

        this.transform.position = posicionInicialGlobal;
        this.transform.rotation = rotacionInicialGlobal;
    }

    private void OnTriggerEnter(Collider other) => ProcesarContactoContenedor(other);
    private void OnTriggerStay(Collider other) => ProcesarContactoContenedor(other);

    // Cuando una pieza sale de la zona del cajón, comprobamos si el robot VGR se ha ido con ella
    // agarrada sin llegar a soltarla aquí: en ese caso, cancelamos la "cita" que teníamos apuntada
    // entre este cajón y esa pieza, porque ya no va a caer dentro.
    private void OnTriggerExit(Collider other)
    {
        if (other.name.ToLower().Contains("pieza"))
        {
            ControladorVGR_mqtt robot = Object.FindFirstObjectByType<ControladorVGR_mqtt>();
            if (robot != null && robot.ObtenerContenedorActual() == this && robot.ObtenerPiezaEnganchada() == other.transform)
            {
                // Si el robot cancela la operación y levanta el brazo sin soltar, limpiamos la cita
                robot.RegistrarContenedorBajoVentosa(null);
            }
        }
    }

    // Decide qué hacer cuando una pieza toca (o sigue tocando) la zona del cajón: si el VGR la
    // trae agarrada, solo anotamos que este es el cajón candidato para cuando la suelte; si la
    // pieza ya está suelta encima, la acoplamos de verdad dentro del hueco.
    private void ProcesarContactoContenedor(Collider other)
    {
        if (other.name.ToLower().Contains("pieza"))
        {
            ControladorVGR_mqtt robot = Object.FindFirstObjectByType<ControladorVGR_mqtt>();

            // Si el robot la trae agarrada, pactamos el intercambio y salimos sin tocar el parentesco
            if (robot != null && robot.ObtenerPiezaEnganchada() == other.transform)
            {
                robot.RegistrarContenedorBajoVentosa(this);
                return;
            }

            // Si ya es hija de este cajón, ignoramos detonaciones repetidas del Trigger
            if (other.transform.parent == this.transform) return;

            // Si es una pieza suelta o el robot acaba de abrir la ventosa, la acoplamos permanentemente
            AcoplarPiezaDirecto(other.transform);
        }
    }

    /// <summary>
    /// Encaja la pieza dentro de este hueco del almacén de forma definitiva: le quita la física
    /// (para que no se mueva ni se caiga) y la coloca exactamente en su sitio dentro del cajón,
    /// igual que quedaría la pieza real guardada de forma estable en su hueco del HBW.
    /// </summary>
    public void AcoplarPiezaDirecto(Transform pieza)
    {
        Rigidbody rb = pieza.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb); // Adiós físicas inestables para evitar desfaces en el estante

        pieza.SetParent(this.transform, true); // Emparentado definitivo con el cajón

        if (tieneOffsetRegistrado)
        {
            pieza.localPosition = offsetLocalPieza;
            pieza.localRotation = offsetRotacionLocalPieza;
        }

        BoxCollider[] allCols = pieza.GetComponentsInChildren<BoxCollider>();
        foreach (BoxCollider c in allCols)
        {
            if (c != null) c.isTrigger = false; // La pieza vuelve a ser física/sólida para moverse solidaria con el cajón
        }

        Debug.Log($"<color=green><b>[Contenedor Proxy]:</b> Pieza [{pieza.name}] acoplada con éxito como hija permanente.</color>");
    }
}
