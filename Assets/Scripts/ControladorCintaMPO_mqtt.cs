using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ControladorCintaMPO_mqtt : MonoBehaviour
{
    [Header("Referencia a la Cinta")]
    public Transform objetoCintaPadre;

    [Header("Configuración de Movimiento")]
    public float multiplicadorVelocidad = 0.001f;
    private float velocidadActual = 0f;

    private List<Transform> eslabonesOrdenados = new List<Transform>();
    private Vector3[] posRailes;
    private float[] xRotRailes;
    private float progresoCiclo = 0f;

    void Start()
    {
        if (objetoCintaPadre == null) return;
        ConfigurarEslabones();
        InvokeRepeating("IntentarSuscripcion", 0.1f, 1f);
    }

    void IntentarSuscripcion()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnMPOBeltUpdateEvent += (activo) => velocidadActual = activo ? 512f : 0f;
            Debug.Log("<color=cyan><b>Cinta MPO:</b> Conectado con éxito.</color>");
            CancelInvoke("IntentarSuscripcion");
        }
    }

    void ConfigurarEslabones()
    {
        List<Transform> sinOrdenar = new List<Transform>();
        foreach (Transform t in objetoCintaPadre) sinOrdenar.Add(t);
        if (sinOrdenar.Count == 0) return;

        eslabonesOrdenados.Clear();
        Transform actual = sinOrdenar[0];
        eslabonesOrdenados.Add(actual);
        sinOrdenar.RemoveAt(0);

        while (sinOrdenar.Count > 0)
        {
            Transform masCercano = sinOrdenar
                .OrderBy(t => Vector3.Distance(t.localPosition, actual.localPosition))
                .First();
            eslabonesOrdenados.Add(masCercano);
            sinOrdenar.Remove(masCercano);
            actual = masCercano;
        }

        int total = eslabonesOrdenados.Count;
        posRailes = new Vector3[total];
        xRotRailes = new float[total];

        for (int i = 0; i < total; i++)
        {
            posRailes[i] = eslabonesOrdenados[i].localPosition;
            float x = eslabonesOrdenados[i].localRotation.eulerAngles.x;

            // Normalización estricta: si Unity dice -90, nosotros guardamos 270.
            if (x < 0) x += 360f;
            // Si está muy cerca de 360 (que es 0), lo tratamos como 0 para la suma.
            if (x > 359f) x = 0f;

            xRotRailes[i] = x;
        }
    }

    void Update()
    {
        if (velocidadActual > 0 && eslabonesOrdenados.Count > 0)
        {
            progresoCiclo += velocidadActual * multiplicadorVelocidad * Time.deltaTime;
            if (progresoCiclo >= 1f) progresoCiclo -= 1f;

            for (int i = 0; i < eslabonesOrdenados.Count; i++)
            {
                int sigIdx = (i + 1) % eslabonesOrdenados.Count;

                // 1. POSICIÓN
                eslabonesOrdenados[i].localPosition = Vector3.Lerp(posRailes[i], posRailes[sigIdx], progresoCiclo);

                // 2. ROTACIÓN X (Lógica Antiresta)
                float angA = xRotRailes[i];
                float angB = xRotRailes[sigIdx];

                // FORZADO: Si vamos de 90 a 270, angB debe ser numéricamente mayor que angA.
                // Si angB es menor (ej. 90 -> 270 que Unity lee como -90 o similar), sumamos 360.
                if (angB < angA && Mathf.Abs(angB - angA) > 100f)
                {
                    angB += 360f;
                }

                // Interpolación lineal pura (Mathf.Lerp no es inteligente, solo suma/resta números)
                float xCalculada = Mathf.Lerp(angA, angB, progresoCiclo);

                // TOPE MÁXIMO: Si el cálculo se pasa de 270 debido al forzado, lo clavamos en 270.
                if (xCalculada > 270f) xCalculada = 270f;

                // 3. APLICACIÓN
                eslabonesOrdenados[i].localRotation = Quaternion.Euler(xCalculada, 0f, -90f);
            }
        }
    }
}