using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.Exd;
using Lumina.Excel.Sheets;

namespace CodexOlympia;

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
    string? Note = null);

/// <summary>Ce que contiennent les deux dépôts, et un échantillon lisible.</summary>
public sealed record Coffre(HashSet<uint> Coiffeuse, HashSet<uint> Armoire);

/// <summary>Une lecture complète : les relevés, et ce qu'on a vu dans les dépôts.</summary>
public sealed record Lecture(List<Releve> Releves, Coffre Coffre);

/// <summary>
/// La lecture du jeu, à un instant donné.
///
/// <b>Rien n'est écrit dans la mémoire du jeu, jamais.</b> Une première version
/// posait un drapeau pour demander au client de charger la coiffeuse : le jeu
/// croyait alors sa demande déjà partie et n'affichait plus rien tant qu'on ne
/// changeait pas de zone. Un greffon qui lit n'a aucune raison d'écrire, et ce
/// qui n'a pas encore été chargé se demande au joueur, pas au client.
///
/// Chaque collection est lue en posant au jeu la même question pour chaque entrée
/// du catalogue : « celle-ci, tu l'as ? ». Rien n'est deviné, rien n'est déduit
/// d'un succès ou d'un objet trouvé ailleurs.
/// </summary>
public static class Photo
{
    /// <summary>Les objets marchands portent un décalage qu'on retire.</summary>
    private const uint SeuilHq = 1_000_000;

    public static unsafe Lecture Prendre(
        Catalogue cat,
        Lumina.Excel.ExcelSheet<AozAction> sorts,
        Lumina.Excel.ExcelSheet<MirageStoreSetItem> ensembles)
    {
        var releves = new List<Releve>();
        var ps = PlayerState.Instance();
        var ui = UIState.Instance();

        // --- Les déverrouillages que le jeu tient collection par collection ---
        releves.Add(Simple(cat, "mounts", id => ps->IsMountUnlocked(id)));
        releves.Add(Simple(cat, "minions", id => ui->IsCompanionUnlocked(id)));
        releves.Add(Simple(cat, "orchestrions", id => ps->IsOrchestrionRollUnlocked(id)));
        releves.Add(Simple(cat, "emotes", id => id <= ushort.MaxValue && ui->IsEmoteUnlocked((ushort)id)));
        releves.Add(Simple(cat, "fashions", id => ps->IsOrnamentUnlocked(id)));
        releves.Add(Simple(cat, "cards", id => id <= ushort.MaxValue && ui->IsTripleTriadCardUnlocked((ushort)id)));

        // --- Ce qui se lit par l'objet qui déverrouille -----------------------
        // Le catalogue ne donne pas d'objet à toutes les entrées. Celles qui n'en
        // ont pas ne sont pas interrogeables : elles sortent de la portée, et
        // l'application ne conclura rien à leur sujet.
        foreach (var cle in new[] { "facewear", "hairstyles", "bardings", "frames" })
            releves.Add(ParObjet(cat, cle));

        // --- Les succès ------------------------------------------------------
        var succes = FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement.Instance();
        if (!succes->IsLoaded())
        {
            releves.Add(new Releve("achievements", [], null, Total(cat, "achievements"),
                "ouvre ton carnet de succès une fois, puis regarde à nouveau"));
        }
        else
        {
            releves.Add(Simple(cat, "achievements", id => id <= int.MaxValue && succes->IsComplete((int)id)));
        }

        // --- Les sorts bleus -------------------------------------------------
        releves.Add(Sorts(cat, sorts, ui));

        // --- Les dépôts ------------------------------------------------------
        var armoireLue = ui->Cabinet.IsCabinetLoaded();
        var coffre = Coffre(cat, ui, armoireLue, ensembles);
        releves.Add(Armoire(cat, ui, armoireLue, coffre));
        releves.AddRange(Tenues(cat, armoireLue, coffre));

        return new Lecture(releves, coffre);
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
            // vraiment — un ensemble déposé peut être incomplet.
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

        return new Coffre(coiffeuse, cases);
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
    /// L'application y suit ce que le joueur <b>possède</b>, pas ce qu'il a
    /// rangé : une pièce déposée à la coiffeuse compte donc autant qu'une pièce
    /// déposée à l'armoire, et il faut le dire, sans quoi l'application propose
    /// sans fin de cocher une case qu'elle sait déjà due.
    ///
    /// Et comme un dépôt ne prouve jamais l'absence, la portée se limite à ce
    /// qu'on a trouvé : cette collection ne peut qu'ajouter.
    /// </summary>
    private static unsafe Releve Armoire(Catalogue cat, UIState* ui, bool lue, Coffre coffre)
    {
        if (!cat.Ids.TryGetValue("armoires", out var ids))
            return new Releve("armoires", [], null, 0, "catalogue absent");
        if (!lue)
            return new Releve("armoires", [], null, ids.Length,
                "ouvre une fois ton armoire chez un rassembleur pour que le jeu la charge");

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
        return new Releve("armoires", trouves, [.. trouves], ids.Length);
    }

    private static int Total(Catalogue cat, string cle) => cat.Ids.TryGetValue(cle, out var l) ? l.Length : 0;

    /// <summary>Une question par entrée du catalogue, sans exception : la portée
    /// est la collection entière.</summary>
    private static Releve Simple(Catalogue cat, string cle, Func<uint, bool> possede)
    {
        if (!cat.Ids.TryGetValue(cle, out var ids)) return new Releve(cle, [], null, 0, "catalogue absent");
        var trouves = new List<uint>();
        foreach (var id in ids)
            if (possede(id))
                trouves.Add(id);
        return new Releve(cle, trouves, null, ids.Length);
    }

    /// <summary>Ce qui se lit par l'objet qui le déverrouille. Les entrées sans
    /// objet connu ne sont pas regardées, et on le déclare.</summary>
    private static unsafe Releve ParObjet(Catalogue cat, string cle)
    {
        if (!cat.Ids.TryGetValue(cle, out var ids) || !cat.Objets.TryGetValue(cle, out var objets))
            return new Releve(cle, [], null, 0, "catalogue absent");

        var ui = UIState.Instance();
        var trouves = new List<uint>();
        var portee = new List<uint>();
        foreach (var id in ids)
        {
            if (!objets.TryGetValue(id, out var objet) || objet == 0) continue;
            var ligne = ExdModule.GetItemRowById(objet);
            if (ligne is null) continue;
            portee.Add(id);
            if (ui->IsItemActionUnlocked(ligne) == 1) trouves.Add(id);
        }
        // Portée déclarée seulement si elle est incomplète : sinon c'est du poids
        // sur le réseau pour rien.
        return new Releve(cle, trouves, portee.Count == ids.Length ? null : portee, ids.Length);
    }

    /// <summary>Un sort bleu s'apprend, et le jeu le note comme n'importe quel
    /// déverrouillage d'action.</summary>
    private static unsafe Releve Sorts(Catalogue cat, Lumina.Excel.ExcelSheet<AozAction> sorts, UIState* ui)
    {
        const string cle = "spells";
        if (!cat.Ids.TryGetValue(cle, out var ids)) return new Releve(cle, [], null, 0, "catalogue absent");

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
        return new Releve(cle, trouves, portee.Count == ids.Length ? null : portee, ids.Length);
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
        var vu = $"coiffeuse : {coffre.Coiffeuse.Count} objets, armoire : " +
                 (armoireLue ? $"{coffre.Armoire.Count} pièces" : "non chargée");

        // La coiffeuse est le dépôt principal : tant qu'on ne l'a pas vue, on
        // n'envoie rien. L'armoire seule est un échantillon trop étroit, et ce
        // qu'elle ne contient pas passerait pour perdu.
        if (coffre.Coiffeuse.Count == 0)
        {
            var quoi = armoireLue
                ? "ouvre ta coiffeuse mirage une fois, puis regarde à nouveau : le jeu ne charge son contenu qu'à ce moment-là"
                : "ouvre ta coiffeuse mirage et ton armoire chez un rassembleur, puis regarde à nouveau";
            return
            [
                new Releve("outfitpieces", [], null, totalPieces, quoi, vu),
                new Releve("outfits", [], null, cat.Tenues.Count, "elles se déduisent des pièces"),
            ];
        }

        // Les deux dépôts se valent : une pièce rangée à l'armoire est possédée
        // autant qu'une pièce rangée à la coiffeuse. Ce qui les distingue tient
        // à l'usage, pas à la possession, et c'est pour ça qu'on compte à part
        // celles qui dorment à l'armoire : elles ne servent à aucun glamour tant
        // qu'elles n'ont pas été déposées.
        var pieces = new List<uint>();
        var entieres = new List<uint>();
        var aDeposer = new List<uint>();
        foreach (var tenue in cat.Tenues)
        {
            var complete = true;
            foreach (var p in tenue.Pieces)
            {
                var enCoiffeuse = coffre.Coiffeuse.Contains(p.Objet);
                var enArmoire = coffre.Armoire.Contains(p.Objet);
                if (enCoiffeuse || enArmoire) pieces.Add(p.Objet);
                else complete = false;
                if (enArmoire && !enCoiffeuse) aDeposer.Add(p.Objet);
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
            new Releve("outfitpieces", vues, [.. vues], totalPieces, null, vu),
            new Releve("outfits", entieres, [.. entieres], cat.Tenues.Count),
            new Releve("adeposer", [.. aDeposer.Distinct()], null, totalPieces),
        ];
    }
}
