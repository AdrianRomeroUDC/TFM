using UnityEngine;

public class CintaHBW_proxy : MonoBehaviour
{
    // Referencia al controlador principal que gestionará el desplazamiento lineal del objeto capturado
    public ControladorCintaHBW_mqtt controladorPrincipal;

    // Este mensaje debe salir SIEMPRE que algo toque el trigger
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("<color=white><b>[FISICA]</b> Algo ha tocado el Trigger: </color>" + other.name);

        // Si el objeto que ingresa posee un cuerpo rígido asociado (en sí mismo o en sus ancestros)...
        if (other.attachedRigidbody != null)
        {
            // Obtenemos la transformación raíz que contiene las físicas
            Transform t = other.attachedRigidbody.transform;

            // Si el objeto viene acoplado a la plataforma del transelevador o a otro objeto...
            if (t.parent != null)
            {
                Debug.Log("<color=green><b>[FISICA]</b> Liberando objeto de su padre: </color>" + t.parent.name);
                t.SetParent(null); // Desvinculamos el contenedor para dejarlo autónomo en la jerarquía mundial
            }

            // Desactivamos el modo cinemático para transferir el control absoluto al motor de físicas de Unity
            t.GetComponent<Rigidbody>().isKinematic = false;
            t.GetComponent<Rigidbody>().useGravity = true; // Habilitamos la gravedad para que asiente sobre la cinta

            // Registramos el objeto transformado en el pool del controlador de la cinta para que comience a desplazarse
            if (controladorPrincipal != null)
                controladorPrincipal.RegistrarObjeto(t);
        }
    }
}