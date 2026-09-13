using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Convierte un componente estándar de Unity <see cref="Toggle"/> (una simple casilla de
/// marcar) en un interruptor visual animado tipo "switch" de móvil: un círculo (el
/// "handle") que se desliza de izquierda a derecha y un fondo que cambia de color, para
/// indicar de forma más vistosa si algo está encendido (ON) o apagado (OFF).
///
/// Este script es puramente decorativo/reutilizable: no sabe nada de MQTT ni de la
/// fábrica, simplemente escucha los cambios del <see cref="Toggle"/> al que está
/// enganchado y anima el círculo y el color de fondo en consecuencia. Otros paneles,
/// como <c>UI_SensorsMonitor</c>, usan este interruptor para su botón de encendido/apagado.
/// </summary>
[RequireComponent(typeof(Toggle))]
public class UI_ToggleSwitch : MonoBehaviour
{
    [Header("Referencias UI")]
    public RectTransform handleTransform; // El círculo que se mueve
    public Image backgroundImage;        // El fondo del switch

    [Header("Configuración de Movimiento")]
    public float posicionOffX = -20f;     // Posición X del círculo cuando es OFF
    public float posicionOnX = 20f;       // Posición X del círculo cuando es ON
    public float velocidadTransicion = 8f;

    [Header("Colores de Fondo")]
    public Color colorOn = new Color(0.2f, 0.6f, 1f);   // Azul encendido
    public Color colorOff = new Color(0.4f, 0.4f, 0.4f); // Gris apagado

    private Toggle toggleComponent;
    private Coroutine corrutinaAnimacion;

    /// <summary>
    /// Al despertar el objeto, obtiene el componente <see cref="Toggle"/> obligatorio,
    /// se suscribe a sus cambios de valor y coloca el interruptor en su posición inicial
    /// sin animación (para que no "salte" visualmente al arrancar la escena).
    /// </summary>
    void Awake()
    {
        toggleComponent = GetComponent<Toggle>();

        // Escuchamos el cambio de estado nativo del Toggle
        toggleComponent.onValueChanged.AddListener(OnToggleChanged);

        // Inicializamos la posición sin animación al arrancar
        ActualizarEstadoInstantaneo(toggleComponent.isOn);
    }

    /// <summary>
    /// Al destruirse este objeto, nos desuscribimos del evento del Toggle para no dejar
    /// una suscripción "colgada" apuntando a un objeto que ya no existe.
    /// </summary>
    void OnDestroy()
    {
        if (toggleComponent != null)
        {
            toggleComponent.onValueChanged.RemoveListener(OnToggleChanged);
        }
    }

    /// <summary>
    /// Se llama automáticamente cada vez que el usuario (o el código) cambia el valor del
    /// Toggle. Lanza la animación del interruptor, salvo que el objeto esté inactivo en la
    /// jerarquía, en cuyo caso Unity no permite iniciar corrutinas y se aplica el cambio
    /// de forma instantánea.
    /// </summary>
    /// <param name="estaActivado">Nuevo valor del Toggle (true = encendido).</param>
    private void OnToggleChanged(bool estaActivado)
    {
        // 🟢 CLAVE: Si el objeto está inactivo en la jerarquía (menú cerrado),
        // no podemos iniciar Corrutinas en Unity. Aplicamos el cambio de forma instantánea.
        if (!gameObject.activeInHierarchy)
        {
            ActualizarEstadoInstantaneo(estaActivado);
            return;
        }

        // Si ya había una animación en marcha, la detenemos antes de lanzar la nueva,
        // para que no se solapen dos animaciones a la vez
        if (corrutinaAnimacion != null) StopCoroutine(corrutinaAnimacion);
        corrutinaAnimacion = StartCoroutine(AnimarSwitch(estaActivado));
    }

    /// <summary>
    /// Corrutina que anima, frame a frame, el desplazamiento del círculo (handle) y el
    /// cambio de color del fondo, interpolando suavemente entre la posición/color actuales
    /// y los de destino (ON u OFF).
    /// </summary>
    /// <param name="activado">true si el destino de la animación es el estado ON.</param>
    private IEnumerator AnimarSwitch(bool activado)
    {
        float posXDestino = activado ? posicionOnX : posicionOffX;
        Color colorDestino = activado ? colorOn : colorOff;

        Vector2 posActual = handleTransform != null ? handleTransform.anchoredPosition : Vector2.zero;
        Color colorActual = backgroundImage != null ? backgroundImage.color : Color.white;

        float t = 0f;
        while (t < 1f)
        {
            // t avanza según el tiempo transcurrido y la velocidad configurada, hasta llegar a 1
            t += Time.deltaTime * velocidadTransicion;

            if (handleTransform != null)
            {
                // Desplazamos el círculo horizontalmente entre su posición actual y la de destino
                float nuevaX = Mathf.Lerp(posActual.x, posXDestino, t);
                handleTransform.anchoredPosition = new Vector2(nuevaX, posActual.y);
            }

            if (backgroundImage != null)
            {
                // Vamos mezclando el color de fondo actual con el color de destino
                backgroundImage.color = Color.Lerp(colorActual, colorDestino, t);
            }

            yield return null;
        }
    }

    /// <summary>
    /// Coloca el interruptor directamente en su posición y color final, sin animación.
    /// Se usa al arrancar la escena o cuando el objeto está inactivo y no se puede animar.
    /// </summary>
    /// <param name="activado">true para colocar el interruptor en estado ON, false para OFF.</param>
    public void ActualizarEstadoInstantaneo(bool activado)
    {
        if (handleTransform != null)
        {
            float posX = activado ? posicionOnX : posicionOffX;
            handleTransform.anchoredPosition = new Vector2(posX, handleTransform.anchoredPosition.y);
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = activado ? colorOn : colorOff;
        }
    }
}
