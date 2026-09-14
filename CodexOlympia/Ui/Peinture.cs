using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace CodexOlympia.Ui;

/// <summary>
/// Le dessin à la main, dans la liste de tracé de la fenêtre.
///
/// ImGui ne sait pas dessiner une carte : on la dessine soi-même, puis on
/// avance le curseur de la même hauteur pour que la mise en page suive.
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

    public static void Lumiere(ImDrawListPtr dl, Vector2 min, Vector2 max, float rond, float a = 0.05f)
        => dl.AddLine(
            new Vector2(min.X + rond, min.Y + 1f),
            new Vector2(max.X - rond, min.Y + 1f),
            Col(new Vector4(1f, 1f, 1f, a)),
            1f);

    public static void Carte(ImDrawListPtr dl, Vector2 min, Vector2 max, float rond,
        Vector4? fond = null, Vector4? bord = null, bool lumiere = true)
    {
        Plein(dl, min, max, fond ?? Teintes.Surface, rond);
        if (lumiere) Lumiere(dl, min, max, rond);
        Contour(dl, min, max, bord ?? Teintes.Filet, rond);
    }

    /// <summary>Le rectangle à quatre couleurs d'ImGui ne s'arrondit pas : on
    /// peint en blanc, puis on recolore les sommets écrits.</summary>
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

    public static void Tache(ImDrawListPtr dl, Vector2 centre, float rayon, Vector4 c, float sommet)
    {
        for (var couche = 5; couche >= 1; couche--)
        {
            var r = rayon * couche / 5f;
            var a = sommet * (1f - (couche - 1f) / 5f);
            dl.AddCircleFilled(centre, r, Col(Teintes.Alpha(c, a)), 48);
        }
    }

    /// <summary>Une image qui remplit sa boîte sans se déformer : on rogne ce
    /// qui dépasse. Un portrait et un avatar rond n'ont pas les mêmes
    /// proportions.</summary>
    public static void ImageCouvrante(ImDrawListPtr dl, ImTextureID image, float largeurImage, float hauteurImage,
        Vector2 min, Vector2 max, float rond)
    {
        var boite = (max.X - min.X) / MathF.Max(1f, max.Y - min.Y);
        var source = largeurImage / MathF.Max(1f, hauteurImage);
        var u0 = Vector2.Zero;
        var u1 = Vector2.One;
        if (source > boite)
        {
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

    /// <summary>Un arc tracé segment par segment : l'arc d'ImGui n'a ni
    /// épaisseur constante ni bouts ronds, que les anneaux du site ont.</summary>
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

    public static void Anneau(ImDrawListPtr dl, Vector2 centre, float rayon, float epaisseur, float part, Vector4 c)
    {
        Arc(dl, centre, rayon, epaisseur, Midi, Midi + MathF.PI * 2f, Teintes.Filet);
        part = Math.Clamp(part, 0f, 1f);
        if (part <= 0.0001f) return;
        Arc(dl, centre, rayon, epaisseur, Midi, Midi + part * MathF.PI * 2f, c);
    }

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
