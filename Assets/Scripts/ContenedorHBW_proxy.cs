using UnityEngine;

public class ContenedorHBW_proxy : MonoBehaviour
{
    private Vector3 posicionInicialGlobal;
    private Quaternion rotacionInicialGlobal;
    private Transform padreOriginalEstante;

    [HideInInspector] public Vector3 offsetLocalPieza;
    [HideInInspector] public Quaternion offsetRotacionLocalPieza;
    [HideInInspector] public bool tieneOffsetRegistrado = false;

    void Start()
    {
        posicionInicialGlobal = this.transform.position;
        rotacionInicialGlobal = this.transform.rotation;
        padreOriginalEstante = this.transform.parent;
    }

    public void RegistrarOffsetTeorico(Vector3 localPos, Quaternion localRot)
    {
        offsetLocalPieza = localPos;
        offsetRotacionLocalPieza = localRot;
        tieneOffsetRegistrado = true;
    }

    public void RetornarAPosicionInicial()
    {
        if (padreOriginalEstante != null) this.transform.SetParent(padreOriginalEstante, true);
        else this.transform.SetParent(null, true);

        this.transform.position = posicionInicialGlobal;
        this.transform.rotation = rotacionInicialGlobal;
    }

    private void OnTriggerEnter(Collider other) => ProcesarContactoContenedor(other);
    private void OnTriggerStay(Collider other) => ProcesarContactoContenedor(other);

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