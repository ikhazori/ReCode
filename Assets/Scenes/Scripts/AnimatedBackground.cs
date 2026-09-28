using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fondo animado por codigo: un tablero de cuadros que se desplaza en diagonal y
/// "respira" (brillo que sube y baja). No necesita imagenes ni prefabs.
/// Ponlo en cualquier objeto de la escena (por ejemplo "Level") y dale Play.
/// Crea su propio Canvas (detras de los sprites del mundo, como la cuadricula).
/// </summary>
public class AnimatedBackground : MonoBehaviour
{
    [Header("Colores del tablero")]
    public Color colorA = new Color(0.08f, 0.11f, 0.26f);
    public Color colorB = new Color(0.11f, 0.16f, 0.36f);

    [Header("Movimiento")]
    [Tooltip("Cuantas casillas caben a lo ancho de la pantalla.")]
    public float tilesAcross = 14f;
    [Tooltip("Velocidad del desplazamiento (en repeticiones de textura por segundo).")]
    public Vector2 scrollSpeed = new Vector2(0.04f, -0.03f);

    [Header("Respiracion (brillo)")]
    public float pulseSpeed = 0.8f;
    [Range(0f, 0.5f)] public float pulseAmount = 0.15f;

    [Header("Lluvia de 0 y 1 (tema code)")]
    public bool binaryRain = true;

    [Header("Margen extra (para que el temblor de ScreenEffects no muestre bordes)")]
    public float bleed = 40f;

    private RawImage raw;
    private RectTransform rt;
    private Vector2 offset;
    private BinaryRain rain;

    private Vector2 normalScrollSpeed;
    private bool rewinding;

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

        GameObject go = new GameObject("AnimatedBackground", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsFirstSibling(); // detras de todo

        rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-bleed, -bleed);
        rt.offsetMax = new Vector2(bleed, bleed);

        raw = go.GetComponent<RawImage>();
        raw.raycastTarget = false;
        raw.texture = BuildCheckerTexture();

        if (binaryRain) rain = BinaryRain.Create(canvas.transform, 1); // justo encima del fondo
    }

    void Update()
    {
        offset += scrollSpeed * Time.deltaTime;
        offset.x = Mathf.Repeat(offset.x, 1f);
        offset.y = Mathf.Repeat(offset.y, 1f);

        // La textura es de 2x2 casillas, asi que cada repeticion cubre 2 casillas.
        float w = tilesAcross / 2f;
        float h = w * rt.rect.height / Mathf.Max(1f, rt.rect.width);
        raw.uvRect = new Rect(offset.x, offset.y, w, h);

        float b = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        raw.color = new Color(b, b, b, 1f);
    }

    /// <summary>El fondo (y la lluvia de numeros) empieza a desplazarse al reves y mas rapido (usalo en el rebobinado de LevelRunner).</summary>
    public void BeginRewindScroll(float speedMultiplier = 2.5f)
    {
        if (rewinding) return;
        rewinding = true;
        normalScrollSpeed = scrollSpeed;
        scrollSpeed = -scrollSpeed * speedMultiplier;
        if (rain != null) rain.BeginRewind(speedMultiplier);
    }

    /// <summary>Vuelve a la velocidad y direccion normales.</summary>
    public void EndRewindScroll()
    {
        if (!rewinding) return;
        rewinding = false;
        scrollSpeed = normalScrollSpeed;
        if (rain != null) rain.EndRewind();
    }

    private Texture2D BuildCheckerTexture()
    {
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, colorA);
        tex.SetPixel(1, 0, colorB);
        tex.SetPixel(0, 1, colorB);
        tex.SetPixel(1, 1, colorA);
        tex.filterMode = FilterMode.Point; // bordes nitidos, estilo pixel art
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Apply();
        return tex;
    }
}