using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD de ReCode (Head-Up Display), construido 100% por codigo con el mismo estilo del
/// resto del juego (borde blanco, fondo negro, texto pixel). Siempre visible mientras
/// juegas y se esconde solo en el menu de inicio, el de pausa y las opciones. Muestra:
///   - Contador de fragmentos:   FRAGMENTOS 3/5
///   - Mini-inventario con iconos: un cuadro por fragmento (apagado = aun no lo tienes)
///   - Pasos de la secuencia:    Pasos: 3/8   (el maximo sale del nivel; sin limite = solo el numero)
///   - Nivel actual y objetivo
///   - Aviso al recoger un fragmento (con su definicion educativa)
///   - Panel modal de "mision cumplida" al guardar los 5 fragmentos
///
/// Uso: ponlo en el mismo objeto que LevelManager (por ejemplo "Level"). No necesita nada mas.
/// </summary>
public class GameHUDManager : MonoBehaviour
{
    public static GameHUDManager Instance { get; private set; }

    public enum Corner { TopLeft, TopRight }

    [Header("Referencias (se buscan solas)")]
    public Canvas canvas;
    public LevelManager levelManager;
    public CommandQueueUI queue;
    public StartMenuUI startMenu;

    [Header("Posicion y tamaño")]
    public Corner corner = Corner.TopLeft;
    [Tooltip("Separacion respecto a la esquina (unidades del Canvas).")]
    public Vector2 margin = new Vector2(14f, 14f);
    [Tooltip("Multiplica el tamaño del HUD. Ya se ajusta solo al tamaño de tu Canvas; usa esto solo para afinar (1 = normal).")]
    [Range(0.3f, 2f)] public float uiScale = 1f;

    [Header("Movimiento (flotar suave)")]
    [Tooltip("El panel se mueve despacito, como flotando. Desmarca para dejarlo fijo.")]
    public bool floatEffect = true;
    [Tooltip("Cuanto se mueve (unidades del Canvas): X = de lado, Y = arriba/abajo.")]
    public Vector2 floatAmplitude = new Vector2(3f, 5f);
    [Tooltip("Velocidad del vaiven (1 = normal, 0.5 = lento).")]
    public float floatSpeed = 0.8f;
    [Tooltip("Inclinacion maxima en grados (0 = sin inclinar).")]
    public float floatTilt = 0.5f;

    [Header("Textos (editables)")]
    public string title = "FRAGMENTOS";
    [Tooltip("{0} se reemplaza por el total de fragmentos.")]
    [TextArea(1, 3)] public string objective = "Recupera los {0} fragmentos.";
    public string goalHint = "Llega a la meta.";
    public string toastTitle = "FRAGMENTO RECUPERADO";
    public float toastSeconds = 3.2f;

    [Header("Mision cumplida (se muestra al guardar los 5 fragmentos)")]
    [Tooltip("Frases que dice el guia justo antes del panel.")]
    [TextArea(2, 4)]
    public string[] missionGuideLines =
    {
        "Lo lograste. Los cinco fragmentos están completos.",
        "Algoritmo, depurar, bucle, booleano y función.",
        "Con eso ya puedes escribir tu primer programa.",
        "Una máquina solo hace lo que le dices... y ahora sabes cómo decírselo."
    };
    public string missionTitle = "¡FELICIDADES!";
    [TextArea(2, 5)]
    public string missionBody = "Recuperaste los 5 fragmentos de código.\nYa conoces las bases del pseudocódigo: algoritmo, depuración, bucle, booleano y función.";
    public string missionContinue = "Presiona E para continuar";

    [Header("Colores")]
    public Color borderColor = Color.white;
    public Color fillColor = Color.black;
    public Color textColor = Color.white;
    public Color accentColor = new Color(1f, 0.92f, 0.2f);     // amarillo (como la opcion elegida)
    public Color levelColor = new Color(0.4f, 1f, 0.9f);
    public Color emptySlotColor = new Color(0.22f, 0.22f, 0.22f);
    [Tooltip("Color de la barra de titulo (como la ventana de un editor de codigo).")]
    public Color headerColor = new Color(0.08f, 0.32f, 0.36f);

    // ---------------------------------------------------------------- estado

    // Medidas pensadas para un Canvas de 1280 de ancho; se escalan solas segun el Canvas real.
    private const float ReferenceWidth = 1280f;
    private const float PanelW = 244f, PanelH = 216f, HeaderH = 26f;
    private const float SlotSize = 40f, SlotGap = 6f;

    private RectTransform hudRoot;
    private CanvasGroup hudGroup;
    private TextMeshProUGUI titleTmp, counterTmp, stepsTmp, levelTmp, objectiveTmp;
    private Image[] slotIcons;
    private Image[] slotBorders;

    private RectTransform toastRt;
    private CanvasGroup toastGroup;
    private TextMeshProUGUI toastTmp;
    private Coroutine toastRoutine;

    private GameObject missionRoot;
    private RectTransform missionBox;
    private RectTransform canvasRt;

    private int lastCount = -1;
    private int lastSteps = -2;
    private int lastMax = -2;
    private int lastChapter = -2, lastLevel = -2;
    private float flashTimer;

    // ---------------------------------------------------------------- ciclo de vida

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (levelManager == null) levelManager = FindFirstObjectByType<LevelManager>();
        if (queue == null) queue = FindFirstObjectByType<CommandQueueUI>();
        if (startMenu == null) startMenu = FindFirstObjectByType<StartMenuUI>();

        if (canvas == null)
        {
            Debug.LogWarning("[GameHUDManager] No encontre ningun Canvas en la escena. El HUD no se puede crear.");
            enabled = false;
            return;
        }

        canvasRt = (RectTransform)canvas.rootCanvas.transform;
        BuildHud();
        BuildToast();
        BuildMissionPanel();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (hudRoot == null) return;

        ApplyScale();

        // Se esconde mientras el menu de inicio, el de pausa o las opciones estan abiertos
        bool hidden = (startMenu != null && startMenu.IsMenuActive) || Time.timeScale <= 0f || OptionsMenuUI.IsOpen;
        float target = hidden ? 0f : 1f;
        hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, target, Time.unscaledDeltaTime * 4f);

        RefreshFragments();
        RefreshSteps();
        RefreshLevel();
        AnimateFlash();
    }

    // El HUD se encoge o crece segun el ancho real del Canvas, para que nunca tape el juego.
    private void ApplyScale()
    {
        float w = canvasRt.rect.width;
        if (w <= 1f) return;
        float k = Mathf.Clamp(w / ReferenceWidth, 0.3f, 2f) * uiScale;
        Vector3 s = new Vector3(k, k, 1f);
        if (hudRoot.localScale != s) hudRoot.localScale = s;
        if (missionBox != null && missionBox.localScale != s) missionBox.localScale = s;
    }

    // ---------------------------------------------------------------- refrescos

    private void RefreshFragments()
    {
        int count = FragmentInventory.Count;
        if (count == lastCount) return;

        bool increased = lastCount >= 0 && count > lastCount;
        lastCount = count;

        int total = FragmentInventory.Total;
        counterTmp.color = count >= total ? accentColor : textColor;
        counterTmp.text = count + "/" + total;

        for (int i = 0; i < slotIcons.Length; i++)
        {
            FragmentInventory.Fragment f = FragmentInventory.All[i];
            bool owned = FragmentInventory.IsOwned(f.id);
            slotIcons[i].color = owned ? f.color : emptySlotColor;
            slotBorders[i].color = owned ? f.color : new Color(1f, 1f, 1f, 0.35f);
        }

        if (increased)
        {
            flashTimer = 0.6f;
            FragmentInventory.Fragment f = FragmentInventory.Get(FragmentInventory.LastCollected);
            if (f != null) ShowToast(f);
        }
    }

    private void RefreshSteps()
    {
        int n = queue != null ? queue.Instructions.Count : 0;
        int max = queue != null ? queue.maxInstructions : 0;
        if (n == lastSteps && max == lastMax) return;
        lastSteps = n;
        lastMax = max;

        if (max > 0)
        {
            string hex = ColorUtility.ToHtmlStringRGB(n >= max ? new Color(1f, 0.35f, 0.4f) : textColor);
            stepsTmp.text = "Pasos: <color=#" + hex + ">" + n + "/" + max + "</color>";
        }
        else
        {
            stepsTmp.text = "Pasos: " + n;
        }
    }

    private void RefreshLevel()
    {
        if (levelManager == null) return;
        int c = levelManager.CurrentChapterIndex;
        int l = levelManager.CurrentLevelIndex;
        if (c == lastChapter && l == lastLevel) return;
        lastChapter = c;
        lastLevel = l;

        string name = "";
        if (levelManager.chapters != null && c >= 0 && c < levelManager.chapters.Count)
        {
            ChapterData ch = levelManager.chapters[c];
            if (ch.levels != null && l >= 0 && l < ch.levels.Count)
                name = ch.name + " - " + ch.levels[l].name;
        }
        levelTmp.text = name;
    }

    private void AnimateFlash()
    {
        if (flashTimer <= 0f) return;
        flashTimer -= Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(flashTimer / 0.6f);
        float s = 1f + 0.12f * Mathf.Sin(k * Mathf.PI * 3f) * k;
        counterTmp.rectTransform.localScale = new Vector3(s, s, 1f);
        if (flashTimer <= 0f) counterTmp.rectTransform.localScale = Vector3.one;
    }

    // ---------------------------------------------------------------- aviso al recoger

    private void ShowToast(FragmentInventory.Fragment f)
    {
        if (toastRt == null) return;
        toastTmp.text = "<color=#" + ColorUtility.ToHtmlStringRGB(f.color) + ">" + toastTitle + ": " + f.title + "</color>\n" + f.definition;
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine());
    }

    private IEnumerator ToastRoutine()
    {
        toastRt.gameObject.SetActive(true);
        float t = 0f;
        while (t < 0.15f) { t += Time.unscaledDeltaTime; toastGroup.alpha = Mathf.Clamp01(t / 0.15f); yield return null; }
        toastGroup.alpha = 1f;

        float wait = 0f;
        while (wait < toastSeconds) { wait += Time.unscaledDeltaTime; yield return null; }

        t = 0f;
        while (t < 0.3f) { t += Time.unscaledDeltaTime; toastGroup.alpha = 1f - Mathf.Clamp01(t / 0.3f); yield return null; }
        toastRt.gameObject.SetActive(false);
        toastRoutine = null;
    }

    /// <summary>
    /// Compatibilidad con el codigo viejo (LevelRunner llamaba ResetProgress al terminar). Ahora solo
    /// devuelve los fragmentos pendientes; los ya guardados NO se tocan. Ya no hace falta llamarlo.
    /// </summary>
    public void ResetProgress()
    {
        FragmentInventory.DiscardPending();
    }

    // ---------------------------------------------------------------- mision cumplida

    /// <summary>Muestra el panel modal de mision cumplida y espera E (o Z/Enter) para cerrarlo.</summary>
    public IEnumerator ShowMissionComplete()
    {
        if (missionRoot == null) yield break;

        missionRoot.transform.SetAsLastSibling();
        missionRoot.SetActive(true);
        SoundFX.Win();
        yield return null;

        while (!(Time.timeScale > 0f && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return))))
            yield return null;

        SoundFX.Confirm();
        missionRoot.SetActive(false);
        yield return null;
    }

    // ---------------------------------------------------------------- construccion de la interfaz

    private void BuildHud()
    {
        Canvas root = canvas.rootCanvas;

        GameObject go = new GameObject("HUD", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        go.transform.SetParent(root.transform, false);
        hudRoot = (RectTransform)go.transform;
        hudGroup = go.GetComponent<CanvasGroup>();
        hudGroup.alpha = 0f; // aparece con un fundido al empezar (y no parpadea detras del menu de inicio)
        hudGroup.blocksRaycasts = false;
        hudGroup.interactable = false;

        bool left = corner == Corner.TopLeft;
        Vector2 anchor = new Vector2(left ? 0f : 1f, 1f);
        hudRoot.anchorMin = anchor;
        hudRoot.anchorMax = anchor;
        hudRoot.pivot = anchor;
        hudRoot.sizeDelta = new Vector2(PanelW, PanelH);
        hudRoot.anchoredPosition = new Vector2(left ? margin.x : -margin.x, -margin.y);

        Image border = go.GetComponent<Image>();
        border.color = borderColor;
        border.raycastTarget = false;

        RectTransform fill = NewImage("Fill", hudRoot, fillColor, 3f);

        // Barra de titulo (como la ventana de un editor de codigo): nombre a la izquierda, contador a la derecha
        NewStrip("Header", fill, headerColor, HeaderH);

        titleTmp = NewText("Title", fill, 15f, TextAlignmentOptions.MidlineLeft, textColor);
        titleTmp.text = title;
        SetRect(titleTmp.rectTransform, 8f, 0f, 150f, HeaderH);

        counterTmp = NewText("Counter", fill, 17f, TextAlignmentOptions.MidlineRight, textColor);
        RectTransform crt = counterTmp.rectTransform;
        crt.anchorMin = new Vector2(1f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 0.5f);
        crt.sizeDelta = new Vector2(64f, HeaderH);
        crt.anchoredPosition = new Vector2(-8f, -HeaderH / 2f);

        // Mini-inventario: 5 cuadros con icono y su nombre corto debajo
        int total = FragmentInventory.Total;
        slotIcons = new Image[total];
        slotBorders = new Image[total];
        float x0 = 8f;
        for (int i = 0; i < total; i++)
        {
            float x = x0 + i * (SlotSize + SlotGap);

            RectTransform slot = NewImage("Slot" + i, fill, new Color(1f, 1f, 1f, 0.35f), 0f);
            SetRect(slot, x, 34f, SlotSize, SlotSize);
            slotBorders[i] = slot.GetComponent<Image>();

            RectTransform inner = NewImage("SlotFill", slot, Color.black, 2f);

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(inner, false);
            RectTransform irt = (RectTransform)iconGo.transform;
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(7f, 5f);
            irt.offsetMax = new Vector2(-7f, -5f);
            Image icon = iconGo.GetComponent<Image>();
            icon.sprite = PixelSprites.Fragment();
            icon.preserveAspect = true;
            icon.color = emptySlotColor;
            icon.raycastTarget = false;
            slotIcons[i] = icon;

            TextMeshProUGUI lbl = NewText("SlotLabel" + i, fill, 12f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.75f));
            lbl.text = FragmentInventory.All[i].shortName;
            SetRect(lbl.rectTransform, x - 3f, 76f, SlotSize + 6f, 16f);
        }

        stepsTmp = NewText("Steps", fill, 15f, TextAlignmentOptions.Left, textColor);
        SetRect(stepsTmp.rectTransform, 8f, 94f, PanelW - 22f, 20f);

        // El nombre del nivel ahora puede ocupar 2 lineas (antes se cortaba con "...")
        levelTmp = NewText("Level", fill, 13f, TextAlignmentOptions.TopLeft, levelColor);
        levelTmp.textWrappingMode = TextWrappingModes.Normal;
        SetRect(levelTmp.rectTransform, 8f, 116f, PanelW - 22f, 32f);

        objectiveTmp = NewText("Objective", fill, 13f, TextAlignmentOptions.TopLeft, new Color(1f, 1f, 1f, 0.85f));
        objectiveTmp.textWrappingMode = TextWrappingModes.Normal;
        objectiveTmp.text = objective.Replace("{0}", FragmentInventory.Total.ToString()) + "\n" + goalHint;
        SetRect(objectiveTmp.rectTransform, 8f, 150f, PanelW - 22f, 60f);

        // Vaiven suave (el panel flota alrededor de su posicion)
        if (floatEffect)
        {
            PanelFloat f = go.AddComponent<PanelFloat>();
            f.amplitude = floatAmplitude;
            f.speed = floatSpeed;
            f.tiltDegrees = floatTilt;
            f.phase = 0f;
            f.Capture();
        }
    }

    private void BuildToast()
    {
        GameObject go = new GameObject("Toast", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        go.transform.SetParent(hudRoot, false);
        toastRt = (RectTransform)go.transform;
        toastRt.anchorMin = new Vector2(0f, 1f);
        toastRt.anchorMax = new Vector2(0f, 1f);
        toastRt.pivot = new Vector2(0f, 1f);
        toastRt.sizeDelta = new Vector2(PanelW, 78f);
        toastRt.anchoredPosition = new Vector2(0f, -(PanelH + 8f));

        toastGroup = go.GetComponent<CanvasGroup>();
        toastGroup.blocksRaycasts = false;
        toastGroup.interactable = false;

        Image border = go.GetComponent<Image>();
        border.color = borderColor;
        border.raycastTarget = false;
        RectTransform fill = NewImage("Fill", toastRt, fillColor, 3f);

        toastTmp = NewText("ToastText", fill, 13f, TextAlignmentOptions.TopLeft, textColor);
        toastTmp.textWrappingMode = TextWrappingModes.Normal;
        SetRect(toastTmp.rectTransform, 8f, 6f, PanelW - 22f, 66f);

        go.SetActive(false);
    }

    private void BuildMissionPanel()
    {
        Canvas root = canvas.rootCanvas;

        // Fondo oscuro a pantalla completa
        missionRoot = new GameObject("MissionComplete", typeof(RectTransform), typeof(Image));
        missionRoot.transform.SetParent(root.transform, false);
        RectTransform rt = (RectTransform)missionRoot.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image dim = missionRoot.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.7f);

        // Cuadro central
        RectTransform box = NewImage("Box", rt, borderColor, 0f);
        missionBox = box;
        box.anchorMin = new Vector2(0.5f, 0.5f);
        box.anchorMax = new Vector2(0.5f, 0.5f);
        box.pivot = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(760f, 300f);
        box.anchoredPosition = Vector2.zero;
        RectTransform fill = NewImage("Fill", box, fillColor, 4f);

        TextMeshProUGUI head = NewText("Head", fill, 40f, TextAlignmentOptions.Center, accentColor);
        head.text = missionTitle;
        SetRect(head.rectTransform, 20f, 20f, 712f, 56f);

        TextMeshProUGUI body = NewText("Body", fill, 24f, TextAlignmentOptions.Center, textColor);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.text = missionBody;
        SetRect(body.rectTransform, 30f, 90f, 692f, 120f);

        TextMeshProUGUI cont = NewText("Continue", fill, 22f, TextAlignmentOptions.Center, accentColor);
        cont.text = missionContinue;
        SetRect(cont.rectTransform, 20f, 232f, 712f, 40f);

        missionRoot.SetActive(false);
    }

    // ---------------------------------------------------------------- utilidades de UI

    private static RectTransform NewImage(string objName, Transform parent, Color color, float inset)
    {
        GameObject go = new GameObject(objName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    // Franja horizontal pegada arriba del padre (para la barra de titulo).
    private static RectTransform NewStrip(string objName, Transform parent, Color color, float height)
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
        return rt;
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
