using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace CodexOlympia.Ui;

internal readonly record struct Geste(bool Clic, bool Dessus);

/// <summary>
/// Les pièces de la fenêtre, toutes bâties pareil : on retient où le curseur
/// est, on pose un bouton invisible qui prend le clic et fait avancer la mise
/// en page, puis on dessine par-dessus. Aucune ne retient rien entre deux
/// images.
/// </summary>
internal static class Pieces
{
    private static float E => Peinture.Echelle;

    public static Geste Zone(string id, Vector2 taille, bool actif = true)
    {
        taille = new Vector2(MathF.Max(1f, taille.X), MathF.Max(1f, taille.Y));
        if (!actif)
        {
            ImGui.Dummy(taille);
            return default;
        }
        var clic = ImGui.InvisibleButton(id, taille);
        var dessus = ImGui.IsItemHovered();
        if (dessus) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return new Geste(clic, dessus);
    }

    public static void Infobulle(string texte)
    {
        if (!ImGui.IsItemHovered() || texte.Length == 0) return;
        ImGui.SetTooltip(texte);
    }

    /// <summary>Le bouton principal, en or : un seul par écran, celui du geste
    /// du moment.</summary>
    public static bool BoutonOr(string id, string texte, float largeur = 0f, bool actif = true, float hauteur = 34f)
    {
        var h = hauteur * E;
        var l = largeur > 0f ? largeur : Texte.Mesurer(texte).X + 34f * E;
        var origine = ImGui.GetCursorScreenPos();
        var g = Zone(id, new Vector2(l, h), actif);
        var fin = origine + new Vector2(l, h);
        var dl = ImGui.GetWindowDrawList();
        var chaud = Mouvement.Survol(id + "#survol", g.Dessus);
        var fond = actif
            ? Teintes.Eclaircir(Teintes.Or, chaud * 0.12f)
            : Teintes.Alpha(Teintes.Or, 0.35f);
        Peinture.Degrade(dl, origine, fin, Teintes.Eclaircir(fond, 0.08f), fond, Teintes.RondTuile * E);
        Texte.Milieu(texte, origine, fin, actif ? Teintes.SurOr : Teintes.Alpha(Teintes.SurOr, 0.6f));
        return actif && g.Clic;
    }

    public static bool BoutonFantome(string id, string texte, float largeur = 0f, bool actif = true,
        float hauteur = 34f, Vector4? teinte = null)
    {
        var h = hauteur * E;
        var l = largeur > 0f ? largeur : Texte.Mesurer(texte).X + 28f * E;
        var origine = ImGui.GetCursorScreenPos();
        var g = Zone(id, new Vector2(l, h), actif);
        var fin = origine + new Vector2(l, h);
        var dl = ImGui.GetWindowDrawList();
        var chaud = Mouvement.Survol(id + "#survol", g.Dessus);
        var encre = teinte ?? Teintes.Encre2;
        var fond = Teintes.Melanger(Teintes.Surface2, encre, chaud * 0.18f);
        Peinture.Plein(dl, origine, fin, actif ? fond : Teintes.Alpha(Teintes.Surface2, 0.5f), Teintes.RondTuile * E);
        Peinture.Contour(dl, origine, fin, Teintes.Melanger(Teintes.Filet, encre, chaud * 0.6f), Teintes.RondTuile * E);
        Texte.Milieu(texte, origine, fin, actif ? encre : Teintes.Discret);
        return actif && g.Clic;
    }

    /// <summary>L'état en deux mots, dans la barre de titre (PLG-R41). Le point
    /// bat tant que quelque chose se fait.</summary>
    public static void Pastille(string texte, Vector4 teinte, bool bat = false)
    {
        var dl = ImGui.GetWindowDrawList();
        var origine = ImGui.GetCursorScreenPos();
        var t = Texte.Mesurer(texte);
        var rayon = 3.5f * E;
        var l = 10f * E + rayon * 2f + 6f * E + t.X + 10f * E;
        var h = t.Y + 6f * E;
        var fin = origine + new Vector2(l, h);
        Peinture.Plein(dl, origine, fin, Teintes.Alpha(teinte, 0.12f), h * 0.5f);
        Peinture.Contour(dl, origine, fin, Teintes.Alpha(teinte, 0.32f), h * 0.5f);
        var a = bat ? 0.45f + 0.55f * Mouvement.Battement() : 1f;
        dl.AddCircleFilled(new Vector2(origine.X + 10f * E + rayon, origine.Y + h * 0.5f), rayon,
            Peinture.Col(Teintes.Alpha(teinte, a)));
        Texte.A(texte, new Vector2(origine.X + 10f * E + rayon * 2f + 6f * E, origine.Y + 3f * E), teinte);
        ImGui.Dummy(new Vector2(l, h));
    }

    public static void Anneau(Vector2 centre, float rayon, float epaisseur, float part, Vector4 teinte,
        string valeur, string mot, bool tourne = false)
    {
        var dl = ImGui.GetWindowDrawList();
        Peinture.Anneau(dl, centre, rayon, epaisseur, part, teinte);
        if (tourne) Peinture.Comete(dl, centre, rayon, epaisseur, teinte);

        ImGui.SetWindowFontScale(1.45f);
        var v = Texte.Mesurer(valeur);
        ImGui.SetWindowFontScale(1f);
        var m = mot.Length > 0 ? new Vector2(Texte.LargeurPetitesCapitales(mot), Texte.Mesurer(mot).Y) : Vector2.Zero;
        var ecart = mot.Length > 0 ? 1f * E : 0f;
        var haut = centre.Y - (v.Y + ecart + m.Y) * 0.5f;

        ImGui.SetWindowFontScale(1.45f);
        Texte.A(valeur, new Vector2(centre.X - v.X * 0.5f, haut), Teintes.Encre);
        ImGui.SetWindowFontScale(1f);
        if (mot.Length > 0)
            Texte.PetitesCapitales(mot, new Vector2(centre.X - m.X * 0.5f, haut + v.Y + ecart), Teintes.Discret);
    }

    public enum Etat
    {
        Faite,
        EnCours,
        AVenir,
    }

    /// <summary>Les étapes du geste, une barre chacune et le mot dessous.</summary>
    public static void Etapes(Vector2 origine, float largeur, IReadOnlyList<(string Mot, Etat Etat)> etapes)
    {
        if (etapes.Count == 0) return;
        var dl = ImGui.GetWindowDrawList();
        var ecart = 8f * E;
        var hauteurBarre = 3f * E;
        var l = (largeur - ecart * (etapes.Count - 1)) / etapes.Count;
        var yMot = origine.Y + hauteurBarre + 5f * E;
        for (var i = 0; i < etapes.Count; i++)
        {
            var (mot, etat) = etapes[i];
            var x = origine.X + i * (l + ecart);
            var barre = etat switch
            {
                Etat.Faite => Teintes.Vert,
                Etat.EnCours => Teintes.Alpha(Teintes.Or, 0.45f + 0.55f * Mouvement.Battement(1100)),
                _ => Teintes.Filet,
            };
            Peinture.Plein(dl, new Vector2(x, origine.Y), new Vector2(x + l, origine.Y + hauteurBarre),
                barre, hauteurBarre * 0.5f);
            var encre = etat switch
            {
                Etat.Faite => Teintes.Encre2,
                Etat.EnCours => Teintes.Encre,
                _ => Teintes.Discret,
            };
            Texte.A(Texte.Tronquer(mot, l), new Vector2(x, yMot), encre);
        }
    }

    /// <summary>Leur hauteur, pour leur réserver la place avant de dessiner.</summary>
    public static float HauteurEtapes() => 3f * E + 5f * E + ImGui.GetTextLineHeight();

    public static void TuileStat(string id, string intitule, string valeur, string aide, Vector4 teinte,
        float largeur, float hauteur = 62f)
    {
        var h = hauteur * E;
        var origine = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(largeur, h));
        var fin = origine + new Vector2(largeur, h);
        var dl = ImGui.GetWindowDrawList();
        Peinture.Carte(dl, origine, fin, Teintes.RondTuile * E, Teintes.Surface2);

        var padX = 11f * E;
        var padY = 9f * E;
        var rayon = 3f * E;
        var hIntitule = ImGui.GetTextLineHeight();
        dl.AddCircleFilled(new Vector2(origine.X + padX + rayon, origine.Y + padY + hIntitule * 0.5f), rayon,
            Peinture.Col(teinte));
        Texte.PetitesCapitales(intitule, new Vector2(origine.X + padX + rayon * 2f + 6f * E, origine.Y + padY),
            Teintes.Discret);

        var largeurAide = 0f;
        if (aide.Length > 0)
        {
            var a = Texte.Mesurer(aide);
            largeurAide = a.X + 8f * E;
            Texte.A(aide, new Vector2(fin.X - padX - a.X, fin.Y - padY - a.Y), Teintes.Discret);
        }

        ImGui.SetWindowFontScale(1.25f);
        var v = Texte.Tronquer(valeur, largeur - padX * 2f - largeurAide);
        var taille = Texte.Mesurer(v);
        Texte.A(v, new Vector2(origine.X + padX, fin.Y - padY - taille.Y), Teintes.Encre);
        ImGui.SetWindowFontScale(1f);
        _ = id;
    }

    public static bool Interrupteur(string id, ref bool valeur)
    {
        var l = 34f * E;
        var h = 19f * E;
        var origine = ImGui.GetCursorScreenPos();
        var g = Zone(id, new Vector2(l, h));
        var dl = ImGui.GetWindowDrawList();
        var glisse = Mouvement.Vers(id + "#glisse", valeur ? 1f : 0f, 16f);
        var fond = Vector4.Lerp(Teintes.Filet, Teintes.Or, glisse);
        Peinture.Plein(dl, origine, origine + new Vector2(l, h), fond, h * 0.5f);
        var rayon = h * 0.5f - 2f * E;
        var x = origine.X + 2f * E + rayon + (l - 4f * E - rayon * 2f) * glisse;
        dl.AddCircleFilled(new Vector2(x, origine.Y + h * 0.5f), rayon,
            Peinture.Col(Vector4.Lerp(Teintes.Discret, Teintes.SurOr, glisse)));
        if (g.Clic) valeur = !valeur;
        return g.Clic;
    }
}
