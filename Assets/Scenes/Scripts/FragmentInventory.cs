using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Los 5 "fragmentos de codigo" que el jugador recupera a lo largo del Prologo. Cada uno
/// es un concepto de pseudocodigo (algoritmo, depurar, bucle, booleano, funcion) y es el
/// mismo concepto que explica el guia en el nivel donde aparece.
///
/// Es una clase estatica: no va en ningun objeto de la escena.
///
/// Regla de oro (la que hace que se sienta como ReCode):
///   - Al pasar por la casilla de un fragmento, queda "pendiente" (se ve en el HUD).
///   - Si ganas el nivel, los pendientes se GUARDAN para siempre (PlayerPrefs).
///   - Si chocas o no llegas a la meta, el sistema rebobina y los pendientes se devuelven.
/// Asi un fragmento nunca se cuenta dos veces (prueba P3) y el rebobinado tiene sentido.
/// </summary>
public static class FragmentInventory
{
    public class Fragment
    {
        public string id;          // clave interna (la que escribes en LevelExtras)
        public string title;       // nombre largo, ej. "BUCLE"
        public string shortName;   // 3 letras para el HUD, ej. "BUC"
        public string definition;  // frase educativa que sale al recogerlo
        public Color color;        // color del icono
    }

    public static readonly Fragment[] All =
    {
        new Fragment { id = "algoritmo", title = "ALGORITMO", shortName = "ALG",
            definition = "Lista de pasos, en orden, para resolver un problema.",
            color = new Color(1f, 0.55f, 0.1f) },
        new Fragment { id = "depurar", title = "DEPURAR", shortName = "DEP",
            definition = "Buscar y corregir los errores de un programa.",
            color = new Color(1f, 0.35f, 0.4f) },
        new Fragment { id = "bucle", title = "BUCLE", shortName = "BUC",
            definition = "Repetir una acción hasta que algo la detenga.",
            color = new Color(0.4f, 0.85f, 1f) },
        new Fragment { id = "booleano", title = "BOOLEANO", shortName = "BOO",
            definition = "Un valor que solo puede ser verdadero o falso.",
            color = new Color(1f, 0.9f, 0.25f) },
        new Fragment { id = "funcion", title = "FUNCIÓN", shortName = "FUN",
            definition = "Muchos pasos guardados bajo un solo nombre.",
            color = new Color(0.8f, 0.5f, 1f) },
    };

    private const string KeyPrefix = "ReCode_Fragment_";
    private const string MissionKey = "ReCode_MissionDone";

    private static readonly HashSet<string> pending = new HashSet<string>();

    /// <summary>Id del ultimo fragmento que se recogio (lo usa el HUD para su aviso).</summary>
    public static string LastCollected { get; private set; }

    // Por si en el editor tienes desactivado "Reload Domain": limpia el estado al dar Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pending.Clear();
        LastCollected = null;
    }

    public static int Total => All.Length;

    public static Fragment Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < All.Length; i++)
            if (All[i].id == id) return All[i];
        return null;
    }

    /// <summary>Fragmentos ya guardados (ganaste el nivel donde estaban).</summary>
    public static int CommittedCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < All.Length; i++) if (IsCommitted(All[i].id)) n++;
            return n;
        }
    }

    /// <summary>Guardados + pendientes de la secuencia en curso. Es lo que muestra el HUD.</summary>
    public static int Count => CommittedCount + pending.Count;

    public static bool IsCommitted(string id) => PlayerPrefs.GetInt(KeyPrefix + id, 0) == 1;
    public static bool IsPending(string id) => pending.Contains(id);
    public static bool IsOwned(string id) => IsCommitted(id) || IsPending(id);

    /// <summary>true cuando ya guardaste los 5 fragmentos.</summary>
    public static bool MissionComplete => CommittedCount >= Total;

    /// <summary>true si ya se mostro el panel de "mision cumplida" (para no repetirlo cada vez).</summary>
    public static bool MissionCelebrated
    {
        get { return PlayerPrefs.GetInt(MissionKey, 0) == 1; }
        set { PlayerPrefs.SetInt(MissionKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>Lo recoge (pendiente). Devuelve false si no existe o ya lo tenias: asi nunca se cuenta doble.</summary>
    public static bool AddPending(string id)
    {
        if (Get(id) == null || IsOwned(id)) return false;
        pending.Add(id);
        LastCollected = id;
        return true;
    }

    public static bool RemovePending(string id)
    {
        return pending.Remove(id);
    }

    /// <summary>Ganaste el nivel: los pendientes se guardan. Devuelve cuantos eran.</summary>
    public static int CommitPending()
    {
        int n = pending.Count;
        if (n == 0) return 0;
        foreach (string id in pending) PlayerPrefs.SetInt(KeyPrefix + id, 1);
        pending.Clear();
        PlayerPrefs.Save();
        return n;
    }

    /// <summary>Chocaste o no llegaste a la meta: los pendientes se devuelven.</summary>
    public static void DiscardPending()
    {
        pending.Clear();
    }

    /// <summary>"Nuevo juego": borra todo.</summary>
    public static void ResetAll()
    {
        pending.Clear();
        LastCollected = null;
        for (int i = 0; i < All.Length; i++) PlayerPrefs.DeleteKey(KeyPrefix + All[i].id);
        PlayerPrefs.DeleteKey(MissionKey);
        PlayerPrefs.Save();
    }
}
