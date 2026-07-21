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

    void Start()
    {
        if (camaraPrincipal == null)
        {
            camaraPrincipal = Camera.main;
        }

        // 🚀 SIMULA EL CLIC EN EL BOTÓN 0 AL ARRANCAR
        StartCoroutine(SimularClicBotonVistaCero());
    }

    private IEnumerator SimularClicBotonVistaCero()
    {
        // Esperamos 1 fotograma a que todo en la escena termine de inicializarse
        yield return null;

        // Ejecutamos exactamente la misma función que llama el botón
        MoverAVistaIndex(0);
    }

    /// <summary>
    /// Función invocada tanto al arrancar como por el OnClick() del botón de la UI
    /// </summary>
    public void MoverAVistaIndex(int index)
    {
        if (camaraPrincipal == null)
        {
            camaraPrincipal = Camera.main;
        }

        if (listaVistas == null || index < 0 || index >= listaVistas.Length)
        {
            Debug.LogWarning($"[UI_ViewController] El índice {index} está fuera de rango o la lista no está configurada.");
            return;
        }

        Transform destino = listaVistas[index].transformObjetivo;

        if (destino != null && camaraPrincipal != null)
        {
            if (corrutinaMovimiento != null) StopCoroutine(corrutinaMovimiento);
            corrutinaMovimiento = StartCoroutine(TransicionarCamara(destino.position, destino.rotation));
        }
        else
        {
            Debug.LogWarning($"[UI_ViewController] Falta asignar la Cámara o el Transform Objetivo de la vista {index}.");
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