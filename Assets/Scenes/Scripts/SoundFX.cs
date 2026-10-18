using UnityEngine;

/// <summary>
/// Efectos de sonido retro generados POR CODIGO (ondas cuadradas y ruido), asi que no
/// hace falta ningun archivo de audio. Se crea solo la primera vez que se usa.
/// Uso: SoundFX.Move(); SoundFX.Confirm(); SoundFX.Step(); SoundFX.Crash(); SoundFX.Win(); SoundFX.Blip();
/// </summary>
public static class SoundFX
{
    private static AudioSource src;
    private static AudioClip move, confirm, step, crash, win, blip, save, rewindStep, click, teleport, pickup;

    private const int Rate = 44100;

    public static void Move()    { Init(); Play(move, 0.5f, 1f); }
    public static void Confirm() { Init(); Play(confirm, 0.6f, 1f); }
    public static void Step()    { Init(); Play(step, 0.4f, Random.Range(0.95f, 1.05f)); }
    public static void Crash()   { Init(); Play(crash, 0.8f, 1f); }
    public static void Win()     { Init(); Play(win, 0.7f, 1f); }
    public static void Blip()    { Init(); Play(blip, 0.4f, Random.Range(0.95f, 1.08f)); }
    /// <summary>Jingle suave al terminar un rebobinado (como un "punto de guardado").</summary>
    public static void Save()    { Init(); Play(save, 0.6f, 1f); }
    /// <summary>Sonido cortito y ascendente para cada paso al retroceder en el rebobinado.</summary>
    public static void RewindStep() { Init(); Play(rewindStep, 0.3f, Random.Range(0.95f, 1.1f)); }
    /// <summary>Click seco al pisar un boton.</summary>
    public static void Click()   { Init(); Play(click, 0.45f, 1f); }
    /// <summary>Sonido "ta-doop" al teletransportarse.</summary>
    public static void Teleport(){ Init(); Play(teleport, 0.5f, 1f); }
    /// <summary>"Ping" doble y ascendente al recoger un fragmento de codigo.</summary>
    public static void Pickup()  { Init(); Play(pickup, 0.55f, 1f); }

    private static void Play(AudioClip clip, float volume, float pitch)
    {
        Init();
        src.pitch = pitch;
        src.PlayOneShot(clip, volume * GameSettings.Sfx);
    }

    private static void Init()
    {
        if (src != null) return; // (si el objeto fue destruido, Unity lo ve como null y se regenera todo)

        GameObject go = new GameObject("SoundFX");
        Object.DontDestroyOnLoad(go);
        src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;

        move    = Tone(520f, 520f, 0.05f, 0.25f, false);
        confirm = Sequence(new float[] { 660f, 990f }, 0.06f, 0.25f);
        step    = Tone(300f, 200f, 0.07f, 0.25f, false);
        crash   = Tone(220f, 40f, 0.45f, 0.35f, true);
        win     = Sequence(new float[] { 523f, 659f, 784f, 1047f }, 0.11f, 0.25f);
        blip    = Tone(700f, 700f, 0.035f, 0.2f, false);
        save    = Sequence(new float[] { 784f, 988f, 1318f }, 0.1f, 0.28f);
        rewindStep = Tone(260f, 780f, 0.05f, 0.18f, false); // tono que sube (al reves del Step normal, que baja)
        click   = Tone(900f, 500f, 0.03f, 0.25f, false);
        teleport = Sequence(new float[] { 400f, 900f, 1500f }, 0.05f, 0.22f);
        pickup  = Sequence(new float[] { 880f, 1175f, 1760f }, 0.06f, 0.22f);
    }

    // Una nota: frecuencia f0 -> f1, onda cuadrada (o ruido si noise = true), con fade out.
    private static float[] ToneData(float f0, float f1, float dur, float vol, bool noise)
    {
        int n = Mathf.Max(1, (int)(Rate * dur));
        float[] d = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            phase += Mathf.Lerp(f0, f1, t) / Rate;
            float s = noise ? Random.Range(-1f, 1f) : ((phase % 1f) < 0.5f ? 1f : -1f);
            d[i] = s * vol * (1f - t);
        }
        return d;
    }

    private static AudioClip Tone(float f0, float f1, float dur, float vol, bool noise)
    {
        return MakeClip(ToneData(f0, f1, dur, vol, noise));
    }

    // Varias notas seguidas (arpegio)
    private static AudioClip Sequence(float[] freqs, float noteDur, float vol)
    {
        int per = (int)(Rate * noteDur);
        float[] all = new float[per * freqs.Length];
        for (int k = 0; k < freqs.Length; k++)
        {
            float[] note = ToneData(freqs[k], freqs[k], noteDur, vol, false);
            System.Array.Copy(note, 0, all, k * per, Mathf.Min(note.Length, per));
        }
        return MakeClip(all);
    }

    private static AudioClip MakeClip(float[] data)
    {
        AudioClip clip = AudioClip.Create("fx", data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}