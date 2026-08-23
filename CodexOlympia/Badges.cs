using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;

namespace CodexOlympia;

/// <summary>
/// Une pastille sur les objets qu'il reste à déposer.
///
/// <para>Le jeu ne dit pas quel sac une grille affiche. Le deviner à partir de
/// l'onglet ouvert obligerait à connaître la disposition de cinq fenêtres
/// différentes, et cette disposition change à chaque grande mise à jour.</para>
///
/// <para>On l'identifie donc par <b>empreinte</b> : la suite des icônes d'une
/// grille est comparée à la suite des icônes de chaque sac, et celui qui
/// correspond est celui qu'on regarde. C'est de la donnée, pas de la
/// disposition : ça survit aux mises à jour, et quand rien ne correspond on ne
/// dessine rien plutôt que de dessiner faux.</para>
/// </summary>
public static class Badges
{
    /// <summary>Les grilles possibles. Celles qui n'existent pas sont ignorées :
    /// la liste peut donc rester généreuse sans risque.</summary>
    private static readonly string[] Grilles =
    [
        "InventoryGrid", "InventoryGrid0", "InventoryGrid1", "InventoryGrid2", "InventoryGrid3",
        "InventoryGrid0E", "InventoryGrid1E", "InventoryGrid2E", "InventoryGrid3E",
    ];

    private static readonly GameInventoryType[] Sacs =
    [
        GameInventoryType.Inventory1,
        GameInventoryType.Inventory2,
        GameInventoryType.Inventory3,
        GameInventoryType.Inventory4,
    ];

    private const uint SeuilHq = 1_000_000;

    public static unsafe void Dessiner(
        IGameGui gui,
        IGameInventory inv,
        Lumina.Excel.ExcelSheet<Item> objets,
        HashSet<uint> cibles)
    {
        if (cibles.Count == 0 || gui.GameUiHidden) return;

        // L'empreinte de chaque sac, et ce qu'il contient case par case.
        var empreintes = new List<(int[] Icones, uint[] Ids)>();
        foreach (var sac in Sacs)
        {
            var items = inv.GetInventoryItems(sac);
            if (items.Length == 0) continue;
            var icones = new int[items.Length];
            var ids = new uint[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                var id = items[i].ItemId >= SeuilHq ? items[i].ItemId - SeuilHq : items[i].ItemId;
                ids[i] = id;
                icones[i] = id == 0 ? 0 : objets.GetRowOrDefault(id)?.Icon ?? 0;
            }
            empreintes.Add((icones, ids));
        }
        if (empreintes.Count == 0) return;

        var dessin = ImGui.GetBackgroundDrawList();
        foreach (var nom in Grilles)
        {
            var grille = (AddonInventoryGrid*)gui.GetAddonByName(nom).Address;
            if (grille is null || !grille->AtkUnitBase.IsVisible) continue;

            var cases = grille->Slots;
            if (cases.Length == 0) continue;

            // Les icônes affichées, dans l'ordre des cases.
            var vues = new int[cases.Length];
            for (var i = 0; i < cases.Length; i++)
            {
                var c = cases[i].Value;
                vues[i] = c is null ? 0 : c->GetIconId();
            }

            var sac = Reconnaitre(empreintes, vues);
            if (sac is null) continue;

            var echelle = grille->AtkUnitBase.Scale;
            for (var i = 0; i < cases.Length && i < sac.Value.Ids.Length; i++)
            {
                if (!cibles.Contains(sac.Value.Ids[i])) continue;
                var c = cases[i].Value;
                if (c is null) continue;
                var n = c->OwnerNode;
                if (n is null || !n->AtkResNode.IsVisible()) continue;

                var x = n->AtkResNode.ScreenX;
                var y = n->AtkResNode.ScreenY;
                var l = n->AtkResNode.GetWidth() * echelle;
                Pastille(dessin, new Vector2(x + l - 6 * echelle, y + 6 * echelle), 5.5f * echelle);
            }
        }
    }

    /// <summary>Le sac dont l'empreinte correspond, si un seul correspond.
    ///
    /// <para>Deux sacs vides ont la même empreinte : on ne tranche pas, et on ne
    /// dessine rien. C'est sans conséquence, un sac vide n'a rien à marquer.</para>
    /// </summary>
    private static (int[] Icones, uint[] Ids)? Reconnaitre(
        List<(int[] Icones, uint[] Ids)> empreintes, int[] vues)
    {
        (int[] Icones, uint[] Ids)? trouve = null;
        foreach (var e in empreintes)
        {
            if (e.Icones.Length != vues.Length) continue;
            var pareil = true;
            for (var i = 0; i < vues.Length; i++)
            {
                if (e.Icones[i] == vues[i]) continue;
                pareil = false;
                break;
            }
            if (!pareil) continue;
            if (trouve is not null) return null; // deux candidats : on s'abstient
            trouve = e;
        }
        return trouve;
    }

    /// <summary>Un point doré cerné de noir : lisible sur n'importe quelle icône,
    /// sans cacher ce qu'elle montre.</summary>
    private static void Pastille(ImDrawListPtr dessin, Vector2 centre, float rayon)
    {
        dessin.AddCircleFilled(centre, rayon + 1.5f, 0xFF000000);
        dessin.AddCircleFilled(centre, rayon, 0xFF4FBFEF);
    }
}
