using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Secuencia de dialogo estilo Undertale: entra un personaje deslizandose desde la
/// izquierda, el cuadro se abre y el texto se escribe letra por letra. E, Z, Enter o Espacio
/// completan la frase si aun se esta escribiendo, o pasa a la siguiente si ya termino.
/// Al final el cuadro se cierra, el personaje se va y vuelven las opciones.
/// </summary>
public class DialogueController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El BattleBoxManager (se busca solo si lo dejas vacio).")]
    public BattleBoxController battleBox;
    [Tooltip("El objeto UI (Image) del personaje que habla. Debe ser hijo del Canvas.")]
    public RectTransform character;

    [Header("Guion de la victoria (cada elemento = una frase; Z para avanzar)")]
    [TextArea(2, 4)]
    public string[] winLines =
    {
        "Saludos!",
        "Me presento, seré tu guía en esta misión.",
        "¿Que cómo me llamo?",
        "Es curioso que lo digas...",
        "......"
    };

    [Header("Maquina de escribir")]
    [Tooltip("Segundos entre cada letra. Mas bajo = mas rapido.")]
    public float charDelay = 0.04f;
    [Tooltip("Pausa extra despues de . , ! ? (da mas dramatismo a los puntos suspensivos).")]
    public float punctuationDelay = 0.12f;
    [Tooltip("Sonido que suena con cada letra (opcional).")]
    public AudioClip blipSound;
    [Range(0f, 1f)] public float blipVolume = 0.5f;

    [Header("Personaje")]
    [Tooltip("Donde se queda el personaje (coordenadas del Canvas; 0,0 es el centro de la pantalla).")]
    public Vector2 characterPosition = new Vector2(0f, 120f);
    [Tooltip("Sube (+) o baja (-) al guia respecto a 'Character Position'. Si sale muy arriba, usa un numero negativo.")]
    public float guideYOffset = -45f;
    public float slideDuration = 0.6f;
    [Tooltip("Si esta marcado, el personaje se va deslizandose al terminar el dialogo.")]
    public bool characterExits = true;

    [Header("Musica de fondo")]
    [Tooltip("Cancion que suena en bucle cuando aparece el personaje.")]
    public AudioClip music;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Tooltip("Segundos que tarda el volumen en subir al empezar (0 = de golpe).")]
    public float musicFadeIn = 1f;
    [Tooltip("Si esta marcado, la musica se apaga suavemente cuando el personaje se va (es 'su tema'). Si no, sigue sonando en bucle.")]
    public bool stopMusicWhenGuideLeaves = true;
    [Tooltip("Segundos que tarda la musica en apagarse cuando el personaje se va.")]
    public float musicFadeOut = 0.8f;

    [Header("Estilo Undertale")]
    [Tooltip("Agrega '* ' al inicio de cada frase (si no lo tiene), como en Undertale.")]
    public bool asteriskPrefix = true;

    [Header("Interaccion con el guia (tecla E)")]
    [Tooltip("Si esta marcado, cuando el guia llega flota 'Presiona E para hablar' y el dialogo empieza al pulsar E.")]
    public bool requireTalkPrompt = true;
    [Tooltip("Si esta marcado, el dialogo empieza solo cuando el guia termina de entrar (sin pedir E). E sigue sirviendo para pasar de una frase a la siguiente.")]
    public bool autoStartTalk = true;
    public string talkPrompt = "Presiona E para hablar";
    [Tooltip("El Canvas donde flota el indicador. Se busca solo.")]
    public Canvas canvas;

    /// <summary>True mientras hay un dialogo o un panel de terminal abierto (evita que se abran dos a la vez).</summary>
    public bool IsPlaying { get; private set; }

    private float GuideY { get { return characterPosition.y + guideYOffset; } }
    private bool boxClosing;

    private GuideIdle guideIdle;
    private AudioSource audioSource;
    private AudioSource musicSource;
    private Coroutine musicRoutine;

    void Awake()
    {
        if (battleBox == null) battleBox = FindFirstObjectByType<BattleBoxController>();
        if (canvas == null) canvas = character != null ? character.GetComponentInParent<Canvas>() : FindFirstObjectByType<Canvas>();

        if (music != null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.clip = music;
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = 0f;
        }

        if (blipSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        if (character != null)
        {
            guideIdle = character.GetComponent<GuideIdle>();
            if (guideIdle == null) guideIdle = character.gameObject.AddComponent<GuideIdle>();
            character.gameObject.SetActive(false);
        }
    }

    /// <summary>Corre la secuencia completa de dialogo de victoria (usalo con yield return).</summary>
    public IEnumerator PlayWinDialogue()
    {
        yield return PlayDialogue(winLines);
    }

    public IEnumerator PlayDialogue(string[] lines)
    {
        if (IsPlaying || battleBox == null || character == null || lines == null) yield break;
        IsPlaying = true;

        try
        {
            // 1. El cuadro de opciones se cierra mientras el personaje entra desde la izquierda
            float offscreenX = GetOffscreenX();
            character.anchoredPosition = new Vector2(offscreenX, GuideY);
            character.gameObject.SetActive(true);
            StartMusic(); // aparece el personaje -> empieza la musica
            if (musicSource != null) MusicManager.Duck(true); // la musica de los niveles cede ante el tema del guia

            boxClosing = true;
            StartCoroutine(CloseBoxRoutine());
            yield return Slide(offscreenX, characterPosition.x);
            while (boxClosing) yield return null;

            // 1b. El texto empieza solo (o, si lo prefieres, flota "Presiona E para hablar")
            if (requireTalkPrompt && !autoStartTalk) yield return WaitForTalk();
            else yield return new WaitForSeconds(0.3f);

            // 2. Modo mensaje y el cuadro se abre (si venia de la animacion de ejecucion, aqui reaparece)
            battleBox.ShowMessageContent("");
            RunCinematic cinematic = FindFirstObjectByType<RunCinematic>();
            if (cinematic != null) cinematic.ShowMenus();
            yield return battleBox.OpenBox();

            // 3. Cada frase: se escribe, y E / Z / Enter / Espacio para pasar a la siguiente
            foreach (string line in lines)
            {
                yield return TypeLine(line, true);
                yield return WaitForConfirm();
            }

            // 4. El cuadro se cierra y vuelve a las opciones; el personaje se va
            if (stopMusicWhenGuideLeaves)
            {
                StopMusic();
                MusicManager.Duck(false); // vuelve la musica de los niveles (fundido cruzado)
            }
            yield return battleBox.CloseBox();
            battleBox.ShowOptionsContent();
            if (characterExits)
            {
                yield return Slide(characterPosition.x, offscreenX);
            }
            character.gameObject.SetActive(false);
            yield return battleBox.OpenBox();
        }
        finally
        {
            PromptBubble.HideIfExists();
            IsPlaying = false;
        }
    }

    /// <summary>
    /// Panel de una terminal: sin personaje, solo el cuadro de abajo con el texto escribiendose
    /// letra por letra. Cada elemento de 'pages' es una pagina; E / Z / Enter / Espacio avanza.
    /// </summary>
    public IEnumerator PlayTerminal(string[] pages)
    {
        if (IsPlaying || battleBox == null || pages == null || pages.Length == 0) yield break;
        IsPlaying = true;

        try
        {
            yield return battleBox.CloseBox();
            battleBox.ShowMessageContent("");
            yield return battleBox.OpenBox();

            foreach (string page in pages)
            {
                yield return TypeLine(page, false); // las terminales no llevan el '* ' del guia
                yield return WaitForConfirm();
            }

            yield return battleBox.CloseBox();
            battleBox.ShowOptionsContent();
            yield return battleBox.OpenBox();
        }
        finally
        {
            IsPlaying = false;
        }
    }

    private IEnumerator CloseBoxRoutine()
    {
        yield return battleBox.CloseBox();
        boxClosing = false;
    }

    // El guia ya esta en pantalla: flota el indicador y se espera a que el jugador pulse E.
    private IEnumerator WaitForTalk()
    {
        PromptBubble bubble = PromptBubble.Get(canvas);
        if (bubble != null) bubble.ShowAtRect(character, talkPrompt);
        SoundFX.Blip();

        while (!ConfirmPressed()) yield return null;

        if (bubble != null) bubble.Hide();
        SoundFX.Confirm();
        yield return null; // consume la pulsacion
    }

    // ---------- Musica de fondo ----------

    private void StartMusic()
    {
        if (musicSource == null) return;
        if (musicRoutine != null) StopCoroutine(musicRoutine);
        if (!musicSource.isPlaying)
        {
            musicSource.volume = 0f;
            musicSource.Play();
        }
        musicRoutine = StartCoroutine(FadeMusic(musicVolume * GameSettings.Music, musicFadeIn));
    }

    private void StopMusic()
    {
        if (musicSource == null || !musicSource.isPlaying) return;
        if (musicRoutine != null) StopCoroutine(musicRoutine);
        musicRoutine = StartCoroutine(FadeOutAndStop());
    }

    private IEnumerator FadeOutAndStop()
    {
        yield return FadeMusic(0f, musicFadeOut);
        musicSource.Stop();
    }

    private IEnumerator FadeMusic(float target, float duration)
    {
        float start = musicSource.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        musicSource.volume = target;
    }

    private IEnumerator TypeLine(string line, bool guideStyle)
    {
        TMP_Text tmp = battleBox.messageText;
        tmp.text = guideStyle ? FormatLine(line) : line;
        tmp.maxVisibleCharacters = 0;
        tmp.ForceMeshUpdate();
        int total = tmp.textInfo.characterCount;

        for (int i = 1; i <= total; i++)
        {
            tmp.maxVisibleCharacters = i;
            char c = tmp.textInfo.characterInfo[i - 1].character;
            if (!char.IsWhiteSpace(c)) PlayBlip();

            float wait = charDelay + (IsPause(c) ? punctuationDelay : 0f);
            float timer = 0f;
            while (timer < wait)
            {
                if (ConfirmPressed())
                {
                    // Z mientras se escribe: mostrar la frase completa de golpe
                    tmp.maxVisibleCharacters = total;
                    yield return null; // consume esta pulsacion para que no salte la siguiente frase
                    yield break;
                }
                timer += Time.deltaTime;
                yield return null;
            }
        }
    }

    private IEnumerator WaitForConfirm()
    {
        while (!ConfirmPressed()) yield return null;
        yield return null; // consume la pulsacion
    }

    private IEnumerator Slide(float fromX, float toX)
    {
        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / slideDuration));
            character.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, k), GuideY);
            yield return null;
        }
        character.anchoredPosition = new Vector2(toX, GuideY);
    }

    // X (en coordenadas del Canvas) donde el personaje queda completamente fuera de pantalla por la izquierda
    private float GetOffscreenX()
    {
        RectTransform root = character.root as RectTransform;
        float halfScreen = root != null ? root.rect.width / 2f : 400f;
        return -(halfScreen + character.rect.width / 2f + 20f);
    }

    // Z / Enter (estilo Undertale) y tambien E / Espacio. En pausa (timeScale 0) no lee nada.
    private static bool ConfirmPressed()
    {
        if (Time.timeScale <= 0f) return false;
        return Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space);
    }

    private static bool IsPause(char c)
    {
        return c == '.' || c == ',' || c == '!' || c == '?' || c == '…';
    }

    private string FormatLine(string line)
    {
        if (!asteriskPrefix || string.IsNullOrEmpty(line)) return line;
        return line.TrimStart().StartsWith("*") ? line : "* " + line;
    }

    private void PlayBlip()
    {
        if (guideIdle != null) guideIdle.Pulse(); // el guia "mueve la boca" con cada letra
        if (audioSource == null || blipSound == null) { SoundFX.Blip(); return; }
        audioSource.pitch = Random.Range(0.95f, 1.05f);
        audioSource.PlayOneShot(blipSound, blipVolume * GameSettings.Sfx);
    }
}