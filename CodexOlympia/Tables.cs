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
    IReadOnlyDictionary<uint, Entree> Pieces,
    IReadOnlyDictionary<uint, uint> ObjetsArmoire);

/// <summary>
/// Ce qui existe, lu dans les tables du client (PLG-R46).
///
/// Le nom et l'icône viennent du jeu, dans sa langue, et se mettent à jour avec
/// lui : un patch n'a rien à télécharger. Les numéros sont ceux que le reste du
/// plugin emploie déjà pour interroger le jeu, donc ceux que le serveur attend.
///
/// <b>Chaque collection se bâtit pour son compte (PLG-R59).</b> Une table dont
/// la description ne correspond plus à ce que le client contient lève une
/// erreur, et un patch suffit à la désaccorder ; elle ne doit emporter qu'une
/// collection, jamais la galerie entière.
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

    /// <summary>Du numéro de brochure vers le lien de déverrouillage que le jeu
    /// sait interroger. Vide tant que les tables ne sont pas bâties.</summary>
    public static IReadOnlyDictionary<uint, uint> LiensCoiffures { get; private set; } =
        new Dictionary<uint, uint>();

    /// <summary>Les collections que le jeu sait décrire tout seul.</summary>
    public static Jeu Batir(IDataManager donnees, IPluginLog journal)
    {
        var sortie = new Dictionary<string, List<Entree>>();
        var ensembles = new Dictionary<uint, uint[]>();
        var pieces = new Dictionary<uint, Entree>();
        var armoire = new Dictionary<uint, uint>();

        Poser(sortie, journal, "mounts", Feuille<Mount>(donnees, journal), r =>
            r.Icon == 0 || r.Singular.ExtractText().Length == 0
                ? null
                : new Entree(r.RowId, Majuscule(r.Singular.ExtractText()), r.Icon));

        Poser(sortie, journal, "minions", Feuille<Companion>(donnees, journal), r =>
            r.Icon == 0 || r.Singular.ExtractText().Length == 0
                ? null
                : new Entree(r.RowId, Majuscule(r.Singular.ExtractText()), r.Icon));

        // Un rouleau n'a pas d'icône à lui : les huit cent quatre-vingt-six
        // objets qui les donnent portent tous la même, celle du rouleau.
        Poser(sortie, journal, "orchestrions", Feuille<Orchestrion>(donnees, journal), r =>
            r.Name.ExtractText().Length == 0
                ? null
                : new Entree(r.RowId, r.Name.ExtractText(), IconeRouleau));

        Poser(sortie, journal, "emotes", Feuille<Emote>(donnees, journal), r =>
            r.Icon == 0 || r.Name.ExtractText().Length == 0
                ? null
                : new Entree(r.RowId, Majuscule(r.Name.ExtractText()), r.Icon));

        // L'icône d'une carte se déduit de son numéro : le jeu les range côte à
        // côte, une planche par carte.
        Poser(sortie, journal, "cards", Feuille<TripleTriadCard>(donnees, journal), r =>
            r.Name.ExtractText().Length == 0
                ? null
                : new Entree(r.RowId, r.Name.ExtractText(), PlancheCarte + r.RowId));

        Poser(sortie, journal, "fashions", Feuille<Ornament>(donnees, journal), r =>
            r.Icon == 0 || r.Singular.ExtractText().Length == 0
                ? null
                : new Entree(r.RowId, Majuscule(r.Singular.ExtractText()), r.Icon));

        // Une barde se porte en trois pièces ; la tête la représente, comme dans
        // la fenêtre du jeu.
        Poser(sortie, journal, "bardings", Feuille<BuddyEquip>(donnees, journal), r =>
            r.Name.ExtractText().Length == 0
                ? null
                : new Entree(r.RowId, r.Name.ExtractText(), (uint)r.IconHead));

        // Les sorts bleus : la table les numérote à part, et chaque ligne renvoie
        // à l'action qui porte le nom et l'icône du sort.
        Poser(sortie, journal, "spells", Feuille<AozAction>(donnees, journal), r =>
        {
            var action = r.Action.ValueNullable;
            var nom = action?.Name.ExtractText() ?? string.Empty;
            return nom.Length == 0 ? null : new Entree(r.RowId, nom, action?.Icon ?? 0);
        });

        // Le bestiaire du dresseur. Le schéma public ne nomme pas encore les
        // colonnes de sa table : la quatrième porte l'icône, la cinquième le
        // numéro de la bête dans la feuille des familiers, qui a son nom. Ce nom
        // est un nom commun, que le jeu écrit en minuscule ; la galerie le montre
        // seul, avec sa capitale.
        var familiers = Feuille<Pet>(donnees, journal);
        if (familiers is not null)
        {
            Poser(sortie, journal, "beastmaster", Feuille<XBMPet>(donnees, journal), r =>
            {
                if (r.Unknown4 <= 0) return null;
                var nom = familiers.GetRowOrDefault((uint)r.Unknown4)?.Name.ExtractText() ?? string.Empty;
                return nom.Length == 0 ? null : new Entree(r.RowId, Majuscule(nom), r.Unknown3);
            });
        }

        // Les lunettes : la table en compte une ligne par teinte, douze par
        // modèle. Seule la première de chaque groupe est un modèle, et c'est elle
        // que le jeu sait dire débloquée ou non. Soixante et une, le compte exact
        // des lunettes du jeu.
        Poser(sortie, journal, "facewear", Feuille<Glasses>(donnees, journal), r =>
            r.RowId % PasDeTeinte != 1 || r.Name.ExtractText().Length == 0
                ? null
                : new Entree(r.RowId, r.Name.ExtractText(), (uint)r.Icon));

        // Les succès. Quatre mille lignes, dont la grille ne dessine que ce
        // qu'on voit ; les lignes sans nom ne sont pas des entrées.
        //
        // Les quêtes n'y sont pas, et c'est un choix : leur table n'a pas
        // d'icône d'entrée, seulement l'image de la scène qui les ouvre, et une
        // grille de cinq mille vignettes de cinématique ne montre rien. Elles
        // restent suivies par la synchronisation, qui les compte sans les
        // dessiner.
        Poser(sortie, journal, "achievements", Feuille<Achievement>(donnees, journal), r =>
            r.Name.ExtractText().Length == 0
                ? null
                : new Entree(r.RowId, r.Name.ExtractText(), r.Icon));

        var objets = Feuille<Item>(donnees, journal);
        if (objets is null) return new Jeu(sortie, ensembles, pieces, armoire);

        // Les coiffures. La table des apparences en compte une ligne par race et
        // par sexe ; ce qui distingue une coiffure, c'est la brochure qui
        // l'enseigne, et toutes ses lignes portent le même lien de
        // déverrouillage. Le nom du jeu est celui de la brochure, et la coiffure
        // se lit entre ses guillemets.
        Essayer(journal, "hairstyles", () =>
        {
            var apparences = Feuille<CharaMakeCustomize>(donnees, journal);
            if (apparences is null) return;
            var coiffures = new Dictionary<uint, Entree>();
            var liens = new Dictionary<uint, uint>();
            foreach (var ligne in apparences)
            {
                var brochure = ligne.HintItem.RowId;
                if (brochure == 0 || ligne.UnlockLink == 0 || liens.ContainsKey(brochure)) continue;
                if (Objet(objets, brochure) is not { } objet) continue;
                liens[brochure] = ligne.UnlockLink;
                coiffures[brochure] = objet with { Nom = EntreGuillemets(objet.Nom), Icone = ligne.Icon };
            }
            LiensCoiffures = liens;
            if (coiffures.Count > 0) sortie["hairstyles"] = [.. coiffures.Values];
        });

        // Les tenues et leurs pièces. Une ligne d'ensemble mirage est un objet,
        // et ses onze emplacements sont les objets qui la composent.
        Essayer(journal, "outfits", () =>
        {
            var mirages = Feuille<MirageStoreSetItem>(donnees, journal);
            if (mirages is null) return;
            var tenues = new List<Entree>();
            foreach (var ligne in mirages)
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
            if (tenues.Count == 0) return;
            sortie["outfits"] = tenues;
            sortie["outfitpieces"] = [.. pieces.Values];
        });

        // L'armoire. Le catalogue numérote ses cases à partir de un, le jeu à
        // partir de zéro : la galerie suit le catalogue, pour que les deux
        // comptes parlent des mêmes cases.
        Poser(sortie, journal, "armoires", Feuille<Cabinet>(donnees, journal), r =>
        {
            if (Objet(objets, r.Item.RowId) is not { } o) return null;
            armoire[r.RowId + 1] = r.Item.RowId;
            return o with { Id = r.RowId + 1 };
        });

        journal.Information("tables du jeu : {0} collections", sortie.Count);
        return new Jeu(sortie, ensembles, pieces, armoire);
    }

    /// <summary>L'icône d'une bête du dresseur : le jeu les range côte à côte,
    /// une par numéro.</summary>
    private const uint PlancheBete = 242000;

    /// <summary>
    /// Ce que le catalogue ajoute à la galerie une fois chargé.
    ///
    /// Le bestiaire du dresseur y passe : sa table est décrite dans Lumina d'une
    /// façon que le client ne reconnaît plus, et la demander échoue. Le catalogue
    /// en connaît les cinquante entrées, avec le même numéro que le jeu, et leur
    /// icône se déduit de ce numéro.
    /// </summary>
    public static IReadOnlyDictionary<string, List<Entree>> Completer(
        IReadOnlyDictionary<string, List<Entree>> deja, Catalogue cat)
    {
        if (deja.ContainsKey("beastmaster")) return deja;
        if (!cat.Ids.TryGetValue("beastmaster", out var ids) || ids.Length == 0) return deja;

        var betes = new List<Entree>(ids.Length);
        foreach (var id in ids)
            betes.Add(new Entree(id, cat.Nom("beastmaster", id), PlancheBete + id));

        var sortie = new Dictionary<string, List<Entree>>(deja) { ["beastmaster"] = betes };
        return sortie;
    }

    /// <summary>
    /// Une table du client, ou rien.
    ///
    /// Lumina décrit chaque table par une empreinte de ses colonnes, et refuse
    /// de la servir quand le client n'a plus la même. Un patch suffit, et
    /// l'erreur ne se prévoit pas : elle se rattrape, et la collection manque au
    /// lieu d'emporter les autres.
    /// </summary>
    private static Lumina.Excel.ExcelSheet<T>? Feuille<T>(IDataManager donnees, IPluginLog journal)
        where T : struct, Lumina.Excel.IExcelRow<T>
    {
        try
        {
            return donnees.GetExcelSheet<T>();
        }
        catch (Exception e)
        {
            journal.Warning("table {0} illisible : {1}", typeof(T).Name, e.Message);
            return null;
        }
    }

    /// <summary>Un morceau de construction qui a le droit d'échouer seul.</summary>
    private static void Essayer(IPluginLog journal, string cle, System.Action faire)
    {
        try
        {
            faire();
        }
        catch (Exception e)
        {
            journal.Warning("collection {0} illisible : {1}", cle, e.Message);
        }
    }

    /// <summary>Ce que le jeu met entre guillemets dans le nom d'une brochure :
    /// le nom de la coiffure, sans « Méthode de coiffure ».</summary>
    private static string EntreGuillemets(string nom)
    {
        var debut = nom.IndexOf('«');
        var fin = nom.LastIndexOf('»');
        if (debut < 0 || fin <= debut) return nom;
        return nom[(debut + 1)..fin].Trim();
    }

    /// <summary>Un objet du jeu en entrée de galerie, ou rien quand il n'a pas de
    /// nom.</summary>
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
        IPluginLog journal,
        string cle,
        Lumina.Excel.ExcelSheet<T>? feuille,
        Func<T, Entree?> lire)
        where T : struct, Lumina.Excel.IExcelRow<T>
    {
        if (feuille is null) return;
        var liste = new List<Entree>();
        try
        {
            foreach (var ligne in feuille)
            {
                if (ligne.RowId == 0) continue;
                if (lire(ligne) is { } e) liste.Add(e);
            }
        }
        catch (Exception e)
        {
            // Ce qu'on a deja lu reste bon : une collection entamee vaut mieux
            // qu'une collection absente, et le compte le dira.
            journal.Warning("collection {0} lue en partie ({1} entrées) : {2}", cle, liste.Count, e.Message);
        }
        if (liste.Count > 0) ou[cle] = liste;
    }

    /// <summary>Le jeu écrit ses noms en minuscule quand ils s'insèrent dans une
    /// phrase. Une galerie les montre seuls, et une capitale s'impose.</summary>
    private static string Majuscule(string t) =>
        t.Length == 0 ? t : char.ToUpper(t[0]) + t[1..];
}
