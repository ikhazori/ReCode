using UnityEngine;

/// <summary>
/// Sprites pixel-art generados POR CODIGO (sin imagenes): el corazon "alma" de Undertale,
/// la estrella de guardado (SAVE) y utilidades para dibujar mapas de texto como sprites.
/// Todos usan filtro Point, asi que se ven con pixeles nitidos al escalarlos.
/// </summary>
public static class PixelSprites
{
    private static Sprite heart, star;

    private static readonly string[] HeartMap =
    {
        ".XX...XX.",
        "XXXX.XXXX",
        "XXXXXXXXX",
        "XXXXXXXXX",
        ".XXXXXXX.",
        "..XXXXX..",
        "...XXX...",
        "....X...."
    };

    private static readonly string[] StarMap =
    {
        ".....X.....",
        ".....X.....",
        "....XXX....",
        "....XXX....",
        "...XXXXX...",
        "XXXXXXXXXXX",
        "...XXXXX...",
        "....XXX....",
        "....XXX....",
        ".....X.....",
        ".....X....."
    };

    // Hoja de codigo con la esquina doblada y tres lineas de texto (8x10 px)
    private static readonly string[] FragmentMap =
    {
        "XXXXXX..",
        "X....XX.",
        "X....X.X",
        "X....XXX",
        "X.XXX..X",
        "X......X",
        "X.XXXX.X",
        "X......X",
        "X.XX...X",
        "XXXXXXXX"
    };

    // Monitor con el cursor de consola ">_" (9x9 px)
    private static readonly string[] TerminalMap =
    {
        "XXXXXXXXX",
        "X.......X",
        "X.X.....X",
        "X..X....X",
        "X.X..XXXX",
        "X.......X",
        "XXXXXXXXX",
        "...XXX...",
        "..XXXXX.."
    };

    private static Sprite fragment, terminal;

    /// <summary>Hoja de codigo (8x10 px): el fragmento coleccionable. Mide 1 unidad de ancho.</summary>
    public static Sprite Fragment()
    {
        if (fragment == null) fragment = FromMap(FragmentMap);
        return fragment;
    }

    /// <summary>Monitor de consola (9x9 px): la terminal interactiva. Mide 1 unidad de ancho.</summary>
    public static Sprite Terminal()
    {
        if (terminal == null) terminal = FromMap(TerminalMap);
        return terminal;
    }

    /// <summary>El corazon del alma (9x8 px). Mide 1 unidad de ancho; escalalo con el transform.</summary>
    public static Sprite Heart()
    {
        if (heart == null) heart = FromMap(HeartMap);
        return heart;
    }

    /// <summary>Estrella de cuatro puntas (11x11 px), como el punto de guardado.</summary>
    public static Sprite Star()
    {
        if (star == null) star = FromMap(StarMap);
        return star;
    }

    /// <summary>Convierte un mapa de texto ('X' = pixel blanco, cualquier otra cosa = vacio) en un sprite de 1 unidad de ancho.</summary>
    public static Sprite FromMap(string[] map)
    {
        int h = map.Length;
        int w = map[0].Length;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
                tex.SetPixel(c, h - 1 - r, map[r][c] == 'X' ? Color.white : Color.clear);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
    }
}
