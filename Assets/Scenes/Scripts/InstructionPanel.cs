using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel con borde blanco y fondo negro (mismo estilo del cuadro de dialogo) donde viven
/// los iconos de las instrucciones. Dibuja su propio marco, acomoda la fila de iconos
/// dentro y se abre/cierra por los lados segun haya instrucciones o no.
/// </summary>
[ExecuteAlways]
public class InstructionPanel : MonoBehaviour
{
    [Header("Contenido")]
    [Tooltip("El objeto 'InstructionRow' (donde CommandQueueUI pone los iconos). Se mueve solo dentro del panel al dar Play.")]
    public RectTransform iconRow;
    [Tooltip("Se busca solo si lo dejas vacio.")]
    public CommandQueueUI queue;

    [Header("Apariencia")]
    public Color borderColor = Color.white;
    public Color fillColor = Color.black;
    [Tooltip("Grosor del borde blanco.")]
    public float borderThickness = 4f;
    [Tooltip("Margen entre el borde y los iconos.")]
    public float padding = 10f;
    [Tooltip("Separacion entre un icono y el siguiente.")]
    public float iconSpacing = 6f;

    [Header("Aparicion")]
    [Tooltip("Si esta marcado, el panel esta cerrado hasta que hay al menos una instruccion. Desmarcalo para que siempre este visible.")]
    public bool hideWhenEmpty = true;
    [Tooltip("Duracion de la animacion de abrir/cerrar (segundos).")]
    public float animDuration = 0.12f;

    private RectTransform fill;
    private float currentScaleX = 1f;

    void Awake()
    {
        BuildFrame();
    }

    void Start()
    {
        if (!Application.isPlaying) return;

        if (queue == null) queue = FindFirstObjectByType<CommandQueueUI>();
        SetupRow();

        if (hideWhenEmpty && (queue == null || queue.Instructions.Count == 0))
        {
            currentScaleX = 0f;
            ApplyScale();
        }
    }

    void Update()
    {
        if (!Application.isPlaying || queue == null) return;

        float target = (!hideWhenEmpty || queue.Instructions.Count > 0) ? 1f : 0f;
        if (!Mathf.Approximately(currentScaleX, target))
        {
            currentScaleX = Mathf.MoveTowards(currentScaleX, target, Time.deltaTime / Mathf.Max(0.01f, animDuration));
            ApplyScale();
        }
    }

#if UNITY_EDITOR
    // Al cambiar colores/grosor en el Inspector, el marco se actualiza al instante.
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) ApplyStyle();
        };
    }
#endif

    private void ApplyScale()
    {
        Vector3 s = transform.localScale;
        s.x = currentScaleX;
        transform.localScale = s;
    }

    // ---------- Construccion del marco ----------

    private void BuildFrame()
    {
        RectTransform border = GetOrCreateChild("Border");
        fill = GetOrCreateChild("Fill");
        border.SetSiblingIndex(0);
        fill.SetSiblingIndex(1);
        ApplyStyle();
    }

    private RectTransform GetOrCreateChild(string childName)
    {
        Transform existing = transform.Find(childName);
        if (existing != null) return (RectTransform)existing;

        GameObject go = new GameObject(childName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        go.GetComponent<Image>().raycastTarget = false;
        return (RectTransform)go.transform;
    }

    private void ApplyStyle()
    {
        Transform border = transform.Find("Border");
        Transform fillT = transform.Find("Fill");
        if (border == null || fillT == null) return;

        StyleImage((RectTransform)border, borderColor, 0f);
        StyleImage((RectTransform)fillT, fillColor, borderThickness);
    }

    private static void StyleImage(RectTransform rt, Color color, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);

        Image img = rt.GetComponent<Image>();
        if (img != null) img.color = color;
    }

    // Mete la fila de iconos dentro del panel y la acomoda
    private void SetupRow()
    {
        if (iconRow == null || fill == null) return;

        iconRow.SetParent(fill, false);
        iconRow.anchorMin = Vector2.zero;
        iconRow.anchorMax = Vector2.one;
        iconRow.offsetMin = new Vector2(padding, padding);
        iconRow.offsetMax = new Vector2(-padding, -padding);
        iconRow.localScale = Vector3.one;

        HorizontalLayoutGroup layout = iconRow.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = iconRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = iconSpacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }
}
