using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.Exd;
using FFXIVClientStructs.STD;
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
    int NonLues = 0);

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

    /// <summary>L'ordre de lecture, qui est aussi l'ordre d'affichage : une
    /// lecture qui remplit de haut en bas se suit des yeux.</summary>
    public static readonly string[] Ordre =
    [
        "mounts", "minions", "orchestrions", "emotes", "hairstyles", "fashions",
        "facewear", "bardings", "cards", "frames", "spells", "beastmaster", "achievements",
        "quests", "armoires", "outfitpieces",
    ];

    /// <summary>Fait UNE étape de la lecture. Les pointeurs du jeu sont repris
    /// à chaque étape et jamais gardés d'une image sur l'autre : ce qui est
    /// valide maintenant ne l'est pas forcément dans deux secondes.</summary>
    public static unsafe List<Releve> Etape(
        string cle,
        Catalogue cat,
        Lumina.Excel.ExcelSheet<AozAction> sorts,
        Lumina.Excel.ExcelSheet<MirageStoreSetItem> ensembles,
        Lumina.Excel.ExcelSheet<Item> objets,
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

            // Sans objet déverrouillant au catalogue, une entrée n'est pas
            // interrogeable : elle sort de la portée.
            case "hairstyles":
            case "bardings":
            case "frames":
                return [ParObjet(cat, cle)];

            case "spells":
                return [Sorts(cat, sorts, ui)];

            case "beastmaster":
                return [Bestiaire(cat)];

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

            default:
                return [];
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

    private static unsafe Releve ParObjet(Catalogue cat, string cle)
    {
        if (!cat.Ids.TryGetValue(cle, out var ids) || !cat.Objets.TryGetValue(cle, out var objets))
            return new Releve(cle, [], null, 0, Mots.CatalogueAbsent);

        var ui = UIState.Instance();
        var trouves = new List<uint>();
        var portee = new List<uint>();
        // Deux façons de sortir de la portée, qui n'ont rien à voir (PLG-R44) :
        // sans objet au catalogue, c'est définitif et ça se coche à la main ;
        // objet connu mais ligne pas encore chargée, c'est une lecture en
        // retard que la revérification reprend.
        var nonLues = 0;
        foreach (var id in ids)
        {
            if (!objets.TryGetValue(id, out var objet) || objet == 0) continue;
            var ligne = ExdModule.GetItemRowById(objet);
            if (ligne is null)
            {
                nonLues++;
                continue;
            }
            portee.Add(id);
            if (ui->IsItemActionUnlocked(ligne) == 1) trouves.Add(id);
        }
        // Portée déclarée seulement si elle est incomplète : sinon c'est du poids
        // sur le réseau pour rien.
        var complet = portee.Count == ids.Length;
        return new Releve(
            cle, trouves, complet ? null : portee, ids.Length, null, null,
            complet ? Limite.Aucune : Limite.Capacite, nonLues);
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

    /// <summary>Où le module XBMNote range son vecteur : juste après l'en-tête
    /// commun des fichiers de sauvegarde client (0x48), comme ses deux voisins
    /// de même taille, MKDSupportJobNote et MKDLore, que FFXIVClientStructs
    /// décrit.</summary>
    private const int DecalageVecteurNote = 0x48;

    /// <summary>Une entrée du bestiaire tient sur quatre octets : le numéro de la
    /// bête (sa ligne dans XBMPet), deux octets dont on ignore le sens (vus à 0,
    /// 1 et 2, puis toujours 1), et un octet nul.</summary>
    private const int TailleEntreeNote = 4;

    /// <summary>
    /// Le bestiaire du dresseur (7.56).
    ///
    /// Le jeu le tient dans son module XBMNote, un fichier de sauvegarde client
    /// comme le carnet des jobs de soutien du Croissant occulte : un en-tête,
    /// puis un vecteur d'entrées, UNE PAR BÊTE ENREGISTRÉE, dans l'ordre où
    /// elles sont entrées. Une bête qui n'y figure pas n'a pas été capturée.
    /// Vérifié le 9 septembre 2026 devant le bestiaire de Vincent : quarante-
    /// quatre entrées, « 44/50 » à l'écran, les six absentes étant les bêtes
    /// de donjon et de raid qu'il n'avait pas encore. FFXIVClientStructs ne
    /// décrit pas ce module ; on lit là où ses voisins rangent leur vecteur.
    /// </summary>
    private static Releve Bestiaire(Catalogue cat)
    {
        const string cle = "beastmaster";
        if (!cat.Ids.TryGetValue(cle, out var ids))
            return new Releve(cle, [], null, 0, Mots.CatalogueAbsent);
        var octets = OctetsBestiaire();
        if (octets is null)
            return new Releve(cle, [], null, ids.Length, Mots.BestiaireNonCharge);
        var entrees = EntreesBestiaire(octets);
        // Une forme qui n'est plus celle qu'on connaît (un patch a pu changer
        // l'entrée) ne donne rien plutôt qu'une collection fausse.
        if (entrees is null)
            return new Releve(cle, [], null, ids.Length, Mots.BestiaireFormeInattendue);
        var trouves = ids.Where(entrees.Contains).ToList();
        return new Releve(cle, trouves, null, ids.Length);
    }

    /// <summary>Les numéros des bêtes enregistrées, ou rien si le vecteur n'a
    /// pas la forme attendue : des entrées de quatre octets, un numéro non nul
    /// et jamais répété en tête, un octet nul en queue.</summary>
    private static HashSet<uint>? EntreesBestiaire(byte[] octets)
    {
        if (octets.Length % TailleEntreeNote != 0) return null;
        var ids = new HashSet<uint>();
        for (var i = 0; i < octets.Length; i += TailleEntreeNote)
        {
            var id = octets[i];
            if (id == 0 || octets[i + 3] != 0 || !ids.Add(id)) return null;
        }
        return ids;
    }

    /// <summary>Le vecteur du module XBMNote, copié. Rien si le jeu ne l'a pas
    /// chargé (personne de connecté) ou s'il n'a pas la forme attendue.</summary>
    private static unsafe byte[]? OctetsBestiaire()
    {
        var ui = UIModule.Instance();
        if (ui == null) return null;
        var module = (byte*)ui->GetXBMNoteModule();
        if (module == null) return null;
        var vecteur = (StdVector<byte>*)(module + DecalageVecteurNote);
        if (vecteur->First == null || vecteur->Last == null || vecteur->Last < vecteur->First) return null;
        var n = vecteur->Last - vecteur->First;
        // Cinquante bêtes tiennent dans quelques dizaines d'octets : bien
        // au-delà, ce n'est pas le vecteur qu'on croit.
        if (n == 0 || n > 4096) return null;
        var copie = new byte[n];
        for (var i = 0; i < n; i++) copie[i] = vecteur->First[i];
        return copie;
    }

    /// <summary>Ce que /codex bestiaire écrit dans le journal Dalamud : les
    /// octets bruts des deux modules du dresseur, puis chaque entrée du vecteur
    /// décodée, numéro, nom et les deux octets qu'on ne sait pas lire. C'est
    /// avec ça qu'on vérifie la forme, en jeu, devant le bestiaire.</summary>
    public static unsafe string DiagnosticBestiaire(Catalogue cat)
    {
        var ui = UIModule.Instance();
        if (ui == null) return "UIModule : absent";
        var sb = new System.Text.StringBuilder();
        var note = (byte*)ui->GetXBMNoteModule();
        var xbm = (byte*)ui->GetXBMModule();
        sb.AppendLine("XBMNoteModule (0x60) : " + Hex(note, 0x60));
        sb.AppendLine("XBMModule (0x88) : " + Hex(xbm, 0x88));
        var octets = OctetsBestiaire();
        if (octets is null)
        {
            sb.AppendLine("vecteur XBMNote : absent ou vide");
            return sb.ToString();
        }
        sb.AppendLine($"vecteur XBMNote : {octets.Length} octets : {BitConverter.ToString(octets)}");
        var entrees = EntreesBestiaire(octets);
        if (entrees is null)
        {
            sb.AppendLine("entrées : forme inattendue");
            return sb.ToString();
        }
        sb.AppendLine($"entrées : {entrees.Count}");
        for (var i = 0; i + TailleEntreeNote <= octets.Length; i += TailleEntreeNote)
            sb.AppendLine($"  {octets[i],3} {cat.Nom("beastmaster", octets[i])} : {octets[i + 1]} {octets[i + 2]}");
        var ids = cat.Ids.GetValueOrDefault("beastmaster") ?? [];
        var absentes = ids.Where(id => !entrees.Contains(id)).ToList();
        sb.AppendLine($"absentes du bestiaire : {absentes.Count} : " +
                      string.Join(", ", absentes.Select(id => $"{id} {cat.Nom("beastmaster", id)}")));
        return sb.ToString();
    }

    /// <summary>Le résumé pour le journal de discussion : les bêtes
    /// enregistrées, avec leurs noms.</summary>
    public static string ResumeBestiaire(Catalogue cat)
    {
        var octets = OctetsBestiaire();
        var ids = cat.Ids.GetValueOrDefault("beastmaster") ?? [];
        if (octets is null) return Mots.BestiaireNonCharge;
        var entrees = EntreesBestiaire(octets);
        if (entrees is null) return Mots.BestiaireFormeInattendue;
        var trouves = ids.Where(entrees.Contains).Select(id => cat.Nom("beastmaster", id)).ToList();
        return Mots.BestiaireLu(trouves.Count, ids.Length, string.Join(", ", trouves));
    }

    private static unsafe string Hex(byte* p, int n)
    {
        if (p == null) return "null";
        var sb = new System.Text.StringBuilder(n * 3);
        for (var i = 0; i < n; i++)
        {
            if (i > 0 && i % 8 == 0) sb.Append(' ');
            sb.Append(p[i].ToString("X2"));
        }
        return sb.ToString();
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
