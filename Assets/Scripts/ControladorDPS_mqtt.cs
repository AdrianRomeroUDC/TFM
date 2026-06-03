using UnityEngine;
using System.Collections;

public class ControladorDPS_mqtt : MonoBehaviour
{
    [Header("Referencias")]
    public Transform puntoEntrada;
    public Transform pinzaVGR;

    [Header("Prefabs Visuales")]
    public GameObject prefabBaseGris;
    public GameObject prefabBlanco;
    public GameObject prefabRojo;
    public GameObject prefabAzul;

    private GameObject piezaActual;
    private string modoPendiente = "";
    private bool gripActivo = false;

    void Start()
    {
        StartCoroutine(IntentarSuscripcionSegura());
    }

    IEnumerator IntentarSuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;

        MQTTClient.Instance.OnDPSPiezaEvent += ActualizarPieza;
        MQTTClient.Instance.OnDPSColorEvent += ActualizarColor;
        MQTTClient.Instance.OnVGRGripEvent += ActualizarGrip;

        Debug.Log("<color=green><b>DPS:</b> Suscripción completada con éxito.</color>");
    }

    private void OnDisable()
    {
        if (MQTTClient.Instance != null)
        {
            MQTTClient.Instance.OnDPSPiezaEvent -= ActualizarPieza;
            MQTTClient.Instance.OnDPSColorEvent -= ActualizarColor;
            MQTTClient.Instance.OnVGRGripEvent -= ActualizarGrip;
        }
    }

    private void ActualizarPieza(bool detectada)
    {
        if (detectada) modoPendiente = "SPAWN_BASE";
        else if (!gripActivo) modoPendiente = "DELETE";
    }

    private void ActualizarColor(string color)
    {
        if (color == "BLUE" || color == "RED" || color == "WHITE")
            modoPendiente = color;
    }

    private void ActualizarGrip(bool activo)
    {
        gripActivo = activo;
        if (!activo) modoPendiente = "DROP";
    }

    void Update()
    {
        if (modoPendiente != "")
        {
            EjecutarOrden();
            modoPendiente = "";
        }
    }

    void EjecutarOrden()
    {
        switch (modoPendiente)
        {
            case "SPAWN_BASE":
                if (piezaActual != null) Destroy(piezaActual);
                piezaActual = Instantiate(prefabBaseGris, puntoEntrada.position, puntoEntrada.rotation);
                ConfigurarFisicas(piezaActual);
                break;

            case "DELETE":
                if (piezaActual != null && !gripActivo && piezaActual.transform.parent == null)
                {
                    Destroy(piezaActual);
                }
                piezaActual = null;
                break;

            case "DROP":
                if (piezaActual != null)
                {
                    piezaActual.transform.SetParent(null);
                    Rigidbody rb = piezaActual.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.useGravity = true;
                    }
                }
                break;

            case "WHITE":
            case "RED":
            case "BLUE":
                MutarColorSinDestruir(modoPendiente);
                break;
        }
    }

    // >>> MUTACIÓN BLINDADA: PRESERVA Y REGENERA EL BOXCOLLIDER <<<
    void MutarColorSinDestruir(string color)
    {
        if (piezaActual == null) return;

        // 1. Identificamos el prefab destino
        GameObject prefabDestino = (color == "WHITE") ? prefabBlanco : (color == "RED") ? prefabRojo : prefabAzul;
        if (prefabDestino == null) return;

        // 2. Limpiamos cualquier elemento visual/malla anterior que tuviera la pieza base
        foreach (var mesh in piezaActual.GetComponentsInChildren<MeshRenderer>())
        {
            if (mesh.gameObject != piezaActual) Destroy(mesh.gameObject);
        }
        if (piezaActual.TryGetComponent<MeshFilter>(out MeshFilter mfRaiz)) Destroy(mfRaiz);
        if (piezaActual.TryGetComponent<MeshRenderer>(out MeshRenderer mrRaiz)) Destroy(mrRaiz);

        // 3. Clonamos el aspecto visual del nuevo prefab dentro de nuestra pieza viva
        GameObject visualNuevo = Instantiate(prefabDestino, piezaActual.transform.position, piezaActual.transform.rotation, piezaActual.transform);
        visualNuevo.transform.localPosition = Vector3.zero;
        visualNuevo.transform.localRotation = Quaternion.identity;
        visualNuevo.transform.localScale = Vector3.one;

        // Renombramos el objeto raíz
        piezaActual.name = "pieza_" + color.ToLower();

        // >>> SOLUCIÓN AL BOXCOLLIDER ELIMINADO <<<
        // Nos aseguramos de que la raíz de la pieza conserve o tenga un BoxCollider activo
        BoxCollider colRaiz = piezaActual.GetComponent<BoxCollider>();
        if (colRaiz == null)
        {
            colRaiz = piezaActual.AddComponent<BoxCollider>();
        }

        // Buscamos si el nuevo modelo clonado traía un colisionador interno para heredar sus medidas
        BoxCollider colHijo = visualNuevo.GetComponentInChildren<BoxCollider>();
        if (colHijo != null)
        {
            // Transferimos el tamaño exacto y el centro al colisionador de la raíz
            colRaiz.center = colHijo.center;
            colRaiz.size = colHijo.size;

            // Destruimos el del hijo inmediatamente para que no haya colisiones duplicadas
            Destroy(colHijo);
        }

        // IMPORTANTE: Mantenemos el colisionador de la raíz configurado correctamente
        // Si el VGR lo tiene sujeto, el propio script del VGR se encargará de pasarlo temporalmente a Trigger,
        // pero al mutar nos aseguramos de que el componente exista y esté listo.
        colRaiz.isTrigger = gripActivo;

        Debug.Log($"<color=cyan><b>[DPS Mutación]:</b> Pieza mutó a {color}. BoxCollider asegurado en la raíz con éxito.</color>");
    }

    void ConfigurarFisicas(GameObject p)
    {
        p.name = "pieza_base";
        Rigidbody rb = p.GetComponent<Rigidbody>();
        if (rb == null) rb = p.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        BoxCollider colFisico = p.GetComponent<BoxCollider>();
        if (colFisico == null) colFisico = p.AddComponent<BoxCollider>();
        colFisico.isTrigger = false;
    }
}