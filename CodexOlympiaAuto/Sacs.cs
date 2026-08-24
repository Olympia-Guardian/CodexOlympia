using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace CodexOlympiaAuto;

/// <summary>Une pièce trouvée quelque part, et où.</summary>
public sealed record Trouvaille(uint Objet, string Ou);

/// <summary>
/// Ce que le joueur possède sans l'avoir déposé.
///
/// Une pièce d'équipement rangée dans un sac, portée sur soi, ou confiée à un
/// servant est possédée. Elle ne compte pourtant pour rien tant qu'elle n'est
/// pas dans un dépôt définitif : elle peut se vendre, se jeter, se perdre de
/// vue. Ce module ne la coche donc jamais. Il dit seulement où elle est, pour
/// qu'on aille la ranger.
///
/// Les servants sont un cas à part : le jeu ne charge leur contenu que pendant
/// qu'on leur parle. On retient donc ce qu'on a vu la dernière fois, en le
/// datant, parce qu'une liste vieille de trois jours vaut mieux qu'une liste
/// vide.
/// </summary>
public static class Sacs
{
    /// <summary>Les contenants du joueur, avec un nom lisible pour chacun.</summary>
    private static readonly (GameInventoryType Type, bool Sac)[] Miens =
    [
        (GameInventoryType.Inventory1, true),
        (GameInventoryType.Inventory2, true),
        (GameInventoryType.Inventory3, true),
        (GameInventoryType.Inventory4, true),
        (GameInventoryType.ArmoryMainHand, false),
        (GameInventoryType.ArmoryOffHand, false),
        (GameInventoryType.ArmoryHead, false),
        (GameInventoryType.ArmoryBody, false),
        (GameInventoryType.ArmoryHands, false),
        (GameInventoryType.ArmoryLegs, false),
        (GameInventoryType.ArmoryFeets, false),
        (GameInventoryType.ArmoryEar, false),
        (GameInventoryType.ArmoryNeck, false),
        (GameInventoryType.ArmoryWrist, false),
        (GameInventoryType.ArmoryRings, false),
    ];

    private static readonly GameInventoryType[] Cabas =
    [
        GameInventoryType.SaddleBag1,
        GameInventoryType.SaddleBag2,
        GameInventoryType.PremiumSaddleBag1,
        GameInventoryType.PremiumSaddleBag2,
    ];

    private static readonly GameInventoryType[] Servant =
    [
        GameInventoryType.RetainerPage1,
        GameInventoryType.RetainerPage2,
        GameInventoryType.RetainerPage3,
        GameInventoryType.RetainerPage4,
        GameInventoryType.RetainerPage5,
        GameInventoryType.RetainerPage6,
        GameInventoryType.RetainerPage7,
        GameInventoryType.RetainerEquippedItems,
    ];

    /// <summary>Les objets marchands portent un décalage qu'on retire.</summary>
    private const uint SeuilHq = 1_000_000;

    private static void Verser(IGameInventory inv, GameInventoryType type, string ou, List<Trouvaille> dans)
    {
        foreach (var it in inv.GetInventoryItems(type))
        {
            if (it.IsEmpty) continue;
            var id = it.ItemId >= SeuilHq ? it.ItemId - SeuilHq : it.ItemId;
            if (id != 0) dans.Add(new Trouvaille(id, ou));
        }
    }

    /// <summary>Tout ce que le personnage a sous la main, ici et maintenant.</summary>
    public static List<Trouvaille> Miennes(IGameInventory inv)
    {
        var sortie = new List<Trouvaille>();
        foreach (var (type, sac) in Miens) Verser(inv, type, sac ? Mots.OuSac : Mots.OuArmurerie, sortie);
        Verser(inv, GameInventoryType.EquippedItems, Mots.OuPorte, sortie);
        // Le cabas n'est chargé que si on l'a ouvert : une liste vide n'y veut
        // pas dire qu'il est vide.
        foreach (var type in Cabas) Verser(inv, type, Mots.OuCabas, sortie);
        return sortie;
    }

    /// <summary>Le nom du servant à qui on parle, ou rien.</summary>
    public static unsafe string? ServantOuvert()
    {
        var m = RetainerManager.Instance();
        if (m is null || !m->IsReady) return null;
        var r = m->GetActiveRetainer();
        if (r is null) return null;
        var nom = r->NameString;
        return string.IsNullOrWhiteSpace(nom) ? null : nom;
    }

    /// <summary>Ce que le servant ouvert porte, à ne garder que si on lui parle.</summary>
    public static List<uint> ChezLeServant(IGameInventory inv)
    {
        var brut = new List<Trouvaille>();
        foreach (var type in Servant) Verser(inv, type, string.Empty, brut);
        return [.. brut.Select(t => t.Objet).Distinct()];
    }
}
