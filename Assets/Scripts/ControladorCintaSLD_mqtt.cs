using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ControladorCintaSLD_mqtt : MonoBehaviour
{
    [Header("Referencia a la Cinta")]
    public Transform objetoCintaPadre;

    [Header("Configuración de Movimiento")]
    public float multiplicadorVelocidad = 0.001f;
    [SerializeField] private float velocidadActual = 0f; // La ponemos serializada para verla en el Inspector

    private List<Transform> eslabonesOrdenados = new List<Transform>();
    private Vector3[] posRailes;
    private Quaternion[] rotRailes;
    private float progresoCiclo = 0f;

    // --- CONEXIÓN CON EL DELEGADO ---
    void OnEnable()
    {
        // Suscribirse al evento del Manager
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnBeltUpdateEvent += ActualizarVelocidad;
    }

    void OnDisable()
    {
        // Desuscribirse al desactivar
        if (MQTTClient.Instance != null)
            MQTTClient.Instance.OnBeltUpdateEvent -= ActualizarVelocidad;
    }

    void Start()
    {
        if (objetoCintaPadre == null)
        {
            Debug.LogError("¡Falta asignar el Objeto Cinta Padre en el Inspector!");
            return;
        }

        ConfigurarEslabones();
    }

    void ConfigurarEslabones()
    {
        // 1. Recoger todos los hijos del padre
        List<Transform> sinOrdenar = new List<Transform>();
        foreach (Transform t in objetoCintaPadre) sinOrdenar.Add(t);

        if (sinOrdenar.Count == 0) return;

        // 2. Ordenarlos por cercanía física para formar la cadena
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

        // 3. Guardar la "foto" de las posiciones y rotaciones iniciales
        posRailes = new Vector3[eslabonesOrdenados.Count];
        rotRailes = new Quaternion[eslabonesOrdenados.Count];

        for (int i = 0; i < eslabonesOrdenados.Count; i++)
        {
            posRailes[i] = eslabonesOrdenados[i].localPosition;
            rotRailes[i] = eslabonesOrdenados[i].localRotation;
        }
    }

    void ActualizarVelocidad(float nuevaVelocidad)
    {
        velocidadActual = nuevaVelocidad;
    }

    void Update()
    {
        // Solo se mueve si recibimos velocidad > 0 y tenemos eslabones configurados
        if (velocidadActual > 0 && eslabonesOrdenados.Count > 0)
        {
            progresoCiclo += velocidadActual * multiplicadorVelocidad * Time.deltaTime;

            // Si el progreso supera 1 (un paso de eslabón), reiniciamos
            if (progresoCiclo >= 1f) progresoCiclo -= 1f;

            for (int i = 0; i < eslabonesOrdenados.Count; i++)
            {
                int sigIdx = (i + 1) % eslabonesOrdenados.Count;

                // Interpolación entre el punto actual y el siguiente del carril
                eslabonesOrdenados[i].localPosition = Vector3.Lerp(posRailes[i], posRailes[sigIdx], progresoCiclo);
                eslabonesOrdenados[i].localRotation = Quaternion.Slerp(rotRailes[i], rotRailes[sigIdx], progresoCiclo);
            }
        }
    }
}