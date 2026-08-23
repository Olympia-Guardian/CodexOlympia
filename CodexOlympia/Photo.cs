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
public sealed record Coffre(
    HashSet<uint> Coiffeuse,
    HashSet<uint> Armoire,
    List<string> Echantillon);

/// <summary>Une lecture complète : les relevés, et ce qu'on a vu dans les dépôts.</summary>
public sealed record Lecture(List<Releve> Releves, Coffre Coffre);

/// <summary>
/// La lecture du jeu, à un instant donné.
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
        Lumina.Excel.ExcelSheet<Item> objets)
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
            succes->RequestCompletedAchievements();
            releves.Add(new Releve("achievements", [], null, Total(cat, "achievements"),
                "la liste des succès n'est pas encore arrivée ; réessaie dans un instant"));
        }
        else
        {
            releves.Add(Simple(cat, "achievements", id => id <= int.MaxValue && succes->IsComplete((int)id)));
        }

        // --- L'armoire -------------------------------------------------------
        // Le catalogue numérote les cases à partir de 1, le jeu à partir de 0.
        var armoireLue = ui->Cabinet.IsCabinetLoaded();
        if (!armoireLue)
        {
            releves.Add(new Releve("armoires", [], null, Total(cat, "armoires"),
                "ouvre une fois ton armoire chez un rassembleur pour que le jeu la charge"));
        }
        else
        {
            releves.Add(Simple(cat, "armoires", id => id > 0 && ui->Cabinet.IsItemInCabinet(id - 1)));
        }

        // --- Les sorts bleus -------------------------------------------------
        releves.Add(Sorts(cat, sorts, ui));

        // --- Les pièces de tenue ---------------------------------------------
        var coffre = Coffre(cat, ui, armoireLue, objets);
        releves.AddRange(Tenues(cat, armoireLue, coffre));

        return new Lecture(releves, coffre);
    }

    /// <summary>
    /// Ce que les deux dépôts contiennent, tel quel.
    ///
    /// L'échantillon n'est pas décoratif : quand une lecture ne trouve rien, il
    /// dit si le greffon lit des identifiants d'objet ou tout autre chose. Sans
    /// lui, on en serait réduit à supposer.
    /// </summary>
    private static unsafe Coffre Coffre(
        Catalogue cat, UIState* ui, bool armoireLue, Lumina.Excel.ExcelSheet<Item> objets)
    {
        var mirage = MirageManager.Instance();
        var brut = new List<uint>();
        foreach (var v in mirage->PrismBoxItemIds)
            if (v != 0)
                brut.Add(v);
        if (brut.Count == 0) mirage->PrismBoxRequested = true;

        var coiffeuse = new HashSet<uint>();
        foreach (var v in brut) coiffeuse.Add(v >= SeuilHq ? v - SeuilHq : v);

        var cases = new HashSet<uint>();
        if (armoireLue)
            foreach (var t in cat.Tenues)
                foreach (var p in t.Pieces)
                    if (p.Armoire > 0 && ui->Cabinet.IsItemInCabinet(p.Armoire - 1))
                        cases.Add(p.Objet);

        var echantillon = new List<string>();
        foreach (var v in brut.Take(12))
        {
            var net = v >= SeuilHq ? v - SeuilHq : v;
            var nom = objets.GetRowOrDefault(net)?.Name.ExtractText();
            echantillon.Add(string.IsNullOrEmpty(nom) ? $"{v} : aucun objet de ce numéro" : $"{v} : {nom}");
        }

        return new Coffre(coiffeuse, cases, echantillon);
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

        // Une tenue déposée d'un bloc n'occupe qu'un emplacement, et c'est
        // l'identifiant de la tenue qui y figure, pas celui de ses pièces. Un
        // dépôt de ce genre vaut donc pour toutes ses pièces à la fois.
        var pieces = new List<uint>();
        var entieres = new List<uint>();
        foreach (var tenue in cat.Tenues)
        {
            var enBloc = coffre.Coiffeuse.Contains(tenue.Id);
            var complete = true;
            foreach (var p in tenue.Pieces)
            {
                var la = enBloc || coffre.Coiffeuse.Contains(p.Objet) || coffre.Armoire.Contains(p.Objet);
                if (la) pieces.Add(p.Objet);
                else complete = false;
            }
            if (complete) entieres.Add(tenue.Id);
        }

        return
        [
            new Releve("outfitpieces", [.. pieces.Distinct()], null, totalPieces, null, vu),
            new Releve("outfits", entieres, null, cat.Tenues.Count),
        ];
    }
}
