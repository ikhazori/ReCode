using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Datos de un nivel: el mapa de texto y sus reglas.
/// Simbolos del mapa: S=inicio, G=meta, #=muro, .=libre,
/// ~=hielo (te deslizas hasta chocar o salir del hielo),
/// B=boton (abre/cierra TODAS las puertas D del nivel),
/// D=puerta (empieza cerrada, como un muro, hasta que se presiona un boton),
/// T=teletransportador (se conectan de a pares, en el orden en que aparecen:
/// el primer T con el segundo, el tercero con el cuarto, etc).
/// </summary>
[System.Serializable]
public class LevelData
{
    public string name = "Nivel";
    [TextArea(2, 8)]
    public string[] layout = { "S.......", ".......G", "........" };
    [Tooltip("Maximo de instrucciones que puede armar el jugador. 0 = sin limite.")]
    public int maxInstructions = 0;
    [Tooltip("Frases del guia al ganar este nivel. Si lo dejas vacio, usa las del DialogueController.")]
    [TextArea(2, 4)]
    public string[] guideLines;
}

/// <summary>
/// Un capitulo: un nombre (ej. "Prologo", "Capitulo 1") y su lista de niveles en orden.
/// </summary>
[System.Serializable]
public class ChapterData
{
    public string name = "Capitulo";
    public List<LevelData> levels = new List<LevelData>();
}

/// <summary>
/// Maneja la progresion entre niveles: guarda la lista de capitulos, escucha cuando el
/// LevelRunner completa un nivel (OnLevelComplete), guarda el progreso en PlayerPrefs y
/// carga el siguiente nivel, mostrando antes una pantalla breve con su nombre. Ponlo en
/// cualquier objeto de la escena (por ejemplo "Level"). Con este script en la escena,
/// GridLevel deja de usar su propio 'layout' del Inspector: la lista de aqui abajo manda.
/// El PauseMenuUI usa 'chapters', 'IsLevelUnlocked' e 'IsLevelCompleted' para armar el
/// menu de seleccion de nivel, y 'GoToLevel' para saltar a uno cuando el jugador lo elige.
/// </summary>
public class LevelManager : MonoBehaviour
{
    [Header("Referencias (se buscan solas si las dejas vacias)")]
    public GridLevel level;
    public LevelRunner runner;
    public CommandQueueUI queue;
    [Tooltip("El Canvas principal (donde tambien viven el cuadro de dialogo, etc). Se busca solo si lo dejas vacio.")]
    public Canvas canvas;
    [Tooltip("Se busca solo. Se usa para cortar de golpe la estatica/destellos si saltas de nivel a mitad de un efecto (ej. desde el menu de pausa).")]
    public ScreenEffects effects;
    public AnimatedBackground background;
    [Tooltip("Fragmentos y terminales que se agregan a cada nivel. Se busca solo; si no existe en la escena, los niveles quedan como antes.")]
    public LevelExtras extras;

    [Header("Capitulos y niveles, en orden")]
    public List<ChapterData> chapters = new List<ChapterData>
    {
        new ChapterData
        {
            name = "Prólogo",
            levels = new List<LevelData>
            {
                new LevelData
                {
                    name = "Primeros pasos",
                    layout = new [] { "S.......", ".......G", "........" },
                    maxInstructions = 0,
                    guideLines = new []
                    {
                        "Saludos!",
                        "Me presento, seré tu guía en esta misión.",
                        "¿Que cómo me llamo?",
                        "Es curioso que lo digas...",
                        "......",
                        "Acabas de escribir tu primer algoritmo: pasos en orden.",
                        "Una máquina solo hace lo que le dices. Ni más, ni menos."
                    }
                },
                new LevelData
                {
                    name = "Paredes",
                    layout = new [] { "S....", ".###.", ".....", ".###.", "....G" },
                    maxInstructions = 0,
                    guideLines = new []
                    {
                        "Los muros no están para molestar.",
                        "Están para que pienses el camino ANTES de ejecutarlo.",
                        "Y si algo falla, el sistema rebobina...",
                        "Buscar el error y corregirlo se llama depurar."
                    }
                },
                new LevelData
                {
                    name = "Hielo",
                    layout = new [] { "S....", "....#", ".~~~.", "....G" },
                    maxInstructions = 0,
                    guideLines = new []
                    {
                        "El hielo no te deja parar a mitad de camino.",
                        "Es como repetir una acción hasta que algo la detenga.",
                        "En el código eso se llama bucle.",
                        "Guarda esa palabra. La vas a necesitar."
                    }
                },
                new LevelData
                {
                    name = "Botón y puerta",
                    layout = new [] { "SB..", "###D", "...G" },
                    maxInstructions = 0,
                    guideLines = new []
                    {
                        "Un botón cambia el estado de la puerta.",
                        "Abierta o cerrada. Sí o no.",
                        "En programación, a eso se le llama booleano: verdadero o falso.",
                        "1 o 0. Todo lo que ves aquí está hecho de eso."
                    }
                },
                new LevelData
                {
                    name = "Teletransporte",
                    layout = new [] { "S...T", ".###.", "T...G" },
                    maxInstructions = 0,
                    guideLines = new []
                    {
                        "Apareciste en otro lugar sin recorrer lo de en medio.",
                        "Se parece a una función: la llamas y pasan muchas cosas.",
                        "Una función encierra muchos pasos bajo un solo nombre.",
                        "Pronto vas a crear las tuyas."
                    }
                },
                new LevelData
                {
                    name = "A prueba",
                    layout = new [] { "S....", ".###.", ".....", ".###.", "....G" },
                    maxInstructions = 8,
                    guideLines = new []
                    {
                        "Ocho instrucciones y ni una más.",
                        "No basta con que funcione: tiene que ser eficiente.",
                        "Menos pasos, mismo resultado. Eso es programar bien.",
                        "...Estás aprendiendo rápido. Casi demasiado."
                    }
                },
            }
        },
        new ChapterData
        {
            name = "Capítulo 1",
            levels = new List<LevelData>
            {
                new LevelData
                {
                    name = "(Placeholder)",
                    layout = new [] { "S.......", ".......G", "........" },
                    maxInstructions = 0,
                    guideLines = new []
                    {
                        "(Aquí va el primer nivel donde eliges el script correcto.",
                        " Lo armamos en el siguiente paso.)"
                    }
                },
            }
        },
    };

    [Header("Si se acaban los niveles")]
    [Tooltip("Si esta marcado, al ganar el ultimo nivel del ultimo capitulo se vuelve a empezar desde el primero.")]
    public bool loopLevels = true;

    [Header("Pantalla intermedia (antes de cada nivel)")]
    public float labelFadeTime = 0.3f;
    public float labelHoldTime = 1.1f;
    public float titleFontSize = 42f;
    public float subtitleFontSize = 22f;

    private const string SaveKeyPrefix = "ReCode_Completed_";

    private int currentChapterIndex = -1;
    private int currentLevelIndex = -1;

    public int CurrentChapterIndex => currentChapterIndex;
    public int CurrentLevelIndex => currentLevelIndex;

    void Start()
    {
        if (level == null) level = FindFirstObjectByType<GridLevel>();
        if (runner == null) runner = FindFirstObjectByType<LevelRunner>();
        if (queue == null) queue = FindFirstObjectByType<CommandQueueUI>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (effects == null) effects = FindFirstObjectByType<ScreenEffects>();
        if (background == null) background = FindFirstObjectByType<AnimatedBackground>();
        if (extras == null) extras = FindFirstObjectByType<LevelExtras>();

        if (runner != null) runner.OnLevelComplete.AddListener(OnLevelWon);

        StartCoroutine(GoToLevelRoutine(0, 0));
    }

    // ---------- Progreso guardado (PlayerPrefs) ----------

    /// <summary>El nivel [chapterIndex, levelIndex] ya fue completado alguna vez.</summary>
    public bool IsLevelCompleted(int chapterIndex, int levelIndex)
    {
        return PlayerPrefs.GetInt(SaveKeyPrefix + chapterIndex + "_" + levelIndex, 0) == 1;
    }

    /// <summary>
    /// El nivel [chapterIndex, levelIndex] se puede jugar: es el primero de todos, ya fue
    /// completado antes, o es el siguiente inmediato despues del ultimo completado.
    /// </summary>
    public bool IsLevelUnlocked(int chapterIndex, int levelIndex)
    {
        if (chapterIndex == 0 && levelIndex == 0) return true;
        if (IsLevelCompleted(chapterIndex, levelIndex)) return true;

        int prevChapter = chapterIndex;
        int prevLevel = levelIndex - 1;
        if (prevLevel < 0)
        {
            prevChapter = chapterIndex - 1;
            if (prevChapter < 0) return true;
            List<LevelData> prevLevels = chapters[prevChapter].levels;
            prevLevel = (prevLevels != null ? prevLevels.Count : 1) - 1;
        }
        return IsLevelCompleted(prevChapter, prevLevel);
    }

    private void MarkLevelCompleted(int chapterIndex, int levelIndex)
    {
        PlayerPrefs.SetInt(SaveKeyPrefix + chapterIndex + "_" + levelIndex, 1);
        PlayerPrefs.Save();
    }

    // ---------- Frases del guia ----------

    /// <summary>Las frases del guia del nivel que se esta jugando (null si no hay).</summary>
    public string[] GetCurrentGuideLines()
    {
        LevelData data = GetLevelData(currentChapterIndex, currentLevelIndex);
        return data != null ? data.guideLines : null;
    }

    private LevelData GetLevelData(int chapterIndex, int levelIndex)
    {
        if (chapters == null || chapterIndex < 0 || chapterIndex >= chapters.Count) return null;
        List<LevelData> lv = chapters[chapterIndex].levels;
        if (lv == null || levelIndex < 0 || levelIndex >= lv.Count) return null;
        return lv[levelIndex];
    }

    // ---------- Avance entre niveles ----------

    private void OnLevelWon()
    {
        MarkLevelCompleted(currentChapterIndex, currentLevelIndex);
        StartCoroutine(AdvanceWhenReady());
    }

    // Espera a que el LevelRunner termine TODA su secuencia de victoria (chispas,
    // destello, dialogo, etc.) antes de tirar el nivel actual y armar el siguiente.
    private IEnumerator AdvanceWhenReady()
    {
        yield return new WaitUntil(() => runner == null || !runner.IsBusy);

        int nextChapter = currentChapterIndex;
        int nextLevel = currentLevelIndex + 1;
        List<LevelData> curLevels = chapters[currentChapterIndex].levels;

        if (nextLevel >= curLevels.Count)
        {
            nextChapter++;
            nextLevel = 0;
            if (nextChapter >= chapters.Count)
            {
                if (!loopLevels) yield break;
                nextChapter = 0;
            }
        }
        yield return GoToLevelRoutine(nextChapter, nextLevel);
    }

    /// <summary>Salta directo a un nivel especifico (lo usa el PauseMenuUI al elegir uno en la lista).</summary>
    public void GoToLevel(int chapterIndex, int levelIndex)
    {
        StartCoroutine(GoToLevelRoutine(chapterIndex, levelIndex));
    }

    private IEnumerator GoToLevelRoutine(int chapterIndex, int levelIndex)
    {
        LevelData data = GetLevelData(chapterIndex, levelIndex);
        if (data == null || level == null) yield break;

        // Por si veniamos de un efecto interrumpido (rebobinado, choque) al saltar de
        // nivel desde el menu de pausa: corta todo de golpe para que no quede nada pegado.
        if (effects != null) effects.ForceReset();
        if (background != null) background.EndRewindScroll();
        RewindEffect.ForceReset();

        if (queue != null) queue.Locked = true;
        level.SetVisible(false);

        yield return ShowLabel(chapters[chapterIndex].name.ToUpper() + " - NIVEL " + (levelIndex + 1), data.name);

        level.LoadLevel(data);
        if (extras != null) extras.BuildFor(chapterIndex, levelIndex); // fragmentos y terminales de este nivel
        if (queue != null)
        {
            queue.maxInstructions = data.maxInstructions;
            queue.ClearInstructions();
        }

        level.SetVisible(true);
        if (queue != null) queue.Locked = false;

        currentChapterIndex = chapterIndex;
        currentLevelIndex = levelIndex;
    }

    // ---------- Pantalla intermedia: texto grande al centro, con fundido ----------

    private IEnumerator ShowLabel(string title, string subtitle)
    {
        if (canvas == null) yield break;

        GameObject go = new GameObject("LevelLabel", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = false;

        TextMeshProUGUI titleTmp = CreateLabelText(go.transform, title, titleFontSize, 20f);
        TextMeshProUGUI subTmp = !string.IsNullOrEmpty(subtitle)
            ? CreateLabelText(go.transform, subtitle, subtitleFontSize, -22f)
            : null;

        SetLabelAlpha(bg, titleTmp, subTmp, 0f);
        yield return FadeLabel(bg, titleTmp, subTmp, 0f, 1f, labelFadeTime);
        yield return new WaitForSeconds(labelHoldTime);
        yield return FadeLabel(bg, titleTmp, subTmp, 1f, 0f, labelFadeTime);

        Destroy(go);
    }

    private TextMeshProUGUI CreateLabelText(Transform parent, string text, float size, float offsetY)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        RectTransform rt = tmp.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1400f, 100f);
        rt.anchoredPosition = new Vector2(0f, offsetY);
        return tmp;
    }

    private void SetLabelAlpha(Image bg, TextMeshProUGUI title, TextMeshProUGUI sub, float a)
    {
        bg.color = new Color(0f, 0f, 0f, a * 0.75f);
        if (title != null) title.alpha = a;
        if (sub != null) sub.alpha = a;
    }

    private IEnumerator FadeLabel(Image bg, TextMeshProUGUI title, TextMeshProUGUI sub, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetLabelAlpha(bg, title, sub, Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        SetLabelAlpha(bg, title, sub, to);
    }
}