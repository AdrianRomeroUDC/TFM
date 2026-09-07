using UnityEngine;
using System.IO;

public class GeneradorCapturasVistas : MonoBehaviour
{
    public Camera camaraPrincipal;
    public UI_ViewController controllerVistas;

    [ContextMenu("📸 ¡Generar las 8 Capturas de Vista!")]
    public void GenerarCapturas()
    {
        if (camaraPrincipal == null) camaraPrincipal = Camera.main;
        if (controllerVistas == null) controllerVistas = GetComponent<UI_ViewController>();

        if (controllerVistas == null || controllerVistas.listaVistas == null)
        {
            Debug.LogError("No se encontró el script UI_ViewController o la lista de vistas está vacía.");
            return;
        }

        string rutaCarpeta = Application.dataPath + "/Sprites/Previews/";
        if (!Directory.Exists(rutaCarpeta)) Directory.CreateDirectory(rutaCarpeta);

        RenderTexture rt = new RenderTexture(512, 512, 24);
        camaraPrincipal.targetTexture = rt;
        Texture2D screenShot = new Texture2D(512, 512, TextureFormat.RGB24, false);

        for (int i = 0; i < controllerVistas.listaVistas.Length; i++)
        {
            var vista = controllerVistas.listaVistas[i];
            if (vista.transformObjetivo == null) continue;

            // Posicionamos la cámara en el punto exacto
            camaraPrincipal.transform.position = vista.transformObjetivo.position;
            camaraPrincipal.transform.rotation = vista.transformObjetivo.rotation;

            // Renderizamos el fotograma
            camaraPrincipal.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            screenShot.Apply();

            // 🚀 LIMPIEZA DE CARACTERES: Limpiamos caracteres prohibidos en archivos (\, /, :, *, etc.)
            string nombreBruto = string.IsNullOrEmpty(vista.nombreZona) ? $"Vista_{i}" : vista.nombreZona;
            string nombreLimpio = LimpiarNombreArchivo(nombreBruto);

            // Guardamos el PNG de forma segura
            byte[] bytes = screenShot.EncodeToPNG();
            File.WriteAllBytes(rutaCarpeta + nombreLimpio + ".png", bytes);
        }

        camaraPrincipal.targetTexture = null;
        RenderTexture.active = null;
        DestroyImmediate(rt);

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif

        Debug.Log("<color=green><b>¡Las capturas se han guardado con éxito en Assets/Sprites/Previews!</b></color>");
    }

    private string LimpiarNombreArchivo(string nombreOriginal)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            nombreOriginal = nombreOriginal.Replace(c, '_');
        }
        return nombreOriginal;
    }
}