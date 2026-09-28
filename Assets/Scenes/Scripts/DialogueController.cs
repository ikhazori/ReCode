using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Secuencia de dialogo estilo Undertale: entra un personaje deslizandose desde la
/// izquierda, el cuadro se abre y el texto se escribe letra por letra. Z (o Enter)
/// completa la frase si aun se esta escribiendo, o pasa a la siguiente si ya termino.
/// Al final el cuadro se cierra, el personaje se va y vuelven las opciones.
/// </summary>
public class DialogueController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El BattleBoxManager (se busca solo si lo dejas vacio).")]
    public BattleBoxController battleBox;
    [Tooltip("El objeto UI (Image) del personaje que habla. Debe ser hijo del Canvas.")]
    public RectTransform character;

    [Header("Guion de la victoria (cada elemento = una frase; Z para avanzar)")]
    [TextArea(2, 4)]
    public string[] winLines =
    {
        "Saludos!",
        "Me presento, seré tu guía en esta misión.",
        "¿Que cómo me llamo?",
        "Es curioso que lo digas...",
        "......"
    };

    [Header("Maquina de escribir")]
    [Tooltip("Segundos entre cada letra. Mas bajo = mas rapido.")]
    public float charDelay = 0.04f;
    [Tooltip("Pausa extra despues de . , ! ? (da mas dramatismo a los puntos suspensivos).")]
    public float punctuationDelay = 0.12f;
    [Tooltip("Sonido que suena con cada letra (opcional).")]
    public AudioClip blipSound;
    [Range(0f, 1f)] public float blipVolume = 0.5f;

    [Header("Personaje")]
    [Tooltip("Donde se queda el personaje (coordenadas del Canvas; 0,0 es el centro de la pantalla).")]
    public Vector2 characterPosition = new Vector2(0f, 120f);
    public float slideDuration = 0.6f;
    [Tooltip("Si esta marcado, el personaje se va deslizandose al terminar el dialogo.")]
    public bool characterExits = true;

    private AudioSource audioSource;

    void Awake()
    {
        if (battleBox == null) battleBox = FindFirstObjectByType<BattleBoxController>();

        if (blipSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        if (character != null) character.gameObject.SetActive(false);
    }

    /// <summary>Corre la secuencia completa de dialogo de victoria (usalo con yield return).</summary>
    public IEnumerator PlayWinDialogue()
    {
        yield return PlayDialogue(winLines);
    }

    public IEnumerator PlayDialogue(string[] lines)
    {
        if (battleBox == null || character == null || lines == null) yield break;

        // 1. Entra el personaje desde la izquierda hasta su posicion
        float offscreenX = GetOffscreenX();
        character.anchoredPosition = new Vector2(offscreenX, characterPosition.y);
        character.gameObject.SetActive(true);
        yield return Slide(offscreenX, characterPosition.x);

        // 2. El cuadro se cierra, cambia a modo mensaje y se abre
        yield return battleBox.CloseBox();
        battleBox.ShowMessageContent("");
        yield return battleBox.OpenBox();

        // 3. Cada frase: se escribe, y Z para pasar a la siguiente
        foreach (string line in lines)
        {
            yield return TypeLine(line);
            yield return WaitForConfirm();
        }

        // 4. El cuadro se cierra y vuelve a las opciones; el personaje se va
        yield return battleBox.CloseBox();
        battleBox.ShowOptionsContent();
        if (characterExits)
        {
            yield return Slide(characterPosition.x, offscreenX);
        }
        character.gameObject.SetActive(false);
        yield return battleBox.OpenBox();
    }

    private IEnumerator TypeLine(string line)
    {
        TMP_Text tmp = battleBox.messageText;
        tmp.text = line;
        tmp.maxVisibleCharacters = 0;
        tmp.ForceMeshUpdate();
        int total = tmp.textInfo.characterCount;

        for (int i = 1; i <= total; i++)
        {
            tmp.maxVisibleCharacters = i;
            char c = tmp.textInfo.characterInfo[i - 1].character;
            if (!char.IsWhiteSpace(c)) PlayBlip();

            float wait = charDelay + (IsPause(c) ? punctuationDelay : 0f);
            float timer = 0f;
            while (timer < wait)
            {
                if (ConfirmPressed())
                {
                    // Z mientras se escribe: mostrar la frase completa de golpe
                    tmp.maxVisibleCharacters = total;
                    yield return null; // consume esta pulsacion para que no salte la siguiente frase
                    yield break;
                }
                timer += Time.deltaTime;
                yield return null;
            }
        }
    }

    private IEnumerator WaitForConfirm()
    {
        while (!ConfirmPressed()) yield return null;
        yield return null; // consume la pulsacion
    }

    private IEnumerator Slide(float fromX, float toX)
    {
        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / slideDuration));
            character.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, k), characterPosition.y);
            yield return null;
        }
        character.anchoredPosition = new Vector2(toX, characterPosition.y);
    }

    // X (en coordenadas del Canvas) donde el personaje queda completamente fuera de pantalla por la izquierda
    private float GetOffscreenX()
    {
        RectTransform root = character.root as RectTransform;
        float halfScreen = root != null ? root.rect.width / 2f : 400f;
        return -(halfScreen + character.rect.width / 2f + 20f);
    }

    private static bool ConfirmPressed()
    {
        return Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return);
    }

    private static bool IsPause(char c)
    {
        return c == '.' || c == ',' || c == '!' || c == '?' || c == '…';
    }

    private void PlayBlip()
    {
        if (audioSource == null || blipSound == null) return;
        audioSource.pitch = Random.Range(0.95f, 1.05f);
        audioSource.PlayOneShot(blipSound, blipVolume);
    }
}