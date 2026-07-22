using UnityEngine;
using UnityEngine.UI;
using System.Collections;

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

    void Awake()
    {
        toggleComponent = GetComponent<Toggle>();

        // Escuchamos el cambio de estado nativo del Toggle
        toggleComponent.onValueChanged.AddListener(OnToggleChanged);

        // Inicializamos la posición sin animación al arrancar
        ActualizarEstadoInstantaneo(toggleComponent.isOn);
    }

    void OnDestroy()
    {
        if (toggleComponent != null)
        {
            toggleComponent.onValueChanged.RemoveListener(OnToggleChanged);
        }
    }

    private void OnToggleChanged(bool estaActivado)
    {
        if (corrutinaAnimacion != null) StopCoroutine(corrutinaAnimacion);
        corrutinaAnimacion = StartCoroutine(AnimarSwitch(estaActivado));
    }

    private IEnumerator AnimarSwitch(bool activado)
    {
        float posXDestino = activado ? posicionOnX : posicionOffX;
        Color colorDestino = activado ? colorOn : colorOff;

        Vector2 posActual = handleTransform != null ? handleTransform.anchoredPosition : Vector2.zero;
        Color colorActual = backgroundImage != null ? backgroundImage.color : Color.white;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * velocidadTransicion;

            // CORREGIDO: Usamos posActual.y
            if (handleTransform != null)
            {
                float nuevaX = Mathf.Lerp(posActual.x, posXDestino, t);
                handleTransform.anchoredPosition = new Vector2(nuevaX, posActual.y);
            }

            // Animamos el color del fondo
            if (backgroundImage != null)
            {
                backgroundImage.color = Color.Lerp(colorActual, colorDestino, t);
            }

            yield return null;
        }
    }

    private void ActualizarEstadoInstantaneo(bool activado)
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