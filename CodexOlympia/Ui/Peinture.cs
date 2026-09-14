using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace CodexOlympia.Ui;

/// <summary>
/// Ce qui se dessine à la main, sous les éléments d'ImGui : des rectangles
/// arrondis, des dégradés, des arcs, des lueurs.
///
/// ImGui ne sait pas dessiner une carte comme le fait un navigateur ; on la
/// dessine donc soi-même dans la liste de tracé de la fenêtre, et on avance
/// ensuite le curseur de la même hauteur. C'est la recette de tous les plugins
/// qui ont une belle fenêtre, et c'est moins compliqué que ça en a l'air : une
/// carte, c'est un rectangle plein, un trait clair en haut, un contour.
/// </summary>
internal static class Peinture
{
    public static float Echelle => ImGuiHelpers.GlobalScale;

    public static uint Col(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    public static void Plein(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 c, float rond,
        ImDrawFlags coins = ImDrawFlags.RoundCornersAll)
        => dl.AddRectFilled(min, max, Col(c), rond, coins);

    public static void Contour(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 c, float rond,
        float epaisseur = 1f, ImDrawFlags coins = ImDrawFlags.RoundCornersAll)
        => dl.AddRect(min, max, Col(c), rond, coins, epaisseur);

    public static void Filet(ImDrawListPtr dl, Vector2 a, Vector2 b)
        => dl.AddLine(a, b, Col(Teintes.Filet), 1f);

    /// <summary>Le trait clair du haut d'une carte : ce qui lui donne du
    /// relief sans ombre. Une ligne à un pixel, presque transparente.</summary>
    public static void Lumiere(ImDrawListPtr dl, Vector2 min, Vector2 max, float rond, float a = 0.05f)
        => dl.AddLine(
            new Vector2(min.X + rond, min.Y + 1f),
            new Vector2(max.X - rond, min.Y + 1f),
            Col(new Vector4(1f, 1f, 1f, a)),
            1f);

    /// <summary>Une carte de l'application : le fond, le trait clair, le
    /// contour d'un cheveu.</summary>
    public static void Carte(ImDrawListPtr dl, Vector2 min, Vector2 max, float rond,
        Vector4? fond = null, Vector4? bord = null, bool lumiere = true)
    {
        Plein(dl, min, max, fond ?? Teintes.Surface, rond);
        if (lumiere) Lumiere(dl, min, max, rond);
        Contour(dl, min, max, bord ?? Teintes.Filet, rond);
    }

    /// <summary>
    /// Un dégradé vertical dans un rectangle arrondi.
    ///
    /// ImGui a bien un rectangle à quatre couleurs, mais il ne sait pas
    /// l'arrondir. On peint donc le rectangle arrondi en blanc, puis on
    /// recolore les sommets qu'il vient d'écrire : c'est la recette d'ImGui
    /// lui-même pour ses dégradés.
    /// </summary>
    public static void Degrade(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 haut, Vector4 bas,
        float rond, ImDrawFlags coins = ImDrawFlags.RoundCornersAll)
    {
        var debut = dl.VtxBuffer.Size;
        dl.AddRectFilled(min, max, Col(new Vector4(1f, 1f, 1f, haut.W)), rond, coins);
        var fin = dl.VtxBuffer.Size;
        ImGuiP.ShadeVertsLinearColorGradientKeepAlpha(
            dl, debut, fin, min, new Vector2(min.X, max.Y), Opaque(haut), Opaque(bas));
    }

    private static uint Opaque(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c with { W = 1f });

    /// <summary>Une lueur autour d'une carte : trois rectangles de plus en
    /// plus larges et de plus en plus pâles. Pas de flou dans ImGui, mais
    /// l'empilement en donne l'idée.</summary>
    public static void Lueur(ImDrawListPtr dl, Vector2 min, Vector2 max, float rond, Vector4 c, float force = 1f)
    {
        for (var couche = 3; couche >= 1; couche--)
        {
            var marge = couche * 3f * Echelle;
            var a = 0.04f * (4 - couche) * force;
            dl.AddRectFilled(min - new Vector2(marge), max + new Vector2(marge),
                Col(Teintes.Alpha(c, a)), rond + marge);
        }
    }

    /// <summary>Une tache de couleur très diluée, posée au fond de la fenêtre :
    /// cinq cercles concentriques de plus en plus pâles.</summary>
    public static void Tache(ImDrawListPtr dl, Vector2 centre, float rayon, Vector4 c, float sommet)
    {
        for (var couche = 5; couche >= 1; couche--)
        {
            var r = rayon * couche / 5f;
            var a = sommet * (1f - (couche - 1f) / 5f);
            dl.AddCircleFilled(centre, r, Col(Teintes.Alpha(c, a)), 48);
        }
    }

    /// <summary>
    /// Une image qui remplit sa boîte sans se déformer : on rogne ce qui
    /// dépasse, comme le fait le site. Le portrait découpé d'un personnage et
    /// son avatar rond n'ont pas les mêmes proportions ; les étirer l'un ou
    /// l'autre se verrait tout de suite.
    /// </summary>
    public static void ImageCouvrante(ImDrawListPtr dl, ImTextureID image, float largeurImage, float hauteurImage,
        Vector2 min, Vector2 max, float rond)
    {
        var boite = (max.X - min.X) / MathF.Max(1f, max.Y - min.Y);
        var source = largeurImage / MathF.Max(1f, hauteurImage);
        var u0 = Vector2.Zero;
        var u1 = Vector2.One;
        if (source > boite)
        {
            // Trop large : on garde le milieu.
            var part = boite / source;
            u0.X = (1f - part) * 0.5f;
            u1.X = 1f - u0.X;
        }
        else if (source < boite)
        {
            // Trop haute : on garde le haut, c'est là qu'est le visage.
            var part = source / boite;
            u1.Y = part;
        }
        dl.AddImageRounded(image, min, max, u0, u1, Col(Vector4.One), rond, ImDrawFlags.RoundCornersAll);
    }

    // ------------------------------------------------------------- les jauges

    /// <summary>Une barre en gélule : le creux, puis le plein.</summary>
    public static void Barre(ImDrawListPtr dl, Vector2 origine, float largeur, float hauteur, float part, Vector4 c)
    {
        var rond = hauteur * 0.5f;
        var fin = origine + new Vector2(largeur, hauteur);
        Plein(dl, origine, fin, Teintes.Filet, rond);
        part = Math.Clamp(part, 0f, 1f);
        if (part <= 0f) return;
        // Jamais plus étroit qu'un bout arrondi, sinon la gélule se pince.
        var l = MathF.Max(hauteur, largeur * part);
        Degrade(dl, origine, new Vector2(origine.X + l, fin.Y), Teintes.Eclaircir(c, 0.2f), c, rond);
    }

    /// <summary>
    /// Un arc, tracé segment par segment, avec un bout rond à chaque extrémité.
    ///
    /// On pourrait demander l'arc à ImGui ; le tracer soi-même donne une
    /// épaisseur constante et des bouts ronds, exactement comme les anneaux du
    /// site, qui sont des cercles SVG à bout rond.
    /// </summary>
    public static void Arc(ImDrawListPtr dl, Vector2 centre, float rayon, float epaisseur,
        float depuis, float jusqua, Vector4 c)
    {
        var couleur = Col(c);
        var ecart = MathF.Abs(jusqua - depuis);
        var pas = Math.Max(2, (int)MathF.Ceiling(ecart / (MathF.PI / 48f)));
        var precedent = centre + Sens(depuis) * rayon;
        for (var i = 1; i <= pas; i++)
        {
            var angle = depuis + (jusqua - depuis) * (i / (float)pas);
            var courant = centre + Sens(angle) * rayon;
            dl.AddLine(precedent, courant, couleur, epaisseur);
            precedent = courant;
        }
        var bout = epaisseur * 0.5f;
        dl.AddCircleFilled(centre + Sens(depuis) * rayon, bout, couleur);
        dl.AddCircleFilled(centre + Sens(jusqua) * rayon, bout, couleur);
    }

    /// <summary>Midi : les anneaux du site partent du haut (rotation -90°).</summary>
    public const float Midi = -MathF.PI / 2f;

    private static Vector2 Sens(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));

    /// <summary>L'anneau du site : le creux entier, puis la part faite.</summary>
    public static void Anneau(ImDrawListPtr dl, Vector2 centre, float rayon, float epaisseur, float part, Vector4 c)
    {
        Arc(dl, centre, rayon, epaisseur, Midi, Midi + MathF.PI * 2f, Teintes.Filet);
        part = Math.Clamp(part, 0f, 1f);
        if (part <= 0.0001f) return;
        Arc(dl, centre, rayon, epaisseur, Midi, Midi + part * MathF.PI * 2f, c);
    }

    /// <summary>Une comète qui tourne : ce qui dit « ça travaille » sans
    /// chiffre. La traîne s'éteint derrière la tête.</summary>
    public static void Comete(ImDrawListPtr dl, Vector2 centre, float rayon, float epaisseur, Vector4 c,
        double periodeMs = 1400)
    {
        var tete = Midi + Mouvement.Phase(periodeMs) * MathF.PI * 2f;
        var queue = tete - MathF.PI * 0.55f;
        var pas = 14;
        var precedent = centre + Sens(queue) * rayon;
        for (var i = 1; i <= pas; i++)
        {
            var t = i / (float)pas;
            var courant = centre + Sens(queue + (tete - queue) * t) * rayon;
            dl.AddLine(precedent, courant, Col(Teintes.Alpha(c, t * t)), epaisseur);
            precedent = courant;
        }
        dl.AddCircleFilled(centre + Sens(tete) * rayon, epaisseur * 0.62f, Col(c));
    }
}
