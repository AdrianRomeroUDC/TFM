using UnityEngine;

public class ControladorCilindrosSLD_mqtt : MonoBehaviour
{
    [Header("Referencias de Pistones")]
    public Transform pistonBlanco;
    public Transform pistonRojo;
    public Transform pistonAzul;

    [Header("Coordenadas Estándar (Rojo y Azul)")]
    public float xReposoEstandar = 0.001122198f;
    public float xEstiradoEstandar = 0.000826f;

    [Header("Coordenadas Especiales (Blanco)")]
    public float xReposoBlanco = 0.0002811983f;
    public float xEstiradoBlanco = -0.0000149997f; // -2.9e-05 convertido a float

    [Header("Ajustes")]
    public float velocidadPiston = 0.01f;

    private float targetBlanco, targetRojo, targetAzul;

    void OnEnable()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnCylinderUpdateEvent += ProcesarComandoCilindro;
    }

    void OnDisable()
    {
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnCylinderUpdateEvent -= ProcesarComandoCilindro;
    }

    void ProcesarComandoCilindro(string color, int estado)
    {
        float valor = (float)estado;
        if (color == "WHITE") targetBlanco = valor;
        else if (color == "RED") targetRojo = valor;
        else if (color == "BLUE") targetAzul = valor;
    }

    void Update()
    {
        // El blanco usa sus propias coordenadas
        MoverPiston(pistonBlanco, targetBlanco, xReposoBlanco, xEstiradoBlanco);

        // El rojo y azul usan las estándar
        MoverPiston(pistonRojo, targetRojo, xReposoEstandar, xEstiradoEstandar);
        MoverPiston(pistonAzul, targetAzul, xReposoEstandar, xEstiradoEstandar);
    }

    void MoverPiston(Transform piston, float estadoActual, float reposo, float estirado)
    {
        if (piston == null) return;

        // Calculamos el objetivo usando las coordenadas que le pasemos
        float xObjetivo = Mathf.Lerp(reposo, estirado, estadoActual);

        Vector3 pos = piston.localPosition;
        pos.x = Mathf.MoveTowards(pos.x, xObjetivo, velocidadPiston * Time.deltaTime);
        piston.localPosition = pos;
    }
}