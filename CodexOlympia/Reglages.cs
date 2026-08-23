using Dalamud.Configuration;

namespace CodexOlympia;

/// <summary>
/// Ce que le greffon retient d'une session à l'autre.
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

    /// <summary>Le jeton de synchronisation, créé dans la page de compte.</summary>
    public string Jeton { get; set; } = string.Empty;

    /// <summary>
    /// L'identifiant Lodestone de chaque personnage, retenu par identifiant de
    /// sauvegarde. Le jeu ne connaît pas le Lodestone : c'est le joueur qui fait
    /// le lien, une fois par personnage.
    /// </summary>
    public Dictionary<ulong, uint> Personnages { get; set; } = new();

    /// <summary>Un rappel lisible, pour reconnaître une ligne du tableau.</summary>
    public Dictionary<ulong, string> Noms { get; set; } = new();
}
