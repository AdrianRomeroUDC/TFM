using UnityEngine;

/// <summary>
/// Este script va sobre la plataforma real que hay dentro del horno del MPO. Se encarga de
/// encajar cualquier pieza que llegue ahí (ya sea porque cae físicamente encima o porque el brazo
/// la suelta a propósito) en el punto de contacto exacto calibrado a mano, para que en Unity se
/// vea apoyada sobre la bandeja del horno exactamente igual que quedaría la pieza física real.
/// </summary>
public class PlataformaHorno_proxy : MonoBehaviour
{
    // =======================================================================
    // COORDENADAS EXACTAS DE LA IMAGEN 2
    // -6.2e-05f equivale exactamente a -0.000062f en Unity
    // =======================================================================
    private static readonly Vector3 posicionPerfectaImagen2 = new Vector3(-0.000154f, -0.000152f, -0.000062f);

    /// <summary>
    /// Posición local calibrada a mano (mirando la "Imagen 2" de referencia) donde debe quedar
    /// cualquier pieza apoyada dentro del horno para que se vea perfectamente encajada.
    /// </summary>
    public static Vector3 PosicionCalibradaPieza
    {
        get
        {
            return posicionPerfectaImagen2;
        }
    }

    // Si una pieza cae por física normal y choca contra la plataforma del horno (en vez de que el
    // brazo la coloque a propósito), también la encajamos aquí, siempre que no tenga ya un padre.
    private void OnCollisionEnter(Collision collision)
    {
        // Solo se acopla si el impacto pertenece a la pieza y no tiene un padre activo
        if (collision.gameObject.name.ToLower().Contains("pieza") && collision.transform.parent == null)
        {
            AcoplarPiezaEnPuntoDeContacto(collision.transform);
        }
    }

    /// <summary>
    /// Coloca la pieza indicada justo en el punto exacto de la bandeja del horno donde debe
    /// apoyarse, dejándola quieta (sin física) como si el horno real la sostuviera dentro.
    /// </summary>
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
