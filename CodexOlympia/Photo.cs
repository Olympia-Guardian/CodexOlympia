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
    string? Empeche = null);

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

    public static unsafe List<Releve> Prendre(Catalogue cat, Lumina.Excel.ExcelSheet<AozAction> sorts)
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
        releves.AddRange(Tenues(cat, ui, armoireLue));

        return releves;
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
    private static unsafe List<Releve> Tenues(Catalogue cat, UIState* ui, bool armoireLue)
    {
        var totalPieces = cat.Tenues.Sum(t => t.Pieces.Count);
        var mirage = MirageManager.Instance();
        if (!mirage->PrismBoxLoaded)
        {
            mirage->PrismBoxRequested = true;
            return
            [
                new Releve("outfitpieces", [], null, totalPieces,
                    "ouvre une fois ta coiffeuse mirage pour que le jeu la charge"),
                new Releve("outfits", [], null, cat.Tenues.Count,
                    "les tenues se déduisent des pièces"),
            ];
        }
        if (!armoireLue)
        {
            return
            [
                new Releve("outfitpieces", [], null, totalPieces,
                    "l'armoire doit être chargée elle aussi : une pièce rangée là passerait pour perdue"),
                new Releve("outfits", [], null, cat.Tenues.Count,
                    "les tenues se déduisent des pièces"),
            ];
        }

        var coiffeuse = new HashSet<uint>();
        foreach (var brut in mirage->PrismBoxItemIds)
        {
            var objet = brut >= SeuilHq ? brut - SeuilHq : brut;
            if (objet != 0) coiffeuse.Add(objet);
        }

        var pieces = new List<uint>();
        var entieres = new List<uint>();
        foreach (var tenue in cat.Tenues)
        {
            var complete = true;
            foreach (var p in tenue.Pieces)
            {
                var la = coiffeuse.Contains(p.Objet)
                    || (p.Armoire > 0 && ui->Cabinet.IsItemInCabinet(p.Armoire - 1));
                if (la) pieces.Add(p.Objet);
                else complete = false;
            }
            if (complete) entieres.Add(tenue.Id);
        }

        return
        [
            new Releve("outfitpieces", [.. pieces.Distinct()], null, totalPieces),
            new Releve("outfits", entieres, null, cat.Tenues.Count),
        ];
    }
}
