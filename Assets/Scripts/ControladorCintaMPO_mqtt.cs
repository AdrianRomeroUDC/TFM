using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ControladorCintaMPO_mqtt : MonoBehaviour
{
    [Header("Referencias")]
    public Transform objetoCintaPadre;
    public Transform railesCintaMPO;

    [Header("Configuración")]
    public float velocidadFija = 512f;
    public float multiplicadorVelocidad = 0.001f;

    private bool estaActiva = false;
    private List<Transform> eslabones;
    private Vector3[] puntosRail;
    private float progresoCiclo = 0f;

    void Start()
    {
        // 1. Obtener eslabones tal cual están en la jerarquía
        eslabones = objetoCintaPadre.GetComponentsInChildren<Transform>()
                    .Where(t => t != objetoCintaPadre).ToList();

        // 2. Obtener puntos del raíl en el orden de sus hijos (esto define el camino real)
        puntosRail = railesCintaMPO.GetComponentsInChildren<Transform>()
                    .Where(t => t != railesCintaMPO)
                    .Select(t => t.localPosition)
                    .ToArray();

        InvokeRepeating("IntentarSuscripcion", 0f, 1f);
    }

    void IntentarSuscripcion()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnMPOBeltUpdateEvent += (bool activo) => estaActiva = activo;
            CancelInvoke("IntentarSuscripcion");
        }
    }

    void Update()
    {
        if (estaActiva && eslabones.Count > 0 && puntosRail.Length > 1)
        {
            progresoCiclo += velocidadFija * multiplicadorVelocidad * Time.deltaTime;
            if (progresoCiclo >= 1f) progresoCiclo -= 1f;

            for (int i = 0; i < eslabones.Count; i++)
            {
                // Distribuimos los eslabones a lo largo de la ruta de puntosRail
                float offset = (float)i / eslabones.Count;
                float t = (progresoCiclo + offset) % 1f;

                float rutaPos = t * (puntosRail.Length - 1);
                int idxA = Mathf.FloorToInt(rutaPos);
                int idxB = (idxA + 1) % puntosRail.Length;
                float localT = rutaPos - idxA;

                // Mover eslabón exactamente a la ruta definida por los hijos de RailesCintaMPO
                eslabones[i].localPosition = Vector3.Lerp(puntosRail[idxA], puntosRail[idxB], localT);
            }
        }
    }
}