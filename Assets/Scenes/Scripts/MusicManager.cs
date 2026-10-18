using UnityEngine;

/// <summary>
/// Musica de fondo de los NIVELES, con fundidos suaves entre canciones.
///
/// Reparto de musicas (cada una suena en su momento y se hacen fundido cruzado):
///  - Menu de inicio: la maneja StartMenuUI (campo "Music" de ese script).
///  - Niveles: este script (campo "Level Music").
///  - Guia: la maneja DialogueController (su "tema"). Mientras suena, la musica de
///    los niveles baja (o se apaga) y vuelve sola cuando el guia se va.
///
/// Ademas:
///  - PAUSA: al abrir el menu de pausa (Time.timeScale = 0) la musica baja de volumen y se
///    "amortigua" (como si sonara detras de una pared); al reanudar, vuelve suave.
///  - REBOBINADO: mientras dura la estatica, la musica baja, se amortigua y se pone grave
///    (como una cinta que se arrastra). Lo activa ScreenEffects.SetRewindStatic.
///
/// Uso: pon este script en el objeto "Level" y arrastra tu cancion a "Level Music".
/// Necesita que StartMenuUI tenga la propiedad IsMenuActive.
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Referencias (se buscan solas si las dejas vacias)")]
    public StartMenuUI startMenu;

    [Header("Musica de los niveles")]
    public AudioClip levelMusic;
    [Range(0f, 1f)] public float levelVolume = 0.4f;

    [Header("Fundidos (rapidos y suaves)")]
    [Tooltip("Segundos que tarda en subir al empezar.")]
    public float fadeInDuration = 0.8f;
    [Tooltip("Segundos que tarda en bajar al irse.")]
    public float fadeOutDuration = 0.5f;

    [Header("Mientras suena el tema del guia")]
    [Tooltip("Volumen de la musica de niveles mientras suena el tema del guia (0 = se apaga del todo, 1 = no baja).")]
    [Range(0f, 1f)] public float guideDuckFactor = 0f;

    [Header("Menu de pausa")]
    [Tooltip("Que fraccion del volumen queda en pausa (1 = no baja).")]
    [Range(0f, 1f)] public float pauseVolumeFactor = 0.35f;
    [Tooltip("Frecuencia de corte del 'amortiguado' en pausa (Hz). Mas bajo = mas apagado. 22000 = sin efecto.")]
    public float pauseLowPassHz = 900f;
    public float pauseFadeDuration = 0.25f;

    [Header("Rebobinado (estatica)")]
    [Tooltip("Que fraccion del volumen queda durante el rebobinado.")]
    [Range(0f, 1f)] public float rewindVolumeFactor = 0.35f;
    [Tooltip("Tono de la musica durante el rebobinado (1 = normal, menos = mas grave, como cinta lenta).")]
    [Range(0.3f, 1f)] public float rewindPitch = 0.65f;
    [Tooltip("Frecuencia de corte del 'amortiguado' durante el rebobinado (Hz). 22000 = sin efecto.")]
    public float rewindLowPassHz = 2000f;
    public float rewindFadeDuration = 0.25f;

    private AudioSource src;
    private AudioLowPassFilter lowPass;
    private float gain;      // 0..1, se acerca poco a poco a 'target'
    private bool ducked;
    private bool rewinding;
    private float pauseAmt;  // 0..1
    private float rewindAmt; // 0..1

    /// <summary>Baja/sube la musica de niveles (lo llama DialogueController cuando entra/sale el guia).</summary>
    public static void Duck(bool on)
    {
        if (Instance != null) Instance.ducked = on;
    }

    /// <summary>Activa/desactiva el efecto de rebobinado en la musica (lo llama ScreenEffects).</summary>
    public static void SetRewind(bool on)
    {
        if (Instance != null) Instance.rewinding = on;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;

        if (levelMusic != null)
        {
            // Objeto hijo propio: asi el filtro de "amortiguado" solo afecta a esta musica
            // (y no a la del guia ni a otros AudioSource que esten en el mismo objeto).
            GameObject child = new GameObject("LevelMusic");
            child.transform.SetParent(transform, false);

            src = child.AddComponent<AudioSource>();
            src.clip = levelMusic;
            src.loop = true;
            src.playOnAwake = false;
            src.volume = 0f;

            lowPass = child.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f;
            lowPass.enabled = false;
        }
    }

    void Start()
    {
        if (startMenu == null) startMenu = FindFirstObjectByType<StartMenuUI>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (src == null) return;

        // Tiempo sin escala: los fundidos siguen aunque el juego este en pausa
        float dt = Time.unscaledDeltaTime;

        bool menuActive = startMenu != null && startMenu.IsMenuActive;
        float target = menuActive ? 0f : (ducked ? guideDuckFactor : 1f);

        float rate = 1f / Mathf.Max(0.01f, target > gain ? fadeInDuration : fadeOutDuration);
        gain = Mathf.MoveTowards(gain, target, dt * rate);

        // Pausa = el juego esta congelado (PauseMenuUI pone Time.timeScale en 0)
        bool paused = Time.timeScale <= 0.0001f;
        pauseAmt = Mathf.MoveTowards(pauseAmt, paused ? 1f : 0f, dt / Mathf.Max(0.01f, pauseFadeDuration));
        rewindAmt = Mathf.MoveTowards(rewindAmt, rewinding ? 1f : 0f, dt / Mathf.Max(0.01f, rewindFadeDuration));

        if (target > 0f && !src.isPlaying) src.Play();

        float mod = Mathf.Lerp(1f, pauseVolumeFactor, pauseAmt) * Mathf.Lerp(1f, rewindVolumeFactor, rewindAmt);
        src.volume = gain * levelVolume * mod * GameSettings.Music;
        src.pitch = Mathf.Lerp(1f, rewindPitch, rewindAmt);

        // Amortiguado: se interpola en escala logaritmica (asi el barrido suena parejo)
        float cutoff = 22000f
            * Mathf.Pow(Mathf.Clamp(pauseLowPassHz, 300f, 22000f) / 22000f, pauseAmt)
            * Mathf.Pow(Mathf.Clamp(rewindLowPassHz, 300f, 22000f) / 22000f, rewindAmt);
        lowPass.cutoffFrequency = Mathf.Clamp(cutoff, 300f, 22000f);
        lowPass.enabled = cutoff < 21000f;

        // En el menu la cancion se detiene del todo (asi vuelve a empezar de cero al jugar)
        if (menuActive && gain <= 0.001f && src.isPlaying) src.Stop();
    }
}
