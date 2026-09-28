using UnityEngine;

/// <summary>
/// Efectos del circulo azul: deja una estela que se desvanece, se estira en la direccion
/// en que avanza (squash and stretch) y "respira" cuando esta quieto.
/// Lo agrega solo GridLevel al jugador.
/// </summary>
public class PlayerFX : MonoBehaviour
{
    [Header("Estela")]
    public float trailInterval = 0.04f;
    public float trailLifetime = 0.3f;
    [Range(0f, 1f)] public float trailAlpha = 0.5f;

    [Header("Squash & stretch")]
    [Tooltip("Cuanto se estira segun la velocidad.")]
    public float stretchAmount = 0.06f;
    public float maxStretch = 0.35f;
    [Tooltip("Respiracion cuando esta quieto.")]
    public float idleBreathe = 0.04f;

    private SpriteRenderer sr;
    private Vector3 baseScale;
    private Vector3 lastPos;
    private float trailTimer;
    private float stretch;
    private bool horizontal = true;
    private float celebrateT = -1f;

    /// <summary>Pequeño rebote de alegria (lo llama GridLevel al ganar).</summary>
    public void Celebrate() { celebrateT = 0f; }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        lastPos = transform.position;
    }

    void Update()
    {
        Vector3 delta = transform.position - lastPos;
        lastPos = transform.position;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = delta.magnitude / dt;
        bool teleport = delta.magnitude > 1.5f; // reinicio del nivel: sin efectos

        if (!teleport && speed > 0.1f)
        {
            horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
            float target = Mathf.Min(speed * stretchAmount, maxStretch);
            stretch = Mathf.Lerp(stretch, target, dt * 20f);

            trailTimer += dt;
            if (trailTimer >= trailInterval)
            {
                trailTimer = 0f;
                SpawnGhost();
            }
        }
        else
        {
            stretch = Mathf.Lerp(stretch, 0f, dt * 12f);
            trailTimer = trailInterval;
        }

        float pop = 0f;
        if (celebrateT >= 0f)
        {
            celebrateT += dt;
            const float life = 1f;
            if (celebrateT >= life) celebrateT = -1f;
            else pop = Mathf.Sin(celebrateT * Mathf.PI * 6f) * 0.25f * (1f - celebrateT / life);
        }

        float idle = stretch < 0.01f ? Mathf.Sin(Time.time * 3f) * idleBreathe : 0f;
        float along = 1f + stretch + idle;
        float across = 1f / along;
        Vector3 sc = horizontal
            ? new Vector3(baseScale.x * along, baseScale.y * across, 1f)
            : new Vector3(baseScale.x * across, baseScale.y * along, 1f);
        transform.localScale = new Vector3(sc.x * (1f + pop), sc.y * (1f + pop), 1f);
    }

    private void SpawnGhost()
    {
        GameObject g = new GameObject("TrailGhost");
        g.transform.SetParent(transform.parent, false); // hijo de LevelVisuals: se oculta junto con la cuadricula
        g.transform.position = transform.position;
        g.transform.localScale = transform.localScale;

        SpriteRenderer gsr = g.AddComponent<SpriteRenderer>();
        gsr.sprite = sr.sprite;
        gsr.color = new Color(sr.color.r, sr.color.g, sr.color.b, trailAlpha);
        gsr.sortingOrder = sr.sortingOrder - 1;

        TrailGhost ghost = g.AddComponent<TrailGhost>();
        ghost.lifetime = trailLifetime;
        ghost.startAlpha = trailAlpha;
    }
}

/// <summary>Copia desvanecida del jugador; se destruye sola.</summary>
public class TrailGhost : MonoBehaviour
{
    public float lifetime = 0.3f;
    public float startAlpha = 0.5f;

    private SpriteRenderer sr;
    private float age;
    private Vector3 startScale;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        startScale = transform.localScale;
    }

    void Update()
    {
        age += Time.deltaTime;
        float k = Mathf.Clamp01(age / lifetime);

        Color c = sr.color;
        c.a = startAlpha * (1f - k);
        sr.color = c;
        transform.localScale = startScale * (1f - 0.4f * k);

        if (k >= 1f) Destroy(gameObject);
    }
}