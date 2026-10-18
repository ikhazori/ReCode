using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animacion al ejecutar el camino: mientras el corazon recorre la cuadricula,
///   - se esconden (fundido) el cuadro de opciones (Arriba/Abajo/Izquierda/Derecha) y el cuadro de flechas,
///   - la camara se acerca y centra el tablero para que se vea mejor el recorrido,
///   - el pseudocodigo queda al lado derecho del tablero y el HUD de fragmentos se acomoda debajo de el.
/// Cuando termina (o antes del dialogo del guia) todo vuelve a su sitio con una animacion suave.
///
/// Uso: ponlo en el mismo objeto que LevelManager ("Level"). LevelRunner lo encuentra solo.
/// Si no te gusta, desmarca "Use Cinematic" o quita el componente: el juego queda como antes.
/// </summary>
// Se ejecuta de ultimo en cada cuadro: asi, si otro script tambien mueve la camara, esta animacion tiene la ultima palabra.
[DefaultExecutionOrder(32000)]
public class RunCinematic : MonoBehaviour
{
    [Header("Activar / desactivar")]
    public bool useCinematic = true;

    [Header("Referencias (se buscan solas)")]
    public GridLevel level;
    public Camera cam;

    [Header("Objetos del Canvas que se esconden mientras corre la secuencia (por nombre)")]
    public string[] hideObjectNames = { "DialogueBox", "DialogueBoxBorder", "CommandQueue", "InstructionPanel" };

    [Header("Animacion")]
    [Tooltip("Segundos que tarda en acercarse el tablero antes de empezar a moverse.")]
    public float enterDuration = 0.55f;
    [Tooltip("Segundos que tarda en volver todo a su sitio.")]
    public float exitDuration = 0.45f;
    [Tooltip("Que tan grande queda el tablero dentro del espacio libre (1 = justo, menos = con mas aire).")]
    [Range(0.6f, 1f)] public float fillFraction = 0.94f;
    [Tooltip("Altura del centro del tablero en pantalla (0.5 = justo al medio).")]
    [Range(0.35f, 0.65f)] public float verticalCenter = 0.5f;
    [Tooltip("Espacio libre a la izquierda del tablero (unidades del Canvas).")]
    public float leftPadding = 24f;
    [Tooltip("Espacio entre el tablero y el pseudocodigo (unidades del Canvas).")]
    public float panelGap = 22f;
    [Tooltip("Espacio entre el pseudocodigo y el HUD de fragmentos (unidades del Canvas).")]
    public float hudGap = 12f;

    private class Fade { public CanvasGroup group; public float original; }

    private readonly List<Fade> fades = new List<Fade>();
    private RectTransform canvasRt, hudRt, codeRt;
    private PanelFloat hudFloat;
    private PseudocodePanel codePanel;

    private bool zoomCamera;
    private float camSize0, camSizeT;
    private Vector3 camPos0, camPosT;
    private Vector2 hudBase0, hudBaseT;
    private bool active;
    private bool menusHeld;
    private float currentE;
    private float currentFadeE;
    private Vector3 lastCamPos;
    private bool warnedCamera;

    /// <summary>true mientras la animacion esta aplicada (o en transicion).</summary>
    public bool IsActive { get { return active; } }

    // ---------------------------------------------------------------- entrar / salir

    /// <summary>Esconde menus, acerca el tablero y acomoda los paneles. LevelRunner la espera antes de mover al corazon.</summary>
    public IEnumerator Enter()
    {
        if (!useCinematic || active) yield break;
        if (menusHeld) { SetFades(0f); menusHeld = false; } // por si seguian escondidos: se restauran antes de guardar los valores
        if (!Prepare()) yield break;

        active = true;
        yield return Transition(0f, 1f, enterDuration, false);
    }

    /// <summary>
    /// Devuelve todo a su sitio. Si no estaba activa no hace nada, asi que se puede llamar sin miedo.
    /// Con keepMenusHidden = true, la camara y los paneles vuelven pero los cuadros de abajo siguen escondidos
    /// (los muestra DialogueController con ShowMenus cuando el guia ya va a hablar).
    /// </summary>
    public IEnumerator Exit(bool keepMenusHidden = false)
    {
        if (!active)
        {
            if (menusHeld) ShowMenus(); // por si quedaron escondidos y nadie los volvio a mostrar
            yield break;
        }

        yield return Transition(1f, 0f, exitDuration, keepMenusHidden);
        active = false;
        menusHeld = keepMenusHidden;
    }

    /// <summary>Muestra con un fundido los cuadros que Exit(true) dejo escondidos.</summary>
    public void ShowMenus()
    {
        if (!menusHeld) return;
        menusHeld = false;
        StartCoroutine(FadeMenusIn(0.25f));
    }

    private IEnumerator FadeMenusIn(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetFades(1f - Mathf.Clamp01(t / duration));
            yield return null;
        }
        SetFades(0f);
    }

    private IEnumerator Transition(float from, float to, float duration, bool holdFades = false)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            float e = Mathf.Lerp(from, to, k);
            Apply(e, holdFades ? 1f : e);
            yield return null;
        }
        Apply(to, holdFades ? 1f : to);
    }

    // e = 0 (todo normal) .. 1 (todo en modo ejecucion)
    private void SetFades(float e)
    {
        for (int i = 0; i < fades.Count; i++)
        {
            if (fades[i].group != null)
                fades[i].group.alpha = Mathf.Lerp(fades[i].original, 0f, e);
        }
    }

    // ---------------------------------------------------------------- aplicar

    // Se repite en LateUpdate (despues de todos los demas scripts) para que nadie le gane a la animacion.
    void LateUpdate()
    {
        if (active && zoomCamera && cam != null) ApplyCamera(currentE);

        // Los cuadros escondidos tambien se reafirman al final del cuadro, por si otro script les cambia la transparencia
        if (active || menusHeld) SetFades(currentFadeE);
    }

    private void ApplyCamera(float e)
    {
        Transform ct = cam.transform;

        // Aviso (una sola vez) si otro script movio la camara por su cuenta
        if (!warnedCamera && Vector3.Distance(ct.position, lastCamPos) > 0.001f)
        {
            warnedCamera = true;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (MonoBehaviour mb in cam.GetComponentsInParent<MonoBehaviour>())
                sb.Append(mb.GetType().Name).Append(' ');
            Debug.LogWarning("[RunCinematic] Otro script esta moviendo la camara mientras corre la animacion. Scripts en la camara (y sus padres): " + sb);
        }

        cam.orthographicSize = Mathf.Lerp(camSize0, camSizeT, e);
        ct.position = Vector3.Lerp(camPos0, camPosT, e);
        lastCamPos = ct.position;
    }

    private void Apply(float e, float fadeE)
    {
        currentE = e;
        currentFadeE = fadeE;

        SetFades(fadeE);

        if (zoomCamera && cam != null) ApplyCamera(e);

        if (hudFloat != null)
            hudFloat.BasePos = Vector2.Lerp(hudBase0, hudBaseT, e);
    }

    // ---------------------------------------------------------------- preparacion

    private bool Prepare()
    {
        if (level == null) level = FindFirstObjectByType<GridLevel>();
        if (cam == null) cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (codePanel == null) codePanel = FindFirstObjectByType<PseudocodePanel>();
        if (level == null || cam == null) return false;

        // Canvas principal: el mismo que usan el HUD y el pseudocodigo
        Canvas canvas = null;
        if (GameHUDManager.Instance != null) canvas = GameHUDManager.Instance.canvas;
        if (canvas == null && codePanel != null) canvas = codePanel.canvas;
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return false;

        Canvas root = canvas.rootCanvas;
        canvasRt = (RectTransform)root.transform;
        if (canvasRt.rect.width <= 1f) return false;

        hudRt = root.transform.Find("HUD") as RectTransform;
        codeRt = root.transform.Find("PseudocodePanel") as RectTransform;

        PrepareFades(root);
        PrepareCamera();
        PrepareHud();

        return true;
    }

    private void PrepareFades(Canvas root)
    {
        fades.Clear();
        RectTransform[] all = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (string n in hideObjectNames)
        {
            bool found = false;
            foreach (RectTransform rt in all)
            {
                if (rt.name != n) continue;
                // Ya no se exige que sea del mismo Canvas que el HUD: en un .exe el orden en que se
                // encuentran los Canvas puede ser distinto al del editor y los cuadros no se escondian.
                if (rt.GetComponentInParent<Canvas>(true) == null) continue;

                found = true;
                CanvasGroup g = rt.GetComponent<CanvasGroup>();
                if (g == null) g = rt.gameObject.AddComponent<CanvasGroup>();
                fades.Add(new Fade { group = g, original = g.alpha });
            }
            if (!found)
                Debug.LogWarning("[RunCinematic] No encontre ningun objeto llamado '" + n + "' en el Canvas. Revisa el nombre en la lista \"Hide Object Names\".");
        }
    }

    private void PrepareCamera()
    {
        zoomCamera = false;
        warnedCamera = false;
        lastCamPos = cam.transform.position;
        if (!cam.orthographic)
        {
            Debug.LogWarning("[RunCinematic] La camara no es ortografica: no se puede acercar el tablero.");
            return;
        }

        float W = canvasRt.rect.width;
        float k = Mathf.Clamp(W / 1280f, 0.3f, 2f);

        // Espacio libre en pantalla (en unidades del Canvas): desde la izquierda hasta el pseudocodigo
        float codeW = 290f * k;
        float codeMargin = 14f;
        if (codeRt != null) codeW = codeRt.sizeDelta.x * codeRt.localScale.x;
        if (codePanel != null) codeMargin = codePanel.margin.x;

        float leftEdge = leftPadding;
        float rightEdge = W - codeMargin - codeW - panelGap;
        float freeW = Mathf.Max(rightEdge - leftEdge, W * 0.3f);
        float fw = freeW / W;                               // fraccion de pantalla disponible
        float cx = (leftEdge + leftEdge + freeW) * 0.5f / W; // donde queda el centro del tablero (0..1)

        // Tamaño real del tablero en el mundo (casillas + marco)
        float outer = level.framePadding + level.frameThickness;
        float gw = level.Columns * level.cellSize + 2f * outer;
        float gh = level.Rows * level.cellSize + 2f * outer;

        float aspect = cam.aspect;
        camSize0 = cam.orthographicSize;
        camPos0 = cam.transform.position;

        float sW = gw / (2f * aspect * fw * fillFraction);
        float sH = gh / (2f * 0.8f);
        camSizeT = Mathf.Min(Mathf.Max(sW, sH), camSize0); // solo acercar, nunca alejar

        // Posicion de camara para que el centro del tablero caiga en (cx, verticalCenter) de la pantalla
        Vector2 gc = level.gridCenter;
        float camX = gc.x - (cx - 0.5f) * 2f * camSizeT * aspect;
        float camY = gc.y - (verticalCenter - 0.5f) * 2f * camSizeT;
        camPosT = new Vector3(camX, camY, camPos0.z);

        zoomCamera = true;
    }

    private void PrepareHud()
    {
        hudFloat = null;
        if (hudRt == null) return;

        hudFloat = hudRt.GetComponent<PanelFloat>();
        if (hudFloat == null)
        {
            // El HUD no flota (Float Effect apagado): le ponemos un PanelFloat quieto solo para poder deslizarlo
            hudFloat = hudRt.gameObject.AddComponent<PanelFloat>();
            hudFloat.amplitude = Vector2.zero;
            hudFloat.tiltDegrees = 0f;
            hudFloat.Capture();
        }

        hudBase0 = hudFloat.BasePos;

        // Columna derecha: pseudocodigo arriba y el HUD justo debajo, centrado respecto a el
        float W = canvasRt.rect.width;
        float hudW = hudRt.sizeDelta.x * hudRt.localScale.x;
        float codeW = hudW, codeH = 0f, codeMargin = 14f, codeTop = 14f;
        if (codeRt != null)
        {
            codeW = codeRt.sizeDelta.x * codeRt.localScale.x;
            codeH = codeRt.sizeDelta.y * codeRt.localScale.y;
        }
        if (codePanel != null) { codeMargin = codePanel.margin.x; codeTop = codePanel.margin.y; }

        float x = W - codeMargin - codeW + (codeW - hudW) * 0.5f;
        float y = -(codeTop + codeH + hudGap);
        hudBaseT = new Vector2(x, y);
    }
}