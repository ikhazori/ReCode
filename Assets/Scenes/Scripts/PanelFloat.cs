using UnityEngine;

/// <summary>
/// Hace que un panel de UI "flote" suavemente: un vaiven lento arriba/abajo y de lado,
/// con una inclinacion casi imperceptible. Asi los cuadros del HUD y del pseudocodigo
/// se sienten vivos, igual que el fondo animado, en vez de verse rigidos.
///
/// No hace falta ponerlo a mano: GameHUDManager y PseudocodePanel lo agregan solos.
/// Los valores se ajustan desde esos dos componentes (seccion "Movimiento").
/// </summary>
[DisallowMultipleComponent]
public class PanelFloat : MonoBehaviour
{
    [Tooltip("Cuanto se mueve (unidades del Canvas): X = de lado, Y = arriba/abajo.")]
    public Vector2 amplitude = new Vector2(3f, 5f);
    [Tooltip("Velocidad del vaiven (1 = normal, 0.5 = lento).")]
    public float speed = 0.8f;
    [Tooltip("Inclinacion maxima en grados (0 = sin inclinar).")]
    public float tiltDegrees = 0.5f;
    [Tooltip("Desfase para que dos paneles no se muevan al mismo tiempo.")]
    public float phase = 0f;

    private const float ReferenceWidth = 1280f;

    private RectTransform rt;
    private RectTransform canvasRt;
    private Vector2 basePos;
    private bool ready;

    /// <summary>Posicion "central" del vaiven. Si la cambias, el panel se desliza hacia ahi (lo usa RunCinematic).</summary>
    public Vector2 BasePos
    {
        get { return basePos; }
        set { basePos = value; }
    }

    /// <summary>Llamalo cuando el panel ya esta en su sitio: guarda esa posicion como "centro" del vaiven.</summary>
    public void Capture()
    {
        rt = (RectTransform)transform;
        Canvas c = GetComponentInParent<Canvas>();
        canvasRt = c != null ? (RectTransform)c.rootCanvas.transform : null;
        basePos = rt.anchoredPosition;
        ready = true;
    }

    void LateUpdate()
    {
        if (!ready) return;

        // El movimiento se escala con el Canvas, igual que los paneles
        float k = 1f;
        if (canvasRt != null && canvasRt.rect.width > 1f)
            k = Mathf.Clamp(canvasRt.rect.width / ReferenceWidth, 0.3f, 2f);

        float t = Time.unscaledTime * speed + phase;
        float x = Mathf.Sin(t * 0.7f + 1.3f) * amplitude.x * k;
        float y = Mathf.Sin(t) * amplitude.y * k;
        float rot = Mathf.Sin(t * 0.5f + 0.6f) * tiltDegrees;

        rt.anchoredPosition = basePos + new Vector2(x, y);
        rt.localRotation = Quaternion.Euler(0f, 0f, rot);
    }

    void OnDisable()
    {
        // Si se apaga, el panel vuelve exactamente a su sitio
        if (ready && rt != null)
        {
            rt.anchoredPosition = basePos;
            rt.localRotation = Quaternion.identity;
        }
    }
}