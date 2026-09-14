using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace CodexOlympia.Ui;

/// <summary>
/// Écrire à une position choisie, plutôt que là où le curseur d'ImGui se
/// trouve : une carte place ses textes, le curseur ne sert qu'à réserver la
/// place à la fin.
/// </summary>
internal static class Texte
{
    public static Vector2 Mesurer(string t) => ImGui.CalcTextSize(t);

    public static void A(string t, Vector2 ou, Vector4 c)
        => ImGui.GetWindowDrawList().AddText(ou, Peinture.Col(c), t);

    public static void Milieu(string t, Vector2 min, Vector2 max, Vector4 c)
    {
        var s = Mesurer(t);
        A(t, new Vector2((min.X + max.X - s.X) * 0.5f, (min.Y + max.Y - s.Y) * 0.5f), c);
    }

    public static void Coupe(string t, Vector2 ou, float largeur, Vector4 c)
        => ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), ImGui.GetFontSize(), ou, Peinture.Col(c), t, largeur);

    /// <summary>Une petite capitale espacée, comme les intitulés du site.</summary>
    public static void PetitesCapitales(string t, Vector2 ou, Vector4 c)
    {
        var haut = t.ToUpperInvariant();
        var x = ou.X;
        // ImGui n'espace pas les lettres : on les pose une par une.
        foreach (var lettre in haut)
        {
            var s = lettre.ToString();
            A(s, new Vector2(x, ou.Y), c);
            x += Mesurer(s).X + 0.9f * Peinture.Echelle;
        }
    }

    public static float LargeurPetitesCapitales(string t)
    {
        var haut = t.ToUpperInvariant();
        var x = 0f;
        foreach (var lettre in haut) x += Mesurer(lettre.ToString()).X + 0.9f * Peinture.Echelle;
        return x;
    }

    /// <summary>Le texte réduit à ce qui tient, suivi de trois points. Par
    /// dichotomie : la liste se redessine à chaque image.</summary>
    public static string Tronquer(string t, float largeur)
    {
        if (largeur <= 0f) return string.Empty;
        if (Mesurer(t).X <= largeur) return t;
        var points = Mesurer("…").X;
        var bas = 0;
        var haut = t.Length;
        while (bas < haut)
        {
            var milieu = (bas + haut + 1) / 2;
            if (Mesurer(t[..milieu]).X + points <= largeur) bas = milieu;
            else haut = milieu - 1;
        }
        return bas <= 0 ? "…" : t[..bas] + "…";
    }

    public static float HauteurRepliee(string t, float largeur) => ImGui.CalcTextSize(t, false, largeur).Y;
}
