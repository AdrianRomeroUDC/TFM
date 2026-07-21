using UnityEngine;
using System.Collections;

public class UI_ViewController : MonoBehaviour
{
    [Header("Cámara Principal 3D")]
    public Camera camaraPrincipal;

    [Header("Velocidad de Transición")]
    public float velocidadTransicion = 3.0f;

    [System.Serializable]
    public class PuntoDeVista
    {
        public string nombreZona;
        public Transform transformObjetivo;
    }

    [Header("Lista de Vistas Definidas")]
    public PuntoDeVista[] listaVistas = new PuntoDeVista[8];

    private Coroutine corrutinaMovimiento;

    void Awake()
    {
        if (camaraPrincipal == null)
        {
            camaraPrincipal = Camera.main;
        }

        // ⚡ CORTE INSTANTÁNEO EN EL FOTOGRAMA 0 (Sin animaciones)
        ColocarVistaInstantanea(0);
    }

    void Start()
    {
        // Re-confirmamos en Start por si la escena tarda en cargar
        ColocarVistaInstantanea(0);
    }

    /// <summary>
    /// Coloca la cámara al instante en las coordenadas exactas sin hacer animación
    /// </summary>
    public void ColocarVistaInstantanea(int index)
    {
        if (camaraPrincipal == null) camaraPrincipal = Camera.main;

        if (listaVistas != null && index >= 0 && index < listaVistas.Length)
        {
            Transform destino = listaVistas[index].transformObjetivo;
            if (destino != null && camaraPrincipal != null)
            {
                camaraPrincipal.transform.position = destino.position;
                camaraPrincipal.transform.rotation = destino.rotation;
            }
        }
    }

    /// <summary>
    /// Mueve la cámara suavemente a la vista (Invocado al pulsar un botón de la UI)
    /// </summary>
    public void MoverAVistaIndex(int index)
    {
        if (listaVistas == null || index < 0 || index >= listaVistas.Length) return;

        Transform destino = listaVistas[index].transformObjetivo;
        if (destino != null && camaraPrincipal != null)
        {
            if (corrutinaMovimiento != null) StopCoroutine(corrutinaMovimiento);
            corrutinaMovimiento = StartCoroutine(TransicionarCamara(destino.position, destino.rotation));
        }
    }

    private IEnumerator TransicionarCamara(Vector3 posDestino, Quaternion rotDestino)
    {
        float t = 0f;
        Vector3 posInicial = camaraPrincipal.transform.position;
        Quaternion rotInicial = camaraPrincipal.transform.rotation;

        while (t < 1.0f)
        {
            t += Time.deltaTime * velocidadTransicion;
            float tSuave = Mathf.SmoothStep(0f, 1f, t);

            camaraPrincipal.transform.position = Vector3.Lerp(posInicial, posDestino, tSuave);
            camaraPrincipal.transform.rotation = Quaternion.Slerp(rotInicial, rotDestino, tSuave);

            yield return null;
        }

        camaraPrincipal.transform.position = posDestino;
        camaraPrincipal.transform.rotation = rotDestino;
    }
}