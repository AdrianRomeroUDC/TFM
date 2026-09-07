using UnityEngine;

public class PlataformaHorno_proxy : MonoBehaviour
{
    // =======================================================================
    // COORDENADAS EXACTAS DE LA IMAGEN 2
    // -6.2e-05f equivale exactamente a -0.000062f en Unity
    // =======================================================================
    private static readonly Vector3 posicionPerfectaImagen2 = new Vector3(-0.000154f, -0.000152f, -0.000062f);

    public static Vector3 PosicionCalibradaPieza
    {
        get
        {
            return posicionPerfectaImagen2;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Solo se acopla si el impacto pertenece a la pieza y no tiene un padre activo
        if (collision.gameObject.name.ToLower().Contains("pieza") && collision.transform.parent == null)
        {
            AcoplarPiezaEnPuntoDeContacto(collision.transform);
        }
    }

    public void AcoplarPiezaEnPuntoDeContacto(Transform pieza)
    {
        // Inmovilización del Rigidbody al impactar la superficie física
        Rigidbody rb = pieza.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Emparentado inicial respetando la escala y posición global (Mantenemos TRUE para evitar deformaciones)
        pieza.SetParent(this.transform, true);

        // Forzamos la posición local exacta a los valores de la Imagen 2
        pieza.localPosition = posicionPerfectaImagen2;

        // --- SISTEMA DE CAPTURA SILENCIOSA ---
        PlayerPrefs.SetFloat("Horno_LocalX", posicionPerfectaImagen2.x);
        PlayerPrefs.SetFloat("Horno_LocalZ", posicionPerfectaImagen2.z);
        PlayerPrefs.Save();

        Debug.Log($"[Horno]: Pieza {pieza.name} registrada en plataforma. Posición local fijada en la calibración de la Imagen 2: {posicionPerfectaImagen2}");
    }
}