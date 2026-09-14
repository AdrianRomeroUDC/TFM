using UnityEngine;
using System.Collections;

/// <summary>
/// Controla la cámara principal 3D de la escena para poder "viajar" entre distintos
/// puntos de vista predefinidos de la fábrica (por ejemplo, una vista general, una vista
/// centrada en el HBW, otra en el VGR, etc.).
///
/// Cada punto de vista es simplemente la posición y rotación de un Transform vacío
/// colocado en la escena (el "objetivo"). Este script puede colocar la cámara de golpe en
/// uno de esos puntos (sin animación) o desplazarla suavemente hasta él (con animación),
/// normalmente al pulsar un botón de la interfaz.
///
/// Importante: a pesar de su nombre, este script NO cambia entre paneles de la interfaz;
/// solo mueve la cámara 3D dentro de la misma escena.
/// </summary>
public class UI_ViewController : MonoBehaviour
{
    [Header("Cámara Principal 3D")]
    public Camera camaraPrincipal;

    [Header("Velocidad de Transición")]
    public float velocidadTransicion = 3.0f;

    /// <summary>
    /// Representa un punto de vista concreto: un nombre identificativo (para reconocerlo
    /// fácilmente en el Inspector) y el Transform de la escena cuya posición/rotación debe
    /// copiar la cámara al viajar a esa vista.
    /// </summary>
    [System.Serializable]
    public class PuntoDeVista
    {
        public string nombreZona;
        public Transform transformObjetivo;
    }

    [Header("Lista de Vistas Definidas")]
    public PuntoDeVista[] listaVistas = new PuntoDeVista[8];

    private Coroutine corrutinaMovimiento;

    /// <summary>
    /// Al despertar el objeto, si no se ha asignado una cámara concreta en el Inspector,
    /// se usa la cámara principal de la escena (<see cref="Camera.main"/>). Después coloca
    /// la cámara de golpe en la primera vista de la lista, sin ninguna animación.
    /// </summary>
    void Awake()
    {
        if (camaraPrincipal == null)
        {
            camaraPrincipal = Camera.main;
        }

        // CORTE INSTANTÁNEO EN EL FOTOGRAMA 0 (Sin animaciones)
        ColocarVistaInstantanea(0);
    }

    /// <summary>
    /// Vuelve a colocar la cámara en la primera vista al iniciar la escena, por si en
    /// <see cref="Awake"/> la escena todavía no había terminado de cargar del todo
    /// (por ejemplo, si el Transform objetivo aún no estaba listo).
    /// </summary>
    void Start()
    {
        // Re-confirmamos en Start por si la escena tarda en cargar
        ColocarVistaInstantanea(0);
    }

    /// <summary>
    /// Coloca la cámara al instante en las coordenadas exactas sin hacer animación
    /// </summary>
    /// <param name="index">Índice de la vista dentro de <see cref="listaVistas"/> a la que se quiere saltar directamente.</param>
    public void ColocarVistaInstantanea(int index)
    {
        if (camaraPrincipal == null) camaraPrincipal = Camera.main;

        if (listaVistas != null && index >= 0 && index < listaVistas.Length)
        {
            Transform destino = listaVistas[index].transformObjetivo;
            if (destino != null && camaraPrincipal != null)
            {
                // Copiamos directamente la posición y rotación del objetivo a la cámara
                camaraPrincipal.transform.position = destino.position;
                camaraPrincipal.transform.rotation = destino.rotation;
            }
        }
    }

    /// <summary>
    /// Mueve la cámara suavemente a la vista (Invocado al pulsar un botón de la UI)
    /// </summary>
    /// <param name="index">Índice de la vista dentro de <see cref="listaVistas"/> hacia la que se quiere viajar con animación.</param>
    public void MoverAVistaIndex(int index)
    {
        if (listaVistas == null || index < 0 || index >= listaVistas.Length) return;

        Transform destino = listaVistas[index].transformObjetivo;
        if (destino != null && camaraPrincipal != null)
        {
            // Si ya había un movimiento de cámara en marcha, lo cancelamos antes de
            // empezar el nuevo, para que no se mezclen dos transiciones a la vez
            if (corrutinaMovimiento != null) StopCoroutine(corrutinaMovimiento);
            corrutinaMovimiento = StartCoroutine(TransicionarCamara(destino.position, destino.rotation));
        }
    }

    /// <summary>
    /// Corrutina que interpola, frame a frame, la posición y rotación de la cámara desde
    /// donde está ahora hasta el punto de destino, usando una curva suave
    /// (<see cref="Mathf.SmoothStep(float, float, float)"/>) para que el movimiento no
    /// empiece ni termine de golpe.
    /// </summary>
    /// <param name="posDestino">Posición final a la que debe llegar la cámara.</param>
    /// <param name="rotDestino">Rotación final que debe tener la cámara.</param>
    private IEnumerator TransicionarCamara(Vector3 posDestino, Quaternion rotDestino)
    {
        float t = 0f;
        Vector3 posInicial = camaraPrincipal.transform.position;
        Quaternion rotInicial = camaraPrincipal.transform.rotation;

        while (t < 1.0f)
        {
            // t avanza con el tiempo, y SmoothStep lo suaviza para que la cámara acelere
            // al principio del movimiento y frene suavemente al final
            t += Time.deltaTime * velocidadTransicion;
            float tSuave = Mathf.SmoothStep(0f, 1f, t);

            camaraPrincipal.transform.position = Vector3.Lerp(posInicial, posDestino, tSuave);
            camaraPrincipal.transform.rotation = Quaternion.Slerp(rotInicial, rotDestino, tSuave);

            yield return null;
        }

        // Al terminar el bucle, forzamos los valores exactos de destino para evitar
        // que, por redondeos, la cámara se quede a medio milímetro/grado del objetivo
        camaraPrincipal.transform.position = posDestino;
        camaraPrincipal.transform.rotation = rotDestino;
    }
}
