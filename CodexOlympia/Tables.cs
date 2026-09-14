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
    /// <summary>L'icône commune à tous les rouleaux d'orchestrion.</summary>
    private const uint IconeRouleau = 25945;

    /// <summary>La première planche des cartes de Triple Triade : la carte n
    /// porte la planche <c>PlancheCarte + n</c>.</summary>
    private const uint PlancheCarte = 88000;

    /// <summary>Douze teintes par modèle de lunettes, la première étant la
    /// couleur d'origine.</summary>
    private const uint PasDeTeinte = 12;

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

        // Un rouleau n'a pas d'icône à lui : les huit cent quatre-vingt-six
        // objets qui les donnent portent tous la même, celle du rouleau.
        Poser(sortie, "orchestrions", donnees.GetExcelSheet<Orchestrion>(), r =>
            r.Name.ExtractText().Length == 0 ? null : new Entree(r.RowId, r.Name.ExtractText(), IconeRouleau));

        Poser(sortie, "emotes", donnees.GetExcelSheet<Emote>(), r =>
            r.Icon == 0 || r.Name.ExtractText().Length == 0
                ? null
                : new Entree(r.RowId, Majuscule(r.Name.ExtractText()), r.Icon));

        // L'icône d'une carte se déduit de son numéro : le jeu les range
        // côte à côte, une planche par carte.
        Poser(sortie, "cards", donnees.GetExcelSheet<TripleTriadCard>(), r =>
            r.Name.ExtractText().Length == 0 ? null : new Entree(r.RowId, r.Name.ExtractText(), PlancheCarte + r.RowId));

        Poser(sortie, "fashions", donnees.GetExcelSheet<Ornament>(), r =>
            r.Icon == 0 || r.Singular.ExtractText().Length == 0
                ? null
                : new Entree(r.RowId, Majuscule(r.Singular.ExtractText()), r.Icon));

        // Une barde se porte en trois pièces ; la tête la représente, comme
        // dans la fenêtre du jeu.
        Poser(sortie, "bardings", donnees.GetExcelSheet<BuddyEquip>(), r =>
            r.Name.ExtractText().Length == 0 ? null : new Entree(r.RowId, r.Name.ExtractText(), (uint)r.IconHead));

        // Les sorts bleus : la table les numerote a part, et chaque ligne
        // renvoie a l'action qui porte le nom du sort. L'icone du grimoire
        // vient de la table jumelle, celle qui sert la fenetre du jeu.
        var grimoire = donnees.GetExcelSheet<AozActionTransient>();
        Poser(sortie, "spells", donnees.GetExcelSheet<AozAction>(), r =>
        {
            var nom = r.Action.ValueNullable?.Name.ExtractText() ?? string.Empty;
            return nom.Length == 0
                ? null
                : new Entree(r.RowId, nom, (uint)(grimoire.GetRowOrDefault(r.RowId)?.Icon ?? 0));
        });

        // Le bestiaire du dresseur. Le schema public ne nomme pas encore les
        // colonnes de sa table : la quatrieme porte l'icone, la cinquieme le
        // numero de la bete dans la feuille des familiers, qui a son nom. Ce
        // nom est un nom commun, que le jeu ecrit en minuscule ; la galerie le
        // montre seul, avec sa capitale.
        var familiers = donnees.GetExcelSheet<Pet>();
        Poser(sortie, "beastmaster", donnees.GetExcelSheet<XBMPet>(), r =>
        {
            if (r.Unknown4 <= 0) return null;
            var nom = familiers.GetRowOrDefault((uint)r.Unknown4)?.Name.ExtractText() ?? string.Empty;
            return nom.Length == 0 ? null : new Entree(r.RowId, Majuscule(nom), r.Unknown3);
        });

        // Les lunettes : la table en compte une ligne par teinte, douze par
        // modèle. Seule la première de chaque groupe est un modèle, et c'est
        // elle que le jeu sait dire débloquée ou non. Soixante et une, le
        // compte exact des lunettes du jeu.
        Poser(sortie, "facewear", donnees.GetExcelSheet<Glasses>(), r =>
            r.RowId % PasDeTeinte != 1 || r.Name.ExtractText().Length == 0
                ? null
                : new Entree(r.RowId, r.Name.ExtractText(), (uint)r.Icon));

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
