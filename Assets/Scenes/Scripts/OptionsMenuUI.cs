using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel de OPCIONES (estilo del resto del juego: borde blanco, fondo negro, corazon,
/// opcion seleccionada en amarillo). Se construye por codigo y se crea solo la primera
/// vez que se abre, asi que NO hay que ponerlo en ninguna escena.
///
/// Lo abren el menu de inicio y el menu de pausa con:   OptionsMenuUI.Show();
///
/// Controles: Arriba/Abajo cambian de fila, Izquierda/Derecha cambian el valor,
/// Z/Enter sobre "Volver" (o X / Esc en cualquier momento) cierra el panel.
///
/// Filas: Volumen general, Musica, Efectos y Velocidad del juego (ver GameSettings).
/// Usa tiempo SIN escala (unscaled), asi que funciona aunque el juego este en pausa.
/// </summary>
public class OptionsMenuUI : MonoBehaviour
{
    public static OptionsMenuUI Instance { get; private set; }

    /// <summary>
    /// True mientras el panel esta abierto (y durante el frame en que se cierra, para que
    /// el mismo Esc/X que lo cierra no le llegue tambien al menu que esta debajo).
    /// </summary>
    public static bool IsOpen
    {
        get { return Instance != null && (Instance.open || Time.frameCount == Instance.closedFrame); }
    }

    /// <summary>Abre el panel de opciones (lo crea si todavia no existe).</summary>
    public static void Show(System.Action onClose = null)
    {
        if (Instance == null) new GameObject("OptionsMenu").AddComponent<OptionsMenuUI>();
        Instance.Open(onClose);
    }

    [Header("Colores (mismo estilo que el resto del juego)")]
    public Color panelFill = Color.black;
    public Color panelBorder = Color.white;
    public Color normalColor = Color.white;
    public Color selectedColor = new Color(1f, 0.92f, 0.2f);
    public Color hintColor = new Color(0.55f, 0.55f, 0.55f);
    public Color heartColor = new Color(0.9f, 0.1f, 0.15f);

    [Header("Corazon")]
    public float heartMoveSpeed = 20f;

    private enum Kind { Master, Music, Sfx, Speed, Back }

    private class Row
    {
        public Kind kind;
        public TextMeshProUGUI label;
        public TextMeshProUGUI value;
        public RectTransform rt;
    }

    private const float PanelWidth = 820f, PanelHeight = 440f;
    private const float LabelLeft = -350f, LabelWidth = 330f;
    private const float ValueCenterX = 170f, ValueWidth = 420f;

    private readonly System.Collections.Generic.List<Row> rows = new System.Collections.Generic.List<Row>();
    private GameObject root;
    private RectTransform heartRt;
    private Vector2 heartTarget;
    private int selected;
    private bool open;
    private int openedFrame = -1;
    private int closedFrame = -1;
    private System.Action onCloseCallback;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BuildUI();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Open(System.Action onClose)
    {
        onCloseCallback = onClose;
        open = true;
        openedFrame = Time.frameCount;
        selected = 0;

        RefreshAll();
        root.SetActive(true);
        heartRt.anchoredPosition = heartTarget;
    }

    private void Close()
    {
        open = false;
        closedFrame = Time.frameCount;
        GameSettings.Save();
        root.SetActive(false);

        System.Action cb = onCloseCallback;
        onCloseCallback = null;
        if (cb != null) cb();
    }

    void Update()
    {
        if (!open) return;

        // El mismo Z/Enter que abrio este panel no debe contar aqui tambien.
        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.DownArrow)) MoveSelection(1);
        else if (Input.GetKeyDown(KeyCode.UpArrow)) MoveSelection(-1);
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) ChangeValue(-1);
        else if (Input.GetKeyDown(KeyCode.RightArrow)) ChangeValue(1);
        else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.X))
        {
            SoundFX.Confirm();
            Close();
            return;
        }
        else if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return))
        {
            if (rows[selected].kind == Kind.Back)
            {
                SoundFX.Confirm();
                Close();
                return;
            }
        }

        // unscaledDeltaTime: con el juego en pausa (timeScale 0) deltaTime seria 0.
        heartRt.anchoredPosition = Vector2.Lerp(heartRt.anchoredPosition, heartTarget, Time.unscaledDeltaTime * heartMoveSpeed);
    }

    // ---------- Logica ----------

    private void MoveSelection(int dir)
    {
        selected = (selected + dir + rows.Count) % rows.Count;
        SoundFX.Move();
        RefreshAll();
    }

    private void ChangeValue(int dir)
    {
        Row row = rows[selected];
        float before;

        switch (row.kind)
        {
            case Kind.Master:
                before = GameSettings.Master;
                GameSettings.Master = StepVolume(before, dir);
                if (!Mathf.Approximately(before, GameSettings.Master)) SoundFX.Move();
                break;

            case Kind.Music:
                before = GameSettings.Music;
                GameSettings.Music = StepVolume(before, dir);
                if (!Mathf.Approximately(before, GameSettings.Music)) SoundFX.Move();
                break;

            case Kind.Sfx:
                before = GameSettings.Sfx;
                GameSettings.Sfx = StepVolume(before, dir);
                // El sonido de prueba ya suena con el volumen NUEVO, para que lo oigas al cambiarlo.
                if (!Mathf.Approximately(before, GameSettings.Sfx)) SoundFX.Move();
                break;

            case Kind.Speed:
                GameSettings.CycleSpeed(dir);
                SoundFX.Move();
                break;

            default:
                return;
        }

        RefreshRow(row);
    }

    // Sube o baja de 10 en 10 %, sin pasarse de 0 ni de 100.
    private static float StepVolume(float v, int dir)
    {
        return Mathf.Clamp01(Mathf.Round((v + dir * 0.1f) * 10f) / 10f);
    }

    // ---------- Textos ----------

    private void RefreshAll()
    {
        for (int i = 0; i < rows.Count; i++) RefreshRow(rows[i]);

        Row sel = rows[selected];
        heartTarget = new Vector2(LabelLeft - 26f, sel.rt.anchoredPosition.y);
    }

    private void RefreshRow(Row row)
    {
        bool isSelected = rows[selected] == row;
        Color c = isSelected ? selectedColor : normalColor;
        row.label.color = c;
        row.value.color = c;

        switch (row.kind)
        {
            case Kind.Master: row.value.text = "< " + Bar(GameSettings.Master) + " >"; break;
            case Kind.Music: row.value.text = "< " + Bar(GameSettings.Music) + " >"; break;
            case Kind.Sfx: row.value.text = "< " + Bar(GameSettings.Sfx) + " >"; break;
            case Kind.Speed:
                row.value.text = "< " + GameSettings.Speed.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "x >";
                break;
            default: row.value.text = ""; break;
        }
    }

    // Barra hecha con caracteres normales (# y -) para que cualquier fuente la pueda dibujar.
    private static string Bar(float v)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(v * 10f), 0, 10);
        return "[" + new string('#', n) + new string('-', 10 - n) + "] " + Mathf.RoundToInt(v * 100f) + "%";
    }

    // ---------- Construccion de la UI ----------

    private void BuildUI()
    {
        GameObject canvasGo = new GameObject("OptionsCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1100; // por encima del menu de inicio (1000) y del de pausa (500)

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        root = new GameObject("OptionsRoot", typeof(RectTransform));
        root.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)root.transform);

        // Capa oscura que tapa lo que hay detras
        Image overlay = NewImage(root.transform, "Overlay", new Color(0f, 0f, 0f, 0.88f));
        Stretch(overlay.rectTransform);

        // Panel: borde blanco + fondo negro
        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        RectTransform prt = (RectTransform)panel.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        prt.anchoredPosition = Vector2.zero;

        Image border = NewImage(panel.transform, "Border", panelBorder);
        Stretch(border.rectTransform);
        Image fill = NewImage(panel.transform, "Fill", panelFill);
        Stretch(fill.rectTransform);
        fill.rectTransform.offsetMin = new Vector2(6f, 6f);
        fill.rectTransform.offsetMax = new Vector2(-6f, -6f);

        CreateText(panel.transform, "Title", 28f, new Vector2(0f, 175f), new Vector2(500f, 50f), TextAlignmentOptions.Center, normalColor).text = "OPCIONES";

        // Filas
        AddRow(panel.transform, Kind.Master, "Volumen general", 100f);
        AddRow(panel.transform, Kind.Music, "Música", 50f);
        AddRow(panel.transform, Kind.Sfx, "Efectos", 0f);
        AddRow(panel.transform, Kind.Speed, "Velocidad", -50f);
        AddRow(panel.transform, Kind.Back, "Volver", -120f);

        // Ayuda de teclas
        TextMeshProUGUI help = CreateText(panel.transform, "Hint", 14f, new Vector2(0f, -185f), new Vector2(780f, 30f), TextAlignmentOptions.Center, hintColor);
        help.textWrappingMode = TextWrappingModes.NoWrap;
        help.text = "Arriba/Abajo: mover   Izq/Der: cambiar   X: volver";

        // Corazon pixel-art (el mismo sprite que usa PixelSprites)
        GameObject heart = new GameObject("Heart", typeof(RectTransform), typeof(Image));
        heart.transform.SetParent(panel.transform, false);
        heartRt = (RectTransform)heart.transform;
        heartRt.anchorMin = heartRt.anchorMax = new Vector2(0.5f, 0.5f);
        heartRt.sizeDelta = new Vector2(27f, 24f);
        Image heartImg = heart.GetComponent<Image>();
        heartImg.sprite = PixelSprites.Heart();
        heartImg.color = heartColor;
        heartImg.preserveAspect = true;
        heartImg.raycastTarget = false;

        root.SetActive(false);
    }

    private void AddRow(Transform parent, Kind kind, string labelText, float y)
    {
        Row row = new Row { kind = kind };

        row.label = CreateText(parent, "Label_" + labelText, 20f, new Vector2(LabelLeft + LabelWidth / 2f, y),
                               new Vector2(LabelWidth, 40f), TextAlignmentOptions.Left, normalColor);
        row.label.text = labelText;
        row.label.textWrappingMode = TextWrappingModes.NoWrap;
        row.rt = row.label.rectTransform;

        row.value = CreateText(parent, "Value_" + labelText, 20f, new Vector2(ValueCenterX, y),
                               new Vector2(ValueWidth, 40f), TextAlignmentOptions.Center, normalColor);
        row.value.textWrappingMode = TextWrappingModes.NoWrap;

        rows.Add(row);
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, float size, Vector2 pos, Vector2 dim,
                                              TextAlignmentOptions align, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = dim;
        rt.anchoredPosition = pos;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Image NewImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}