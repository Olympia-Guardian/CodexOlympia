using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace CodexOlympia;

/// <summary>Un objet d'une collection, tel que le jeu le connaît.</summary>
public sealed record Entree(uint Id, string Nom, uint Icone);

/// <summary>
/// Ce que les tables du client savent dire, une fois lues.
///
/// <c>Collections</c> est la galerie ; <c>Ensembles</c> dit quelles pièces
/// composent une tenue ; <c>Pieces</c> retrouve une pièce par son objet, pour
/// la nommer dans la fiche d'une tenue sans reparcourir la liste.
/// </summary>
public sealed record Jeu(
    IReadOnlyDictionary<string, List<Entree>> Collections,
    IReadOnlyDictionary<uint, uint[]> Ensembles,
    IReadOnlyDictionary<uint, Entree> Pieces);

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
    public static Jeu Batir(IDataManager donnees)
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

        var objets = donnees.GetExcelSheet<Item>();

        // Les succes et les quetes. Deux tables enormes, quatre mille et vingt
        // mille lignes : la grille n'en dessine que ce qu'on voit, et les
        // lignes sans nom ne sont pas des entrees.
        Poser(sortie, "achievements", donnees.GetExcelSheet<Achievement>(), r =>
            r.Name.ExtractText().Length == 0 ? null : new Entree(r.RowId, r.Name.ExtractText(), r.Icon));

        Poser(sortie, "quests", donnees.GetExcelSheet<Quest>(), r =>
            r.Name.ExtractText().Length == 0
                ? null
                : new Entree(r.RowId, r.Name.ExtractText(), r.Icon));

        // Les coiffures. La table des apparences en compte une ligne par race
        // et par sexe ; ce qui distingue une coiffure, c'est la brochure qui
        // l'enseigne, et toutes ses lignes portent le meme lien de
        // deverrouillage. Le nom du jeu est celui de la brochure, et la
        // coiffure se lit entre ses guillemets.
        var coiffures = new Dictionary<uint, Entree>();
        LiensCoiffures = BatirLiensCoiffures(donnees, coiffures, objets);
        if (coiffures.Count > 0) sortie["hairstyles"] = [.. coiffures.Values];

        // Les tenues, leurs pièces et l'armoire. Le jeu porte les trois : une
        // ligne d'ensemble mirage est un objet, et ses onze emplacements sont
        // les objets qui la composent ; une ligne d'armoire désigne l'objet
        // qu'elle range. Le catalogue numerote l'armoire a partir de un, le jeu
        // a partir de zero : la galerie suit le catalogue, pour que les deux
        // comptes parlent des memes cases.
        var ensembles = new Dictionary<uint, uint[]>();
        var pieces = new Dictionary<uint, Entree>();

        var tenues = new List<Entree>();
        foreach (var ligne in donnees.GetExcelSheet<MirageStoreSetItem>())
        {
            if (ligne.RowId == 0) continue;
            var dedans = Emplacements(ligne).Where(x => x != 0).Distinct().ToArray();
            if (dedans.Length == 0) continue;
            if (Objet(objets, ligne.RowId) is not { } tenue) continue;
            tenues.Add(tenue);
            ensembles[ligne.RowId] = dedans;
            foreach (var p in dedans)
                if (!pieces.ContainsKey(p) && Objet(objets, p) is { } piece)
                    pieces[p] = piece;
        }
        if (tenues.Count > 0)
        {
            sortie["outfits"] = tenues;
            sortie["outfitpieces"] = [.. pieces.Values];
        }

        Poser(sortie, "armoires", donnees.GetExcelSheet<Cabinet>(), r =>
            Objet(objets, r.Item.RowId) is { } o ? o with { Id = r.RowId + 1 } : null);

        return new Jeu(sortie, ensembles, pieces);
    }

    /// <summary>Du numéro de brochure vers le lien de déverrouillage que le jeu
    /// sait interroger. Vide tant que les tables ne sont pas bâties.</summary>
    public static IReadOnlyDictionary<uint, uint> LiensCoiffures { get; private set; } =
        new Dictionary<uint, uint>();

    private static Dictionary<uint, uint> BatirLiensCoiffures(
        IDataManager donnees,
        Dictionary<uint, Entree> coiffures,
        Lumina.Excel.ExcelSheet<Item> objets)
    {
        var liens = new Dictionary<uint, uint>();
        foreach (var ligne in donnees.GetExcelSheet<CharaMakeCustomize>())
        {
            var brochure = ligne.HintItem.RowId;
            if (brochure == 0 || ligne.UnlockLink == 0 || liens.ContainsKey(brochure)) continue;
            if (Objet(objets, brochure) is not { } objet) continue;
            liens[brochure] = ligne.UnlockLink;
            coiffures[brochure] = objet with { Nom = EntreGuillemets(objet.Nom), Icone = ligne.Icon };
        }
        return liens;
    }

    /// <summary>Ce que le jeu met entre guillemets dans le nom d'une brochure :
    /// le nom de la coiffure, sans « Méthode de coiffure ».</summary>
    private static string EntreGuillemets(string nom)
    {
        var debut = nom.IndexOf('\u00ab');
        var fin = nom.LastIndexOf('\u00bb');
        if (debut < 0 || fin <= debut) return nom;
        return nom[(debut + 1)..fin].Trim();
    }

    /// <summary>Un objet du jeu en entrée de galerie, ou rien quand il n'a ni
    /// nom ni icône.</summary>
    private static Entree? Objet(Lumina.Excel.ExcelSheet<Item> objets, uint id)
    {
        if (id == 0) return null;
        var ligne = objets.GetRowOrDefault(id);
        if (ligne is null) return null;
        var nom = ligne.Value.Name.ExtractText();
        return nom.Length == 0 ? null : new Entree(id, nom, ligne.Value.Icon);
    }

    /// <summary>Les onze emplacements d'un ensemble, dans l'ordre de la feuille.</summary>
    private static uint[] Emplacements(MirageStoreSetItem s) =>
    [
        s.MainHand.RowId, s.OffHand.RowId, s.Head.RowId, s.Body.RowId, s.Hands.RowId,
        s.Legs.RowId, s.Feet.RowId, s.Earrings.RowId, s.Necklace.RowId, s.Bracelets.RowId,
        s.Ring.RowId,
    ];

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
