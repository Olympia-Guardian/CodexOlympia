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

    /// <summary>L'icône commune à tous les vents éthérés : celle que le jeu
    /// montre pour un vent reçu en récompense de quête (deuxième ligne des
    /// autres récompenses), la même qu'au catalogue du site.</summary>
    private const uint IconeCourant = 60033;

    /// <summary>Le premier des cent cinquante-deux courants du terrain dans la
    /// table des noms d'objets. Tous portent ce nom-là, « vent éthéré » dans
    /// le client français, que le jeu écrit en minuscule.</summary>
    private const uint ObjetVentEthere = 2_005_536;

    /// <summary>Du numéro de brochure vers le lien de déverrouillage que le jeu
    /// sait interroger. Vide tant que les tables ne sont pas bâties.</summary>
    public static IReadOnlyDictionary<uint, uint> LiensCoiffures { get; private set; } =
        new Dictionary<uint, uint>();

    /// <summary>Vrai pour un carnet de récolte, une collection par métier : la
    /// table des objets de récolte ne dit pas à quel carnet une ligne
    /// appartient, le catalogue si. Le code qui sait lire ces carnets, pas une
    /// liste des collections (PLG-R63).</summary>
    private static bool CarnetDeRecolte(string cle)
    {
        switch (cle)
        {
            case "mining":
            case "botany":
                return true;
            default:
                return false;
        }
    }

    /// <summary>Chaque ligne de la table des objets de récolte, sous son
    /// numéro, avec le nom et l'icône de l'objet récolté. Vide tant que les
    /// tables ne sont pas bâties ; les galeries des carnets s'y prennent une
    /// fois le catalogue chargé (Completer).</summary>
    private static IReadOnlyDictionary<uint, Entree> ObjetsDeRecolte { get; set; } =
        new Dictionary<uint, Entree>();

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

        // Le bestiaire du dresseur : l'icône de chaque bête, et son numéro dans
        // la feuille des familiers, qui a son nom. Ce nom est un nom commun, que
        // le jeu écrit en minuscule ; la galerie le montre seul, avec sa
        // capitale. La 7.56 a ajouté cinq colonnes à la table, et la description
        // stable de Lumina.Excel est restée celle de la 7.5 : le client la
        // refuse. La description expérimentale, livrée dans le même Lumina.Excel,
        // a la bonne empreinte. Si elle se désaccorde à son tour, le catalogue
        // complète (Completer).
        var familiers = Feuille<Pet>(donnees, journal);
        if (familiers is not null)
        {
#pragma warning disable PendingExcelSchema
            Poser(sortie, journal, "beastmaster", Feuille<Lumina.Excel.Sheets.Experimental.XBMPet>(donnees, journal), r =>
            {
                if (r.Pet.RowId == 0) return null;
                var nom = familiers.GetRowOrDefault(r.Pet.RowId)?.Name.ExtractText() ?? string.Empty;
                return nom.Length == 0 ? null : new Entree(r.RowId, Majuscule(nom), r.Icon);
            });
#pragma warning restore PendingExcelSchema
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

        // L'exploration, d'abord les vues du carnet : une par ligne nommée de
        // la table des vues, sous son numéro dans le carnet en partant de un
        // (sa ligne moins PremiereVue, plus un), comme au catalogue ; le jeu,
        // lui, range ses découvertes à partir de zéro. L'icône est celle de la
        // liste du carnet ; les deux autres colonnes sont l'illustration de la
        // vue, découverte ou non, en 400 sur 240.
        Poser(sortie, journal, "exploration", Feuille<Adventure>(donnees, journal), r =>
        {
            var nom = r.Name.ExtractText();
            return r.RowId < Photo.PremiereVue || nom.Length == 0
                ? null
                : new Entree(r.RowId - Photo.PremiereVue + 1, nom, (uint)Math.Max(r.IconList, 0));
        });

        // Puis les courants éthérés, à la suite des vues, sous IdCourants plus
        // leur bit (leur ligne moins PremierCourant), comme au catalogue. Seuls
        // ceux qu'une zone de la fenêtre des courants référence en sont : la
        // table garde des lignes que plus aucune zone ne compte. La table des
        // courants n'a ni nom ni icône. Le nom est celui que le jeu donne à
        // l'objet du terrain, suivi de la zone ; l'icône, celle du menu qui
        // ouvre la fenêtre des courants.
        Essayer(journal, "exploration", () =>
        {
            var zones = Feuille<AetherCurrentCompFlgSet>(donnees, journal);
            if (zones is null) return;
            var mot = Majuscule(Feuille<EObjName>(donnees, journal)?.GetRowOrDefault(ObjetVentEthere)
                ?.Singular.ExtractText() ?? string.Empty);
            var courants = new List<Entree>();
            var vus = new HashSet<uint>();
            foreach (var z in zones)
            {
                var zone = z.Territory.ValueNullable?.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
                var nom = mot.Length == 0 ? zone : zone.Length == 0 ? mot : $"{mot} ({zone})";
                foreach (var c in z.AetherCurrents)
                {
                    if (c.RowId < Photo.PremierCourant) continue;
                    var id = c.RowId - Photo.PremierCourant + Photo.IdCourants;
                    if (vus.Add(id)) courants.Add(new Entree(id, nom, IconeCourant));
                }
            }
            if (courants.Count == 0) return;
            sortie["exploration"] = sortie.TryGetValue("exploration", out var vues) ? [.. vues, .. courants] : courants;
        });

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

        // Les poissons du carnet de pêche, la ligne et le harpon ensemble,
        // chacun sous le numéro de sa table, comme au catalogue et comme le jeu
        // range ses prises. Seuls ceux que le carnet montre en sont. Le nom et
        // l'icône sont ceux de l'objet pêché ; une ligne qui renverrait à un
        // objet d'événement n'a pas d'objet à montrer, et on la laisse. Un
        // numéro rangé du mauvais côté de PremierHarpon se lirait dans l'autre
        // rangée de bits : il n'entre pas.
        Essayer(journal, "fish", () =>
        {
            var poissons = new List<Entree>();
            var ligne = Feuille<FishParameter>(donnees, journal);
            if (ligne is not null)
            {
                foreach (var r in ligne)
                {
                    if (r.RowId == 0 || r.RowId >= Photo.PremierHarpon || !r.IsInLog || !r.Item.Is<Item>()) continue;
                    if (Objet(objets, r.Item.RowId) is { } o) poissons.Add(o with { Id = r.RowId });
                }
            }
            var harpon = Feuille<SpearfishingItem>(donnees, journal);
            if (harpon is not null)
            {
                // La colonne que la description stable de Lumina nomme
                // IsVisible est celle que l'expérimentale, à la même empreinte,
                // nomme IsInLog : la présence au carnet.
                foreach (var r in harpon)
                {
                    if (r.RowId < Photo.PremierHarpon || !r.IsVisible) continue;
                    if (Objet(objets, r.Item.RowId) is { } o) poissons.Add(o with { Id = r.RowId });
                }
            }
            if (poissons.Count > 0) sortie["fish"] = poissons;
        });

        // Les objets de récolte, pour les carnets du mineur et du botaniste.
        // Une entrée de carnet est une ligne de la table des objets de récolte,
        // sous son numéro, comme au catalogue et comme le jeu tient sa marque ;
        // le nom et l'icône sont ceux de l'objet récolté. Une ligne qui
        // renverrait à un objet d'événement n'a pas d'objet à montrer, et on la
        // laisse. Un objet des deux carnets n'a qu'une ligne, que chaque galerie
        // reprend.
        Essayer(journal, "mining, botany", () =>
        {
            var lignes = Feuille<GatheringItem>(donnees, journal);
            if (lignes is null) return;
            var recoltes = new Dictionary<uint, Entree>();
            foreach (var r in lignes)
            {
                if (r.RowId == 0 || !r.Item.Is<Item>()) continue;
                if (Objet(objets, r.Item.RowId) is { } o) recoltes[r.RowId] = o with { Id = r.RowId };
            }
            ObjetsDeRecolte = recoltes;
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
    /// Le bestiaire du dresseur y passe quand sa table ne se lit pas : un patch
    /// suffit à désaccorder sa description dans Lumina, et la demander échoue.
    /// Le catalogue en connaît les cinquante entrées, avec le même numéro que le
    /// jeu, et leur icône se déduit de ce numéro.
    ///
    /// Les carnets de récolte y passent toujours : leurs entrées sont des
    /// lignes de la table des objets de récolte, mais c'est le catalogue qui
    /// dit lesquelles sont à chaque carnet. La galerie en prend les numéros,
    /// dans l'ordre du catalogue, et le nom et l'icône que le jeu leur donne.
    /// Un numéro que le client ne connaît pas encore (un catalogue en avance
    /// d'un patch) n'y entre pas.
    /// </summary>
    public static IReadOnlyDictionary<string, List<Entree>> Completer(
        IReadOnlyDictionary<string, List<Entree>> deja, Catalogue cat)
    {
        var sortie = new Dictionary<string, List<Entree>>(deja);

        if (!sortie.ContainsKey(ACompleter) && cat.Ids.TryGetValue(ACompleter, out var ids) && ids.Length > 0)
        {
            var betes = new List<Entree>(ids.Length);
            foreach (var id in ids)
                betes.Add(new Entree(id, cat.Nom(ACompleter, id), PlancheBete + id));
            sortie[ACompleter] = betes;
        }

        foreach (var (cle, numeros) in cat.Ids)
        {
            if (!CarnetDeRecolte(cle) || sortie.ContainsKey(cle)) continue;
            var carnet = new List<Entree>(numeros.Length);
            foreach (var id in numeros)
                if (ObjetsDeRecolte.TryGetValue(id, out var e)) carnet.Add(e);
            if (carnet.Count > 0) sortie[cle] = carnet;
        }

        return sortie;
    }

    /// <summary>La collection que le catalogue complète quand le client ne sait
    /// pas la décrire.</summary>
    private const string ACompleter = "beastmaster";

    /// <summary>Vrai si la galerie montre cette collection : le client la
    /// décrit, ou le catalogue la complète.</summary>
    public static bool EnGalerie(Jeu jeu, string cle) =>
        jeu.Collections.ContainsKey(cle) || cle == ACompleter
        || (CarnetDeRecolte(cle) && ObjetsDeRecolte.Count > 0);

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
