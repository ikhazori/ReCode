using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Convierte tu cola de flechas en PSEUDOCODIGO, en vivo, en un panel del lado derecho:
///
///     INICIO
///       mover(derecha)
///       REPETIR 3 VECES
///         mover(abajo)
///       mover(izquierda)
///     FIN
///
/// - Mientras armas la secuencia, el panel se escribe solo (las repeticiones seguidas se
///   resumen como REPETIR n VECES, que es la idea de bucle).
/// - Al ejecutar (R), una flecha ">" marca la linea que esta corriendo.
/// - Si chocas, esa linea se pone roja y aparece el error: "// ERROR en la instruccion 4".
/// - Si terminas sin llegar a la meta, el error es "// ERROR: no llegaste a la meta".
///
/// Asi el jugador ve que lo que arma ES un programa, y depurar es leer ese error.
/// Todo se construye por codigo. Uso: ponlo en el mismo objeto que LevelManager ("Level").
/// </summary>
public class PseudocodePanel : MonoBehaviour
{
    public enum Corner { TopLeft, TopRight }

    [Header("Referencias (se buscan solas)")]
    public Canvas canvas;
    public CommandQueueUI queue;
    public LevelRunner runner;
    public StartMenuUI startMenu;

    [Header("Posicion y tamaño")]
    public Corner corner = Corner.TopRight;
    public Vector2 margin = new Vector2(14f, 14f);
    [Tooltip("Multiplica el tamaño del panel. Ya se ajusta solo al tamaño de tu Canvas; usa esto solo para afinar (1 = normal).")]
    [Range(0.3f, 2f)] public float uiScale = 1f;
    [Tooltip("Maximo de lineas visibles; si la secuencia es mas larga, el panel sigue a la linea activa.")]
    public int maxVisibleLines = 11;
    public float fontSize = 11f;

    [Header("Movimiento (flotar suave)")]
    [Tooltip("El panel se mueve despacito, como flotando. Desmarca para dejarlo fijo.")]
    public bool floatEffect = true;
    [Tooltip("Cuanto se mueve (unidades del Canvas): X = de lado, Y = arriba/abajo.")]
    public Vector2 floatAmplitude = new Vector2(3f, 5f);
    [Tooltip("Velocidad del vaiven (1 = normal, 0.5 = lento).")]
    public float floatSpeed = 0.8f;
    [Tooltip("Inclinacion maxima en grados (0 = sin inclinar).")]
    public float floatTilt = 0.5f;

    [Header("Textos")]
    public string title = "PSEUDOCÓDIGO";
    public string emptyHint = "// arma la secuencia";
    public string crashError = "// ERROR en la instrucción {0}: choque con un muro";
    public string shortError = "// ERROR: no llegaste a la meta";

    [Header("Colores")]
    public Color borderColor = Color.white;
    public Color fillColor = Color.black;
    public Color keywordColor = new Color(0.4f, 1f, 0.9f);    // INICIO, FIN, REPETIR
    public Color normalColor = Color.white;
    public Color doneColor = new Color(0.5f, 0.5f, 0.5f);
    public Color activeColor = new Color(1f, 0.92f, 0.2f);
    public Color errorColor = new Color(1f, 0.35f, 0.4f);
    public Color hintColor = new Color(0.55f, 0.55f, 0.55f);
    [Tooltip("Color de la barra de titulo (como la ventana de un editor de codigo).")]
    public Color headerColor = new Color(0.08f, 0.32f, 0.36f);

    // Medidas pensadas para un Canvas de 1280 de ancho; se escalan solas segun el Canvas real.
    private const float ReferenceWidth = 1280f;
    private const float PanelW = 290f;
    private const float LineH = 19f;
    private const float HeaderH = 34f, StripH = 26f;
    private const float Pad = 10f;

    private RectTransform root;
    private RectTransform canvasRt;
    private CanvasGroup group;
    private TextMeshProUGUI titleTmp, bodyTmp;

    // Una linea de pseudocodigo = una o varias instrucciones consecutivas iguales
    private class Group { public Direction dir; public int count; public int first; }

    private readonly List<Group> groups = new List<Group>();
    private string lastSignature = "";

    void Awake()
    {
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (queue == null) queue = FindFirstObjectByType<CommandQueueUI>();
        if (runner == null) runner = FindFirstObjectByType<LevelRunner>();
        if (startMenu == null) startMenu = FindFirstObjectByType<StartMenuUI>();

        if (canvas == null)
        {
            Debug.LogWarning("[PseudocodePanel] No encontre ningun Canvas. El panel no se puede crear.");
            enabled = false;
            return;
        }
        canvasRt = (RectTransform)canvas.rootCanvas.transform;
        Build();
    }

    void Update()
    {
        if (root == null || queue == null) return;
        ApplyScale();

        bool hidden = (startMenu != null && startMenu.IsMenuActive) || Time.timeScale <= 0f || OptionsMenuUI.IsOpen;
        group.alpha = Mathf.MoveTowards(group.alpha, hidden ? 0f : 1f, Time.unscaledDeltaTime * 4f);

        int current = runner != null ? runner.CurrentInstruction : -1;
        int crashed = runner != null ? runner.CrashedInstruction : -1;
        bool endedShort = runner != null && runner.EndedShort;

        // Solo se reconstruye el texto si algo cambio
        string sig = Signature(current, crashed, endedShort);
        if (sig == lastSignature) return;
        lastSignature = sig;
        Rebuild(current, crashed, endedShort);
    }

    private void ApplyScale()
    {
        float w = canvasRt.rect.width;
        if (w <= 1f) return;
        float k = Mathf.Clamp(w / ReferenceWidth, 0.3f, 2f) * uiScale;
        Vector3 s = new Vector3(k, k, 1f);
        if (root.localScale != s) root.localScale = s;
    }

    private string Signature(int current, int crashed, bool endedShort)
    {
        StringBuilder sb = new StringBuilder();
        foreach (Direction d in queue.Instructions) sb.Append((int)d);
        sb.Append('|').Append(current).Append('|').Append(crashed).Append('|').Append(endedShort ? 1 : 0);
        return sb.ToString();
    }

    // ---------------------------------------------------------------- texto

    private static string Word(Direction d)
    {
        switch (d)
        {
            case Direction.Up: return "arriba";
            case Direction.Down: return "abajo";
            case Direction.Left: return "izquierda";
            default: return "derecha";
        }
    }

    private void BuildGroups()
    {
        groups.Clear();
        IList<Direction> list = queue.Instructions;
        for (int i = 0; i < list.Count; i++)
        {
            if (groups.Count > 0 && groups[groups.Count - 1].dir == list[i])
                groups[groups.Count - 1].count++;
            else
                groups.Add(new Group { dir = list[i], count = 1, first = i });
        }
    }

    private static string Hex(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }

    private void Rebuild(int current, int crashed, bool endedShort)
    {
        BuildGroups();

        // Cada elemento es una linea ya con su color. 'focus' = la linea que el panel debe mantener visible.
        List<string> lines = new List<string>();
        int focus = 0;

        lines.Add(Colored(keywordColor, "INICIO"));

        if (groups.Count == 0)
            AddWrapped(lines, hintColor, emptyHint, "  ");

        for (int g = 0; g < groups.Count; g++)
        {
            Group grp = groups[g];
            int last = grp.first + grp.count - 1;

            bool isCrash = crashed >= grp.first && crashed <= last;
            bool isActive = current >= grp.first && current <= last;
            bool isDone = false;
            if (current >= 0) isDone = last < current;                                   // ya se ejecuto
            else if (crashed >= 0) isDone = last < crashed;                              // choco mas adelante
            else if (endedShort) isDone = true;                                          // termino toda la secuencia

            Color state = normalColor;
            string marker = "  ";
            if (isCrash) { state = errorColor; marker = "> "; }
            else if (isActive) { state = activeColor; marker = "> "; }
            else if (isDone) { state = doneColor; }
            bool plain = !isCrash && !isActive && !isDone;

            string code = "mover(" + Word(grp.dir) + ")";
            if (grp.count > 1)
            {
                // Varias iguales seguidas = un bucle
                lines.Add(Colored(plain ? keywordColor : state, marker + "REPETIR " + grp.count + " VECES"));
                lines.Add(Colored(state, "    " + code));
            }
            else
            {
                lines.Add(Colored(state, marker + code));
            }

            if (isCrash || isActive) focus = lines.Count - 1;
            else if (current < 0 && crashed < 0 && g == groups.Count - 1) focus = lines.Count - 1;
        }

        lines.Add(Colored(keywordColor, "FIN"));

        if (crashed >= 0)
        {
            AddWrapped(lines, errorColor, crashError.Replace("{0}", (crashed + 1).ToString()), "");
            focus = lines.Count - 1;
        }
        else if (endedShort)
        {
            AddWrapped(lines, errorColor, shortError, "");
            focus = lines.Count - 1;
        }

        // Ventana de lineas visibles (sigue a la linea importante)
        int max = Mathf.Max(5, maxVisibleLines);
        int startLine = 0;
        if (lines.Count > max) startLine = Mathf.Clamp(focus - max / 2, 0, lines.Count - max);
        int endLine = Mathf.Min(lines.Count, startLine + max);

        StringBuilder sb = new StringBuilder();
        if (startLine > 0) sb.Append(Colored(hintColor, "  ...")).Append('\n');
        for (int i = startLine; i < endLine; i++)
        {
            sb.Append(lines[i]);
            if (i < endLine - 1) sb.Append('\n');
        }
        if (endLine < lines.Count) sb.Append('\n').Append(Colored(hintColor, "  ..."));

        bodyTmp.text = sb.ToString();
    }

    // Parte un texto largo en varias lineas (por palabras) para que no se salga del panel.
    private const int WrapChars = 22;

    private static void AddWrapped(List<string> lines, Color c, string text, string indent)
    {
        string[] words = text.Split(' ');
        StringBuilder cur = new StringBuilder();
        foreach (string w in words)
        {
            if (w.Length == 0) continue;
            if (cur.Length > 0 && indent.Length + cur.Length + 1 + w.Length > WrapChars)
            {
                lines.Add(Colored(c, indent + cur.ToString()));
                cur.Length = 0;
            }
            if (cur.Length > 0) cur.Append(' ');
            cur.Append(w);
        }
        if (cur.Length > 0) lines.Add(Colored(c, indent + cur.ToString()));
    }

    private static string Colored(Color c, string text)
    {
        return "<color=" + Hex(c) + ">" + text + "</color>";
    }

    // ---------------------------------------------------------------- interfaz

    private void Build()
    {
        Canvas rootCanvas = canvas.rootCanvas;

        GameObject go = new GameObject("PseudocodePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        go.transform.SetParent(rootCanvas.transform, false);
        root = (RectTransform)go.transform;
        group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        bool left = corner == Corner.TopLeft;
        Vector2 anchor = new Vector2(left ? 0f : 1f, 1f);
        root.anchorMin = anchor;
        root.anchorMax = anchor;
        root.pivot = anchor;
        float h = HeaderH + Mathf.Max(5, maxVisibleLines + 2) * LineH + Pad;
        root.sizeDelta = new Vector2(PanelW, h);
        root.anchoredPosition = new Vector2(left ? margin.x : -margin.x, -margin.y);

        Image border = go.GetComponent<Image>();
        border.color = borderColor;
        border.raycastTarget = false;

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(root, false);
        RectTransform fill = (RectTransform)fillGo.transform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(3f, 3f);
        fill.offsetMax = new Vector2(-3f, -3f);
        Image fillImg = fillGo.GetComponent<Image>();
        fillImg.color = fillColor;
        fillImg.raycastTarget = false;

        // Barra de titulo (como la ventana de un editor de codigo) con sus 3 puntitos
        NewStrip("Header", fill, headerColor, StripH);

        titleTmp = NewText("Title", fill, fontSize + 2f, TextAlignmentOptions.MidlineLeft, keywordColor);
        titleTmp.text = title;
        SetRect(titleTmp.rectTransform, Pad, 0f, PanelW - 2f * Pad - 50f, StripH);

        float dotX = PanelW - 6f - 50f;
        NewDot(fill, dotX, 9f, 8f, errorColor);
        NewDot(fill, dotX + 14f, 9f, 8f, activeColor);
        NewDot(fill, dotX + 28f, 9f, 8f, new Color(0.4f, 1f, 0.5f));

        bodyTmp = NewText("Body", fill, fontSize, TextAlignmentOptions.TopLeft, normalColor);
        bodyTmp.richText = true;
        bodyTmp.lineSpacing = 0f;
        SetRect(bodyTmp.rectTransform, Pad, HeaderH, PanelW - 2f * Pad, h - HeaderH - Pad);

        // Vaiven suave (con desfase para que no se mueva igual que el HUD)
        if (floatEffect)
        {
            PanelFloat f = go.AddComponent<PanelFloat>();
            f.amplitude = floatAmplitude;
            f.speed = floatSpeed;
            f.tiltDegrees = floatTilt;
            f.phase = 2.1f;
            f.Capture();
        }
    }

    // Franja horizontal pegada arriba del padre (para la barra de titulo).
    private static void NewStrip(string objName, Transform parent, Color color, float height)
    {
        GameObject go = new GameObject(objName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, -height);
        rt.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    // Cuadradito de color (los 3 puntos de la barra de titulo).
    private static void NewDot(Transform parent, float x, float y, float size, Color color)
    {
        GameObject go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        SetRect((RectTransform)go.transform, x, y, size, size);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    private static TextMeshProUGUI NewText(string objName, Transform parent, float size, TextAlignmentOptions align, Color color)
    {
        GameObject go = new GameObject(objName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    // Posiciona un rectangulo con coordenadas desde la esquina SUPERIOR izquierda del padre.
    private static void SetRect(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -y);
    }
}
