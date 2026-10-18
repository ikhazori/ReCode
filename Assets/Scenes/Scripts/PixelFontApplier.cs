using UnityEngine;
using TMPro;

/// <summary>
/// Cambia la fuente de TODOS los textos TextMeshPro del juego (incluidos los que se crean
/// por codigo: menus, dialogos, pantalla de nivel...) a una fuente pixel-art.
/// Uso: ponlo en el objeto "Level" y arrastra tu TMP Font Asset al campo "Font".
/// </summary>
public class PixelFontApplier : MonoBehaviour
{
    [Tooltip("Tu fuente pixel convertida a TMP Font Asset (Create > TextMeshPro > Font Asset).")]
    public TMP_FontAsset font;
    [Tooltip("Cada cuantos segundos se revisan textos nuevos (ademas de reaccionar al instante cuando cambia un texto).")]
    public float scanInterval = 0.5f;

    private float timer;

    void Awake()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        ApplyAll();
    }

    void OnDestroy()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer >= scanInterval)
        {
            timer = 0f;
            ApplyAll();
        }
    }

    private void OnTextChanged(Object obj)
    {
        if (font == null) return;
        TMP_Text t = obj as TMP_Text;
        if (t != null && t.font != font) t.font = font;
    }

    private void ApplyAll()
    {
        if (font == null) return;
        TMP_Text[] all = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].font != font) all[i].font = font;
        }
    }
}
