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
    /// <summary>2 depuis les filtres de la galerie (PLG-R67) : une
    /// configuration plus ancienne passe par <see cref="Passer"/>.</summary>
    public int Version { get; set; } = 2;

    /// <summary>
    /// Un jeton par personnage, retenu par identifiant de sauvegarde.
    ///
    /// Le jeton désigne lui-même le personnage qu'il alimente : c'est décidé
    /// dans l'application, à sa création. Le plugin n'a donc rien à savoir du
    /// Lodestone, et il ne reste qu'un champ à remplir au lieu de deux.
    /// </summary>
    public Dictionary<ulong, string> Jetons { get; set; } = new();

    /// <summary>Un rappel lisible : de quel jeton on parle.</summary>
    public Dictionary<ulong, string> Noms { get; set; } = new();

    /// <summary>La langue de la fenêtre. Par défaut celle du client de jeu.</summary>
    public Langue Langue { get; set; } = Langue.Auto;

    /// <summary>Prévenir dans le journal quand une pièce de tenue arrive.</summary>
    public bool AvisEnJeu { get; set; } = true;

    /// <summary>
    /// Synchroniser sans geste : lire à la connexion, au changement de zone et
    /// à intervalle régulier, et envoyer dès qu'il y a du neuf.
    ///
    /// Éteint par défaut. C'est un accord donné une fois pour toutes, pas une
    /// surprise : l'envoi automatique ne fait rien qu'un envoi à la main ne
    /// ferait, avec les mêmes lectures et les mêmes règles.
    /// </summary>
    public bool SyncAuto { get; set; }

    /// <summary>Les filtres de la galerie (PLG-R67), gardés d'une session à
    /// l'autre, ici et jamais dans le compte.</summary>
    public FiltresGalerie Filtres { get; set; } = new();

    /// <summary>L'ancien réglage qui cachait la boutique, lu une fois pour son
    /// passage vers l'exclusion de la boutique.</summary>
    public bool CacherBoutique { get; set; }

    /// <summary>L'ancien réglage qui cachait ce qui ne s'obtient plus, lu une
    /// fois pour son passage vers l'exclusion du limité.</summary>
    public bool CacherInobtenables { get; set; }

    /// <summary>
    /// Remet la configuration relue à la forme du jour. Qui cachait la boutique
    /// ou ce qui ne s'obtient plus retrouve les exclusions correspondantes
    /// (PLG-R67). Rend vrai quand quelque chose a changé et mérite d'être écrit.
    /// </summary>
    public bool Passer()
    {
        Filtres ??= new FiltresGalerie();
        Garde.Normaliser(Filtres);
        if (Version >= 2) return false;
        if (CacherBoutique) Filtres.Exclure.Add("boutique");
        if (CacherInobtenables) Filtres.Exclure.Add("limite");
        Garde.Normaliser(Filtres);
        CacherBoutique = false;
        CacherInobtenables = false;
        Version = 2;
        return true;
    }

    /// <summary>
    /// Ce que le plugin a envoyé en dernier, par personnage puis par collection.
    ///
    /// C'est contre cela que le neuf se mesure, jamais contre ce que
    /// l'application possède : le plugin ne la lit pas, il ne fait que déposer.
    /// </summary>
    public Dictionary<ulong, Dictionary<string, List<uint>>> Envoyes { get; set; } = new();

    /// <summary>
    /// La façon dont chaque collection a été lue pour ce qui est parti, par
    /// personnage puis par collection. Absente, elle vaut 1, la première.
    ///
    /// Une lecture corrigée en change : ce que l'ancienne avait envoyé ne sert
    /// alors plus de référence (PLG-R64).
    /// </summary>
    public Dictionary<ulong, Dictionary<string, int>> Manieres { get; set; } = new();

    /// <summary>
    /// Ce que la dernière photo a vu dans les dépôts, par personnage.
    ///
    /// L'avis en jeu s'en sert dès la connexion, avant toute nouvelle photo.
    /// Sans cette mémoire, chaque session repartait de rien : le plugin
    /// conseillait de déposer des pièces qu'il avait lui-même vues rangées la
    /// veille, dès qu'un coffre les faisait défiler.
    /// </summary>
    public Dictionary<ulong, Depots> Depots { get; set; } = new();

    /// <summary>
    /// Ce que les éditeurs de portrait ont montré, par personnage puis par
    /// portrait : débloqué ou non (PLG-R65).
    ///
    /// Le client ne garde les conditions des portraits que tant qu'un de ces
    /// éditeurs est ouvert. Fenêtre fermée, la lecture automatique reprend
    /// cette mémoire, pour ce qui est débloqué seulement : un portrait obtenu
    /// depuis attend la prochaine ouverture. PlatePeek fait de même.
    /// </summary>
    public Dictionary<ulong, Dictionary<uint, bool>> Portraits { get; set; } = new();
}

/// <summary>
/// Le contenu des deux dépôts, sous la forme que les réglages savent écrire et
/// relire : des listes. Un dépôt jamais lu reste vide, et un dépôt vide ne
/// remplace jamais une mémoire pleine (voir <c>Plugin.RetenirDepots</c>).
/// </summary>
public sealed class Depots
{
    public List<uint> Coiffeuse { get; set; } = [];
    public List<uint> Armoire { get; set; } = [];
}
