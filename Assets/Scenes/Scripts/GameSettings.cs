using UnityEngine;

/// <summary>
/// Ajustes del jugador (volumenes y velocidad del juego), guardados en PlayerPrefs para que
/// se recuerden al cerrar y abrir el juego. Es una clase estatica: no hay que ponerla en
/// ningun objeto de la escena, se usa desde cualquier script asi:
///
///   GameSettings.Music   (0..1)  volumen de la musica
///   GameSettings.Sfx     (0..1)  volumen de los efectos (pasos, clicks, dialogo, estatica...)
///   GameSettings.Master  (0..1)  volumen general (afecta a TODO el audio)
///   GameSettings.Speed           velocidad del juego (1, 1.5, 2...)
///
/// Para agregar o quitar velocidades, edita la lista SpeedSteps.
/// </summary>
public static class GameSettings
{
    private const string MasterKey = "ReCode_Master";
    private const string MusicKey = "ReCode_Music";
    private const string SfxKey = "ReCode_Sfx";
    private const string SpeedKey = "ReCode_Speed";

    /// <summary>Velocidades disponibles, en el orden en que se recorren en Opciones.</summary>
    public static readonly float[] SpeedSteps = { 1f, 1.5f, 2f, 3f };

    private static bool loaded;
    private static float master = 1f, music = 1f, sfx = 1f, speed = 1f;

    // Se ejecuta solo al arrancar el juego: aplica el volumen general guardado.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        Load();
        AudioListener.volume = master;
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        master = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 1f));
        music = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 1f));
        sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 1f));
        speed = PlayerPrefs.GetFloat(SpeedKey, 1f);
        if (speed < 0.1f) speed = 1f;
    }

    public static float Master
    {
        get { Load(); return master; }
        set
        {
            Load();
            master = Mathf.Clamp01(value);
            AudioListener.volume = master;
            PlayerPrefs.SetFloat(MasterKey, master);
        }
    }

    public static float Music
    {
        get { Load(); return music; }
        set
        {
            Load();
            music = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicKey, music);
        }
    }

    public static float Sfx
    {
        get { Load(); return sfx; }
        set
        {
            Load();
            sfx = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxKey, sfx);
        }
    }

    /// <summary>Velocidad del juego (es lo que se le pone a Time.timeScale cuando NO esta en pausa).</summary>
    public static float Speed
    {
        get { Load(); return speed; }
        set
        {
            Load();
            speed = Mathf.Max(0.1f, value);
            PlayerPrefs.SetFloat(SpeedKey, speed);
        }
    }

    /// <summary>Posicion de la velocidad actual dentro de SpeedSteps (la mas cercana).</summary>
    public static int SpeedIndex
    {
        get
        {
            Load();
            int best = 0;
            for (int i = 1; i < SpeedSteps.Length; i++)
                if (Mathf.Abs(SpeedSteps[i] - speed) < Mathf.Abs(SpeedSteps[best] - speed)) best = i;
            return best;
        }
    }

    /// <summary>Pasa a la siguiente (dir = 1) o anterior (dir = -1) velocidad; al llegar al final da la vuelta.</summary>
    public static void CycleSpeed(int dir)
    {
        int n = SpeedSteps.Length;
        int i = (SpeedIndex + dir + n) % n;
        Speed = SpeedSteps[i];
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }
}
