using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class ControladorCintaHBW_mqtt : MonoBehaviour
{
    // Dirección espacial (eje) en el que se desplazarán los objetos sobre la cinta
    public Vector3 direccionEntrada = Vector3.forward;

    // Factor de escala para adaptar los valores numéricos recibidos de MQTT a la velocidad en Unity
    public float multiplicadorVel = 0.001f;

    // Variables internas expuestas en el inspector para monitorizar el estado de la cinta en tiempo real
    [SerializeField] private float velocidadActual = 0f;
    [SerializeField] private int factorSentido = 0; // 1 para avanzar, -1 para retroceder, 0 para detenido

    // Lista dinámica que almacena las referencias de los objetos que están actualmente encima de la cinta
    private List<Transform> objetosEnPlataforma = new List<Transform>();

    void Start()
    {
        // Iniciamos la rutina asíncrona que gestiona la conexión con el cliente MQTT
        StartCoroutine(SuscripcionSegura());
    }

    IEnumerator SuscripcionSegura()
    {
        // Bucle de espera condicional: detiene la ejecución hasta que la instancia global de MQTT esté instanciada
        while (MQTTClient.Instance == null) yield return null;

        // Registro del callback al evento de actualización de la cinta transportadora
        MQTTClient.Instance.OnBeltHBWUpdateEvent += (speed, direction) => {
            velocidadActual = speed; // Almacenamos la velocidad bruta recibida

            // Evaluamos la cadena de dirección para determinar el sentido físico del movimiento
            if (direction == "CCW") factorSentido = 1;       // Counter-Clockwise (Sentido antihorario) -> Avanzar
            else if (direction == "CW") factorSentido = -1;  // Clockwise (Sentido horario) -> Retroceder
            else factorSentido = 0;                          // Cualquier otro estado detiene la cinta
        };
    }

    // Método público invocado externamente para registrar un nuevo objeto sobre la cinta
    public void RegistrarObjeto(Transform obj)
    {
        // El "if" es vital para evitar que el objeto se duplique si sus colisionadores activan varios triggers a la vez
        if (!objetosEnPlataforma.Contains(obj))
        {
            objetosEnPlataforma.Add(obj);
        }
    }

    // Método público invocado externamente para remover un objeto de la influencia de la cinta
    public void EliminarObjeto(Transform obj)
    {
        if (objetosEnPlataforma.Contains(obj))
            objetosEnPlataforma.Remove(obj);
    }

    void Update()
    {
        // El movimiento físico sólo se ejecuta si la cinta tiene velocidad asignada, sentido activo y hay objetos registrados
        if (velocidadActual != 0 && factorSentido != 0 && objetosEnPlataforma.Count > 0)
        {
            // Calculamos la distancia de desplazamiento para este frame en base al delta time
            float paso = velocidadActual * factorSentido * multiplicadorVel * Time.deltaTime;
            Vector3 movimiento = direccionEntrada * paso;

            // Recorremos la lista de atrás hacia adelante para poder eliminar elementos nulos de manera segura sin alterar el índice
            for (int i = objetosEnPlataforma.Count - 1; i >= 0; i--)
            {
                if (objetosEnPlataforma[i] != null)
                {
                    // Aplicamos la traslación al objeto en coordenadas globales del mundo
                    objetosEnPlataforma[i].Translate(movimiento, Space.World);
                }
                else
                {
                    // Si el objeto fue destruido o retirado abruptamente de la escena, limpiamos su ranura vacía de la lista
                    objetosEnPlataforma.RemoveAt(i);
                }
            }
        }
    }
}