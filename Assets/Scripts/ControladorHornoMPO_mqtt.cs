using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ControladorHorno_mqtt : MonoBehaviour
{
    [Header("Componentes")]
    public Transform puerta;
    [Tooltip("Asigna aquí el objeto raíz 'Horno_BasePonerPieza'. El script buscará automáticamente el hijo que contenga la palabra 'plataforma'.")]
    public Transform plataformaPieza;
    public Light luzHorno;

    [Header("Prefabs de Auto-Sanación")]
    [Tooltip("Arrastra aquí el prefab de tu pieza base gris (el mismo que usa el DPS).")]
    public GameObject prefabBaseGris;

    [Header("Posiciones (Usar clic derecho para capturar)")]
    [ContextMenuItem("Capturar Cerrada", "CapturarPuertaCerrada")] public Vector3 posPuertaCerrada;
    [ContextMenuItem("Capturar Abierta", "CapturarPuertaAbierta")] public Vector3 posPuertaAbierta;
    [ContextMenuItem("Capturar Fuera", "CapturarPlataformaFuera")] public Vector3 posPlataformaFuera;
    [ContextMenuItem("Capturar Dentro", "CapturarPlataformaDentro")] public Vector3 posPlataformaDentro;

    [Header("Configuración de Velocidad (Duración en segundos)")]
    public float duracionMovimientoPuerta = 1.5f;
    public float duracionMovimientoPlataforma = 1.0f;

    private Queue<MPOHornoPayload> colaMensajes = new Queue<MPOHornoPayload>();
    private Coroutine movimientoPuerta;
    private Coroutine movimientoPlataforma;

    void CapturarPuertaCerrada() => posPuertaCerrada = puerta.position;
    void CapturarPuertaAbierta() => posPuertaAbierta = puerta.position;
    void CapturarPlataformaFuera() => posPlataformaFuera = plataformaPieza.position;
    void CapturarPlataformaDentro() => posPlataformaDentro = plataformaPieza.position;

    private void Start() => StartCoroutine(SuscripcionSegura());

    private IEnumerator SuscripcionSegura()
    {
        while (MQTTClient.Instance == null) yield return null;

        MQTTClient.Instance.OnHornoUpdateEvent += (data) => {
            lock (colaMensajes) { colaMensajes.Enqueue(data); }
        };
        Debug.Log("<color=cyan>Controlador Horno suscrito correctamente</color>");
    }

    private void Update()
    {
        lock (colaMensajes)
        {
            while (colaMensajes.Count > 0)
            {
                ProcesarHorno(colaMensajes.Dequeue());
            }
        }
    }

    private void ProcesarHorno(MPOHornoPayload data)
    {
        if (luzHorno) luzHorno.enabled = (data.lights == 1);

        // MOVIMIENTO PUERTA
        if (data.openDoor == 1)
        {
            if (movimientoPuerta != null) StopCoroutine(movimientoPuerta);
            movimientoPuerta = StartCoroutine(MoverObjeto(puerta, posPuertaAbierta, duracionMovimientoPuerta));
        }
        else if (data.closeDoor == 1)
        {
            if (movimientoPuerta != null) StopCoroutine(movimientoPuerta);
            movimientoPuerta = StartCoroutine(MoverObjeto(puerta, posPuertaCerrada, duracionMovimientoPuerta));
        }

        // MOVIMIENTO PLATAFORMA
        if (data.move2Ref5 == 1)
        {
            if (movimientoPlataforma != null) StopCoroutine(movimientoPlataforma);
            movimientoPlataforma = StartCoroutine(MoverObjeto(plataformaPieza, posPlataformaDentro, duracionMovimientoPlataforma));
        }
        else if (data.move2Ref6 == 1)
        {
            if (movimientoPlataforma != null) StopCoroutine(movimientoPlataforma);
            movimientoPlataforma = StartCoroutine(MoverObjeto(plataformaPieza, posPlataformaFuera, duracionMovimientoPlataforma));
        }

        // SENSOR DEL HORNO: Solo actúa si detecta pieza (True).
        if (data.ovenSensor == 1)
        {
            IntentarSpawnPiezaHorno();
        }
    }

    private Transform BuscarPlataformaRealHijo()
    {
        if (plataformaPieza == null) return null;

        if (plataformaPieza.name.ToLower().Contains("plataforma")) return plataformaPieza;

        foreach (Transform t in plataformaPieza.GetComponentsInChildren<Transform>(true))
        {
            if (t != plataformaPieza && t.name.ToLower().Contains("plataforma"))
            {
                return t;
            }
        }

        return plataformaPieza;
    }

    private void IntentarSpawnPiezaHorno()
    {
        if (plataformaPieza == null) return;

        if (prefabBaseGris == null)
        {
            Debug.LogError("<color=red><b>[HORNO SPAWN - ERROR]:</b> ¡Falta asignar el Prefab Base Gris en el Inspector!</color>");
            return;
        }

        // 🛡️ ESCUDO DE PROTECCIÓN VGR
        ControladorVGR_mqtt vgr = Object.FindFirstObjectByType<ControladorVGR_mqtt>();
        if (vgr != null && vgr.ObtenerPiezaEnganchada() != null)
        {
            Debug.Log("<color=yellow><b>[HORNO SPAWN]:</b> El VGR tiene una pieza sujeta. Se cancela el Spawn de respaldo para evitar colisiones en el aire.</color>");
            return;
        }

        Transform plataformaReal = BuscarPlataformaRealHijo();

        Collider colPlat = plataformaReal.GetComponent<Collider>();
        Vector3 centroPlatMundo = (colPlat != null) ? colPlat.bounds.center : plataformaReal.position;

        // Evitar duplicaciones
        bool yaHayPieza = false;
        Collider[] collidersCercanos = Physics.OverlapSphere(centroPlatMundo, 0.05f);

        foreach (Collider col in collidersCercanos)
        {
            string nombreCol = col.name.ToLower();

            if (col.transform == plataformaReal ||
                col.transform == plataformaPieza ||
                nombreCol.Contains("plataforma") ||
                nombreCol.Contains("puerta") ||
                nombreCol.Contains("door"))
            {
                continue;
            }

            if (nombreCol.Contains("pieza"))
            {
                yaHayPieza = true;
                break;
            }
        }

        if (!yaHayPieza)
        {
            // 1. Instanciamos la pieza de respaldo
            GameObject nuevaPieza = Instantiate(prefabBaseGris);
            nuevaPieza.name = "pieza_base_horno";

            nuevaPieza.transform.localScale = prefabBaseGris.transform.localScale;

            // 2. La emparentamos al hijo plataforma real de forma segura (Mantenemos TRUE para proteger la escala)
            nuevaPieza.transform.SetParent(plataformaReal, true);

            // 3. Leemos la última posición real (que ahora es la de la Imagen 2) y la aplicamos
            Vector3 posicionSincronizada = PlataformaHorno_proxy.PosicionCalibradaPieza;
            nuevaPieza.transform.localPosition = posicionSincronizada;
            nuevaPieza.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            // Inmovilizamos físicas de la pieza de respaldo
            Rigidbody rb = nuevaPieza.GetComponent<Rigidbody>();
            if (rb == null) rb = nuevaPieza.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            BoxCollider[] colliders = nuevaPieza.GetComponentsInChildren<BoxCollider>();
            foreach (BoxCollider col in colliders) if (col != null) col.isTrigger = false;

            Debug.Log($"<color=green><b>[HORNO SPAWN]:</b> Pieza de respaldo 'pieza_base_horno' instanciada en posición calibrada ({posicionSincronizada.x:F6}, {posicionSincronizada.y:F6}, {posicionSincronizada.z:F6}).</color>");
        }
    }

    private IEnumerator MoverObjeto(Transform objeto, Vector3 destino, float duracion)
    {
        Vector3 inicio = objeto.position;
        float tiempo = 0f;

        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            float t = tiempo / duracion;

            t = Mathf.SmoothStep(0f, 1f, t);

            objeto.position = Vector3.Lerp(inicio, destino, t);
            yield return null;
        }
        objeto.position = destino;
    }
}