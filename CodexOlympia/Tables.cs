using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace CodexOlympia;

/// <summary>Un objet d'une collection, tel que le jeu le connaît.</summary>
public sealed record Entree(uint Id, string Nom, uint Icone);

/// <summary>
/// Ce qui existe, lu dans les tables du client (PLG-R46).
///
/// Le nom et l'icône viennent du jeu, dans sa langue, et se mettent à jour avec
/// lui : un patch n'a rien à télécharger. Les numéros sont ceux que le reste du
/// plugin emploie déjà pour interroger le jeu, donc ceux que le serveur attend.
///
/// Toutes les collections n'y sont pas. Celles dont l'application numérote les
/// entrées autrement que le jeu — coiffures, portraits — gardent leur liste du
/// catalogue publié, faute d'un numéro commun.
/// </summary>
public static class Tables
{
    /// <summary>Les collections que le jeu sait décrire tout seul.</summary>
    public static IReadOnlyDictionary<string, List<Entree>> Batir(IDataManager donnees)
    {
        var sortie = new Dictionary<string, List<Entree>>();

        Poser(sortie, "mounts", donnees.GetExcelSheet<Mount>(), r =>
            r.Icon == 0 || r.Singular.ExtractText().Length == 0
                ? null
                : new Entree(r.RowId, Majuscule(r.Singular.ExtractText()), r.Icon));

        Poser(sortie, "minions", donnees.GetExcelSheet<Companion>(), r =>
            r.Icon == 0 || r.Singular.ExtractText().Length == 0
                ? null
                : new Entree(r.RowId, Majuscule(r.Singular.ExtractText()), r.Icon));

        Poser(sortie, "orchestrions", donnees.GetExcelSheet<Orchestrion>(), r =>
            r.Name.ExtractText().Length == 0 ? null : new Entree(r.RowId, r.Name.ExtractText(), 0));

        Poser(sortie, "emotes", donnees.GetExcelSheet<Emote>(), r =>
            r.Icon == 0 || r.Name.ExtractText().Length == 0
                ? null
                : new Entree(r.RowId, Majuscule(r.Name.ExtractText()), r.Icon));

        Poser(sortie, "cards", donnees.GetExcelSheet<TripleTriadCard>(), r =>
            r.Name.ExtractText().Length == 0 ? null : new Entree(r.RowId, r.Name.ExtractText(), 0));

        Poser(sortie, "fashions", donnees.GetExcelSheet<Ornament>(), r =>
            r.Icon == 0 || r.Singular.ExtractText().Length == 0
                ? null
                : new Entree(r.RowId, Majuscule(r.Singular.ExtractText()), r.Icon));

        Poser(sortie, "bardings", donnees.GetExcelSheet<BuddyEquip>(), r =>
            r.Name.ExtractText().Length == 0 ? null : new Entree(r.RowId, r.Name.ExtractText(), 0));

        return sortie;
    }

    private static void Poser<T>(
        Dictionary<string, List<Entree>> ou,
        string cle,
        Lumina.Excel.ExcelSheet<T> feuille,
        Func<T, Entree?> lire)
        where T : struct, Lumina.Excel.IExcelRow<T>
    {
        var liste = new List<Entree>();
        foreach (var ligne in feuille)
        {
            if (ligne.RowId == 0) continue;
            if (lire(ligne) is { } e) liste.Add(e);
        }
        if (liste.Count > 0) ou[cle] = liste;
    }

    /// <summary>Le jeu écrit ses noms en minuscule quand ils s'insèrent dans une
    /// phrase. Une galerie les montre seuls, et une capitale s'impose.</summary>
    private static string Majuscule(string t) =>
        t.Length == 0 ? t : char.ToUpper(t[0]) + t[1..];
}
