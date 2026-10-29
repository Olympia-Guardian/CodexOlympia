using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.Exd;
using Lumina.Excel.Sheets;

namespace CodexOlympia;

/// <summary>Pourquoi une portée est restreinte. Les deux raisons n'ont rien à
/// voir.</summary>
public enum Limite
{
    /// <summary>Portée entière : l'omission vaut absence.</summary>
    Aucune,

    /// <summary>Le jeu ne sait pas répondre pour toutes les entrées. Celles-là
    /// sont laissées tranquilles.</summary>
    Capacite,

    /// <summary>Ce qui se constate par un dépôt : on voit ce qui y est, jamais
    /// ce qui n'y est pas. La collection ne peut donc qu'ajouter.</summary>
    Depot,
}

/// <summary>
/// Ce qu'on a trouvé pour une collection.
///
/// <para><c>Portee</c> est la liste des entrées qu'on a su interroger. Quand elle
/// est nulle, on a regardé toute la collection : ce qui n'y figure pas n'est pas
/// débloqué. Quand elle est remplie, on n'a pas tout vu, et l'application n'a le
/// droit de conclure que sur ce qu'on lui déclare avoir regardé.</para>
///
/// <para><c>Empeche</c> dit pourquoi la collection n'a pas pu être lue. Une
/// collection empêchée n'est pas envoyée du tout : mieux vaut une collection
/// absente qu'une collection fausse.</para>
/// </summary>
public sealed record Releve(
    string Cle,
    List<uint> Trouves,
    List<uint>? Portee,
    int Total,
    string? Empeche = null,
    string? Note = null,
    Limite Limite = Limite.Aucune,
    /// <summary>Des entrees que le catalogue rattache bien a un objet, mais
    /// dont le jeu n'a pas su donner la ligne : une lecture en retard, pas une
    /// portee (PLG-R44). La chaine de reverification les reprend.</summary>
    int NonLues = 0,
    /// <summary>Le plugin ne sait pas encore lire cette collection dans le jeu
    /// (PLG-R63) : elle reste nommee, rien ne part, et la relire n'y changerait
    /// rien.</summary>
    bool Illisible = false,
    /// <summary>La facon dont la collection a ete lue. Elle change quand une
    /// lecture fausse est corrigee : ce que l'ancienne avait envoye ne sert
    /// alors plus de reference (PLG-R64).</summary>
    int Maniere = 1);

public sealed record Coffre(HashSet<uint> Coiffeuse, HashSet<uint> Armoire, bool ArmoireLue = false);

/// <summary>
/// La lecture du jeu, à un instant donné.
///
/// <b>Rien n'est écrit dans la mémoire du jeu, jamais.</b> Poser un drapeau
/// pour faire charger la coiffeuse a suffi à convaincre le client que sa
/// demande était partie : il n'affichait plus rien jusqu'au changement de
/// zone. Ce qui n'est pas chargé se demande au joueur, pas au client.
///
/// Chaque collection se lit en posant au jeu la même question pour chaque
/// entrée du catalogue. Rien n'est deviné ni déduit d'ailleurs.
/// </summary>
public static class Photo
{
    /// <summary>Les objets marchands portent un décalage qu'on retire.</summary>
    private const uint SeuilHq = 1_000_000;

    /// <summary>Le numéro du premier poisson du harpon. La table du harpon
    /// numérote ses poissons à partir de lui, le catalogue aussi, et le jeu
    /// range leurs prises à partir de lui. Ce qui est en dessous est un
    /// poisson de la ligne.</summary>
    public const uint PremierHarpon = 20_000;

    /// <summary>La façon de lire des collections que Dalamud sait demander
    /// (PLG-R55). Elles se lisaient par la ligne de l'objet qui les débloque,
    /// et le client répondait « débloqué » pour toutes (PLG-R64).</summary>
    private const int LectureParDalamud = 2;

    /// <summary>
    /// Les collections que le client ne garde que le temps qu'une de leurs
    /// fenêtres reste ouverte, et ces fenêtres : le plugin les relit à leur
    /// ouverture (PLG-R65). Les conditions des portraits ne vivent qu'avec
    /// l'éditeur de portrait et ceux de la carte d'aventurier, ce que le plugin
    /// PlatePeek a constaté en jeu.
    /// </summary>
    public static readonly (string Etape, string[] Fenetres)[] RelireALOuverture =
    [
        ("frames", ["BannerEditor", "CharaCardEditMenu", "CharaCardDesignSetting"]),
        // Le bestiaire, lui, reste connu une fois reçu : on le relit à
        // l'ouverture de sa fenêtre au cas où le serveur n'enverrait sa liste
        // qu'à ce moment-là.
        ("beastmaster", ["XBMMonsterNotebook"]),
    ];

    /// <summary>Les étapes qui lisent ce qu'une autre a trouvé : les pièces de
    /// tenue se cherchent aussi dans l'armoire, qui doit donc être lue avant.</summary>
    private static readonly (string Etape, string Avant)[] Dependances = [("outfitpieces", "armoires")];

    /// <summary>L'ordre de lecture : celui de l'application, qui est aussi celui
    /// de l'affichage, pour qu'une lecture qui remplit de haut en bas se suive
    /// des yeux. Une étape ne passe après une autre que si elle en dépend.</summary>
    public static List<string> Ordre(Catalogue cat)
    {
        var etapes = new List<string>();
        foreach (var c in cat.Affichees)
        {
            var e = Plugin.EtapeDe(c.Cle);
            if (!etapes.Contains(e)) etapes.Add(e);
        }
        foreach (var (etape, avant) in Dependances)
        {
            var i = etapes.IndexOf(etape);
            var j = etapes.IndexOf(avant);
            if (i < 0 || j < 0 || i > j) continue;
            etapes.RemoveAt(i);
            etapes.Insert(etapes.IndexOf(avant) + 1, etape);
        }
        return etapes;
    }

    /// <summary>Fait UNE étape de la lecture. Les pointeurs du jeu sont repris
    /// à chaque étape et jamais gardés d'une image sur l'autre : ce qui est
    /// valide maintenant ne l'est pas forcément dans deux secondes.</summary>
    public static unsafe List<Releve> Etape(
        string cle,
        Catalogue cat,
        Lumina.Excel.ExcelSheet<AozAction> sorts,
        Lumina.Excel.ExcelSheet<MirageStoreSetItem> ensembles,
        Lumina.Excel.ExcelSheet<Item> objets,
        Lumina.Excel.ExcelSheet<BuddyEquip> bardes,
        Lumina.Excel.ExcelSheet<BannerCondition> conditions,
        IUnlockState deblocages,
        Dictionary<uint, bool>? memoirePortraits,
        ref Coffre? coffre)
    {
        var ps = PlayerState.Instance();
        var ui = UIState.Instance();

        switch (cle)
        {
            case "mounts":
                return [Simple(cat, cle, id => ps->IsMountUnlocked(id))];
            case "minions":
                return [Simple(cat, cle, id => ui->IsCompanionUnlocked(id))];
            case "orchestrions":
                return [Simple(cat, cle, id => ps->IsOrchestrionRollUnlocked(id))];
            case "emotes":
                return [Simple(cat, cle, id => id <= ushort.MaxValue && ui->IsEmoteUnlocked((ushort)id))];
            case "fashions":
                return [Simple(cat, cle, id => ps->IsOrnamentUnlocked(id))];
            case "cards":
                return [Simple(cat, cle, id => id <= ushort.MaxValue && ui->IsTripleTriadCardUnlocked((ushort)id))];

            case "facewear":
                return [Lunettes(cat, objets, ps)];

            case "hairstyles":
                return [Coiffures(cat, ui)];

            case "bardings":
                return [Bardes(cat, bardes, deblocages)];

            case "frames":
                return [Portraits(cat, conditions, deblocages, memoirePortraits)];

            case "spells":
                return [Sorts(cat, sorts, ui)];

            case "beastmaster":
                return [Bestiaire(cat)];

            case "fish":
                return [Poissons(cat, ps)];

            case "achievements":
            {
                var succes = FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement.Instance();
                if (!succes->IsLoaded())
                {
                    return
                    [
                        new Releve(cle, [], null, Total(cat, cle),
                            Mots.OuvreSucces),
                    ];
                }
                return [Simple(cat, cle, id => id <= int.MaxValue && succes->IsComplete((int)id))];
            }

            case "quests":
            {
                // Une quête à variantes est faite dès que l'une l'est ; un
                // mandat se demande autrement.
                var qm = QuestManager.Instance();
                var variantes = cat.Variantes.GetValueOrDefault(cle);
                return
                [
                    Simple(cat, cle, id =>
                    {
                        if (cat.Mandats.Contains(id)) return id <= ushort.MaxValue && qm->IsLevequestComplete((ushort)id);
                        if (variantes is not null && variantes.TryGetValue(id, out var v))
                            return v.Any(QuestManager.IsQuestComplete);
                        return QuestManager.IsQuestComplete(id);
                    }),
                ];
            }

            case "armoires":
            {
                var lue = ui->Cabinet.IsCabinetLoaded();
                coffre = Coffre(cat, ui, lue, ensembles);
                return [Armoire(cat, ui, lue, coffre)];
            }

            case "outfitpieces":
                // Les tenues se déduisent des pièces : elles arrivent ensemble.
                return Tenues(cat, ui->Cabinet.IsCabinetLoaded(), coffre ?? new Coffre([], []));

            // Une collection que l'application connaît et que le plugin ne sait
            // pas encore lire : nommée, et dite illisible (PLG-R63).
            default:
                return [new Releve(cle, [], null, Total(cat, cle), Mots.PasEncoreLisible, Illisible: true)];
        }
    }

    /// <summary>Ce que les deux dépôts contiennent.</summary>
    private static unsafe Coffre Coffre(
        Catalogue cat,
        UIState* ui,
        bool armoireLue,
        Lumina.Excel.ExcelSheet<MirageStoreSetItem> ensembles)
    {
        var mirage = MirageManager.Instance();
        var emplacements = mirage->PrismBoxItemIds;

        var coiffeuse = new HashSet<uint>();

        for (var i = 0; i < emplacements.Length; i++)
        {
            var v = emplacements[i];
            // Un emplacement vide répond n'importe quoi à IsSetSlotUnlocked :
            // on ne l'interroge pas.
            if (v == 0) continue;
            var net = v >= SeuilHq ? v - SeuilHq : v;

            // Un emplacement qui porte une ligne de MirageStoreSetItem n'est pas
            // un objet : c'est un ENSEMBLE, rangé d'un bloc. Le jeu dit alors,
            // emplacement par emplacement, lesquelles de ses pièces s'y trouvent
            // vraiment : un ensemble déposé peut être incomplet.
            var set = ensembles.GetRowOrDefault(net);
            var estEnsemble = set is not null && Slots(set.Value).Any(x => x != 0);
            if (estEnsemble)
            {
                var dedans = Slots(set!.Value);
                for (var k = 0; k < dedans.Length; k++)
                {
                    // Un ensemble rangé n'est pas forcément complet : on ne
                    // retient QUE les emplacements que le jeu déclare remplis.
                    // L'identifiant de l'ensemble lui-même n'entre jamais ici,
                    // sans quoi une tenue entamée vaudrait une tenue entière.
                    if (dedans[k] != 0 && mirage->IsSetSlotUnlocked((uint)i, k))
                        coiffeuse.Add(dedans[k]);
                }
            }
            else
            {
                coiffeuse.Add(net);
            }
        }

        var cases = new HashSet<uint>();
        if (armoireLue)
            foreach (var t in cat.Tenues)
                foreach (var p in t.Pieces)
                    if (p.Armoire > 0 && ui->Cabinet.IsItemInCabinet(p.Armoire - 1))
                        cases.Add(p.Objet);

        return new Coffre(coiffeuse, cases, armoireLue);
    }

    /// <summary>
    /// Ce que le joueur possède parmi les entrées des tables du jeu, pour la
    /// galerie (PLG-R52).
    ///
    /// La photo interroge le jeu sur les entrées du <b>catalogue</b> ; la
    /// galerie montre les entrées des <b>tables du client</b>, qui en comptent
    /// davantage. Une émote de base n'est dans aucun catalogue de collection :
    /// elle existe, le joueur l'a, et la galerie la montrerait manquante si
    /// elle se contentait de la photo. La question se pose donc ici aussi,
    /// entrée par entrée, avec les numéros des tables.
    ///
    /// Rien pour une collection dont le jeu n'a pas de question à numéro : la
    /// galerie retombe alors sur ce que la photo a trouvé.
    /// </summary>
    public static unsafe HashSet<uint>? Possedes(
        string cle, IReadOnlyList<Entree> entrees, IDataManager donnees, IUnlockState deblocages)
    {
        var ps = PlayerState.Instance();
        var ui = UIState.Instance();
        if (ps is null || ui is null) return null;

        var vus = new HashSet<uint>();
        switch (cle)
        {
            case "mounts":
                foreach (var x in entrees)
                    if (ps->IsMountUnlocked(x.Id)) vus.Add(x.Id);
                return vus;
            case "minions":
                foreach (var x in entrees)
                    if (ui->IsCompanionUnlocked(x.Id)) vus.Add(x.Id);
                return vus;
            case "orchestrions":
                foreach (var x in entrees)
                    if (ps->IsOrchestrionRollUnlocked(x.Id)) vus.Add(x.Id);
                return vus;
            case "emotes":
                foreach (var x in entrees)
                    if (x.Id <= ushort.MaxValue && ui->IsEmoteUnlocked((ushort)x.Id)) vus.Add(x.Id);
                return vus;
            case "fashions":
                foreach (var x in entrees)
                    if (ps->IsOrnamentUnlocked(x.Id)) vus.Add(x.Id);
                return vus;
            case "cards":
                foreach (var x in entrees)
                    if (x.Id <= ushort.MaxValue && ui->IsTripleTriadCardUnlocked((ushort)x.Id)) vus.Add(x.Id);
                return vus;
            case "facewear":
                foreach (var x in entrees)
                    if (x.Id <= ushort.MaxValue && ps->IsGlassesUnlocked((ushort)x.Id)) vus.Add(x.Id);
                return vus;

            case "bardings":
            {
                // La même question que la synchronisation, à Dalamud (PLG-R55).
                var table = donnees.GetExcelSheet<BuddyEquip>();
                foreach (var x in entrees)
                    if (table.GetRowOrDefault(x.Id) is { } ligne && deblocages.IsBuddyEquipUnlocked(ligne))
                        vus.Add(x.Id);
                return vus;
            }

            case "spells":
            {
                // Un sort bleu s'apprend, et le jeu le note comme n'importe quel
                // deverrouillage d'action : c'est ce lien-la qu'on interroge.
                var sorts = donnees.GetExcelSheet<AozAction>();
                foreach (var x in entrees)
                {
                    var lien = sorts.GetRowOrDefault(x.Id)?.Action.ValueNullable?.UnlockLink.RowId ?? 0;
                    if (lien != 0 && ui->IsUnlockLinkUnlocked(lien)) vus.Add(x.Id);
                }
                return vus;
            }

            case "hairstyles":
            {
                // Une coiffure s'apprend d'une brochure, et le jeu note ce
                // deverrouillage comme les autres.
                foreach (var x in entrees)
                    if (Tables.LiensCoiffures.TryGetValue(x.Id, out var lien)
                        && ui->IsUnlockLinkUnlocked(lien))
                        vus.Add(x.Id);
                return vus;
            }

            case "achievements":
            {
                var succes = FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement.Instance();
                if (succes is null || !succes->IsLoaded()) return null;
                foreach (var x in entrees)
                    if (x.Id <= int.MaxValue && succes->IsComplete((int)x.Id)) vus.Add(x.Id);
                return vus;
            }

            case "beastmaster":
            {
                // La liste du serveur dit ce qui est capture. Pas recue, on ne
                // conclut rien plutot que de montrer tout manquant.
                var capturees = BetesCapturees(entrees.Select(x => x.Id));
                if (capturees is null) return null;
                foreach (var x in entrees)
                    if (capturees.Contains(x.Id)) vus.Add(x.Id);
                return vus;
            }

            case "fish":
            {
                // Les prises du personnage, la même question que la
                // synchronisation. Personnage pas encore chargé : on ne conclut
                // rien plutôt que de montrer tout manquant.
                if (!ps->IsLoaded) return null;
                foreach (var x in entrees)
                    if (PoissonPris(ps, x.Id) == true) vus.Add(x.Id);
                return vus;
            }
            default:
                return null;
        }
    }

    /// <summary>Les onze emplacements d'un ensemble, dans l'ordre de la feuille :
    /// c'est cet ordre-là que le jeu attend pour désigner un emplacement.</summary>
    private static uint[] Slots(MirageStoreSetItem s) =>
    [
        s.MainHand.RowId, s.OffHand.RowId, s.Head.RowId, s.Body.RowId, s.Hands.RowId,
        s.Legs.RowId, s.Feet.RowId, s.Earrings.RowId, s.Necklace.RowId, s.Bracelets.RowId,
        s.Ring.RowId,
    ];

    /// <summary>
    /// L'armoire.
    ///
    /// L'application suit ce que le joueur <b>possède</b>, pas ce qu'il a rangé :
    /// une pièce déposée à la coiffeuse compte autant qu'une pièce déposée à
    /// l'armoire. Et comme un dépôt ne prouve jamais l'absence, la portée se
    /// limite à ce qu'on a trouvé.
    /// </summary>
    private static unsafe Releve Armoire(Catalogue cat, UIState* ui, bool lue, Coffre coffre)
    {
        if (!cat.Ids.TryGetValue("armoires", out var ids))
            return new Releve("armoires", [], null, 0, Mots.CatalogueAbsent);
        if (!lue)
            return new Releve("armoires", [], null, ids.Length,
                Mots.OuvreArmoire);

        // La case d'armoire d'une pièce, quand le catalogue en donne une : c'est
        // par là que la coiffeuse répond pour l'armoire.
        var parCase = new Dictionary<uint, uint>();
        foreach (var t in cat.Tenues)
            foreach (var p in t.Pieces)
                if (p.Armoire > 0)
                    parCase[p.Armoire] = p.Objet;

        var trouves = new List<uint>();
        foreach (var id in ids)
        {
            if (id == 0) continue;
            // Le catalogue numérote les cases à partir de 1, le jeu à partir de 0.
            var rangee = ui->Cabinet.IsItemInCabinet(id - 1);
            var ailleurs = parCase.TryGetValue(id, out var objet) && coffre.Coiffeuse.Contains(objet);
            if (rangee || ailleurs) trouves.Add(id);
        }
        return new Releve("armoires", trouves, [.. trouves], ids.Length, null, null, Limite.Depot);
    }

    private static int Total(Catalogue cat, string cle) => cat.Ids.TryGetValue(cle, out var l) ? l.Length : 0;

    /// <summary>Une question par entrée du catalogue, sans exception : la portée
    /// est la collection entière.</summary>
    private static Releve Simple(Catalogue cat, string cle, Func<uint, bool> possede)
    {
        if (!cat.Ids.TryGetValue(cle, out var ids))
            return new Releve(cle, [], null, 0, Mots.CatalogueAbsent);
        var trouves = new List<uint>();
        foreach (var id in ids)
            if (possede(id))
                trouves.Add(id);
        return new Releve(cle, trouves, null, ids.Length);
    }

    /// <summary>Ce qui se lit par l'objet qui le déverrouille. Les entrées sans
    /// objet connu ne sont pas regardées, et on le déclare.</summary>
    /// <summary>
    /// Les lunettes, demandées par leur numéro (PLG-R55).
    ///
    /// Le catalogue désigne une paire de lunettes par le magazine de mode qui
    /// la débloque. La ligne de ce magazine porte, dans sa donnée annexe, le
    /// numéro de la première teinte dans la table des lunettes, et c'est ce
    /// numéro-là que le jeu attend. Les soixante et un magazines mènent aux
    /// soixante et une entrées, nom pour nom.
    ///
    /// Ce détour évite de passer par la ligne d'objet du client : une ligne
    /// qu'on lui demande n'est pas forcément chargée, et ce qu'il rend alors
    /// peut être la ligne d'un autre objet. La table de Lumina, elle, est
    /// entière et ne bouge pas.
    /// </summary>
    private static unsafe Releve Lunettes(
        Catalogue cat, Lumina.Excel.ExcelSheet<Item> objets, PlayerState* ps)
    {
        const string cle = "facewear";
        if (!cat.Ids.TryGetValue(cle, out var ids) || !cat.Objets.TryGetValue(cle, out var liens))
            return new Releve(cle, [], null, 0, Mots.CatalogueAbsent);

        var trouves = new List<uint>();
        var portee = new List<uint>();
        foreach (var id in ids)
        {
            if (!liens.TryGetValue(id, out var objet) || objet == 0) continue;
            var ligne = objets.GetRowOrDefault(objet);
            var numero = ligne?.AdditionalData.RowId ?? 0;
            if (numero == 0 || numero > ushort.MaxValue) continue;
            portee.Add(id);
            if (ps->IsGlassesUnlocked((ushort)numero)) trouves.Add(id);
        }

        var complet = portee.Count == ids.Length;
        return new Releve(
            cle, trouves, complet ? null : portee, ids.Length, null, null,
            complet ? Limite.Aucune : Limite.Capacite);
    }

    /// <summary>
    /// Les coiffures, demandées par leur lien de déverrouillage (PLG-R55).
    ///
    /// Le catalogue désigne une coiffure par la brochure qui l'enseigne ; la
    /// table des apparences rattache cette brochure à un lien, et c'est ce lien
    /// que le jeu sait dire acquis. Chacune des cinquante-trois brochures mène
    /// à un lien et un seul, vérifié sur la table entière.
    /// </summary>
    private static unsafe Releve Coiffures(Catalogue cat, UIState* ui)
    {
        const string cle = "hairstyles";
        if (!cat.Ids.TryGetValue(cle, out var ids) || !cat.Objets.TryGetValue(cle, out var brochures))
            return new Releve(cle, [], null, 0, Mots.CatalogueAbsent);

        var trouves = new List<uint>();
        var portee = new List<uint>();
        foreach (var id in ids)
        {
            if (!brochures.TryGetValue(id, out var brochure) || brochure == 0) continue;
            if (!Tables.LiensCoiffures.TryGetValue(brochure, out var lien) || lien == 0) continue;
            portee.Add(id);
            if (ui->IsUnlockLinkUnlocked(lien)) trouves.Add(id);
        }

        var complet = portee.Count == ids.Length;
        return new Releve(
            cle, trouves, complet ? null : portee, ids.Length, null, null,
            complet ? Limite.Aucune : Limite.Capacite);
    }

    /// <summary>Les bardes, demandées à Dalamud (PLG-R55) : nos numéros sont
    /// les lignes de leur table dans le jeu, les cent sept, nom pour nom.</summary>
    private static Releve Bardes(
        Catalogue cat, Lumina.Excel.ExcelSheet<BuddyEquip> table, IUnlockState deblocages)
    {
        var releve = ParLigne(cat, "bardings", id => table.GetRowOrDefault(id) is { } ligne
            ? deblocages.IsBuddyEquipUnlocked(ligne)
            : null);
        return releve with { Maniere = LectureParDalamud };
    }

    /// <summary>Les portraits, demandés à Dalamud (PLG-R55) : le numéro d'un
    /// portrait est celui de sa condition de déblocage, et le client ne sait
    /// répondre que sur la ligne de cette condition, quelle qu'en soit la
    /// nature (kit d'encadrement, quête, raid, saison JcJ).
    ///
    /// <para>Le client ne garde ces conditions que tant qu'un éditeur de
    /// portrait est ouvert (PLG-R65). Ce que la lecture y voit se retient pour
    /// le personnage ; éditeurs fermés, elle reprend cette mémoire.</para></summary>
    private static Releve Portraits(
        Catalogue cat, Lumina.Excel.ExcelSheet<BannerCondition> table, IUnlockState deblocages,
        Dictionary<uint, bool>? memoire)
    {
        const string cle = "frames";
        var total = Total(cat, cle);
        if (cat.Ids.TryGetValue(cle, out var ids) && ids.Length > 0 && !ids.Any(ConditionChargee))
        {
            // Éditeurs fermés : ce que la dernière ouverture a montré de
            // débloqué l'est encore, mais ce qui ne l'était pas a pu l'être
            // depuis. La mémoire se lit donc comme un dépôt, qui ne prouve que
            // ce qu'il contient.
            List<uint> debloques = memoire is null ? [] : [.. ids.Where(id => memoire.TryGetValue(id, out var d) && d)];
            // Jamais vue pour ce personnage : la lecture dit quoi ouvrir, et
            // rien ne part.
            if (debloques.Count == 0)
                return new Releve(cle, [], null, total, Mots.OuvrePortraits);
            return new Releve(cle, debloques, [.. debloques], total, null, Mots.PortraitsRetenus, Limite.Depot,
                Maniere: LectureParDalamud);
        }

        var nonLues = 0;
        var releve = ParLigne(cat, cle, id =>
        {
            if (table.GetRowOrDefault(id) is not { } ligne) return null;
            // Dalamud répond « non » quand le client n'a pas encore la ligne de
            // la condition : c'est une lecture en retard, pas un portrait
            // absent (PLG-R44). Elle sort de la portée et part à la
            // revérification.
            if (!ConditionChargee(id))
            {
                nonLues++;
                return null;
            }
            var debloque = deblocages.IsBannerConditionUnlocked(ligne);
            if (memoire is not null) memoire[id] = debloque;
            return debloque;
        });
        return releve with { Maniere = LectureParDalamud, NonLues = nonLues };
    }

    /// <summary>Vrai si le client a chargé la ligne de cette condition.</summary>
    private static unsafe bool ConditionChargee(uint id) => ExdModule.GetBannerConditionByIndex(id) != null;

    /// <summary>Une question par entrée du catalogue, posée sur sa ligne de table
    /// lue par Lumina. Une entrée que le client ne connaît pas encore (un
    /// catalogue en avance d'un patch) répond rien, et sort de la portée.</summary>
    private static Releve ParLigne(Catalogue cat, string cle, Func<uint, bool?> possede)
    {
        if (!cat.Ids.TryGetValue(cle, out var ids))
            return new Releve(cle, [], null, 0, Mots.CatalogueAbsent);

        var trouves = new List<uint>();
        var portee = new List<uint>();
        foreach (var id in ids)
        {
            if (possede(id) is not { } oui) continue;
            portee.Add(id);
            if (oui) trouves.Add(id);
        }
        // Portée déclarée seulement si elle est incomplète : sinon c'est du poids
        // sur le réseau pour rien.
        var complet = portee.Count == ids.Length;
        return new Releve(
            cle, trouves, complet ? null : portee, ids.Length, null, null,
            complet ? Limite.Aucune : Limite.Capacite);
    }

    /// <summary>Un sort bleu s'apprend, et le jeu le note comme n'importe quel
    /// déverrouillage d'action.</summary>
    private static unsafe Releve Sorts(Catalogue cat, Lumina.Excel.ExcelSheet<AozAction> sorts, UIState* ui)
    {
        const string cle = "spells";
        if (!cat.Ids.TryGetValue(cle, out var ids))
            return new Releve(cle, [], null, 0, Mots.CatalogueAbsent);

        var trouves = new List<uint>();
        var portee = new List<uint>();
        foreach (var id in ids)
        {
            var ligne = sorts.GetRowOrDefault(id);
            var lien = ligne?.Action.ValueNullable?.UnlockLink.RowId ?? 0;
            if (lien == 0) continue;
            portee.Add(id);
            if (ui->IsUnlockLinkUnlocked(lien)) trouves.Add(id);
        }
        var complet = portee.Count == ids.Length;
        return new Releve(
            cle, trouves, complet ? null : portee, ids.Length, null, null,
            complet ? Limite.Aucune : Limite.Capacite);
    }

    /// <summary>
    /// Le bestiaire du dresseur (7.56).
    ///
    /// Les bêtes capturées se lisent dans la liste que le serveur envoie au
    /// client, comme Dalamud le fait pour son service des déblocages : le
    /// gestionnaire du dresseur en tient un bit par bête, et sait dire si elle
    /// est capturée. Tant que la liste n'est pas reçue, on ne conclut rien.
    ///
    /// La première lecture prenait le carnet du bestiaire (XBMNoteModule) pour
    /// cette liste. Le carnet ne garde que les réglages de la fenêtre : une
    /// ligne par bête qu'on y a survolée, ou dont on a changé la taille ou
    /// l'apparence. Une bête capturée jamais survolée y manquait, et une seule
    /// bête passée en apparence alternative (le quatrième octet de sa ligne, que
    /// l'on croyait toujours nul) faisait refuser tout le bestiaire. La lecture
    /// change de façon : ce que l'ancienne avait envoyé ne sert plus de
    /// référence (PLG-R64).
    /// </summary>
    private static Releve Bestiaire(Catalogue cat)
    {
        const string cle = "beastmaster";
        if (!cat.Ids.TryGetValue(cle, out var ids))
            return new Releve(cle, [], null, 0, Mots.CatalogueAbsent);
        var capturees = BetesCapturees(ids);
        if (capturees is null)
            return new Releve(cle, [], null, ids.Length, Mots.BestiaireNonCharge, Maniere: LectureParDalamud);
        var trouves = ids.Where(capturees.Contains).ToList();
        return new Releve(cle, trouves, null, ids.Length, Maniere: LectureParDalamud);
    }

    /// <summary>
    /// Les poissons du carnet de pêche, ceux de la ligne et ceux du harpon
    /// dans une même collection.
    ///
    /// Le jeu tient un bit par poisson pris dans l'état du personnage : celui
    /// d'un poisson de la ligne à son numéro dans la table des poissons, celui
    /// d'un poisson du harpon à son numéro dans la table du harpon, moins
    /// vingt mille. Ces bits vivent avec le personnage, pas avec la fenêtre du
    /// carnet : AutoHook les lit en pleine pêche, carnet fermé. Il n'y a donc
    /// rien à relire à l'ouverture du carnet.
    ///
    /// Les deux rangées ont la longueur que FFXIVClientStructs leur connaît.
    /// Un poisson plus loin (un patch en avance sur cette description) sort
    /// de la portée : ni pris ni manquant, jusqu'à ce qu'elle grandisse.
    /// </summary>
    private static unsafe Releve Poissons(Catalogue cat, PlayerState* ps)
    {
        const string cle = "fish";
        if (ps == null || !ps->IsLoaded)
            return new Releve(cle, [], null, Total(cat, cle), Mots.PoissonsNonCharges);
        return ParLigne(cat, cle, id => PoissonPris(ps, id));
    }

    /// <summary>Vrai si ce poisson est pris, faux s'il ne l'est pas, rien quand
    /// sa rangée de bits ne va pas jusqu'à lui. TryGet et jamais Get : Get lève
    /// hors de la rangée (et laisse passer le rang qui la suit), et une erreur
    /// ici annulerait toute la lecture.</summary>
    private static unsafe bool? PoissonPris(PlayerState* ps, uint id)
    {
        var harpon = id >= PremierHarpon;
        var bits = harpon ? ps->CaughtSpearfishBitArray : ps->CaughtFishBitArray;
        var rang = harpon ? id - PremierHarpon : id;
        if (rang > int.MaxValue) return null;
        return bits.TryGet((int)rang, out var pris) ? pris : null;
    }

    /// <summary>Les bêtes capturées parmi celles-ci, ou rien tant que le
    /// serveur n'a pas envoyé sa liste. La même question que Dalamud pose dans
    /// UnlockState.IsXBMPetUnlocked, qui veut une ligne de la table XBMPet,
    /// celle que sa description stable ne sait plus lire depuis la 7.56.</summary>
    private static unsafe HashSet<uint>? BetesCapturees(IEnumerable<uint> ids)
    {
        var ps = PlayerState.Instance();
        var xbm = XBMManager.Instance();
        if (ps == null || !ps->IsLoaded || xbm == null || xbm->State != XBMManager.DataState.Received)
            return null;
        var vues = new HashSet<uint>();
        foreach (var id in ids)
            if (xbm->IsPetUnlocked(id)) vues.Add(id);
        return vues;
    }

    /// <summary>Ce que /codex bestiaire écrit dans le journal Dalamud : la liste
    /// du serveur, puis le carnet du bestiaire ligne par ligne, avec la taille
    /// et l'apparence de chaque bête. Les deux ne disent pas la même chose : le
    /// carnet n'a que les bêtes survolées ou réglées dans la fenêtre.</summary>
    public static unsafe string DiagnosticBestiaire(Catalogue cat)
    {
        var sb = new System.Text.StringBuilder();
        var ids = cat.Ids.GetValueOrDefault("beastmaster") ?? [];
        var xbm = XBMManager.Instance();
        if (xbm == null)
        {
            sb.AppendLine("XBMManager : absent");
        }
        else
        {
            sb.AppendLine($"XBMManager : état {xbm->State}, {xbm->NumUnlockedPets} bêtes capturées selon le serveur");
            var capturees = BetesCapturees(ids);
            if (capturees is null)
            {
                sb.AppendLine("liste du serveur : pas encore reçue");
            }
            else
            {
                var absentes = ids.Where(id => !capturees.Contains(id)).ToList();
                sb.AppendLine($"capturées : {capturees.Count} sur {ids.Length}");
                sb.AppendLine($"absentes : {absentes.Count} : " +
                              string.Join(", ", absentes.Select(id => $"{id} {cat.Nom("beastmaster", id)}")));
            }
        }
        var note = XBMNoteModule.Instance();
        if (note == null)
        {
            sb.AppendLine("carnet du bestiaire : absent");
        }
        else
        {
            var lignes = note->PetSettings.AsSpan();
            sb.AppendLine($"carnet du bestiaire : {lignes.Length} lignes (bêtes survolées ou réglées)");
            foreach (var s in lignes)
                sb.AppendLine($"  {s.Id,3} {cat.Nom("beastmaster", s.Id)} : taille {s.Size}, vue {s.IsNewPetSeen}, apparence {s.Mirage}");
        }
        return sb.ToString();
    }

    /// <summary>Le résumé pour le journal de discussion : les bêtes capturées,
    /// avec leurs noms.</summary>
    public static string ResumeBestiaire(Catalogue cat)
    {
        var ids = cat.Ids.GetValueOrDefault("beastmaster") ?? [];
        var capturees = BetesCapturees(ids);
        if (capturees is null) return Mots.BestiaireNonCharge;
        var noms = ids.Where(capturees.Contains).Select(id => cat.Nom("beastmaster", id)).ToList();
        return Mots.BestiaireLu(noms.Count, ids.Length, string.Join(", ", noms));
    }

    /// <summary>
    /// Les pièces de tenue, et les tenues qu'elles complètent.
    ///
    /// Une pièce d'équipement vit dans un inventaire : on ne peut pas en faire le
    /// tour. Ce qu'on peut constater, en revanche, c'est un dépôt définitif :
    /// la coiffeuse mirage et l'armoire. Les deux prouvent la possession. Aucun
    /// des deux ne prouve l'absence, et c'est pour ça que l'application parle
    /// d'une pièce « non trouvée » et jamais d'une pièce « non possédée ».
    ///
    /// Si l'un des deux dépôts n'a pas été chargé par le jeu, on n'envoie rien du
    /// tout : une lecture partielle ferait passer pour manquantes des pièces
    /// simplement rangées ailleurs.
    /// </summary>
    private static List<Releve> Tenues(Catalogue cat, bool armoireLue, Coffre coffre)
    {
        var totalPieces = cat.Tenues.Sum(t => t.Pieces.Count);
        var vu = Mots.Depots(
            coffre.Coiffeuse.Count,
            armoireLue ? Mots.ArmoirePieces(coffre.Armoire.Count) : Mots.ArmoireNonChargee);

        // La coiffeuse est le dépôt principal : tant qu'on ne l'a pas vue, on
        // n'envoie rien. L'armoire seule est un échantillon trop étroit, et ce
        // qu'elle ne contient pas passerait pour perdu.
        if (coffre.Coiffeuse.Count == 0)
        {
            var quoi = armoireLue ? Mots.OuvreCoiffeuse : Mots.OuvreLesDeux;
            return
            [
                new Releve("outfitpieces", [], null, totalPieces, quoi, vu),
                new Releve("outfits", [], null, cat.Tenues.Count, Mots.TenuesDeduites),
            ];
        }

        // Les deux dépôts se valent, dans les deux sens (PLG-R30) : une pièce
        // rangée à l'armoire est possédée autant qu'une pièce rangée à la
        // coiffeuse, une tenue dont l'armoire tient toutes les pièces est
        // entière, et une pièce vue à la coiffeuse coche sa case d'armoire.
        // Ce qui distingue les deux dépôts tient à l'usage, pas à la possession,
        // et l'usage n'est pas l'affaire du plugin.
        var pieces = new List<uint>();
        var entieres = new List<uint>();
        foreach (var tenue in cat.Tenues)
        {
            var complete = true;
            foreach (var p in tenue.Pieces)
            {
                var enCoiffeuse = coffre.Coiffeuse.Contains(p.Objet);
                var enArmoire = coffre.Armoire.Contains(p.Objet);
                if (enCoiffeuse || enArmoire) pieces.Add(p.Objet);
                else complete = false;
            }
            if (complete) entieres.Add(tenue.Id);
        }

        // La portée se limite à ce qu'on a trouvé, et ce n'est pas une prudence
        // de circonstance : une pièce d'équipement peut dormir dans un sac, chez
        // un servant, ou n'avoir jamais été déposée. Ne pas l'avoir vue ne dit
        // rien. Ces deux collections ne peuvent donc qu'ajouter.
        var vues = pieces.Distinct().ToList();
        return
        [
            new Releve("outfitpieces", vues, [.. vues], totalPieces, null, vu, Limite.Depot),
            new Releve("outfits", entieres, [.. entieres], cat.Tenues.Count, null, null, Limite.Depot),
        ];
    }
}
