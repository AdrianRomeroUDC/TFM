using UnityEngine;

public class ContenedorHBW_proxy : MonoBehaviour
{
    private Vector3 posicionInicialGlobal;
    private Quaternion rotacionInicialGlobal;
    private Transform padreOriginalEstante;

    // Variables de memoria para la pieza teórica
    [HideInInspector] public Vector3 offsetLocalPieza;
    [HideInInspector] public Quaternion offsetRotacionLocalPieza;
    [HideInInspector] public bool tieneOffsetRegistrado = false;

    void Start()
    {
        posicionInicialGlobal = this.transform.position;
        rotacionInicialGlobal = this.transform.rotation;
        padreOriginalEstante = this.transform.parent;
    }

    // Método para registrar la posición calculada por el script de Spawn
    public void RegistrarOffsetTeorico(Vector3 localPos, Quaternion localRot)
    {
        offsetLocalPieza = localPos;
        offsetRotacionLocalPieza = localRot;
        tieneOffsetRegistrado = true;
    }

    public void RetornarAPosicionInicial()
    {
        if (padreOriginalEstante != null)
        {
            this.transform.SetParent(padreOriginalEstante, true);
        }
        else
        {
            this.transform.SetParent(null, true);
        }

        this.transform.position = posicionInicialGlobal;
        this.transform.rotation = rotacionInicialGlobal;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.name.Contains("pieza"))
        {
            if (other.transform.parent != null && other.transform.parent.name.ToLower().Contains("ventosa")) return;
            if (other.transform.parent == this.transform) return;

            Rigidbody rbPieza = other.GetComponent<Rigidbody>();
            if (rbPieza != null) Destroy(rbPieza);

            other.transform.SetParent(this.transform);

            // Si el VGR o las físicas sueltan una pieza aquí, aplicamos el offset memorizado al inicio
            if (tieneOffsetRegistrado)
            {
                other.transform.localPosition = offsetLocalPieza;
                other.transform.localRotation = offsetRotacionLocalPieza;
            }

            // BLINDAJE: Forzamos que se mantenga sólido y no se vuelva trigger
            if (other.TryGetComponent<BoxCollider>(out BoxCollider col))
            {
                col.isTrigger = false;
            }

            Debug.Log($"<color=green><b>[Contenedor Proxy]:</b> Pieza acoplada sólidamente en posición precalculada.</color>");
        }
    }
}