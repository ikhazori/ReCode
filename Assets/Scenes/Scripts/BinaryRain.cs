using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// Lluvia suave de 0 y 1 (tema "code") que cae por detras de todo, muy tenue.
/// La crea AnimatedBackground sola; no hay que ponerla a mano.
/// </summary>
public class BinaryRain : MonoBehaviour
{
    public int columns = 22;
    public float fontSize = 22f;
    public int lines = 40;
    public Vector2 speedRange = new Vector2(30f, 90f);
    public Color color = new Color(0.2f, 1f, 0.4f, 0.12f);

    private class Column
    {
        public TextMeshProUGUI tmp;
        public RectTransform rt;
        public float speed;
        public float y;
    }

    private Column[] cols;
    private RectTransform area;
    private float columnHeight;

    /// <summary>Crea el contenedor dentro del Canvas en la posicion de hermano indicada.</summary>
    public static BinaryRain Create(Transform canvas, int siblingIndex)
    {
        GameObject go = new GameObject("BinaryRain", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        go.transform.SetSiblingIndex(siblingIndex);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return go.AddComponent<BinaryRain>();
    }

    void Start()
    {
        area = (RectTransform)transform;
        columnHeight = lines * fontSize * 1.2f;
        cols = new Column[columns];

        for (int i = 0; i < columns; i++)
        {
            GameObject go = new GameObject("Col" + i, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);

            Column c = new Column();
            c.rt = (RectTransform)go.transform;
            c.rt.anchorMin = new Vector2(0f, 1f);
            c.rt.anchorMax = new Vector2(0f, 1f);
            c.rt.pivot = new Vector2(0.5f, 1f);
            c.rt.sizeDelta = new Vector2(fontSize * 2f, columnHeight);

            c.tmp = go.GetComponent<TextMeshProUGUI>();
            c.tmp.fontSize = fontSize;
            c.tmp.color = color;
            c.tmp.alignment = TextAlignmentOptions.Top;
            c.tmp.raycastTarget = false;
            c.tmp.overflowMode = TextOverflowModes.Overflow;
            c.tmp.text = RandomBits();

            c.speed = Random.Range(speedRange.x, speedRange.y);
            c.y = Random.Range(-Mathf.Max(area.rect.height, 600f), columnHeight);
            cols[i] = c;
        }
    }

    void Update()
    {
        if (cols == null) return;

        float w = area.rect.width;
        float h = area.rect.height;

        for (int i = 0; i < cols.Length; i++)
        {
            Column c = cols[i];
            c.y -= c.speed * Time.deltaTime;

            if (c.y < -h) // ya salio por abajo: vuelve a entrar por arriba con otros digitos
            {
                c.y = columnHeight + Random.Range(0f, 200f);
                c.speed = Random.Range(speedRange.x, speedRange.y);
                c.tmp.text = RandomBits();
            }

            float x = (i + 0.5f) * (w / columns);
            c.rt.anchoredPosition = new Vector2(x, c.y);
        }
    }

    private string RandomBits()
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < lines; i++)
        {
            sb.Append(Random.value < 0.5f ? '0' : '1');
            if (i < lines - 1) sb.Append('\n');
        }
        return sb.ToString();
    }
}
