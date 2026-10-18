using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// El indicador flotante "Presiona E para ..." (estilo cuadro Undertale: borde blanco,
/// fondo negro). Se crea solo por codigo, una unica vez, y flota sobre lo que le pidas:
///   - ShowAtWorld(...): encima de un punto del mundo (una casilla del tablero).
///   - ShowAtRect(...):  encima de un objeto del Canvas (por ejemplo el guia).
/// Lo usan LevelExtras (terminales) y DialogueController (hablar con el guia).
/// No hay que poner este script en ningun objeto.
/// </summary>
public class PromptBubble : MonoBehaviour
{
    private static PromptBubble instance;

    private RectTransform rt;
    private RectTransform canvasRt;
    private Canvas rootCanvas;
    private CanvasGroup group;
    private TextMeshProUGUI label;

    private bool visible;
    private Camera worldCam;
    private Vector3 worldPoint;
    private RectTransform rectTarget;
    private float yOffset;

    /// <summary>Devuelve la burbuja (la crea la primera vez). Puede devolver null si no hay Canvas.</summary>
    public static PromptBubble Get(Canvas canvas)
    {
        if (instance != null) return instance;
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return null;

        Canvas root = canvas.rootCanvas;
        GameObject go = new GameObject("PromptBubble", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(root.transform, false);
        instance = go.AddComponent<PromptBubble>();
        instance.Build(root);
        return instance;
    }

    /// <summary>Esconde la burbuja si existe (no la crea).</summary>
    public static void HideIfExists()
    {
        if (instance != null) instance.Hide();
    }

    private void Build(Canvas root)
    {
        rootCanvas = root;
        canvasRt = (RectTransform)root.transform;
        rt = (RectTransform)transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(260f, 44f);

        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        CreateImage("Border", Color.white, 0f);
        CreateImage("Fill", Color.black, 3f);

        GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(transform, false);
        label = textGo.GetComponent<TextMeshProUGUI>();
        label.fontSize = 20f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.92f, 0.2f); // amarillo, como la opcion seleccionada
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        gameObject.SetActive(false);
    }

    private void CreateImage(string objName, Color color, float inset)
    {
        GameObject go = new GameObject(objName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset);
        r.offsetMax = new Vector2(-inset, -inset);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    /// <summary>Flota encima de un punto del mundo (por ejemplo una casilla del tablero).</summary>
    public void ShowAtWorld(Vector3 position, string text, float offsetY = 34f)
    {
        worldCam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        worldPoint = position;
        rectTarget = null;
        Open(text, offsetY);
    }

    /// <summary>Flota encima de un objeto del Canvas (por ejemplo el guia).</summary>
    public void ShowAtRect(RectTransform target, string text, float offsetY = 14f)
    {
        rectTarget = target;
        worldCam = null;
        Open(text, offsetY);
    }

    private void Open(string text, float offsetY)
    {
        yOffset = offsetY;
        label.text = text;
        visible = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Reposition();
    }

    public void Hide()
    {
        visible = false;
        rectTarget = null;
        if (gameObject != null) gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (!visible) return;
        group.alpha = Time.timeScale > 0f ? 1f : 0f; // si el juego esta en pausa, no se ve encima del menu
        Reposition();
    }

    private void Reposition()
    {
        // Tamaño segun el texto
        rt.localScale = Vector3.one * Mathf.Clamp(canvasRt.rect.width / 1280f, 0.6f, 1.5f); // se ajusta al tamaño del Canvas
        Vector2 pref = label.GetPreferredValues(label.text);
        rt.sizeDelta = new Vector2(Mathf.Max(120f, pref.x + 36f), Mathf.Max(34f, pref.y + 16f));

        Camera uiCam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Vector2 screen;

        if (rectTarget != null)
        {
            Vector3 top = rectTarget.TransformPoint(new Vector3(rectTarget.rect.center.x, rectTarget.rect.yMax, 0f));
            screen = RectTransformUtility.WorldToScreenPoint(uiCam, top);
        }
        else
        {
            if (worldCam == null) return;
            screen = RectTransformUtility.WorldToScreenPoint(worldCam, worldPoint);
        }

        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, uiCam, out local))
        {
            float bob = Mathf.Sin(Time.unscaledTime * 5f) * 4f;
            rt.localPosition = new Vector3(local.x, local.y + yOffset + bob, 0f);
        }
    }
}
