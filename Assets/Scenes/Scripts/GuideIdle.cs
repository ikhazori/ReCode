using UnityEngine;

/// <summary>
/// Hace que el personaje "respire" (se estira y encoge un poquito) y se balancee apenas.
/// Lo agrega solo el DialogueController al Guide, no hay que ponerlo a mano.
/// Usa escala y rotacion (no la posicion) para no chocar con el deslizamiento de entrada/salida.
/// Novedades: animacion "escalonada" (pocos cuadros por segundo, como un sprite pixel-art)
/// y un saltito de "hablar" con cada letra (DialogueController llama a Pulse()).
/// </summary>
public class GuideIdle : MonoBehaviour
{
    [Tooltip("Cuanto se estira al respirar (0.03 = 3%).")]
    public float breathe = 0.03f;
    public float breatheSpeed = 2f;
    [Tooltip("Grados que se balancea de lado a lado.")]
    public float sway = 2f;
    public float swaySpeed = 1.3f;

    [Header("Estilo pixel")]
    [Tooltip("Cuadros por segundo de la animacion (0 = suave). 8-12 da el aspecto de sprite animado a mano.")]
    public float steppedFps = 10f;
    [Tooltip("Cuanto se estira hacia arriba con cada letra que 'dice'.")]
    public float talkBounce = 0.06f;

    private RectTransform rt;
    private Vector3 baseScale;
    private float pulse;

    void Awake()
    {
        rt = (RectTransform)transform;
        baseScale = rt.localScale;
    }

    /// <summary>Un saltito de "hablar" (lo llama el DialogueController con cada letra).</summary>
    public void Pulse() { pulse = 1f; }

    void Update()
    {
        float time = Time.time;
        if (steppedFps > 0f) time = Mathf.Floor(time * steppedFps) / steppedFps;

        pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 8f);
        float talk = pulse * talkBounce;

        float b = Mathf.Sin(time * breatheSpeed) * breathe;
        rt.localScale = new Vector3(baseScale.x * (1f - b * 0.5f - talk * 0.5f), baseScale.y * (1f + b + talk), baseScale.z);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * swaySpeed) * sway);
    }
}