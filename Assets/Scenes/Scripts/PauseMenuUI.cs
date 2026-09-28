using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Menu de pausa, manejado 100% con teclado (igual que el resto del juego, sin mouse):
/// ESC abre/cierra, flechas Arriba/Abajo mueven un corazon entre las opciones, Z o Enter
/// confirma. La opcion seleccionada se pinta de amarillo, como "Derecha" en tu menu de
/// direcciones.
///
/// Panel principal: Reanudar / Escoger nivel / Salir del juego.
/// Panel de niveles: lista con scroll de todos los capitulos y sus niveles. Los ya
/// completados llevan un check, los bloqueados se ven en gris y Z no hace nada sobre
/// ellos. ESC vuelve al panel principal (o cierra el menu si ya estas en el principal).
///
/// Mientras el menu esta abierto, el juego se congela de verdad (Time.timeScale = 0), asi
/// que cualquier animacion en curso se pausa y sigue exactamente donde iba al reanudar.
/// Tambien se bloquea el input de la cola de instrucciones y de los MenuNavigator de la
/// escena, para que no sigas moviendo el corazon del menu de direcciones por detras.
///
/// Todo se construye por codigo: solo pon este script en cualquier objeto de la escena
/// (por ejemplo "Level"). No hace falta armar nada a mano.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [Header("Referencias (se buscan solas si las dejas vacias)")]
    public LevelManager levelManager;
    public LevelRunner runner;
    public CommandQueueUI queue;
    [Tooltip("El menu de inicio (se busca solo). Se usa para la opcion 'Menu principal'.")]
    public StartMenuUI startMenu;

    [Header("Teclas")]
    public KeyCode pauseKey = KeyCode.Escape;
    public KeyCode confirmKey = KeyCode.Z;
    public KeyCode confirmKeyAlt = KeyCode.Return;

    [Header("Colores (mismo estilo que el resto del juego)")]
    public Color panelFill = Color.black;
    public Color panelBorder = Color.white;
    public Color normalColor = Color.white;
    public Color selectedColor = new Color(1f, 0.92f, 0.2f); // amarillo, como "Derecha"
    public Color lockedColor = new Color(0.4f, 0.4f, 0.4f);

    [Header("Corazon")]
    [Tooltip("Separacion entre el borde izquierdo de la opcion y el corazon.")]
    public float heartOffsetX = -26f;
    public float heartMoveSpeed = 20f;
    public float heartFontSize = 22f;

    private class MenuEntry
    {
        public TextMeshProUGUI text;
        public RectTransform rect;
        public System.Action onConfirm;
        public bool locked;
    }

    private GameObject rootPanel;
    private GameObject mainPanel;
    private GameObject levelSelectPanel;
    private RectTransform levelListContent;
    private RectTransform viewportRt;

    private TextMeshProUGUI heartTmp;
    private RectTransform heartRt;
    private Vector2 heartTarget;

    private readonly List<MenuEntry> mainEntries = new List<MenuEntry>();
    private readonly List<MenuEntry> levelEntries = new List<MenuEntry>();
    private List<MenuEntry> activeEntries;
    private int selectedIndex;

    private MenuNavigator[] navigators;
    private bool isPaused;
    private bool prevQueueLocked;

    private const float RowHeight = 34f;
    private const float HeaderHeight = 32f;
    private const float ChapterGap = 16f;
    private const float EntryWidth = 440f;
    private const float ContentWidth = 520f;
    private const float ViewportHeight = 560f;

    void Awake()
    {
        if (levelManager == null) levelManager = FindFirstObjectByType<LevelManager>();
        if (runner == null) runner = FindFirstObjectByType<LevelRunner>();
        if (queue == null) queue = FindFirstObjectByType<CommandQueueUI>();
        if (startMenu == null) startMenu = FindFirstObjectByType<StartMenuUI>();
        navigators = FindObjectsByType<MenuNavigator>(FindObjectsSortMode.None);

        BuildUI();
    }

    void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            if (!isPaused) OpenMain();
            else if (levelSelectPanel.activeSelf) ShowMainPanel();
            else Resume();
            return;
        }

        if (!isPaused) return;

        if (Input.GetKeyDown(KeyCode.DownArrow)) MoveSelection(1);
        else if (Input.GetKeyDown(KeyCode.UpArrow)) MoveSelection(-1);
        else if (Input.GetKeyDown(confirmKey) || Input.GetKeyDown(confirmKeyAlt)) ConfirmSelection();

        if (heartRt != null)
        {
            // Time.unscaledDeltaTime: con el juego en pausa (timeScale 0), Time.deltaTime es 0.
            heartRt.anchoredPosition = Vector2.Lerp(heartRt.anchoredPosition, heartTarget, Time.unscaledDeltaTime * heartMoveSpeed);
        }
    }

    // ---------- Navegacion con teclado ----------

    private void MoveSelection(int dir)
    {
        if (activeEntries == null || activeEntries.Count == 0) return;
        selectedIndex = (selectedIndex + dir + activeEntries.Count) % activeEntries.Count;
        SoundFX.Move();
        UpdateSelectionVisual();
    }

    private void ConfirmSelection()
    {
        if (activeEntries == null || selectedIndex < 0 || selectedIndex >= activeEntries.Count) return;
        MenuEntry entry = activeEntries[selectedIndex];
        if (entry.locked) return; // bloqueado: Z no hace nada
        SoundFX.Confirm();
        entry.onConfirm?.Invoke();
    }

    private void UpdateSelectionVisual()
    {
        for (int i = 0; i < activeEntries.Count; i++)
        {
            MenuEntry e = activeEntries[i];
            bool isSelected = i == selectedIndex;
            e.text.color = e.locked ? lockedColor : (isSelected ? selectedColor : normalColor);
        }

        MenuEntry sel = activeEntries[selectedIndex];
        float leftEdge = sel.rect.anchoredPosition.x - sel.rect.sizeDelta.x / 2f;
        heartTarget = new Vector2(leftEdge + heartOffsetX, sel.rect.anchoredPosition.y);

        if (levelSelectPanel.activeSelf) ScrollToEntry(sel);
    }

    private void ScrollToEntry(MenuEntry entry)
    {
        float entryTop = -entry.rect.anchoredPosition.y;
        float entryCenterY = entryTop + RowHeight / 2f;
        float contentHeight = levelListContent.sizeDelta.y;

        float desiredY = Mathf.Clamp(entryCenterY - ViewportHeight / 2f, 0f, Mathf.Max(0f, contentHeight - ViewportHeight));
        levelListContent.anchoredPosition = new Vector2(levelListContent.anchoredPosition.x, desiredY);
    }

    // ---------- Abrir / cerrar ----------

    private void OpenMain()
    {
        isPaused = true;
        Time.timeScale = 0f;

        prevQueueLocked = queue != null && queue.Locked;
        if (queue != null) queue.Locked = true;
        SetNavigatorsLocked(true);

        rootPanel.SetActive(true);
        ShowMainPanel();
    }

    private void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (queue != null) queue.Locked = prevQueueLocked;
        SetNavigatorsLocked(false);

        rootPanel.SetActive(false);
    }

    private void ShowMainPanel()
    {
        mainPanel.SetActive(true);
        levelSelectPanel.SetActive(false);

        // La opcion "Escoger nivel" se bloquea si hay una secuencia en curso
        // (evita elegir un nivel nuevo a mitad de una animacion).
        mainEntries[1].locked = !CanSelectLevels();
        mainEntries[2].locked = startMenu == null || !CanSelectLevels(); // "Menu principal"

        heartRt.SetParent(mainPanel.transform, false);
        activeEntries = mainEntries;
        selectedIndex = 0;
        UpdateSelectionVisual();
        heartRt.anchoredPosition = heartTarget; // snap, sin animar, al cambiar de panel
    }

    private void OpenLevelSelect()
    {
        mainPanel.SetActive(false);
        levelSelectPanel.SetActive(true);

        heartRt.SetParent(rootPanel.transform, false); // sacarlo antes de borrar la lista vieja
        RebuildLevelList();

        heartRt.SetParent(levelListContent, false);
        activeEntries = levelEntries;
        selectedIndex = 0;
        UpdateSelectionVisual();
        heartRt.anchoredPosition = heartTarget;
    }

    private void GoToLevelAndClose(int chapterIndex, int levelIndex)
    {
        if (levelManager == null) return;
        Resume();
        levelManager.GoToLevel(chapterIndex, levelIndex);
    }

    private void GoToMainMenu()
    {
        if (startMenu == null) return;
        Resume();          // descongela el juego y desbloquea todo
        startMenu.Open();  // el menu de inicio vuelve a bloquear lo que necesita
    }

    private bool CanSelectLevels()
    {
        return runner == null || !runner.IsBusy;
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetNavigatorsLocked(bool locked)
    {
        if (navigators == null) return;
        foreach (MenuNavigator nav in navigators)
        {
            if (nav != null) nav.externalLock = locked;
        }
    }

    // ---------- Construccion de la UI ----------

    private void BuildUI()
    {
        GameObject canvasGo = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // por encima de todo lo demas

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject rootGo = new GameObject("PauseRoot", typeof(RectTransform));
        rootGo.transform.SetParent(canvasGo.transform, false);
        RectTransform rootRt = (RectTransform)rootGo.transform;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;
        rootPanel = rootGo;

        GameObject overlayGo = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        overlayGo.transform.SetParent(rootGo.transform, false);
        RectTransform ort = (RectTransform)overlayGo.transform;
        ort.anchorMin = Vector2.zero;
        ort.anchorMax = Vector2.one;
        ort.offsetMin = Vector2.zero;
        ort.offsetMax = Vector2.zero;
        overlayGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        // Corazon: un solo caracter TMP en forma de corazon, se reutiliza y se reparenta
        // segun el panel activo (asi se mueve junto con el scroll de la lista de niveles).
        GameObject heartGo = new GameObject("Heart", typeof(RectTransform), typeof(TextMeshProUGUI));
        heartRt = (RectTransform)heartGo.transform;
        heartTmp = heartGo.GetComponent<TextMeshProUGUI>();
        heartTmp.text = "\u2665"; // ♥
        heartTmp.color = new Color(0.85f, 0.15f, 0.2f);
        heartTmp.fontSize = heartFontSize;
        heartTmp.alignment = TextAlignmentOptions.Center;
        heartTmp.raycastTarget = false;
        heartRt.sizeDelta = new Vector2(30f, 30f);
        heartRt.anchorMin = new Vector2(0.5f, 0.5f);
        heartRt.anchorMax = new Vector2(0.5f, 0.5f);

        BuildMainPanel(rootGo.transform);
        BuildLevelSelectPanel(rootGo.transform);

        levelSelectPanel.SetActive(false);
        rootPanel.SetActive(false);
    }

    private void BuildMainPanel(Transform parent)
    {
        mainPanel = CreatePanel(parent, new Vector2(420f, 300f));

        CreateLabel(mainPanel.transform, "PAUSA", 28f, new Vector2(0f, 110f), new Vector2(380f, 50f));

        mainEntries.Clear();
        mainEntries.Add(CreateMainEntry("Reanudar", new Vector2(0f, 40f), Resume));
        mainEntries.Add(CreateMainEntry("Escoger nivel", new Vector2(0f, -4f), OpenLevelSelect));
        mainEntries.Add(CreateMainEntry("Menú principal", new Vector2(0f, -48f), GoToMainMenu));
        mainEntries.Add(CreateMainEntry("Salir del juego", new Vector2(0f, -92f), QuitGame));
    }

    private MenuEntry CreateMainEntry(string label, Vector2 anchoredPos, System.Action onConfirm)
    {
        GameObject go = new GameObject("Entry_" + label, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(mainPanel.transform, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(300f, 34f);
        rt.anchoredPosition = anchoredPos;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = normalColor;
        tmp.raycastTarget = false;

        return new MenuEntry { text = tmp, rect = rt, onConfirm = onConfirm, locked = false };
    }

    private void BuildLevelSelectPanel(Transform parent)
    {
        levelSelectPanel = CreatePanel(parent, new Vector2(560f, 700f));

        CreateLabel(levelSelectPanel.transform, "Selecciona un nivel", 26f, new Vector2(0f, 320f), new Vector2(500f, 50f));

        // ---- ScrollView (Viewport + Content), el scroll se mueve a mano segun la seleccion ----
        GameObject scrollGo = new GameObject("ScrollView", typeof(RectTransform));
        scrollGo.transform.SetParent(levelSelectPanel.transform, false);
        RectTransform scrollRt = (RectTransform)scrollGo.transform;
        scrollRt.anchorMin = new Vector2(0.5f, 0.5f);
        scrollRt.anchorMax = new Vector2(0.5f, 0.5f);
        scrollRt.sizeDelta = new Vector2(ContentWidth, ViewportHeight);
        scrollRt.anchoredPosition = new Vector2(0f, 0f);

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        viewportRt = (RectTransform)viewportGo.transform;
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;

        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        levelListContent = (RectTransform)contentGo.transform;
        levelListContent.anchorMin = new Vector2(0.5f, 1f);
        levelListContent.anchorMax = new Vector2(0.5f, 1f);
        levelListContent.pivot = new Vector2(0.5f, 1f);
        levelListContent.sizeDelta = new Vector2(ContentWidth, ViewportHeight);
        levelListContent.anchoredPosition = Vector2.zero;
    }

    // Vacia la lista y la vuelve a armar segun el progreso guardado. Se llama cada vez que
    // se abre la pantalla de seleccion, asi siempre refleja lo ultimo completado.
    private void RebuildLevelList()
    {
        levelEntries.Clear();
        for (int i = levelListContent.childCount - 1; i >= 0; i--)
        {
            Destroy(levelListContent.GetChild(i).gameObject);
        }

        if (levelManager == null) return;

        float y = 0f;

        for (int c = 0; c < levelManager.chapters.Count; c++)
        {
            if (c > 0) y -= ChapterGap; // separa visualmente un capitulo del anterior

            ChapterData chapter = levelManager.chapters[c];

            CreateContentLabel(chapter.name, y);
            y -= HeaderHeight;

            if (chapter.levels == null) continue;

            for (int l = 0; l < chapter.levels.Count; l++)
            {
                bool unlocked = levelManager.IsLevelUnlocked(c, l);
                bool completed = levelManager.IsLevelCompleted(c, l);
                string prefix = completed ? "\u2713 " : (unlocked ? "" : "\uD83D\uDD12 ");

                MenuEntry entry = CreateContentEntry(prefix + chapter.levels[l].name, y);
                entry.locked = !unlocked;

                int chapterIndex = c, levelIndex = l; // copias locales para el closure
                entry.onConfirm = () => GoToLevelAndClose(chapterIndex, levelIndex);

                levelEntries.Add(entry);
                y -= RowHeight;
            }
        }

        levelListContent.sizeDelta = new Vector2(ContentWidth, Mathf.Max(ViewportHeight, -y + 20f));
        levelListContent.anchoredPosition = Vector2.zero;
    }

    private void CreateContentLabel(string text, float y)
    {
        GameObject go = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(levelListContent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(EntryWidth, HeaderHeight);
        rt.anchoredPosition = new Vector2(0f, y);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 22f;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = normalColor;
        tmp.raycastTarget = false;
    }

    private MenuEntry CreateContentEntry(string text, float y)
    {
        GameObject go = new GameObject("Entry", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(levelListContent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(EntryWidth, RowHeight);
        rt.anchoredPosition = new Vector2(0f, y);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = normalColor;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap; // una sola linea, no se corte a la mitad

        return new MenuEntry { text = tmp, rect = rt };
    }

    // ---------- Piezas reutilizables (panel, texto suelto) ----------

    private GameObject CreatePanel(Transform parent, Vector2 size)
    {
        GameObject go = new GameObject("Panel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        GameObject borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
        borderGo.transform.SetParent(go.transform, false);
        RectTransform brt = (RectTransform)borderGo.transform;
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        borderGo.GetComponent<Image>().color = panelBorder;

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(go.transform, false);
        RectTransform frt = (RectTransform)fillGo.transform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(6f, 6f);
        frt.offsetMax = new Vector2(-6f, -6f);
        fillGo.GetComponent<Image>().color = panelFill;

        return go;
    }

    private void CreateLabel(Transform parent, string text, float fontSize, Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = normalColor;
        tmp.raycastTarget = false;
    }
}