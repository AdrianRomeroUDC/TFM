using UnityEngine;

public class PlataformaTurntable_proxy : MonoBehaviour
{
    [Header("Ajuste de Altura Global")]
    [Tooltip("Ajusta la altura en el eje Y global para que la pieza apoye perfectamente sobre el plato.")]
    public float offsetAlturaY = 0.05f;

    private void OnTriggerEnter(Collider other)
    {
        // Al colisionar con cualquier objeto que se llame "pieza"...
        if (other.name.ToLower().Contains("pieza"))
        {
            Debug.Log($"[Turntable]: Pieza '{other.name}' detectada. Centrando con precisión matemática usando Bounds.");

            // 1. GUARDAR ROTACIÓN: Almacenamos la rotación global exacta que trae del brazo
            Quaternion rotacionMundoOriginal = other.transform.rotation;

            // 2. BUSCAR EL CENTRO REAL: Obtenemos el BoxCollider de la mesa
            BoxCollider miCollider = GetComponent<BoxCollider>();

            if (miCollider != null)
            {
                // bounds.center nos da el centro del CUBO VERDE en coordenadas del mundo (olvida los pivotes del CAD)
                Vector3 centroRealMundo = miCollider.bounds.center;

                // 3. TELETRANSPORTE DE PRECISIÓN: Forzamos la posición global en X y Z de la mesa, y ajustamos la Y con el offset
                other.transform.position = new Vector3(centroRealMundo.x, centroRealMundo.y + offsetAlturaY, centroRealMundo.z);
            }
            else
            {
                // Fallback por si acaso el objeto no tuviera collider
                other.transform.position = this.transform.position;
            }

            // 4. RESTAURAR ROTACIÓN: Le devolvemos la rotación exacta que tenía al caer
            other.transform.rotation = rotacionMundoOriginal;

            // 5. EMPARENTAR: La hacemos hija asegurando que preserve su posición global recién corregida (true)
            other.transform.SetParent(this.transform, true);

            // 6. FIJAR FÍSICAS: Sincronizamos y congelamos para el giro
            Physics.SyncTransforms();

            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}