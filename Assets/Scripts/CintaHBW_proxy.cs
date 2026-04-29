using UnityEngine;

public class CintaHBW_proxy : MonoBehaviour
{
    public ControladorCintaHBW_mqtt controladorPrincipal;

    // Este mensaje debe salir SIEMPRE que algo toque el trigger
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("<color=white><b>[FISICA]</b> Algo ha tocado el Trigger: </color>" + other.name);

        // Si detecta el objeto, forzamos la liberación sin importar el nombre
        if (other.attachedRigidbody != null)
        {
            Transform t = other.attachedRigidbody.transform;

            if (t.parent != null)
            {
                Debug.Log("<color=green><b>[FISICA]</b> Liberando objeto de su padre: </color>" + t.parent.name);
                t.SetParent(null);
            }

            t.GetComponent<Rigidbody>().isKinematic = false;
            t.GetComponent<Rigidbody>().useGravity = true;

            if (controladorPrincipal != null)
                controladorPrincipal.RegistrarObjeto(t);
        }
    }
}