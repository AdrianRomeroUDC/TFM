using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class ControladorCintaHBW_mqtt : MonoBehaviour
{
    public Vector3 direccionEntrada = Vector3.forward;
    public float multiplicadorVel = 0.001f;

    [SerializeField] private float velocidadActual = 0f;
    [SerializeField] private int factorSentido = 0;

    private List<Transform> objetosEnPlataforma = new List<Transform>();

    void Start()
    {
        StartCoroutine(SuscripcionSegura());
    }

    IEnumerator SuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;

        MQTTClient.Instance.OnBeltHBWUpdateEvent += (speed, direction) => {
            velocidadActual = speed;
            if (direction == "CCW") factorSentido = 1;
            else if (direction == "CW") factorSentido = -1;
            else factorSentido = 0;
        };
    }

    public void RegistrarObjeto(Transform obj)
{
    // El "if" es vital para que no se duplique si toca dos hijos a la vez
    if (!objetosEnPlataforma.Contains(obj))
    {
        objetosEnPlataforma.Add(obj);
    }
}

    public void EliminarObjeto(Transform obj)
    {
        if (objetosEnPlataforma.Contains(obj))
            objetosEnPlataforma.Remove(obj);
    }

    void Update()
    {
        if (velocidadActual != 0 && factorSentido != 0 && objetosEnPlataforma.Count > 0)
        {
            float paso = velocidadActual * factorSentido * multiplicadorVel * Time.deltaTime;
            Vector3 movimiento = direccionEntrada * paso;

            for (int i = objetosEnPlataforma.Count - 1; i >= 0; i--)
            {
                if (objetosEnPlataforma[i] != null)
                {
                    objetosEnPlataforma[i].Translate(movimiento, Space.World);
                }
                else
                {
                    objetosEnPlataforma.RemoveAt(i);
                }
            }
        }
    }
}