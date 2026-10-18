using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Menu de inicio de ReCode (estilo Undertale + tema "code"). Todo se construye por codigo.
///
/// Secuencia:
///  1) Pantalla negra con lluvia de 0 y 1. Una consola escribe "ReCode.exe --iniciar",
///     carga la memoria, busca al guia... y "1 ERROR ENCONTRADO".
///  2) El titulo se DESCIFRA (0 y 1 que se van convirtiendo en letras) y de vez en cuando
///     se corrompe con un glitch. Debajo se escribe la frase de la mision.
///  3) El guia entra deslizandose (mismo sprite que usa tu DialogueController).
///  4) PRESIONA Z -> se abre el cuadro estilo Undertale con el corazon:
///     Jugar / Continuar / Nuevo juego (pide confirmacion antes de borrar) / Salir.
///     A la derecha, texto que se escribe: frase al azar y descripcion de cada opcion.
///  5) Al elegir, el titulo se corrompe del todo, el cuadro se cierra, todo se desvanece
///     y arranca el nivel con la pantalla intermedia de siempre.
///
/// Uso: pon este script en cualquier objeto de la escena (por ejemplo "Level") y dale Play.
/// Mientras esta abierto bloquea el menu de pausa, la cola de instrucciones y los
/// MenuNavigator, y usa el progreso guardado por LevelManager (PlayerPrefs).
/// Teclas: Flechas arriba/abajo, Z/Enter confirma, X/Esc vuelve atras. Z durante la
/// intro la salta.
/// </summary>
public class StartMenuUI : MonoBehaviour
{
    [Header("Referencias (se buscan solas si las dejas vacias)")]
    public LevelManager levelManager;
    public PauseMenuUI pauseMenu;

    [Header("Activacion")]
    public bool showOnStart = true;
    [Tooltip("Si esta apagado, despues del titulo se abre el menu solo, sin pedir Z.")]
    public bool requirePressToStart = true;

    [Header("Teclas")]
    public KeyCode confirmKey = KeyCode.Z;
    public KeyCode confirmKeyAlt = KeyCode.Return;
    public KeyCode backKey = KeyCode.X;

    [Header("Textos")]
    public string gameTitle = "ReCode";
    [Tooltip("Cuantas letras del titulo van en blanco; el resto va en verde 'code'.")]
    public int whiteLetters = 2;
    public string tagline = "Una máquina solo hace lo que le dices.";
    [TextArea(1, 2)]
    public string[] flavorTexts =
    {
        "* El código no se escribe solo.",
        "* Una máquina solo hace lo que le dices. Ni más, ni menos.",
        "* Tu guía te observa. Sin parpadear.",
        "* 0 o 1. Sí o no. Todo empieza aquí.",
        "* Hay un error en alguna parte.\n  Siempre lo hay.",
        "* Huele a memoria recién formateada.",
        "* Depurar: el arte de buscar\n  lo que tú mismo escribiste."
    };

    [Header("Creditos (una linea por elemento; se escriben como consola)")]
    [TextArea(1, 2)]
    public string[] creditLines =
    {
        "> cat creditos.txt",
        "",
        "ReCode",
        "Aprende pseudocódigo jugando.",
        "",
        "Diseño y programación:",
        "Kenneth Santiago Ceballos Sierra",
        "Danna Valentina Arenas Jimenez",
        "",
        "Música: Toby Fox (Undertale)",
        "y Balatro (LouisF)",
        "Hecho con Unity.",
        "",
        "> presiona X para volver_"
    };

    [Header("Colores")]
    public Color backgroundColor = new Color(0.02f, 0.03f, 0.08f);
    public Color codeColor = new Color(0.25f, 1f, 0.45f);
    public Color normalColor = Color.white;
    public Color selectedColor = new Color(1f, 0.92f, 0.2f);
    public Color heartColor = new Color(0.9f, 0.1f, 0.15f);
    public Color rainColor = new Color(0.2f, 1f, 0.4f, 0.22f);
    [Range(0f, 0.6f)] public float scanlineAlpha = 0.22f;

    [Header("Tiempos")]
    public float bootCharDelay = 0.02f;
    public float boxAnimDuration = 0.12f;
    public float fadeOutDuration = 0.7f;

    [Header("Guia (opcional)")]
    [Tooltip("Si lo activas, el guia aparece deslizandose junto al titulo. Apagado por defecto.")]
    public bool showGuide = false;
    [Tooltip("Si lo dejas vacio, se copia el sprite del personaje del DialogueController.")]
    public Sprite guideSprite;
    public Vector2 guidePosition = new Vector2(-470f, 10f);
    [Tooltip("(0,0) = usar el tamano del personaje del DialogueController.")]
    public Vector2 guideSize = Vector2.zero;

    [Header("Musica (opcional)")]
    public AudioClip music;
    [Range(0f, 1f)] public float musicVolume = 0.5f;

    // ---------------------------------------------------------------- estado

    private enum State { Intro, TitleWait, Menu, Credits, Busy, Closed }
    private State state = State.Closed;

    public bool IsMenuActive => state != State.Closed;

    private class MenuItem
    {
        public string label, hint;
        public System.Action action;
        public TextMeshProUGUI text;
        public RectTransform rt;
    }

    private const string SaveKeyPrefix = "ReCode_Completed_"; // igual que LevelManager
    private const string GlitchChars = "01#$%&@?/|";
    private const float BoxWidth = 880f, BoxHeight = 290f;
    private const float EntryLeft = -380f, EntryWidth = 320f, EntrySpacing = 50f;

    private GameObject root;
    private CanvasGroup group;
    private RectTransform rootRt;
    private RawImage scanlines;
    private float scanScroll;

    private TextMeshProUGUI bootText, titleText, taglineText, promptText, hintText, creditsText;
    private bool closing;
    private RectTransform titleRt, guideRt, boxRt, heartRt;
    private Vector2 titleBasePos = new Vector2(0f, 150f);
    private Vector2 heartTarget;

    private readonly List<MenuItem> items = new List<MenuItem>();
    private int selected;
    private bool inConfirm;
    private bool skip;

    private Coroutine hintRoutine, glitchRoutine, musicRoutine;
    private AudioSource musicSource;
    private MenuNavigator[] navigators;
    private BinaryRain rain;

    // ---------------------------------------------------------------- ciclo de vida

    void Awake()
    {
        if (levelManager == null) levelManager = FindFirstObjectByType<LevelManager>();
        if (pauseMenu == null) pauseMenu = FindFirstObjectByType<PauseMenuUI>();
        navigators = FindObjectsByType<MenuNavigator>(FindObjectsSortMode.None);

        BuildUI();

        if (!showOnStart)
        {
            root.SetActive(false);
            state = State.Closed;
            enabled = false;
            return;
        }

        state = State.Intro;
        Lock(true);

        if (music != null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.clip = music;
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = 0f;
        }
    }

    void Start()
    {
        if (state == State.Intro) StartCoroutine(IntroRoutine(false));
    }

    void Update()
    {
        if (state == State.Closed) return;

        // Por si LevelManager desbloquea la cola mientras el menu sigue abierto.
        CommandQueueUI q = levelManager != null ? levelManager.queue : null;
        if (q != null) q.Locked = true;

        // Scanlines que bajan lentamente (efecto CRT)
        scanScroll = Mathf.Repeat(scanScroll + Time.deltaTime * 0.08f, 1f);
        scanlines.uvRect = new Rect(0f, scanScroll, 1f, rootRt.rect.height / 4f);

        promptText.alpha = state == State.TitleWait ? (Mathf.Sin(Time.time * 5f) > 0f ? 1f : 0.25f) : 0f;

        bool confirm = Input.GetKeyDown(confirmKey) || Input.GetKeyDown(confirmKeyAlt);
        bool back = Input.GetKeyDown(backKey) || Input.GetKeyDown(KeyCode.Escape);

        switch (state)
        {
            case State.Intro:
                if (confirm) skip = true;
                break;

            case State.TitleWait:
                if (confirm) StartCoroutine(OpenMenuRoutine());
                break;

            case State.Credits:
                if (confirm || back) StartCoroutine(ReturnFromCredits());
                break;

            case State.Menu:
                if (OptionsMenuUI.IsOpen) break; // las opciones estan abiertas encima
                if (Input.GetKeyDown(KeyCode.DownArrow)) MoveSelection(1);
                else if (Input.GetKeyDown(KeyCode.UpArrow)) MoveSelection(-1);
                else if (confirm) { SoundFX.Confirm(); items[selected].action?.Invoke(); }
                else if (back && inConfirm) StartCoroutine(SwapItems(BuildMainItems, RandomFlavor(), false));
                break;
        }

        if (boxRt.gameObject.activeSelf)
            heartRt.anchoredPosition = Vector2.Lerp(heartRt.anchoredPosition, heartTarget, Time.deltaTime * 20f);

        // Si no hay un fundido en curso, la musica sigue en vivo el volumen elegido en Opciones
        if (musicSource != null && musicSource.isPlaying && musicRoutine == null)
            musicSource.volume = musicVolume * GameSettings.Music;
    }

    // ---------------------------------------------------------------- secuencia de intro

    private IEnumerator IntroRoutine(bool quick)
    {
        skip = false;

        string errorTag = "<color=#FF4040>1 ERROR ENCONTRADO</color>";
        string boot =
            "> ReCode.exe --iniciar\n" +
            "> cargando memoria ........ OK\n" +
            "> buscando guia ........ OK\n" +
            "> buscando errores ........ " + errorTag;

        if (!quick)
        {
            yield return W(0.4f);
            yield return TypeText(bootText, boot, bootCharDelay, 3, false);
            yield return W(0.6f);
        }
        bootText.gameObject.SetActive(false);

        titleText.gameObject.SetActive(true);
        yield return DecryptTitle();
        SoundFX.Save();
        StartMusic();

        yield return W(0.2f);
        yield return TypeText(taglineText, tagline, 0.03f, 2, true);
        yield return W(0.2f);
        if (guideRt != null) yield return SlideGuide();

        skip = false;
        glitchRoutine = StartCoroutine(GlitchLoop());

        if (requirePressToStart) state = State.TitleWait;
        else yield return OpenMenuRoutine();
    }

    // Espera que se corta si el jugador salta la intro.
    private IEnumerator W(float seconds)
    {
        float e = 0f;
        while (e < seconds && !skip) { e += Time.deltaTime; yield return null; }
    }

    // Las letras del titulo salen como 0/1 y se van "descifrando" de izquierda a derecha.
    private IEnumerator DecryptTitle()
    {
        int n = gameTitle.Length;
        int locked = 0;
        float sinceLock = 0f;
        const float step = 0.05f, lockEvery = 0.14f;

        while (locked < n && !skip)
        {
            titleText.text = BuildTitle(locked, 0f);
            yield return new WaitForSeconds(step);
            sinceLock += step;
            if (sinceLock >= lockEvery) { sinceLock = 0f; locked++; SoundFX.Blip(); }
        }
        titleText.text = BuildTitle(n, 0f);
    }

    // Titulo con colores. Las letras aun no descifradas son bits verdes; 'glitch' corrompe letras ya listas.
    private string BuildTitle(int lockedCount, float glitch)
    {
        string white = ColorUtility.ToHtmlStringRGB(normalColor);
        string green = ColorUtility.ToHtmlStringRGB(codeColor);
        var sb = new System.Text.StringBuilder();

        for (int i = 0; i < gameTitle.Length; i++)
        {
            char c;
            string hex;
            if (i >= lockedCount)
            {
                c = Random.value < 0.5f ? '0' : '1';
                hex = green;
            }
            else
            {
                c = gameTitle[i];
                hex = i < whiteLetters ? white : green;
                if (glitch > 0f && Random.value < glitch)
                {
                    c = GlitchChars[Random.Range(0, GlitchChars.Length)];
                    if (Random.value < 0.3f) hex = "FF4040";
                }
            }
            sb.Append("<color=#").Append(hex).Append('>').Append(c).Append("</color>");
        }
        return sb.ToString();
    }

    // Cada 2-4 segundos el titulo se "corrompe" un instante.
    private IEnumerator GlitchLoop()
    {
        while (state != State.Closed)
        {
            yield return new WaitForSeconds(Random.Range(2f, 4.5f));
            if (state == State.Closed) yield break;

            float d = Random.Range(0.12f, 0.25f), e = 0f;
            while (e < d)
            {
                e += Time.deltaTime;
                titleText.text = BuildTitle(gameTitle.Length, 0.4f);
                titleRt.anchoredPosition = titleBasePos + new Vector2(Random.Range(-8f, 8f), Random.Range(-3f, 3f));
                yield return null;
            }
            titleText.text = BuildTitle(gameTitle.Length, 0f);
            titleRt.anchoredPosition = titleBasePos;
        }
    }

    private IEnumerator SlideGuide()
    {
        float fromX = -900f, toX = guidePosition.x, d = 0.6f, t = 0f;
        guideRt.gameObject.SetActive(true);
        while (t < d && !skip)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / d));
            guideRt.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, k), guidePosition.y);
            yield return null;
        }
        guideRt.anchoredPosition = guidePosition;
    }

    // ---------------------------------------------------------------- menu

    private IEnumerator OpenMenuRoutine()
    {
        state = State.Busy;
        SoundFX.Confirm();
        inConfirm = false;

        SetItems(BuildMainItems());
        selected = 0;
        boxRt.gameObject.SetActive(true);
        SetBoxX(0f);
        RefreshSelection(true, RandomFlavor());
        yield return ScaleBox(1f);
        state = State.Menu;
    }

    private List<MenuItem> BuildMainItems()
    {
        var list = new List<MenuItem>();

        if (HasProgress())
        {
            int c, l; bool allDone;
            GetContinueTarget(out c, out l, out allDone);
            int cc = c, ll = l;

            string hint;
            if (allDone) hint = "* Lo completaste todo.\n  Puedes volver a empezar.";
            else
            {
                ChapterData ch = levelManager.chapters[cc];
                hint = "* " + ch.name + "\n  Nivel " + (ll + 1) + ": " + ch.levels[ll].name;
            }

            list.Add(new MenuItem { label = "Continuar", hint = hint, action = () => StartCoroutine(StartRoutine(cc, ll)) });
            list.Add(new MenuItem
            {
                label = "Nuevo juego",
                hint = "* Empezar de cero.",
                action = () => StartCoroutine(SwapItems(BuildConfirmItems, "* ¿Seguro?\n  Se borrará tu progreso.", true))
            });
        }
        else
        {
            list.Add(new MenuItem { label = "Jugar", hint = "* Empezar la misión.", action = () => StartCoroutine(StartRoutine(0, 0)) });
        }

        list.Add(new MenuItem { label = "Opciones", hint = "* Música, efectos y velocidad.", action = () => OptionsMenuUI.Show() });
        list.Add(new MenuItem { label = "Créditos", hint = "* Quién está detrás del código.", action = () => StartCoroutine(ShowCredits()) });
        list.Add(new MenuItem { label = "Salir", hint = "* Apagar el programa.", action = QuitGame });
        return list;
    }

    private List<MenuItem> BuildConfirmItems()
    {
        return new List<MenuItem>
        {
            new MenuItem
            {
                label = "Cancelar", hint = "* Tu progreso sigue a salvo.",
                action = () => StartCoroutine(SwapItems(BuildMainItems, RandomFlavor(), false))
            },
            new MenuItem
            {
                label = "Borrar todo", hint = "* Esto borrará todo.\n  No hay vuelta atrás.",
                action = () => { ResetProgress(); StartCoroutine(StartRoutine(0, 0)); }
            }
        };
    }

    // El cuadro se cierra, cambia el contenido y se vuelve a abrir (como BattleBoxController).
    private IEnumerator SwapItems(System.Func<List<MenuItem>> builder, string initialHint, bool confirmMode)
    {
        state = State.Busy;
        yield return ScaleBox(0f);
        inConfirm = confirmMode;
        SetItems(builder());
        selected = 0;
        RefreshSelection(true, initialHint);
        yield return ScaleBox(1f);
        state = State.Menu;
    }

    private void MoveSelection(int dir)
    {
        selected = (selected + dir + items.Count) % items.Count;
        SoundFX.Move();
        RefreshSelection(false, null);
    }

    private void RefreshSelection(bool snapHeart, string overrideText)
    {
        for (int i = 0; i < items.Count; i++)
            items[i].text.color = i == selected ? selectedColor : normalColor;

        MenuItem sel = items[selected];
        heartTarget = new Vector2(EntryLeft - 26f, sel.rt.anchoredPosition.y);
        if (snapHeart) heartRt.anchoredPosition = heartTarget;

        if (hintRoutine != null) StopCoroutine(hintRoutine);
        hintRoutine = StartCoroutine(TypeText(hintText, overrideText ?? sel.hint, 0.02f, 2, true));
    }

    private void SetItems(List<MenuItem> newItems)
    {
        foreach (MenuItem old in items)
            if (old.rt != null) Destroy(old.rt.gameObject);
        items.Clear();

        float startY = (newItems.Count - 1) * 0.5f * EntrySpacing;
        for (int i = 0; i < newItems.Count; i++)
        {
            MenuItem it = newItems[i];
            it.text = CreateText(boxRt, "Entry_" + it.label, 26f, new Vector2(EntryLeft + EntryWidth / 2f, startY - i * EntrySpacing),
                                 new Vector2(EntryWidth, 40f), TextAlignmentOptions.Left, normalColor);
            it.text.text = it.label;
            it.text.textWrappingMode = TextWrappingModes.NoWrap;
            it.rt = it.text.rectTransform;
            items.Add(it);
        }
        heartRt.SetAsLastSibling();
    }

    // ---------------------------------------------------------------- arrancar juego

    private IEnumerator StartRoutine(int chapter, int level)
    {
        if (state == State.Busy || state == State.Closed) yield break;
        state = State.Busy;

        if (glitchRoutine != null) StopCoroutine(glitchRoutine);
        SoundFX.Win();

        // El titulo se corrompe del todo
        float e = 0f;
        while (e < 0.5f)
        {
            e += Time.deltaTime;
            titleText.text = BuildTitle(gameTitle.Length, 0.7f);
            titleRt.anchoredPosition = titleBasePos + new Vector2(Random.Range(-14f, 14f), Random.Range(-6f, 6f));
            yield return null;
        }
        yield return ScaleBox(0f);

        // Arranca el nivel (con su pantalla intermedia) mientras el menu se desvanece
        StartLevel(chapter, level);
        Lock(false);
        Time.timeScale = GameSettings.Speed; // velocidad elegida en Opciones
        state = State.Closed;
        closing = true;
        StopMusic();

        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            group.alpha = 1f - Mathf.Clamp01(t / fadeOutDuration);
            yield return null;
        }
        root.SetActive(false);
        closing = false;
    }

    // LevelManager ya arranco el nivel 0 por su cuenta detras del menu: se corta y se reinicia
    // desde donde el jugador eligio, para que la pantalla intermedia se vea al abrirse el telon.
    private void StartLevel(int chapter, int level)
    {
        if (levelManager == null) return;

        levelManager.StopAllCoroutines();
        if (levelManager.canvas != null)
        {
            foreach (Transform child in levelManager.canvas.transform)
                if (child.name == "LevelLabel") Destroy(child.gameObject);
        }
        levelManager.GoToLevel(chapter, level);
    }

    // ---------------------------------------------------------------- creditos

    private IEnumerator ShowCredits()
    {
        state = State.Busy;
        yield return ScaleBox(0f);
        boxRt.gameObject.SetActive(false);

        if (glitchRoutine != null) StopCoroutine(glitchRoutine);
        titleText.gameObject.SetActive(false);
        taglineText.gameObject.SetActive(false);
        if (guideRt != null) guideRt.gameObject.SetActive(false);

        creditsText.gameObject.SetActive(true);
        yield return TypeText(creditsText, string.Join("\n", creditLines), 0.015f, 3, false);
        state = State.Credits;
    }

    private IEnumerator ReturnFromCredits()
    {
        state = State.Busy;
        SoundFX.Confirm();
        creditsText.gameObject.SetActive(false);

        titleText.gameObject.SetActive(true);
        titleText.text = BuildTitle(gameTitle.Length, 0f);
        titleRt.anchoredPosition = titleBasePos;
        taglineText.gameObject.SetActive(true);
        if (guideRt != null) guideRt.gameObject.SetActive(true);
        glitchRoutine = StartCoroutine(GlitchLoop());

        boxRt.gameObject.SetActive(true);
        SetBoxX(0f);
        inConfirm = false;
        SetItems(BuildMainItems());
        selected = 0;
        for (int i = 0; i < items.Count; i++)
            if (items[i].label == "Créditos") selected = i;
        RefreshSelection(true, RandomFlavor());
        yield return ScaleBox(1f);
        state = State.Menu;
    }

    // ---------------------------------------------------------------- volver al menu (lo llama el menu de pausa)

    /// <summary>Vuelve a mostrar el menu de inicio (version corta, sin la consola de arranque).</summary>
    public void Open()
    {
        if (state != State.Closed || closing) return;

        Time.timeScale = 1f;
        enabled = true;
        root.SetActive(true);
        group.alpha = 1f;

        // Deja todo como al principio
        if (glitchRoutine != null) StopCoroutine(glitchRoutine);
        if (hintRoutine != null) StopCoroutine(hintRoutine);
        bootText.gameObject.SetActive(false);
        creditsText.gameObject.SetActive(false);
        titleText.text = "";
        titleText.gameObject.SetActive(false);
        titleRt.anchoredPosition = titleBasePos;
        taglineText.text = "";
        taglineText.gameObject.SetActive(true);
        hintText.text = "";
        boxRt.gameObject.SetActive(false);
        SetBoxX(0f);
        inConfirm = false;
        if (guideRt != null) { guideRt.gameObject.SetActive(false); guideRt.anchoredPosition = new Vector2(-900f, guidePosition.y); }

        Lock(true);
        state = State.Intro;
        StartCoroutine(IntroRoutine(true));
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------------------------------------------------------- progreso

    private bool HasProgress()
    {
        if (levelManager == null || levelManager.chapters == null) return false;
        for (int c = 0; c < levelManager.chapters.Count; c++)
        {
            var lv = levelManager.chapters[c].levels;
            if (lv == null) continue;
            for (int l = 0; l < lv.Count; l++)
                if (levelManager.IsLevelCompleted(c, l)) return true;
        }
        return false;
    }

    private void GetContinueTarget(out int chapter, out int level, out bool allDone)
    {
        chapter = 0; level = 0; allDone = false;
        if (levelManager == null || levelManager.chapters == null) return;

        for (int c = 0; c < levelManager.chapters.Count; c++)
        {
            var lv = levelManager.chapters[c].levels;
            if (lv == null) continue;
            for (int l = 0; l < lv.Count; l++)
            {
                if (!levelManager.IsLevelCompleted(c, l)) { chapter = c; level = l; return; }
            }
        }
        allDone = true;
    }

    private void ResetProgress()
    {
        FragmentInventory.ResetAll(); // "Nuevo juego" tambien borra los fragmentos recogidos
        if (levelManager == null || levelManager.chapters == null) return;
        for (int c = 0; c < levelManager.chapters.Count; c++)
        {
            var lv = levelManager.chapters[c].levels;
            if (lv == null) continue;
            for (int l = 0; l < lv.Count; l++) PlayerPrefs.DeleteKey(SaveKeyPrefix + c + "_" + l);
        }
        PlayerPrefs.Save();
    }

    private string RandomFlavor()
    {
        if (HasProgress() && Random.value < 0.3f) return "* Tu progreso sigue guardado.\n  Casi todo.";
        if (flavorTexts == null || flavorTexts.Length == 0) return "";
        return flavorTexts[Random.Range(0, flavorTexts.Length)];
    }

    // ---------------------------------------------------------------- bloqueo de otros sistemas

    private void Lock(bool locked)
    {
        if (pauseMenu != null) pauseMenu.enabled = !locked;
        if (navigators != null)
            foreach (MenuNavigator nav in navigators)
                if (nav != null) nav.externalLock = locked;
    }

    // ---------------------------------------------------------------- musica

    private void StartMusic()
    {
        if (musicSource == null) return;
        if (musicRoutine != null) StopCoroutine(musicRoutine);
        if (!musicSource.isPlaying) { musicSource.volume = 0f; musicSource.Play(); }
        musicRoutine = StartCoroutine(FadeMusic(musicVolume * GameSettings.Music, 1f, false));
    }

    private void StopMusic()
    {
        if (musicSource == null || !musicSource.isPlaying) return;
        if (musicRoutine != null) StopCoroutine(musicRoutine);
        musicRoutine = StartCoroutine(FadeMusic(0f, fadeOutDuration, true));
    }

    private IEnumerator FadeMusic(float target, float duration, bool stopAtEnd)
    {
        float start = musicSource.volume, t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        musicSource.volume = target;
        if (stopAtEnd) musicSource.Stop();
        musicRoutine = null;
    }

    // ---------------------------------------------------------------- utilidades de animacion

    // Maquina de escribir con maxVisibleCharacters (funciona con rich text).
    private IEnumerator TypeText(TMP_Text tmp, string text, float delay, int blipEvery, bool punctuationPause)
    {
        tmp.text = text;
        tmp.maxVisibleCharacters = 0;
        tmp.ForceMeshUpdate();
        int total = tmp.textInfo.characterCount;

        for (int i = 1; i <= total; i++)
        {
            if (skip) break;
            tmp.maxVisibleCharacters = i;
            char c = tmp.textInfo.characterInfo[i - 1].character;
            if (blipEvery > 0 && i % blipEvery == 0 && !char.IsWhiteSpace(c)) SoundFX.Blip();

            float wait = delay;
            if (c == '\n') wait += 0.15f;
            else if (punctuationPause && (c == '.' || c == ',' || c == '!' || c == '?')) wait += delay * 4f;

            float t = 0f;
            while (t < wait && !skip) { t += Time.deltaTime; yield return null; }
        }
        tmp.maxVisibleCharacters = 99999;
    }

    private IEnumerator ScaleBox(float target)
    {
        float start = boxRt.localScale.x, t = 0f;
        while (t < boxAnimDuration)
        {
            t += Time.deltaTime;
            SetBoxX(Mathf.Lerp(start, target, Mathf.Clamp01(t / boxAnimDuration)));
            yield return null;
        }
        SetBoxX(target);
    }

    private void SetBoxX(float x)
    {
        Vector3 s = boxRt.localScale;
        s.x = x;
        boxRt.localScale = s;
    }

    // ---------------------------------------------------------------- construccion de la UI

    private void BuildUI()
    {
        GameObject canvasGo = new GameObject("StartMenuCanvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // por encima de todo, incluso del menu de pausa

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        root = new GameObject("StartMenuRoot", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvasGo.transform, false);
        rootRt = (RectTransform)root.transform;
        Stretch(rootRt);
        group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        // Fondo + lluvia de 0 y 1 (mas visible que la del juego)
        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(root.transform, false);
        Stretch((RectTransform)bg.transform);
        Image bgImg = bg.GetComponent<Image>();
        bgImg.color = backgroundColor;
        bgImg.raycastTarget = false;

        rain = BinaryRain.Create(root.transform, 1);
        rain.color = rainColor;

        // Guia (si hay sprite)
        BuildGuide();

        // Texto de consola (arriba a la izquierda)
        bootText = CreateText(rootRt, "BootText", 22f, new Vector2(40f, -40f), new Vector2(900f, 260f), TextAlignmentOptions.TopLeft, codeColor);
        bootText.rectTransform.anchorMin = bootText.rectTransform.anchorMax = new Vector2(0f, 1f);
        bootText.rectTransform.pivot = new Vector2(0f, 1f);
        bootText.text = "";

        // Texto de creditos (pantalla completa, estilo consola)
        creditsText = CreateText(rootRt, "CreditsText", 28f, new Vector2(60f, -60f), new Vector2(1100f, 600f), TextAlignmentOptions.TopLeft, codeColor);
        creditsText.rectTransform.anchorMin = creditsText.rectTransform.anchorMax = new Vector2(0f, 1f);
        creditsText.rectTransform.pivot = new Vector2(0f, 1f);
        creditsText.text = "";
        creditsText.gameObject.SetActive(false);

        // Titulo, frase y "PRESIONA Z"
        titleText = CreateText(rootRt, "Title", 120f, titleBasePos, new Vector2(1200f, 170f), TextAlignmentOptions.Center, Color.white);
        titleText.characterSpacing = 8f;
        titleText.richText = true;
        titleRt = titleText.rectTransform;
        titleText.text = "";
        titleText.gameObject.SetActive(false);

        taglineText = CreateText(rootRt, "Tagline", 26f, new Vector2(0f, 45f), new Vector2(1100f, 50f), TextAlignmentOptions.Center, codeColor);
        taglineText.text = "";

        promptText = CreateText(rootRt, "Prompt", 26f, new Vector2(0f, -190f), new Vector2(600f, 50f), TextAlignmentOptions.Center, normalColor);
        promptText.text = "- PRESIONA Z -";
        promptText.alpha = 0f;

        BuildBox();

        // Scanlines al final: por encima de todo
        GameObject sl = new GameObject("Scanlines", typeof(RectTransform), typeof(RawImage));
        sl.transform.SetParent(root.transform, false);
        Stretch((RectTransform)sl.transform);
        scanlines = sl.GetComponent<RawImage>();
        scanlines.raycastTarget = false;
        Texture2D stex = new Texture2D(1, 4, TextureFormat.RGBA32, false);
        stex.filterMode = FilterMode.Point;
        stex.wrapMode = TextureWrapMode.Repeat;
        for (int y = 0; y < 4; y++) stex.SetPixel(0, y, new Color(0f, 0f, 0f, y >= 2 ? scanlineAlpha : 0f));
        stex.Apply();
        scanlines.texture = stex;
        sl.transform.SetAsLastSibling();
    }

    private void BuildGuide()
    {
        if (!showGuide) return;

        Sprite sprite = guideSprite;
        Vector2 size = guideSize;

        DialogueController dlg = FindFirstObjectByType<DialogueController>();
        if (dlg != null && dlg.character != null)
        {
            Image src = dlg.character.GetComponent<Image>();
            if (sprite == null && src != null) sprite = src.sprite;
            if (size == Vector2.zero) size = dlg.character.rect.size;
        }
        if (sprite == null) return;
        if (size == Vector2.zero) size = new Vector2(200f, 200f);

        GameObject go = new GameObject("Guide", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root.transform, false);
        guideRt = (RectTransform)go.transform;
        guideRt.anchorMin = guideRt.anchorMax = new Vector2(0.5f, 0.5f);
        guideRt.sizeDelta = size;
        guideRt.anchoredPosition = new Vector2(-900f, guidePosition.y);

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        go.AddComponent<GuideIdle>(); // respira y se balancea, igual que en el dialogo
        go.SetActive(false);
    }

    private void BuildBox()
    {
        GameObject box = new GameObject("MenuBox", typeof(RectTransform));
        box.transform.SetParent(root.transform, false);
        boxRt = (RectTransform)box.transform;
        boxRt.anchorMin = boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(BoxWidth, BoxHeight);
        boxRt.anchoredPosition = new Vector2(0f, -190f);

        Image border = NewImage(boxRt, "Border", Color.white);
        Stretch(border.rectTransform);
        Image fill = NewImage(boxRt, "Fill", Color.black);
        Stretch(fill.rectTransform);
        fill.rectTransform.offsetMin = new Vector2(6f, 6f);
        fill.rectTransform.offsetMax = new Vector2(-6f, -6f);

        // Divisor vertical entre opciones y texto
        Image divider = NewImage(boxRt, "Divider", new Color(1f, 1f, 1f, 0.25f));
        divider.rectTransform.anchorMin = divider.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        divider.rectTransform.sizeDelta = new Vector2(3f, BoxHeight - 50f);
        divider.rectTransform.anchoredPosition = new Vector2(-40f, 0f);

        // Texto de la derecha (frase al azar / descripcion de la opcion)
        hintText = CreateText(boxRt, "Hint", 22f, new Vector2(200f, 0f), new Vector2(400f, 200f), TextAlignmentOptions.TopLeft, normalColor);
        hintText.text = "";

        // Corazon en pixel art generado por codigo
        GameObject heart = new GameObject("Heart", typeof(RectTransform), typeof(RawImage));
        heart.transform.SetParent(boxRt, false);
        heartRt = (RectTransform)heart.transform;
        heartRt.anchorMin = heartRt.anchorMax = new Vector2(0.5f, 0.5f);
        heartRt.sizeDelta = new Vector2(27f, 24f);
        RawImage heartImg = heart.GetComponent<RawImage>();
        heartImg.texture = BuildHeartTexture();
        heartImg.raycastTarget = false;

        box.SetActive(false);
    }

    private Texture2D BuildHeartTexture()
    {
        string[] rows =
        {
            ".XX...XX.",
            "XXXX.XXXX",
            "XXXXXXXXX",
            "XXXXXXXXX",
            ".XXXXXXX.",
            "..XXXXX..",
            "...XXX...",
            "....X...."
        };
        Texture2D tex = new Texture2D(9, 8, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 9; x++)
                tex.SetPixel(x, 7 - y, rows[y][x] == 'X' ? heartColor : Color.clear);
        tex.Apply();
        return tex;
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
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
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