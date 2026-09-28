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

    private Image flash;

    void Awake()
    {
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (cam == null) cam = Camera.main;

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