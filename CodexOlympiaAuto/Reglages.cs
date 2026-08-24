using Dalamud.Configuration;

namespace CodexOlympiaAuto;

/// <summary>Ce que le plugin retient d'une session à l'autre.</summary>
public sealed class Reglages : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Le catalogue vient du même endroit que l'application.</summary>
    public string Catalogue { get; set; } = "https://olympia-guardian.github.io/data";

    /// <summary>La langue de la fenêtre. Par défaut celle du client de jeu.</summary>
    public Langue Langue { get; set; } = Langue.Auto;

    /// <summary>
    /// Ranger aussi dans l'armoire ce qu'aucune tenue ne prendra.
    ///
    /// Faux par défaut : ce qu'on veut d'abord, ce sont les tenues, et un objet
    /// rangé à l'armoire quitte l'inventaire, donc la coiffeuse ne l'aura plus.
    /// </summary>
    public bool RangerArmoire { get; set; }

    /// <summary>Secondes entre deux gestes du rangement. Trois par défaut :
    /// de quoi suivre ce qui se passe à l'écran.</summary>
    public float CadenceRangement { get; set; } = 3f;

    /// <summary>
    /// Ce que chaque servant portait la dernière fois qu'on lui a parlé, par
    /// personnage puis par nom de servant.
    ///
    /// Le jeu ne charge le sac d'un servant que pendant qu'on lui parle. Sans
    /// cette mémoire, la page oublierait tout dès qu'on referme la fenêtre.
    /// Seules les pièces de tenue y sont gardées.
    /// </summary>
    public Dictionary<ulong, Dictionary<string, uint[]>> Servants { get; set; } = new();
}
