using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Efecto de "choque": destello rojo en toda la pantalla + temblor de la camara
/// y de toda la interfaz. Se crea el overlay rojo solo, no hay que armar nada.
/// </summary>
public class ScreenEffects : MonoBehaviour
{
    [Header("Referencias (se buscan solas si las dejas vacias)")]
    public Canvas canvas;
    public Camera cam;

    [Header("Efecto de choque")]
    public float duration = 0.6f;
    [Tooltip("Cuantos destellos rojos da durante el efecto.")]
    public int flashCount = 3;
    [Range(0f, 1f)] public float maxFlashAlpha = 0.6f;
    public Color flashColor = Color.red;
    [Tooltip("Intensidad del temblor de la interfaz (en pixeles de canvas).")]
    public float uiShake = 10f;
    [Tooltip("Intensidad del temblor de la camara (en unidades del mundo).")]
    public float cameraShake = 0.15f;

    [Header("Efecto de victoria (destello verde suave)")]
    public Color winColor = new Color(0.3f, 1f, 0.5f);
    [Tooltip("Cuanto dura el destello (segundos). Sube y baja suavemente, una sola vez.")]
    public float winDuration = 1.2f;
    [Range(0f, 1f)] public float winMaxAlpha = 0.35f;
    [Tooltip("Texto grande que salta al centro al ganar (vacio = sin texto).")]
    public string winText = "¡NIVEL COMPLETADO!";
    public float winTextSize = 30f;
    [Tooltip("Posicion vertical del texto (0 = centro de la pantalla).")]
    public float winTextOffsetY = 40f;

    [Header("Estatica de rebobinado (efecto VHS)")]
    [Range(0f, 1f)] public float staticAlpha = 0.4f;
    [Tooltip("Cuantas veces por segundo cambia el ruido. Mas alto = mas nervioso.")]
    public float staticRefreshRate = 18f;
    [Tooltip("Resolucion de la textura de ruido. Mas baja = 'granos' de ruido mas grandes (estilo TV vieja).")]
    public int staticResolution = 40;
    [Tooltip("Cuanto tiembla la camara mientras hay estatica (unidades del mundo).")]
    public float staticCameraJitter = 0.05f;

    [Header("Destello blanco (justo al llegar al inicio del rebobinado)")]
    public float rewindFlashDuration = 0.15f;
    [Range(0f, 1f)] public float rewindFlashMaxAlpha = 0.9f;

    private Image flash;
    private RawImage staticOverlay;
    private Texture2D staticTex;
    private AudioSource noiseSource;
    private bool staticActive;
    private float staticFade;
    private float staticTimer;
    private Vector3 camBaseline;

    void Awake()
    {
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (cam == null) cam = Camera.main;
        camBaseline = cam != null ? cam.transform.position : Vector3.zero;

        GameObject go = new GameObject("CrashFlash");
        go.transform.SetParent(canvas.transform, false);
        flash = go.AddComponent<Image>();
        flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        flash.raycastTarget = false;
        RectTransform rt = flash.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Overlay de estatica (ruido) para el rebobinado, estilo TV vieja / VHS
        GameObject staticGo = new GameObject("RewindStatic");
        staticGo.transform.SetParent(canvas.transform, false);
        staticOverlay = staticGo.AddComponent<RawImage>();
        staticOverlay.raycastTarget = false;
        staticOverlay.color = new Color(1f, 1f, 1f, 0f);
        RectTransform srt = staticOverlay.rectTransform;
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = Vector2.zero;

        staticTex = new Texture2D(staticResolution, staticResolution, TextureFormat.RGBA32, false);
        staticTex.filterMode = FilterMode.Point; // pixeles de ruido bien marcados, no borrosos
        staticTex.wrapMode = TextureWrapMode.Repeat;
        staticOverlay.texture = staticTex;
        RandomizeStaticTexture();

        // "Shhh" de ruido blanco que suena mientras dura el rebobinado
        noiseSource = gameObject.AddComponent<AudioSource>();
        noiseSource.playOnAwake = false;
        noiseSource.loop = true;
        noiseSource.volume = 0f;
        noiseSource.clip = BuildNoiseClip(1f);
    }

    void Update()
    {
        // Fundido suave de la estatica: sube cuando esta activa, baja cuando se apaga
        // (asi no aparece/desaparece de golpe).
        staticFade = Mathf.MoveTowards(staticFade, staticActive ? 1f : 0f, Time.deltaTime * 8f);

        if (staticFade > 0.001f)
        {
            // Solo se genera ruido NUEVO mientras la estatica esta realmente activa. Al
            // apagarse, se deja de refrescar el patron y simplemente se desvanece el
            // ultimo que quedo (antes se seguia randomizando durante el apagado, lo que
            // se veia como que quedaban "restos" de ruido pegados en vez de un fundido limpio).
            if (staticActive)
            {
                staticTimer += Time.deltaTime;
                float interval = 1f / Mathf.Max(1f, staticRefreshRate);
                if (staticTimer >= interval)
                {
                    staticTimer = 0f;
                    RandomizeStaticTexture();
                }
            }

            staticOverlay.color = new Color(1f, 1f, 1f, staticAlpha * staticFade);

            if (cam != null)
            {
                Vector2 j = Random.insideUnitCircle * staticCameraJitter * staticFade;
                cam.transform.position = camBaseline + new Vector3(j.x, j.y, 0f);
            }
        }
        else
        {
            // Asegura que quede totalmente invisible (no solo "casi cero").
            staticOverlay.color = new Color(1f, 1f, 1f, 0f);
            if (cam != null) cam.transform.position = camBaseline;
        }
    }

    /// <summary>
    /// Corta de golpe CUALQUIER efecto en curso (estatica, destello, temblor de camara) y
    /// deja todo en su estado normal. Usalo al saltar de nivel desde el menu de pausa, por
    /// si el jugador se salio en medio de un rebobinado o un choque.
    /// </summary>
    public void ForceReset()
    {
        StopAllCoroutines();

        staticActive = false;
        staticFade = 0f;
        if (staticOverlay != null) staticOverlay.color = new Color(1f, 1f, 1f, 0f);
        if (noiseSource != null) noiseSource.Stop();

        if (flash != null) flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, 0f);
        if (cam != null) cam.transform.position = camBaseline;
    }

    /// <summary>Prende o apaga la estatica de rebobinado (ruido en pantalla + "shhh" + temblor leve de camara).</summary>
    public void SetRewindStatic(bool active)
    {
        staticActive = active;
        if (noiseSource == null) return;

        if (active)
        {
            noiseSource.volume = 0.4f;
            if (!noiseSource.isPlaying) noiseSource.Play();
        }
        else
        {
            noiseSource.Stop();
        }
    }

    /// <summary>Destello blanco bien rapido, como diciendo "aqui no paso nada" (usalo al llegar al inicio del rebobinado).</summary>
    public IEnumerator PlayRewindArrivalFlash()
    {
        yield return PlayQuickFlash(Color.white, rewindFlashDuration, rewindFlashMaxAlpha);
    }

    /// <summary>Destello generico: sube y baja una sola vez, bien rapido.</summary>
    public IEnumerator PlayQuickFlash(Color color, float duration, float maxAlpha)
    {
        flash.transform.SetAsLastSibling();
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float alpha = Mathf.Sin(k * Mathf.PI) * maxAlpha;
            flash.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }
        flash.color = new Color(color.r, color.g, color.b, 0f);
    }

    private void RandomizeStaticTexture()
    {
        int n = staticResolution * staticResolution;
        Color32[] pixels = new Color32[n];
        for (int i = 0; i < n; i++)
        {
            byte v = (byte)Random.Range(0, 256);
            pixels[i] = new Color32(v, v, v, 255);
        }
        staticTex.SetPixels32(pixels);
        staticTex.Apply();
    }

    private static AudioClip BuildNoiseClip(float duration)
    {
        int rate = 44100;
        int n = Mathf.Max(1, (int)(rate * duration));
        float[] data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Random.Range(-0.4f, 0.4f);
        AudioClip clip = AudioClip.Create("staticNoise", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Destello verde suave de "ganaste": sube y baja una sola vez, sin temblor.</summary>
    public IEnumerator PlayWinEffect()
    {
        flash.transform.SetAsLastSibling();

        // Texto grande que "salta" al centro y se desvanece al final
        TextMeshProUGUI label = null;
        if (!string.IsNullOrEmpty(winText))
        {
            GameObject go = new GameObject("WinText", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(canvas.transform, false);
            label = go.GetComponent<TextMeshProUGUI>();
            label.text = winText;
            label.fontSize = winTextSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            RectTransform lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0.5f, 0.5f);
            lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(2000f, 120f);
            lrt.anchoredPosition = new Vector2(0f, winTextOffsetY);
            go.transform.SetAsLastSibling(); // por encima del destello
        }

        float t = 0f;
        while (t < winDuration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / winDuration);
            float alpha = Mathf.Sin(progress * Mathf.PI) * winMaxAlpha; // curva suave: 0 -> max -> 0
            flash.color = new Color(winColor.r, winColor.g, winColor.b, alpha);

            if (label != null)
            {
                float pop = EaseOutBack(Mathf.Clamp01(t / 0.3f));
                label.rectTransform.localScale = new Vector3(pop, pop, 1f);
                label.alpha = progress > 0.75f ? (1f - progress) / 0.25f : 1f;
            }
            yield return null;
        }

        flash.color = new Color(winColor.r, winColor.g, winColor.b, 0f);
        if (label != null) Destroy(label.gameObject);
    }

    // Curva con "rebote" al final (el texto se pasa un poquito de tamaño y vuelve)
    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    public IEnumerator PlayCrashEffect()
    {
        flash.transform.SetAsLastSibling(); // siempre por encima de todo

        // Guardamos las posiciones originales para restaurarlas al final
        List<RectTransform> uiItems = new List<RectTransform>();
        List<Vector2> uiOriginals = new List<Vector2>();
        foreach (Transform child in canvas.transform)
        {
            RectTransform rt = child as RectTransform;
            if (rt == null || rt == flash.rectTransform) continue;
            uiItems.Add(rt);
            uiOriginals.Add(rt.anchoredPosition);
        }
        Vector3 camOrigin = cam != null ? cam.transform.position : Vector3.zero;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / duration);
            float falloff = 1f - progress; // el temblor se va apagando

            // Destello pulsante
            float alpha = Mathf.Abs(Mathf.Sin(progress * Mathf.PI * flashCount)) * maxFlashAlpha * falloff;
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);

            // Temblor de la interfaz
            for (int i = 0; i < uiItems.Count; i++)
            {
                uiItems[i].anchoredPosition = uiOriginals[i] + Random.insideUnitCircle * uiShake * falloff;
            }
            // Temblor del mundo
            if (cam != null)
            {
                Vector2 shake = Random.insideUnitCircle * cameraShake * falloff;
                cam.transform.position = camOrigin + new Vector3(shake.x, shake.y, 0f);
            }

            yield return null;
        }

        // Restaurar todo
        for (int i = 0; i < uiItems.Count; i++) uiItems[i].anchoredPosition = uiOriginals[i];
        if (cam != null) cam.transform.position = camOrigin;
        flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
    }
}