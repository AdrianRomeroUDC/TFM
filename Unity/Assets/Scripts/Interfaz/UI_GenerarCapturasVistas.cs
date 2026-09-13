using UnityEngine;
using System.IO;

/// <summary>
/// Herramienta de apoyo para el equipo de desarrollo (no la usa el usuario final):
/// genera automáticamente una imagen PNG de cada "vista" o punto de cámara guardado
/// en <see cref="UI_ViewController"/> (esos puntos de vista predefinidos que el usuario
/// puede elegir en la interfaz para moverse rápidamente por la fábrica: HBW, VGR, DPS,
/// MPO, SLD, SSC, etc.). Estas imágenes sirven, por ejemplo, como miniaturas/previews
/// para los botones de selección de vista. Se ejecuta a mano desde el menú contextual
/// del componente en el Editor de Unity (clic derecho → "Generar las 8 Capturas de Vista"),
/// no ocurre nunca automáticamente durante el juego.
/// </summary>
public class GeneradorCapturasVistas : MonoBehaviour
{
    public Camera camaraPrincipal;
    public UI_ViewController controllerVistas;

    /// <summary>
    /// Recorre todos los puntos de vista definidos en <see cref="UI_ViewController"/>,
    /// coloca la cámara en cada uno de ellos, hace una foto cuadrada de 512x512 píxeles
    /// y la guarda como archivo PNG dentro de "Assets/Sprites/Previews/". El nombre del
    /// archivo se toma del nombre de la zona (por ejemplo "HBW", "VGR"...), limpiando
    /// antes cualquier carácter que no esté permitido en nombres de archivo.
    /// </summary>
    [ContextMenu("📸 ¡Generar las 8 Capturas de Vista!")]
    public void GenerarCapturas()
    {
        // Si no se ha asignado nada a mano en el Inspector, intentamos buscarlo automáticamente
        if (camaraPrincipal == null) camaraPrincipal = Camera.main;
        if (controllerVistas == null) controllerVistas = GetComponent<UI_ViewController>();

        if (controllerVistas == null || controllerVistas.listaVistas == null)
        {
            Debug.LogError("No se encontró el script UI_ViewController o la lista de vistas está vacía.");
            return;
        }

        // Nos aseguramos de que exista la carpeta donde se guardarán las capturas
        string rutaCarpeta = Application.dataPath + "/Sprites/Previews/";
        if (!Directory.Exists(rutaCarpeta)) Directory.CreateDirectory(rutaCarpeta);

        // Creamos una "textura de renderizado" de 512x512: en vez de dibujar en pantalla,
        // la cámara dibujará dentro de esta imagen en memoria
        RenderTexture rt = new RenderTexture(512, 512, 24);
        camaraPrincipal.targetTexture = rt;
        Texture2D screenShot = new Texture2D(512, 512, TextureFormat.RGB24, false);

        // Recorremos cada punto de vista guardado (HBW, VGR, DPS, MPO, SLD, SSC...)
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

            // LIMPIEZA DE CARACTERES: Limpiamos caracteres prohibidos en archivos (\, /, :, *, etc.)
            string nombreBruto = string.IsNullOrEmpty(vista.nombreZona) ? $"Vista_{i}" : vista.nombreZona;
            string nombreLimpio = LimpiarNombreArchivo(nombreBruto);

            // Guardamos el PNG de forma segura
            byte[] bytes = screenShot.EncodeToPNG();
            File.WriteAllBytes(rutaCarpeta + nombreLimpio + ".png", bytes);
        }

        // Dejamos la cámara como estaba antes de empezar (renderizando a pantalla, no a la textura)
        camaraPrincipal.targetTexture = null;
        RenderTexture.active = null;
        DestroyImmediate(rt);

#if UNITY_EDITOR
        // Refrescamos la vista del Editor para que las nuevas imágenes aparezcan de inmediato en el panel de Assets
        UnityEditor.AssetDatabase.Refresh();
#endif

        Debug.Log("<color=green><b>¡Las capturas se han guardado con éxito en Assets/Sprites/Previews!</b></color>");
    }

    /// <summary>
    /// Sustituye por guiones bajos ("_") cualquier carácter que el sistema operativo
    /// no permita usar en un nombre de archivo (como \, /, :, *, ?, etc.), para evitar
    /// errores al guardar la imagen en disco.
    /// </summary>
    /// <param name="nombreOriginal">Nombre de la zona tal cual está guardado en la vista.</param>
    /// <returns>El mismo nombre, pero seguro para usarse como nombre de archivo.</returns>
    private string LimpiarNombreArchivo(string nombreOriginal)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            nombreOriginal = nombreOriginal.Replace(c, '_');
        }
        return nombreOriginal;
    }
}
