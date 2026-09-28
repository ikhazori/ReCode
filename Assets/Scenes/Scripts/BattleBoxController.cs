using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Controla la animacion de "cerrar y volver a abrir" del cuadro de dialogo,
/// alternando entre la vista de opciones y un mensaje de confirmacion,
/// al estilo Undertale (el cuadro se aplasta en X -bordes hacia el centro-,
/// cambia el contenido, y se vuelve a abrir).
/// </summary>
public class BattleBoxController : MonoBehaviour
{
    [Header("Referencias del cuadro")]
    [Tooltip("El objeto 'DialogueBox' (el fondo negro).")]
    public RectTransform boxRect;
    [Tooltip("El objeto 'DialogueBoxBorder' (el borde blanco).")]
    public RectTransform borderRect;

    [Header("Contenido")]
    [Tooltip("Objeto que agrupa las 4 opciones + el corazon (se oculta/muestra entero).")]
    public GameObject optionsGroup;
    [Tooltip("Texto que muestra el mensaje de confirmacion.")]
    public TMP_Text messageText;

    [Header("Tiempos")]
    [Tooltip("Duracion de la animacion de cerrar/abrir (segundos). No tan lenta.")]
    public float closeOpenDuration = 0.12f;
    [Tooltip("Cuanto tiempo se queda mostrando el mensaje antes de volver a las opciones.")]
    public float messageDisplayTime = 1.5f;

    /// <summary>True mientras esta animando o mostrando el mensaje (para bloquear el input del menu).</summary>
    public bool IsBusy { get; private set; }

    void Start()
    {
        // Estado inicial: opciones visibles, mensaje oculto.
        if (messageText != null) messageText.gameObject.SetActive(false);
        if (optionsGroup != null) optionsGroup.SetActive(true);
    }

    public void ShowMessage(string message)
    {
        if (IsBusy) return;
        StartCoroutine(SwapToMessageRoutine(message));
    }

    private IEnumerator SwapToMessageRoutine(string message)
    {
        IsBusy = true;

        yield return ScaleX(0f, closeOpenDuration);

        optionsGroup.SetActive(false);
        messageText.text = message;
        messageText.gameObject.SetActive(true);

        yield return ScaleX(1f, closeOpenDuration);

        yield return new WaitForSeconds(messageDisplayTime);

        yield return ScaleX(0f, closeOpenDuration);

        messageText.gameObject.SetActive(false);
        optionsGroup.SetActive(true);

        yield return ScaleX(1f, closeOpenDuration);

        IsBusy = false;
    }

    // ---------- Piezas sueltas (las usa el DialogueController) ----------

    /// <summary>Cierra el cuadro (bordes hacia el centro).</summary>
    public IEnumerator CloseBox()
    {
        return ScaleX(0f, closeOpenDuration);
    }

    /// <summary>Abre el cuadro (bordes hacia afuera).</summary>
    public IEnumerator OpenBox()
    {
        return ScaleX(1f, closeOpenDuration);
    }

    /// <summary>Cambia el contenido a modo mensaje (oculta las opciones y muestra el texto).</summary>
    public void ShowMessageContent(string text)
    {
        optionsGroup.SetActive(false);
        messageText.text = text;
        messageText.gameObject.SetActive(true);
    }

    /// <summary>Vuelve al modo opciones (oculta el texto del mensaje).</summary>
    public void ShowOptionsContent()
    {
        messageText.maxVisibleCharacters = 99999; // por si el dialogo lo dejo a medias
        messageText.gameObject.SetActive(false);
        optionsGroup.SetActive(true);
    }

    private IEnumerator ScaleX(float target, float duration)
    {
        float startBox = boxRect.localScale.x;
        float startBorder = borderRect.localScale.x;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float lerpFactor = Mathf.Clamp01(t / duration);
            SetScaleX(boxRect, Mathf.Lerp(startBox, target, lerpFactor));
            SetScaleX(borderRect, Mathf.Lerp(startBorder, target, lerpFactor));
            yield return null;
        }

        SetScaleX(boxRect, target);
        SetScaleX(borderRect, target);
    }

    private void SetScaleX(RectTransform rect, float x)
    {
        Vector3 scale = rect.localScale;
        scale.x = x;
        rect.localScale = scale;
    }
}