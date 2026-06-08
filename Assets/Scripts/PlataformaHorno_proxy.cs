using UnityEngine;

public class PlataformaHorno_proxy : MonoBehaviour
{
    private BoxCollider miCollider;
    public float elevacionGlobalY = 0.02f;

    void Awake() => miCollider = GetComponent<BoxCollider>();

    private void OnTriggerEnter(Collider other)
    {
        // Solo la acoplamos si viene del VGR (es decir, no tiene padre o viene suelta)
        if (other.name.ToLower().Contains("pieza") && other.transform.parent == null)
        {
            AcoplarPiezaEnCentroGlobal(other.transform);
        }
    }

    public void AcoplarPiezaEnCentroGlobal(Transform pieza)
    {
        if (miCollider == null) return;

        // APAGAMOS FÍSICAS, PERO NO EL COMPONENTE (Mantenemos el Rigidbody vivo)
        Rigidbody rb = pieza.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Posicionamos y emparentamos
        pieza.position = miCollider.bounds.center + (Vector3.up * elevacionGlobalY);
        pieza.SetParent(this.transform, true);
        pieza.localRotation = Quaternion.Euler(90f, 0f, 0f);
        pieza.localScale = Vector3.one;

        Debug.Log($"[Horno]: Pieza {pieza.name} guardada y lista para el brazo MPO.");
    }
}