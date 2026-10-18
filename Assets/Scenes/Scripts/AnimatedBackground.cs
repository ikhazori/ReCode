using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fondo animado por codigo, estilo Deltarune / Earthbound (hipnotico):
///  - Patron de ROMBOS (o tablero) que se desplaza en diagonal y se ONDULA como una bandera
///    (la textura se corta en franjas horizontales y cada una se mueve con una onda distinta).
///  - Una segunda capa de rombos GRANDES y tenues que va al reves con otra onda (parallax).
///  - Rombos-anillo que salen del centro y se expanden como un portal (rebobinar = se recogen).
///  - El color deriva lentamente del azul al violeta y el brillo "respira".
///  - Cuadritos de pixel que flotan hacia arriba y titilan.
///  - Lluvia de 0 y 1 (tema code) y una viñeta que oscurece los bordes.
/// No necesita imagenes ni prefabs. Ponlo en cualquier objeto de la escena.
/// Crea su propio Canvas (detras de los sprites del mundo, como la cuadricula).
/// Llama a Kick() cuando quieras un "golpe" (ej. al ejecutar un comando o al ganar).
/// </summary>
public class AnimatedBackground : MonoBehaviour
{
    public enum PatternStyle { Diamonds, Checker }

    [Header("Patron")]
    public PatternStyle pattern = PatternStyle.Diamonds;

    [Header("Colores del patron")]
    public Color colorA = new Color(0.08f, 0.11f, 0.26f);
    public Color colorB = new Color(0.11f, 0.16f, 0.36f);
    [Tooltip("Color al que deriva lentamente el fondo (se multiplica sobre el patron).")]
    public Color driftTint = new Color(1f, 0.78f, 1f);
    public float driftSpeed = 0.12f;
    [Range(0f, 1f)] public float driftAmount = 0.6f;

    [Header("Movimiento")]
    [Tooltip("Cuantas casillas / rombos caben a lo ancho de la pantalla.")]
    public float tilesAcross = 10f;
    [Tooltip("Velocidad del desplazamiento (en repeticiones de textura por segundo).")]
    public Vector2 scrollSpeed = new Vector2(0.04f, -0.03f);

    [Header("Ondas (el efecto hipnotico)")]
    public bool waves = true;
    [Range(4, 96)] public int waveStrips = 48;
    [Tooltip("Cuanto se mueve cada franja de lado a lado (en repeticiones de textura). 0.2-0.5 va bien.")]
    public float waveAmplitude = 0.3f;
    [Tooltip("Velocidad con la que viajan las ondas.")]
    public float waveSpeed = 1.1f;
    [Tooltip("Cuantas ondas caben de arriba a abajo de la pantalla.")]
    public float waveFrequency = 2.5f;
    [Tooltip("Cuanto 'respira' la amplitud de las ondas (0 = siempre igual).")]
    [Range(0f, 1f)] public float waveSwell = 0.5f;

    [Header("Respiracion (brillo)")]
    public float pulseSpeed = 0.8f;
    [Range(0f, 0.5f)] public float pulseAmount = 0.15f;

    [Header("Capa lejana (parallax)")]
    public bool farLayer = true;
    [Tooltip("Cuantas veces mas grandes son sus cuadros.")]
    public float farScale = 2.5f;
    [Range(0f, 0.3f)] public float farAlpha = 0.07f;
    [Tooltip("Velocidad relativa (negativa en la practica: va al reves).")]
    public float farParallax = 0.6f;

    [Header("Rombos que se expanden (portal)")]
    public bool rings = true;
    public int ringCount = 5;
    [Tooltip("Segundos que tarda un rombo en ir del centro a los bordes.")]
    public float ringPeriod = 7f;
    public Color ringColor = new Color(0.6f, 0.9f, 1f);
    [Tooltip("Color al que cambia el rombo mientras se aleja.")]
    public Color ringColorEnd = new Color(1f, 0.6f, 1f);
    [Range(0f, 1f)] public float ringAlpha = 0.28f;
    [Tooltip("Giro lento (grados por segundo). Los rombos alternan la direccion.")]
    public float ringSpin = 8f;

    [Header("Cuadritos flotantes")]
    public bool floatingSquares = true;
    public int squareCount = 26;
    public Color squareColor = new Color(0.6f, 0.9f, 1f);
    [Range(0f, 1f)] public float squareAlpha = 0.35f;
    [Tooltip("Velocidad de subida (pixeles por segundo).")]
    public Vector2 squareSpeed = new Vector2(12f, 40f);

    [Header("Viñeta (bordes oscuros)")]
    public bool vignette = true;
    [Range(0f, 1f)] public float vignetteStrength = 0.65f;

    [Header("Lluvia de 0 y 1 (tema code)")]
    public bool binaryRain = true;

    [Header("Margen extra (para que el temblor de ScreenEffects no muestre bordes)")]
    public float bleed = 40f;

    // ---- Capa hecha de franjas horizontales (permite ondularla) ----
    private class WaveLayer
    {
        public RectTransform root;
        public RawImage[] strips;

        public WaveLayer(Transform parent, string name, int count, Texture tex, float bleed)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            root = (RectTransform)go.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = new Vector2(-bleed, -bleed);
            root.offsetMax = new Vector2(bleed, bleed);

            strips = new RawImage[count];
            for (int i = 0; i < count; i++)
            {
                GameObject s = new GameObject("Strip" + i, typeof(RectTransform), typeof(RawImage));
                s.transform.SetParent(root, false);
                RectTransform r = (RectTransform)s.transform;
                r.anchorMin = new Vector2(0f, i / (float)count);
                r.anchorMax = new Vector2(1f, (i + 1) / (float)count);
                r.offsetMin = Vector2.zero;
                // 1 px de solape para que no se cuelen rendijas entre franjas
                r.offsetMax = new Vector2(0f, i < count - 1 ? 1f : 0f);
                RawImage img = s.GetComponent<RawImage>();
                img.texture = tex;
                img.raycastTarget = false;
                strips[i] = img;
            }
        }

        public void SetColor(Color c)
        {
            for (int i = 0; i < strips.Length; i++) strips[i].color = c;
        }

        public void Apply(Vector2 offset, float w, float h, float amp, float freq, float phase, float phaseShift)
        {
            int n = strips.Length;
            float stripH = h / n;
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                float p = t * freq * Mathf.PI * 2f;
                // dos ondas mezcladas: se ve organico y no se repite de forma obvia
                float wv = Mathf.Sin(p + phase + phaseShift) + 0.5f * Mathf.Sin(p * 2.3f - phase * 0.7f);
                float dx = n > 1 ? wv * amp * 0.67f : 0f;
                strips[i].uvRect = new Rect(offset.x + dx, offset.y + i * stripH, w, stripH);
            }
        }
    }

    private WaveLayer main;
    private WaveLayer far;
    private RectTransform rt;
    private Vector2 offset;
    private Vector2 farOffset;
    private BinaryRain rain;

    private Vector2 normalScrollSpeed;
    private bool rewinding;
    private float rewindMul = 1f;
    private float kick;

    private RawImage[] ringImg;
    private RectTransform[] ringRt;
    private float[] ringRot;
    private float ringTime;

    private RectTransform[] sq;
    private Image[] sqImg;
    private float[] sqSpeed;
    private float[] sqPhase;
    private bool sqReady;

    void Awake()
    {
        // Canvas propio, dibujado POR el Main Camera y con orden -100: queda detras de los
        // sprites del mundo (la cuadricula), mientras el Canvas normal sigue encima de todo.
        Camera cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        GameObject canvasGo = new GameObject("BackgroundCanvas", typeof(Canvas));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 100f;
        canvas.sortingOrder = -100;

        int strips = waves ? Mathf.Max(1, waveStrips) : 1;

        main = new WaveLayer(canvas.transform, "AnimatedBackground", strips,
                             BuildPatternTexture(colorA, colorB), bleed);
        rt = main.root;

        if (farLayer)
        {
            far = new WaveLayer(canvas.transform, "AnimatedBackgroundFar", Mathf.Max(1, strips / 2),
                                BuildPatternTexture(new Color(1f, 1f, 1f, 0.6f), new Color(1f, 1f, 1f, 0f)), bleed);
            far.SetColor(new Color(1f, 1f, 1f, farAlpha / 0.6f));
        }

        if (rings) BuildRings(canvas.transform);

        if (floatingSquares) BuildSquares();

        if (binaryRain) rain = BinaryRain.Create(canvas.transform, canvas.transform.childCount); // encima del fondo y los cuadritos

        if (vignette)
        {
            RawImage v = NewLayer(canvas.transform, "Vignette");
            v.texture = BuildVignetteTexture();
            v.uvRect = new Rect(0f, 0f, 1f, 1f);
            v.color = new Color(1f, 1f, 1f, vignetteStrength);
            v.transform.SetAsLastSibling();
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float t = Time.time;
        kick = Mathf.MoveTowards(kick, 0f, dt * 1.2f);

        offset += scrollSpeed * dt;
        offset.x = Mathf.Repeat(offset.x, 1f);
        offset.y = Mathf.Repeat(offset.y, 1f);

        // El tablero es de 2x2 casillas por repeticion; el rombo, de 1 por repeticion.
        float cellsPerTex = pattern == PatternStyle.Checker ? 2f : 1f;
        float w = tilesAcross / cellsPerTex;
        float h = w * rt.rect.height / Mathf.Max(1f, rt.rect.width);

        // La amplitud de las ondas "respira" lentamente (y crece con Kick)
        float swell = 1f - waveSwell * (0.5f + 0.5f * Mathf.Sin(t * 0.35f));
        float amp = waves ? waveAmplitude * swell * (1f + kick * 0.8f) : 0f;

        main.Apply(offset, w, h, amp, waveFrequency, t * waveSpeed, 0f);

        if (far != null)
        {
            farOffset -= scrollSpeed * farParallax * dt; // al reves que la capa cercana
            farOffset.x = Mathf.Repeat(farOffset.x, 1f);
            farOffset.y = Mathf.Repeat(farOffset.y, 1f);
            // otra frecuencia y onda al reves: las dos capas nunca "coinciden"
            far.Apply(farOffset, w / farScale, h / farScale, amp / farScale,
                      waveFrequency * 0.7f, -t * waveSpeed * 0.6f, 1.7f);
        }

        // Brillo entre (1 - pulseAmount) y 1 (el color de un RawImage no puede pasar de 1).
        float b = 1f - pulseAmount * (0.5f + 0.5f * Mathf.Sin(t * pulseSpeed));
        float d = (Mathf.Sin(t * driftSpeed) * 0.5f + 0.5f) * driftAmount;
        Color tint = Color.Lerp(Color.white, driftTint, d);
        main.SetColor(new Color(tint.r * b, tint.g * b, tint.b * b, 1f));

        if (ringImg != null) UpdateRings(dt);
        if (sq != null) UpdateSquares(dt);
    }

    /// <summary>Un "golpe" visual: las ondas se agitan y los rombos salen mas rapido un momento. Ej: al ejecutar un comando.</summary>
    public void Kick(float strength = 1f)
    {
        kick = Mathf.Max(kick, strength);
    }

    /// <summary>El fondo (y la lluvia de numeros) empieza a desplazarse al reves y mas rapido (usalo en el rebobinado de LevelRunner).</summary>
    public void BeginRewindScroll(float speedMultiplier = 2.5f)
    {
        if (rewinding) return;
        rewinding = true;
        rewindMul = speedMultiplier;
        normalScrollSpeed = scrollSpeed;
        scrollSpeed = -scrollSpeed * speedMultiplier;
        if (rain != null) rain.BeginRewind(speedMultiplier);
    }

    /// <summary>Vuelve a la velocidad y direccion normales.</summary>
    public void EndRewindScroll()
    {
        if (!rewinding) return;
        rewinding = false;
        rewindMul = 1f;
        scrollSpeed = normalScrollSpeed;
        if (rain != null) rain.EndRewind();
    }

    // ---------- Construccion ----------

    private RawImage NewLayer(Transform canvas, string layerName)
    {
        GameObject go = new GameObject(layerName, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(canvas, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(-bleed, -bleed);
        r.offsetMax = new Vector2(bleed, bleed);
        RawImage img = go.GetComponent<RawImage>();
        img.raycastTarget = false;
        return img;
    }

    private void BuildRings(Transform canvas)
    {
        int n = Mathf.Max(0, ringCount);
        ringImg = new RawImage[n];
        ringRt = new RectTransform[n];
        ringRot = new float[n];
        Texture2D tex = BuildRingTexture();

        for (int i = 0; i < n; i++)
        {
            GameObject go = new GameObject("Ring" + i, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(canvas, false);
            RectTransform r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero;
            RawImage img = go.GetComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            ringRt[i] = r;
            ringImg[i] = img;
            ringRot[i] = 0f;
        }
    }

    private void UpdateRings(float dt)
    {
        int n = ringImg.Length;
        if (n == 0) return;

        float maxSize = (rt.rect.width + rt.rect.height) * 1.05f;
        float dir = rewinding ? -1f : 1f;
        float mul = rewinding ? rewindMul : 1f;
        ringTime += dt * dir * mul * (1f + kick * 3f);

        for (int i = 0; i < n; i++)
        {
            // u: 0 = nace en el centro, 1 = llega a los bordes (cada rombo va desfasado)
            float u = Mathf.Repeat(ringTime / Mathf.Max(0.1f, ringPeriod) + (float)i / n, 1f);
            float size = Mathf.Lerp(0.03f, 1f, u * u) * maxSize; // acelera al alejarse
            ringRt[i].sizeDelta = new Vector2(size, size);

            ringRot[i] += (i % 2 == 0 ? 1f : -1f) * ringSpin * dt;
            ringRt[i].localRotation = Quaternion.Euler(0f, 0f, ringRot[i]);

            Color c = Color.Lerp(ringColor, ringColorEnd, u);
            c.a = Mathf.Sin(u * Mathf.PI) * ringAlpha; // aparece y se desvanece
            ringImg[i].color = c;
        }
    }

    private void BuildSquares()
    {
        int n = Mathf.Max(0, squareCount);
        sq = new RectTransform[n];
        sqImg = new Image[n];
        sqSpeed = new float[n];
        sqPhase = new float[n];

        for (int i = 0; i < n; i++)
        {
            GameObject go = new GameObject("Pixel" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(rt, false);
            RectTransform r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.zero;
            sq[i] = r;
            sqImg[i] = go.GetComponent<Image>();
            sqImg[i].raycastTarget = false;
            sqSpeed[i] = Random.Range(squareSpeed.x, squareSpeed.y);
            sqPhase[i] = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    private void UpdateSquares(float dt)
    {
        float w = rt.rect.width;
        float h = rt.rect.height;
        if (w < 2f || h < 2f) return;

        float unit = Mathf.Max(2f, Mathf.Round(h / 150f)); // "pixel" del fondo, segun la resolucion

        if (!sqReady)
        {
            for (int i = 0; i < sq.Length; i++)
            {
                float size = unit * Random.Range(1, 4);
                sq[i].sizeDelta = new Vector2(size, size);
                sq[i].anchoredPosition = new Vector2(Random.Range(0f, w), Random.Range(0f, h));
            }
            sqReady = true;
        }

        float dir = rewinding ? -1f : 1f;
        float mul = rewinding ? rewindMul : 1f;

        for (int i = 0; i < sq.Length; i++)
        {
            Vector2 p = sq[i].anchoredPosition;
            p.y += sqSpeed[i] * mul * dir * dt;
            float size = sq[i].sizeDelta.x;

            if (p.y > h + size) { p.y = -size; p.x = Random.Range(0f, w); }
            else if (p.y < -size * 2f) { p.y = h + size; p.x = Random.Range(0f, w); }

            // Se pega a la rejilla de pixeles (movimiento "de a saltitos", no suave)
            sq[i].anchoredPosition = new Vector2(Mathf.Round(p.x / unit) * unit, Mathf.Round(p.y / unit) * unit);

            // Titileo en 3 niveles: apagado / medio / encendido
            float tw = Mathf.Clamp01(Mathf.Floor((Mathf.Sin(Time.time * 2f + sqPhase[i]) * 0.5f + 0.5f) * 3f) / 2f);
            sqImg[i].color = new Color(squareColor.r, squareColor.g, squareColor.b, tw * squareAlpha);
        }
    }

    // ---------- Texturas ----------

    private Texture2D BuildPatternTexture(Color a, Color b)
    {
        if (pattern == PatternStyle.Checker) return BuildCheckerTexture(a, b);
        return BuildDiamondTexture(a, b);
    }

    private Texture2D BuildCheckerTexture(Color a, Color b)
    {
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, a);
        tex.SetPixel(1, 0, b);
        tex.SetPixel(0, 1, b);
        tex.SetPixel(1, 1, a);
        tex.filterMode = FilterMode.Point; // bordes nitidos, estilo pixel art
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Apply();
        return tex;
    }

    // Rombo de 16x16 que encaja consigo mismo (patron tipo "argyle"): el rombo es 'b', el hueco es 'a'
    private Texture2D BuildDiamondTexture(Color a, Color b)
    {
        const int n = 16;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - n * 0.5f);
                float dy = Mathf.Abs(y + 0.5f - n * 0.5f);
                tex.SetPixel(x, y, (dx + dy) < n * 0.5f ? b : a);
            }
        }
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Apply();
        return tex;
    }

    // Contorno de rombo (con un "eco" mas fino por dentro) para los anillos que se expanden
    private Texture2D BuildRingTexture()
    {
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs((x + 0.5f) / n * 2f - 1f);
                float dy = Mathf.Abs((y + 0.5f) / n * 2f - 1f);
                float d = dx + dy;
                float a = 0f;
                if (d > 0.86f && d < 1f) a = 1f;
                else if (d > 0.68f && d < 0.72f) a = 0.6f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        return tex;
    }

    // Degradado radial: transparente al centro, negro en los bordes
    private Texture2D BuildVignetteTexture()
    {
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, dist));
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        }
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        return tex;
    }
}