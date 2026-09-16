using System.Globalization;
using System.Text.RegularExpressions;

namespace CodexOlympia;

/// <summary>
/// Les filtres de la galerie (PLG-R67) : ceux de l'application, avec les mêmes
/// valeurs, gardés dans les réglages du plugin et jamais dans le compte. Par
/// défaut, rien n'est filtré.
/// </summary>
public sealed class FiltresGalerie
{
    /// <summary>tout, possedes ou manquants.</summary>
    public string Possession { get; set; } = "tout";

    /// <summary>tout, echangeables ou non-echangeables.</summary>
    public string Echange { get; set; } = "tout";

    /// <summary>Le numéro majeur de l'extension, ou toutes.</summary>
    public int? Extension { get; set; }

    /// <summary>Les exclusions cochées, dans l'ordre des motifs de la base.</summary>
    public List<string> Exclure { get; set; } = [];

    /// <summary>La façon d'obtenir choisie, collection par collection.</summary>
    public Dictionary<string, string> Source { get; set; } = new();
}

/// <summary>
/// Ce que les filtres gardent d'une collection, ce qu'elle propose, et ce que
/// le badge compte (PLG-R67, comme NAV-R41 à NAV-R43 dans l'application).
///
/// Les exclusions n'ont pas de définition ici : elles se lisent dans la base
/// que l'application publie, qui donne pour chaque motif la liste entière des
/// entrées qu'il touche.
/// </summary>
public static class Garde
{
    public const string Possession = "possession";
    public const string Echange = "echange";
    public const string Extension = "extension";
    public const string Source = "source";

    /// <summary>Les motifs de la base, dans son ordre.</summary>
    public static readonly string[] Motifs = ["boutique", "classe", "limite", "inconnue"];

    /// <summary>Les extensions, la plus récente d'abord, nommées comme dans
    /// l'application.</summary>
    public static readonly (int Majeur, string Nom)[] Extensions =
    [
        (7, "Dawntrail"),
        (6, "Endwalker"),
        (5, "Shadowbringers"),
        (4, "Stormblood"),
        (3, "Heavensward"),
        (2, "A Realm Reborn"),
    ];

    private static readonly Regex DebutDeNombre = new(@"^\s*\d+(\.\d+)?", RegexOptions.Compiled);

    /// <summary>L'extension d'un patch, lue comme l'application : 7.55 est
    /// Dawntrail, le 1.x d'avant la refonte va avec A Realm Reborn, et un patch
    /// illisible n'en a pas.</summary>
    public static int? ExtensionDe(string? patch)
    {
        var m = DebutDeNombre.Match(patch ?? string.Empty);
        if (!m.Success || !double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v < 1)
            return null;
        return Math.Max(2, (int)Math.Floor(v));
    }

    /// <summary>Remet d'aplomb des filtres relus : une valeur inconnue revient
    /// à son défaut sans emporter les autres.</summary>
    public static void Normaliser(FiltresGalerie f)
    {
        if (f.Possession is not ("tout" or "possedes" or "manquants")) f.Possession = "tout";
        if (f.Echange is not ("tout" or "echangeables" or "non-echangeables")) f.Echange = "tout";
        if (f.Extension is { } e && !Extensions.Any(x => x.Majeur == e)) f.Extension = null;
        var exclure = f.Exclure ?? [];
        f.Exclure = [.. Motifs.Where(exclure.Contains)];
        f.Source ??= new();
    }

    /// <summary>
    /// Ce qu'une collection propose : la possession et l'extension toujours,
    /// l'échange si une entrée s'échange, une exclusion si elle touche une
    /// entrée, la façon d'obtenir s'il y en a au moins deux. Les numéros sont
    /// ceux du catalogue.
    /// </summary>
    public static HashSet<string> Offerts(Catalogue? cat, string cle, IEnumerable<uint> ids, int sources)
    {
        var offerts = new HashSet<string> { Possession, Extension };
        if (sources >= 2) offerts.Add(Source);
        if (cat is null) return offerts;
        foreach (var id in ids)
        {
            if (cat.Detail(cle, id)?.Echangeable == true) offerts.Add(Echange);
            foreach (var m in Motifs)
                if (cat.Touche(cle, m, id)) offerts.Add(m);
        }
        return offerts;
    }

    /// <summary>
    /// Vrai si l'entrée reste dans la grille. Chaque filtre ne fait que
    /// retirer. Une exclusion épargne ce qu'on possède, et celle du limité ce
    /// qui s'obtient en ce moment : un événement en cours ou encore échangeable,
    /// ou la série JcJ en cours, que le catalogue ne marque pas perdue.
    /// </summary>
    public static bool Garder(
        FiltresGalerie f, IReadOnlySet<string> offerts, Catalogue? cat, string cle, uint id, bool aMoi)
    {
        if (offerts.Contains(Possession))
        {
            if (f.Possession == "possedes" && !aMoi) return false;
            if (f.Possession == "manquants" && aMoi) return false;
        }
        var d = cat?.Detail(cle, id);
        if (offerts.Contains(Echange))
        {
            if (f.Echange == "echangeables" && d?.Echangeable != true) return false;
            if (f.Echange == "non-echangeables" && d?.Echangeable == true) return false;
        }
        if (offerts.Contains(Extension) && f.Extension is { } extension && ExtensionDe(d?.Patch) != extension)
            return false;
        if (offerts.Contains(Source) && f.Source.TryGetValue(cle, out var genre) && genre.Length > 0
            && (d is null || !d.Sources.Any(s => s.Genre == genre)))
            return false;

        if (aMoi || cat is null) return true;
        foreach (var m in f.Exclure)
        {
            if (!offerts.Contains(m) || !cat.Touche(cle, m, id)) continue;
            if (m == "limite")
            {
                var vivant = cat.EvenementVivant(cle, id);
                if (vivant is { } v && v.Vie is Vie.EnCours or Vie.Echange) continue;
                if (d is { Inobtenable: false }) continue;
            }
            return false;
        }
        return true;
    }

    /// <summary>Le badge : les filtres proposés qui agissent. Une façon
    /// d'obtenir que la collection n'a plus ne compte pas.</summary>
    public static int Actifs(FiltresGalerie f, IReadOnlySet<string> offerts, string cle, IReadOnlyCollection<string> genres)
    {
        var n = 0;
        if (offerts.Contains(Possession) && f.Possession != "tout") n++;
        if (offerts.Contains(Echange) && f.Echange != "tout") n++;
        if (offerts.Contains(Extension) && f.Extension is not null) n++;
        n += f.Exclure.Count(offerts.Contains);
        if (offerts.Contains(Source) && f.Source.TryGetValue(cle, out var g) && genres.Contains(g)) n++;
        return n;
    }

    /// <summary>Remet à leur défaut les filtres que la collection propose, et
    /// eux seuls : une façon d'obtenir choisie ailleurs reste.</summary>
    public static void Reinitialiser(FiltresGalerie f, IReadOnlySet<string> offerts, string cle)
    {
        if (offerts.Contains(Possession)) f.Possession = "tout";
        if (offerts.Contains(Echange)) f.Echange = "tout";
        if (offerts.Contains(Extension)) f.Extension = null;
        f.Exclure = [.. f.Exclure.Where(m => !offerts.Contains(m))];
        if (offerts.Contains(Source)) f.Source.Remove(cle);
    }
}
