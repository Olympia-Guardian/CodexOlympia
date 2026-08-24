using Dalamud.Configuration;

namespace CodexOlympia;

/// <summary>
/// Ce que le plugin retient d'une session à l'autre.
///
/// Le jeton est écrit en clair dans ce fichier, comme n'importe quel réglage.
/// C'est pour cette raison que le serveur ne lui accorde qu'une chose : déposer
/// une photo. Il n'ouvre pas le compte, et quelqu'un qui lit ce fichier ne peut
/// rien en faire d'autre que synchroniser à votre place.
/// </summary>
public sealed class Reglages : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>L'adresse du serveur. Modifiable pour les essais.</summary>
    public string Serveur { get; set; } = "https://ogs-room.olympia-guardian.workers.dev";

    /// <summary>Le catalogue vient du même endroit que l'application.</summary>
    public string Catalogue { get; set; } = "https://olympia-guardian.github.io/data";

    /// <summary>
    /// Un jeton par personnage, retenu par identifiant de sauvegarde.
    ///
    /// Le jeton désigne lui-même le personnage qu'il alimente : c'est décidé
    /// dans l'application, à sa création. Le plugin n'a donc rien à savoir du
    /// Lodestone, et il ne reste qu'un champ à remplir au lieu de deux.
    /// </summary>
    public Dictionary<ulong, string> Jetons { get; set; } = new();

    /// <summary>Un rappel lisible, pour reconnaître une ligne du tableau.</summary>
    public Dictionary<ulong, string> Noms { get; set; } = new();

    /// <summary>La langue de la fenêtre. Par défaut celle du client de jeu.</summary>
    public Langue Langue { get; set; } = Langue.Auto;

    /// <summary>Prévenir dans le journal quand une pièce de tenue arrive.</summary>
    public bool AvisEnJeu { get; set; } = true;

}
