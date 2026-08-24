using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace CodexOlympiaAuto;

/// <summary>Ce que les deux dépôts contiennent.</summary>
public sealed record Coffre(HashSet<uint> Coiffeuse, HashSet<uint> Armoire);

/// <summary>
/// La lecture des dépôts : la coiffeuse mirage et l'armoire.
///
/// Un ensemble rangé d'un bloc n'occupe qu'un emplacement, et cet emplacement
/// porte l'identifiant de la tenue, pas celui de ses pièces. Le jeu dit ensuite,
/// emplacement par emplacement, lesquelles de ses pièces s'y trouvent : un
/// ensemble déposé peut être incomplet, et on ne retient que ce qui y est.
/// </summary>
public static class Depots
{
    private const uint SeuilHq = 1_000_000;

    public static unsafe Coffre Lire(Catalogue cat, Lumina.Excel.ExcelSheet<MirageStoreSetItem> ensembles)
    {
        var mirage = MirageManager.Instance();
        var coiffeuse = new HashSet<uint>();

        var emplacements = mirage->PrismBoxItemIds;
        for (var i = 0; i < emplacements.Length; i++)
        {
            var v = emplacements[i];
            if (v == 0) continue;
            var net = v >= SeuilHq ? v - SeuilHq : v;

            var set = ensembles.GetRowOrDefault(net);
            var dedans = set is null ? [] : Slots(set.Value);
            if (dedans.Any(x => x != 0))
            {
                for (var k = 0; k < dedans.Length; k++)
                    if (dedans[k] != 0 && mirage->IsSetSlotUnlocked((uint)i, k))
                        coiffeuse.Add(dedans[k]);
            }
            else
            {
                coiffeuse.Add(net);
            }
        }

        var ui = UIState.Instance();
        var armoire = new HashSet<uint>();
        if (ui->Cabinet.IsCabinetLoaded())
            foreach (var t in cat.Tenues)
                foreach (var p in t.Pieces)
                    if (p.Armoire > 0 && ui->Cabinet.IsItemInCabinet(p.Armoire - 1))
                        armoire.Add(p.Objet);

        return new Coffre(coiffeuse, armoire);
    }

    private static uint[] Slots(MirageStoreSetItem s) =>
    [
        s.MainHand.RowId, s.OffHand.RowId, s.Head.RowId, s.Body.RowId, s.Hands.RowId,
        s.Legs.RowId, s.Feet.RowId, s.Earrings.RowId, s.Necklace.RowId, s.Bracelets.RowId,
        s.Ring.RowId,
    ];
}
