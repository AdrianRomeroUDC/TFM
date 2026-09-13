using UnityEngine;
using System.Collections;

/// <summary>
/// Controla el gemelo digital del plato giratorio (turntable) de la estación MPO (Multi-Processing
/// Oven): la mesa que gira para colocar la pieza frente al brazo interno, frente la sierra de disco o
/// frente a la cinta de salida, además del mecanismo de expulsión (ejector/pusher) que empuja la pieza
/// ya cortada hacia la cinta transportadora del MPO. A diferencia de otros controladores de esta
/// estación, este script NO usa un evento normal de <see cref="MQTTClient"/>, sino que lee directamente
/// de la cola <see cref="MQTTClient.colaMensajes"/> (una <c>Queue&lt;MPOTurntablePayload&gt;</c>) para
/// procesar los mensajes del PLC real en el mismo orden estricto en que llegaron: como el turntable
/// puede recibir varias órdenes de giro seguidas muy rápido, perder o desordenar un mensaje aquí haría
/// que la mesa virtual acabase apuntando a un ángulo distinto al de la máquina física. También anima la
/// sierra girando sobre sí misma mientras el PLC ordena cortar.
/// </summary>
public class ControladorTurntableMPO_mqtt : MonoBehaviour
{
    [Header("Componentes")]
    public Transform pivotMesaGiratoria;
    public Transform ejector;
    public Transform sierraDisco;

    [Tooltip("Objeto de la cinta transportadora a la que se entregará la pieza (CintaTransportadora_EntradaSLD).")]
    public Transform cintaMPO;

    [Header("Calibración")]
    [ContextMenuItem("Capturar", "CapturarRef7")] public float TurntableBrazo;
    [ContextMenuItem("Capturar", "CapturarRef9")] public float TurntableCinta;
    [ContextMenuItem("Capturar", "CapturarRef10")] public float TurntableSierra;
    [ContextMenuItem("Capturar Eject Extendido", "CapturarEjectExtendido")] public Vector3 posEjectExtendido;
    [ContextMenuItem("Capturar Eject Retraido", "CapturarEjectRetraido")] public Vector3 posEjectRetraido;

    // Normaliza cualquier ángulo (incluso negativo o mayor de 360) al rango [0, 360), para poder
    // comparar y sumar ángulos del turntable sin que las vueltas completas den resultados raros.
    float Normalizar(float angulo) => (angulo % 360 + 360) % 360;
    // Botones de calibración del Inspector: guardan el ángulo actual de la mesa como referencia para
    // cada posición real del turntable (frente al brazo, frente a la cinta, frente a la sierra).
    void CapturarRef7() => TurntableBrazo = Normalizar(pivotMesaGiratoria.localEulerAngles.y);
    void CapturarRef9() => TurntableCinta = Normalizar(pivotMesaGiratoria.localEulerAngles.y);
    void CapturarRef10() => TurntableSierra = Normalizar(pivotMesaGiratoria.localEulerAngles.y);
    void CapturarEjectExtendido() => posEjectExtendido = ejector.localPosition;
    void CapturarEjectRetraido() => posEjectRetraido = ejector.localPosition;

    [Header("Configuración")]
    public float SpeedTurntable = 100f;
    public float VelocidadSierra = 512f;

    [Tooltip("Velocidad del pusher.")]
    public float velocidadEjector = 0.001f;

    private float targetAngleY; // Ángulo final (ya normalizado) al que debe llegar la mesa giratoria.
    private float anguloVirtualActual; // Ángulo "de trabajo" usado para el MoveTowards (puede superar los 360° mientras gira).
    private float targetVirtual; // Objetivo real del MoveTowards, teniendo en cuenta el sentido de giro (puede ser mayor que 360 o negativo).
    private int sentidoGiro = 0; // 1 = horario, -1 = antihorario, 0 = sin giro en curso.
    private bool estaOcupado = false; // true mientras la mesa está girando hacia su objetivo.
    private float sawDir = 0f; // Dirección/velocidad de giro de la sierra recibida del PLC (0 = parada).

    // --- AUTOMATIZACIÓN POR CICLO AUTO-MANTENIDO ---
    private bool _pusherMoviendoseHaciaAfuera = false; // true mientras el ejector avanza hacia la posición extendida.
    private bool piezaLiberadaEnEsteCiclo = false; // Evita soltar la pieza más de una vez en la misma carrera del ejector.

    // El eyector se considera activo si va hacia afuera o si aún no ha regresado a su base de reposo
    public bool EjectorEstaActivo
    {
        get
        {
            if (ejector == null) return false;
            return _pusherMoviendoseHaciaAfuera || (Vector3.Distance(ejector.localPosition, posEjectRetraido) > 0.00001f);
        }
    }

    void Start()
    {
        // Arrancamos con el ángulo virtual igual al ángulo real del modelo 3D en la escena, para que
        // el primer movimiento no dé un salto brusco desde 0.
        anguloVirtualActual = Normalizar(pivotMesaGiratoria.localEulerAngles.y);
        targetAngleY = anguloVirtualActual;
        piezaLiberadaEnEsteCiclo = false;
        _pusherMoviendoseHaciaAfuera = false;
    }

    void Update()
    {
        // 1. MQTT - PROCESAMIENTO DE COLA
        // Miramos (sin sacar todavía) el siguiente mensaje pendiente de la cola compartida del
        // MQTTClient. Si el mensaje solo trae una orden de actuador (sierra o eyector, sin ninguna
        // orden de giro "move2RefX"), lo procesamos aunque la mesa esté ocupada girando, para no
        // bloquear la sierra o el pusher mientras el turntable todavía está en movimiento.
        // Vaciamos todos los mensajes que se puedan procesar ya en este mismo frame (no nos
        // quedamos solo con el primero): así, si alguna vez el framerate cayera por debajo de la
        // cadencia real de publicación (200ms), la cola no se queda acumulando retraso indefinido
        // respecto al PLC real. En funcionamiento normal (framerate al día) esto no cambia nada,
        // porque la cola nunca llega a acumular más de un mensaje entre frames.
        if (MQTTClient.Instance != null)
        {
            lock (MQTTClient.Instance.colaMensajes)
            {
                while (MQTTClient.Instance.colaMensajes.Count > 0)
                {
                    var data = MQTTClient.Instance.colaMensajes.Peek();
                    bool esSoloActuador = (data.move2Ref7 == 0 && data.move2Ref8 == 0 && data.move2Ref9 == 0 && data.move2Ref10 == 0);

                    if (!estaOcupado || esSoloActuador)
                    {
                        ProcesarComando(MQTTClient.Instance.colaMensajes.Dequeue());
                    }
                    else
                    {
                        // Hay una orden de giro pendiente pero la mesa sigue ocupada con el giro
                        // anterior: la dejamos en la cola y esperamos al siguiente frame.
                        break;
                    }
                }
            }
        }

        // 2. Movimiento de Mesa Giratoria
        // Mientras la mesa está "ocupada" (girando), la acercamos poco a poco al ángulo objetivo;
        // al llegar (diferencia menor de 0.01°), fijamos el ángulo final exacto y marcamos el giro
        // como terminado.
        if (estaOcupado)
        {
            anguloVirtualActual = Mathf.MoveTowards(anguloVirtualActual, targetVirtual, SpeedTurntable * Time.deltaTime);
            pivotMesaGiratoria.localRotation = Quaternion.Euler(0, anguloVirtualActual, 0);

            if (Mathf.Abs(anguloVirtualActual - targetVirtual) < 0.01f)
            {
                float anguloFinal = Normalizar(targetAngleY);
                pivotMesaGiratoria.localRotation = Quaternion.Euler(0, anguloFinal, 0);
                anguloVirtualActual = anguloFinal;
                estaOcupado = false;
                sentidoGiro = 0;
            }
        }

        // 3. Control Cinemático Automático del Ejector (Pusher)
        // El pusher se mueve solo, en piloto automático, entre su posición retraída y extendida,
        // según la fase del ciclo en la que se encuentre.
        if (ejector)
        {
            // Determinamos el objetivo dependiendo de la fase del ciclo en la que nos encontremos
            Vector3 targetPosEjector = _pusherMoviendoseHaciaAfuera ? posEjectExtendido : posEjectRetraido;

            ejector.localPosition = Vector3.MoveTowards(ejector.localPosition, targetPosEjector, velocidadEjector * Time.deltaTime);

            // Fase de extensión y entrega
            // En cuanto el pusher llega al final de su carrera hacia afuera, entregamos la pieza a la
            // cinta y arrancamos automáticamente el camino de vuelta hacia la posición de reposo.
            if (_pusherMoviendoseHaciaAfuera && !piezaLiberadaEnEsteCiclo)
            {
                float distanciaAlObjetivo = Vector3.Distance(ejector.localPosition, posEjectExtendido);

                if (distanciaAlObjetivo < 0.00005f) // Margen de llegada seguro
                {
                    Debug.Log("<color=green><b>[Pusher]:</b> Límite de carrera alcanzado. Entregando pieza a la cinta...</color>");
                    LiberarPiezaEnCinta();
                    piezaLiberadaEnEsteCiclo = true;

                    // Conmutación automática: Iniciamos el retorno inmediato a casa
                    _pusherMoviendoseHaciaAfuera = false;
                }
            }
        }

        // 4. SIERRA
        // Si el PLC ha ordenado cortar (sawDir distinto de 0), giramos el disco de la sierra sobre
        // su propio eje a la velocidad configurada, en el sentido indicado por sawDir.
        if (sierraDisco != null && sawDir != 0f)
        {
            sierraDisco.Rotate(Vector3.up, sawDir * VelocidadSierra * Time.deltaTime, Space.Self);
        }
    }

    /// <summary>
    /// Traduce un mensaje del PLC real del turntable en acciones sobre el gemelo digital: dispara el
    /// ciclo del eyector si llega un pulso de expulsión, actualiza la velocidad/sentido de la sierra,
    /// y si el mensaje trae una orden de girar hacia una de las posiciones de referencia (brazo, cinta
    /// o sierra), calcula el nuevo ángulo objetivo y el camino más adecuado (sentido horario o
    /// antihorario) para llegar hasta él.
    /// </summary>
    /// <param name="data">Mensaje MQTT con el estado real del turntable: giro, sierra y eyector.</param>
    void ProcesarComando(MPOTurntablePayload data)
    {
        // El pulso de activación inicia el ciclo blindado
        // Solo arrancamos una nueva carrera del pusher si está realmente en reposo, para no
        // reiniciar el movimiento a medias si llegan varios pulsos "eject" seguidos muy rápido.
        if (data.eject == 1)
        {
            // Solo disparamos si el pusher está en reposo para evitar re-disparos buclados
            if (!_pusherMoviendoseHaciaAfuera && Vector3.Distance(ejector.localPosition, posEjectRetraido) < 0.0001f)
            {
                _pusherMoviendoseHaciaAfuera = true;
                piezaLiberadaEnEsteCiclo = false;
                Debug.Log("<color=lime><b>[Pusher]:</b> Pulso EJECT detectado (30ms). Ejecutando carrera completa auto-mantenida.</color>");
            }
        }
        // NOTA DE INGENIERÍA: Ignoramos 'data.eject == 0' para evitar que los flancos de bajada
        // rápidos destruyan el avance mecánico del gemelo digital.

        sawDir = (float)data.saw;

        // Comprobamos si este mensaje trae alguna orden de giro hacia una posición de referencia.
        bool tieneOrdenDeReferencia = (data.move2Ref7 == 1 || data.move2Ref8 == 1 || data.move2Ref9 == 1 || data.move2Ref10 == 1);

        if (tieneOrdenDeReferencia)
        {
            // Elegimos el ángulo de destino según qué referencia ha pedido el PLC: Ref7 = brazo,
            // Ref9 = cinta, Ref8/Ref10 = sierra.
            float nuevoAngulo = -1;
            if (data.move2Ref7 == 1) nuevoAngulo = TurntableBrazo;
            else if (data.move2Ref9 == 1) nuevoAngulo = TurntableCinta;
            else if (data.move2Ref10 == 1 || data.move2Ref8 == 1) nuevoAngulo = TurntableSierra;

            if (nuevoAngulo != -1)
            {
                targetAngleY = Normalizar(nuevoAngulo);
                anguloVirtualActual = Normalizar(pivotMesaGiratoria.localEulerAngles.y);

                // Si el PLC especifica un sentido de giro explícito lo usamos; si no, deducimos el
                // sentido según qué referencia se pidió (el brazo siempre gira antihorario, la cinta
                // y la sierra siempre horario), o en su defecto por el camino más corto.
                if (data.rotation != 0)
                {
                    sentidoGiro = data.rotation;
                }
                else
                {
                    if (data.move2Ref7 == 1 || data.move2Ref8 == 1) sentidoGiro = -1; // Antihorario
                    else if (data.move2Ref9 == 1 || data.move2Ref10 == 1) sentidoGiro = 1;  // Horario
                    else sentidoGiro = (Mathf.DeltaAngle(anguloVirtualActual, targetAngleY) > 0) ? 1 : -1;
                }

                float diff = Mathf.DeltaAngle(anguloVirtualActual, targetAngleY);

                // Ajustamos el ángulo objetivo "virtual" (que puede superar los 360°) para que el
                // MoveTowards del paso 2 de Update() siempre gire en el sentido correcto, en vez de
                // tomar automáticamente el camino más corto.
                if (sentidoGiro == 1 && diff < 0) targetVirtual = anguloVirtualActual + (diff + 360);
                else if (sentidoGiro == -1 && diff > 0) targetVirtual = anguloVirtualActual + (diff - 360);
                else targetVirtual = anguloVirtualActual + diff;

                estaOcupado = true;
            }
        }
    }

    // Busca la pieza que lleva sujeta el eyector (o, si no la encuentra ahí, en cualquier sitio bajo
    // este objeto) y la transfiere físicamente al eslabón más cercano de la cinta de salida del MPO,
    // dejándola fija (cinemática, sin gravedad) sobre la cinta para que continúe su viaje hacia la SLD.
    private void LiberarPiezaEnCinta()
    {
        Transform piezaSujeta = null;

        foreach (Transform hijo in ejector.GetComponentsInChildren<Transform>())
        {
            if (hijo != ejector && hijo.name.ToLower().Contains("pieza"))
            {
                piezaSujeta = hijo;
                break;
            }
        }

        if (piezaSujeta == null)
        {
            foreach (Transform hijo in transform.GetComponentsInChildren<Transform>())
            {
                if (hijo.name.ToLower().Contains("pieza"))
                {
                    piezaSujeta = hijo;
                    break;
                }
            }
        }

        if (piezaSujeta != null)
        {
            Transform realCinta = cintaMPO;

            if (realCinta != null)
            {
                // Si la referencia "cintaMPO" apunta al objeto del script en vez de al objeto padre
                // que contiene los eslabones, corregimos y usamos el padre real de la cinta.
                ControladorCintaMPO_mqtt scriptCinta = realCinta.GetComponent<ControladorCintaMPO_mqtt>();
                if (scriptCinta != null && scriptCinta.objetoCintaPadre != null)
                {
                    realCinta = scriptCinta.objetoCintaPadre;
                }

                // Buscamos, de entre todos los eslabones de la cinta, el que está físicamente más
                // cerca de la pieza, para "engancharla" justo ahí y que el salto visual sea mínimo.
                Transform eslabonMasCercano = null;
                float distanciaMinima = float.MaxValue;

                foreach (Transform eslabon in realCinta.GetComponentsInChildren<Transform>())
                {
                    if (eslabon == realCinta) continue;

                    float distancia = Vector3.Distance(piezaSujeta.position, eslabon.position);
                    if (distancia < distanciaMinima)
                    {
                        distanciaMinima = distancia;
                        eslabonMasCercano = eslabon;
                    }
                }

                if (eslabonMasCercano != null)
                {
                    Debug.Log($"<color=green>[CINTA]: Transferida con éxito la pieza '{piezaSujeta.name}' al eslabón '{eslabonMasCercano.name}' (Distancia: {distanciaMinima:F5}).</color>");

                    // Hacemos la pieza hija del eslabón más cercano, la fijamos en su posición local
                    // calibrada sobre la cinta, y la volvemos cinemática (sin gravedad) para que
                    // viaje pegada a la cinta en vez de caerse.
                    piezaSujeta.SetParent(eslabonMasCercano, true);

                    Vector3 posDeseada = new Vector3(0f, 0.000154f, 0.000178f);
                    piezaSujeta.localPosition = posDeseada;

                    Rigidbody rb = piezaSujeta.GetComponent<Rigidbody>();
                    if (rb == null) rb = piezaSujeta.gameObject.AddComponent<Rigidbody>();

                    rb.isKinematic = true;
                    rb.useGravity = false;

                    Physics.SyncTransforms();
                }
                else
                {
                    Debug.LogWarning("[CINTA - ERROR]: No se encontraron sub-objetos (eslabones) válidos dentro de la cinta.");
                }
            }
            else
            {
                Debug.LogError("[CINTA - ERROR]: No se ha asignado la cinta en la variable 'cintaMPO' del Inspector.");
            }
        }
        else
        {
            Debug.LogWarning("[Pusher]: Se intentó liberar la pieza pero no se detectó ninguna pieza sujeta.");
        }
    }
}
