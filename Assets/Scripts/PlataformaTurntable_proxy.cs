using UnityEngine;

public class PlataformaTurntable_proxy : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Solo actuamos si la pieza entra en la plataforma de la turntable
        if (other.name.ToLower().Contains("pieza"))
        {
            AlinearEnTurntable(other.transform);
        }
    }

    private void AlinearEnTurntable(Transform pieza)
    {
        // Ajustamos la rotación para que quede plana (X:90, Y:0, Z:0)
        pieza.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // Ajustamos posición al centro del collider de la plataforma
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            pieza.position = col.bounds.center;
        }
    }
}