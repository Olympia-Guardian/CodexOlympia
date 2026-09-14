namespace CodexOlympia;

/// <summary>
/// Les adresses du site, et elles seules.
///
/// Des constantes, pas des réglages : dans la configuration, n'importe quoi
/// capable d'écrire un fichier JSON pouvait les détourner, jeton et
/// collections avec.
/// </summary>
public static class Site
{
    /// <summary>Le serveur qui reçoit les photos.</summary>
    public const string Serveur = "https://ogs-room.olympia-guardian.workers.dev";

    /// <summary>Le catalogue des collections, publié par l'application.</summary>
    public const string Catalogue = "https://codex-olympia.com/data";
}
