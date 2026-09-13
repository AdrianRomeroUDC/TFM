using System.Collections.Generic;
using UnityEngine;
using System.Collections;

/// <summary>
/// Controla la cinta transportadora que hay DENTRO del almacén HBW (no confundir con las cintas de
/// otras estaciones): es la que mueve el cajón/pieza desde la puerta del almacén hasta la posición
/// donde el carro (brazo transelevador) puede cogerla, y viceversa. Se suscribe al evento de
/// <see cref="MQTTClient"/> que informa de la velocidad y el sentido reales de esta cinta, y cada
/// frame desplaza en Unity todos los objetos que estén encima de ella para que se vea el mismo
/// movimiento que hace la cinta física.
/// </summary>
public class ControladorCintaHBW_mqtt : MonoBehaviour
{
    // Eje del espacio 3D sobre el que se desplazan las piezas al ir encima de la cinta (normalmente "hacia delante").
    public Vector3 direccionEntrada = Vector3.forward;

    // Factor de escala para convertir la velocidad "cruda" que manda la fábrica real (MQTT) en una velocidad razonable dentro de Unity.
    public float multiplicadorVel = 0.001f;

    // Variables visibles en el Inspector para poder comprobar en vivo qué velocidad y sentido tiene la cinta real en cada momento.
    [SerializeField] private float velocidadActual = 0f;
    [SerializeField] private int factorSentido = 0; // 1 para avanzar, -1 para retroceder, 0 para detenido

    // Lista de todas las piezas/objetos 3D que están ahora mismo encima de la cinta y que, por tanto, deben moverse con ella.
    private List<Transform> objetosEnPlataforma = new List<Transform>();

    void Start()
    {
        // Lanzamos la corrutina que espera al cliente MQTT y se suscribe a los avisos de la cinta real.
        StartCoroutine(SuscripcionSegura());
    }

    // Espera a que el cliente MQTT ya exista en la escena antes de suscribirse a sus eventos, para no
    // intentar leer una referencia que todavía no se ha creado.
    IEnumerator SuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;

        // Cada vez que la fábrica real informa de un cambio de velocidad/sentido en la cinta del HBW,
        // guardamos esos datos para poder mover los objetos 3D exactamente igual en Update().
        MQTTClient.Instance.OnBeltHBWUpdateEvent += (speed, direction) => {
            velocidadActual = speed; // Guardamos la velocidad que reporta la cinta real.

            // Traducimos el sentido de giro real del motor a un signo que usaremos para mover las piezas.
            if (direction == "CCW") factorSentido = 1;       // Sentido antihorario -> la cinta avanza (hacia la salida)
            else if (direction == "CW") factorSentido = -1;  // Sentido horario -> la cinta retrocede (hacia la entrada)
            else factorSentido = 0;                          // Cualquier otro valor significa que la cinta está parada
        };
    }

    // Los scripts de las piezas o del proxy de la cinta llaman a este método cuando una pieza entra
    // en contacto con la cinta, para que a partir de ahora se mueva con ella.
    public void RegistrarObjeto(Transform obj)
    {
        // Comprobamos que no esté ya en la lista, para no moverla el doble de rápido si varios sensores la detectan a la vez.
        if (!objetosEnPlataforma.Contains(obj))
        {
            objetosEnPlataforma.Add(obj);
        }
    }

    // Se llama cuando una pieza sale de la cinta (por ejemplo, la coge el carro del HBW), para que deje de arrastrarse con ella.
    public void EliminarObjeto(Transform obj)
    {
        if (objetosEnPlataforma.Contains(obj))
            objetosEnPlataforma.Remove(obj);
    }

    void Update()
    {
        // Solo movemos piezas si la cinta real tiene velocidad, tiene un sentido definido y además hay algo encima de ella.
        if (velocidadActual != 0 && factorSentido != 0 && objetosEnPlataforma.Count > 0)
        {
            // Calculamos cuánto debe avanzar cada pieza en este frame, según la velocidad real de la cinta.
            float paso = velocidadActual * factorSentido * multiplicadorVel * Time.deltaTime;
            Vector3 movimiento = direccionEntrada * paso;

            // Recorremos la lista de atrás hacia delante para poder quitar piezas destruidas sin liarnos con los índices.
            for (int i = objetosEnPlataforma.Count - 1; i >= 0; i--)
            {
                if (objetosEnPlataforma[i] != null)
                {
                    // Desplazamos la pieza en coordenadas del mundo, igual que la movería la cinta física.
                    objetosEnPlataforma[i].Translate(movimiento, Space.World);
                }
                else
                {
                    // Si la pieza ya no existe (fue destruida o retirada de golpe), la quitamos de la lista.
                    objetosEnPlataforma.RemoveAt(i);
                }
            }
        }
    }
}
