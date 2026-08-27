namespace CodexOlympia;

/// <summary>
/// Les adresses du site, et elles seules.
///
/// Ce plugin est exclusif à Codex Olympia : il ne parle qu'à son serveur et ne
/// lit que son catalogue. Les adresses sont des constantes, pas des réglages.
/// Elles vivaient auparavant dans la configuration, où n'importe quoi capable
/// d'écrire un fichier JSON pouvait les détourner, jeton et collections avec :
/// un réglage que l'écran ne montre plus n'est pas un réglage, c'est une porte.
/// </summary>
public static class Site
{
    /// <summary>Le serveur qui reçoit les photos.</summary>
    public const string Serveur = "https://ogs-room.olympia-guardian.workers.dev";

    /// <summary>Le catalogue des collections, publié par l'application.</summary>
    public const string Catalogue = "https://codex-olympia.com/data";
}
