using UnityEngine;

public class PlataformaHorno_proxy : MonoBehaviour
{
    private BoxCollider miCollider;

    [Header("Ajustes de Posicionamiento")]
    [Tooltip("Offset en el eje Y global para elevar la pieza un poco sobre el centro y que no se hunda en la plataforma")]
    public float elevacionGlobalY = 0.02f;

    void Awake()
    {
        // Obtenemos el BoxCollider de la plataforma
        miCollider = GetComponent<BoxCollider>();
        if (miCollider == null)
        {
            Debug.LogError($"<color=red><b>[Horno Proxy]:</b> ¡Falta BoxCollider en {gameObject.name}! Es necesario para obtener el centro global.</color>");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.name.ToLower().Contains("pieza"))
        {
            AcoplarPiezaEnCentroGlobal(other.transform);
        }
    }

    public void AcoplarPiezaEnCentroGlobal(Transform pieza)
    {
        if (miCollider == null) return;

        // 1. Desactivamos físicas para evitar comportamientos erráticos
        Rigidbody rb = pieza.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        // 2. Teletransportamos la pieza a la posición central de la plataforma
        // Usamos el centro del collider (posición global)
        pieza.position = miCollider.bounds.center + (Vector3.up * elevacionGlobalY);

        // 3. PRIMERO: Hacemos que sea hija de la plataforma
        // Esto asegura que la pieza "entre" en la jerarquía del Horno
        pieza.SetParent(this.transform, true);

        // 4. SEGUNDO: Forzamos la rotación LOCAL a (90, 0, 0)
        // Al hacerlo de forma LOCAL después del SetParent, le decimos a Unity:
        // "No me importa cómo esté girado el padre, quiero que la pieza tenga este ángulo respecto a él"
        pieza.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // 5. Opcional: Aseguramos que la escala no se deforme (por si acaso el padre tiene escalas raras)
        pieza.localScale = Vector3.one;

        // 6. Volvemos el colisionador sólido
        BoxCollider[] allCols = pieza.GetComponentsInChildren<BoxCollider>();
        foreach (BoxCollider c in allCols)
        {
            if (c != null) c.isTrigger = false;
        }

        Debug.Log($"<color=green><b>[Horno Proxy]:</b> Pieza [{pieza.name}] fijada con rotación local 90, 0, 0.</color>");
    }
}