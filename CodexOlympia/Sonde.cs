using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace CodexOlympia;

/// <summary>
/// Ce que la fenetre « Ranger » du jeu contient, tel quel.
///
/// <para>Le jeu n'expose aucun appel pour deposer une piece seule dans la
/// coiffeuse : ce geste passe obligatoirement par cette fenetre. Pour la
/// piloter, il faut savoir comment elle range sa liste, et cette disposition ne
/// se lit nulle part ailleurs qu'a l'execution.</para>
///
/// <para>Cette sonde ne fait que LIRE et afficher. Elle ne clique rien, ne
/// depose rien, ne consomme aucun prisme. Automatiser a l'aveugle une operation
/// qui deplace des objets serait le meilleur moyen d'en perdre.</para>
/// </summary>
public static class Sonde
{
    /// <summary>Les fenetres qui nous interessent, dans l'ordre ou on les cherche.</summary>
    public static readonly string[] Fenetres =
    [
        "MiragePrismPrismBoxCrystallize",
        "MiragePrismPrismBox",
        "SelectYesno",
    ];

    public sealed record Valeur(int Index, string Type, string Contenu);

    /// <summary>Les valeurs d'une fenetre ouverte, ou rien si elle ne l'est pas.</summary>
    public static unsafe List<Valeur>? Lire(IGameGui gui, string nom, int combien)
    {
        var addon = (AtkUnitBase*)gui.GetAddonByName(nom).Address;
        if (addon is null || !addon->IsVisible) return null;

        var sortie = new List<Valeur>();
        var n = Math.Min((int)addon->AtkValuesCount, combien);
        for (var i = 0; i < n; i++)
        {
            var v = addon->AtkValues[i];
            var (type, contenu) = Decrire(&v);
            if (contenu.Length == 0) continue;
            sortie.Add(new Valeur(i, type, contenu));
        }
        return sortie;
    }

    private static unsafe (string, string) Decrire(AtkValue* v)
    {
        switch (v->Type)
        {
            case AtkValueType.Int:
                return ("int", v->Int.ToString());
            case AtkValueType.UInt:
                return ("uint", v->UInt.ToString());
            case AtkValueType.Int64:
                return ("int64", v->Int64.ToString());
            case AtkValueType.UInt64:
                return ("uint64", v->UInt64.ToString());
            case AtkValueType.Bool:
                return ("bool", v->Byte != 0 ? "vrai" : "faux");
            case AtkValueType.String:
            case AtkValueType.ConstString:
            {
                var t = v->String.ToString();
                return ("txt", string.IsNullOrWhiteSpace(t) ? string.Empty : t);
            }
            default:
                return (v->Type.ToString(), string.Empty);
        }
    }
}
