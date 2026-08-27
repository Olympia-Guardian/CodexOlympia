using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace CodexOlympia;

/// <summary>
/// La synchronisation automatique, et la mesure du neuf.
///
/// Elle ne fait rien qu'une photo à la main ne ferait : mêmes lectures, mêmes
/// règles, mêmes rapports. Ce qu'elle ajoute, c'est le moment : à la connexion,
/// au changement de zone et à intervalle régulier, elle regarde ; et s'il y a
/// du neuf par rapport au dernier envoi, elle envoie. Jamais en combat, en
/// instance ou en cinématique, jamais deux fois en moins d'une minute.
///
/// Le neuf se mesure contre ce que le plugin a envoyé en dernier, retenu par
/// personnage dans les réglages : le plugin ne lit jamais ce que l'application
/// possède, il ne fait que déposer.
/// </summary>
public sealed partial class Plugin
{
    /// <summary>Entre deux lectures automatiques, en secondes.</summary>
    private const double Intervalle = 300;

    /// <summary>Après la connexion : le temps que le jeu charge ce qu'il charge.</summary>
    private const double DelaiConnexion = 20;

    /// <summary>Après un changement de zone : une sortie d'instance apporte
    /// souvent une monture ou un succès, et le jeu met un instant à le savoir.</summary>
    private const double DelaiZone = 30;

    /// <summary>Entre deux envois automatiques, en secondes.</summary>
    private const double EntreDeuxEnvois = 60;

    /// <summary>L'horloge du plugin : des secondes qui ne reculent jamais.</summary>
    private readonly Stopwatch chrono = Stopwatch.StartNew();

    /// <summary>Quand la prochaine lecture automatique doit partir, en secondes
    /// de <see cref="chrono"/>. Zéro : aucune n'est programmée.</summary>
    private double prochaineLecture;

    /// <summary>Vrai une fois la dernière lecture terminée examinée : une lecture
    /// ne déclenche qu'un seul envoi, pas un par image.</summary>
    private bool evalue = true;

    /// <summary>Quand le dernier envoi automatique est parti.</summary>
    private double dernierEnvoiAuto = -EntreDeuxEnvois;

    /// <summary>Ce qu'un envoi réussi laisse à ranger dans les réglages, depuis
    /// le fil du jeu.</summary>
    private (ulong Pour, Dictionary<string, List<uint>> Envoye)? aRetenir;

    /// <summary>Le neuf, calculé une fois par relevé et par envoi, pas à chaque image.</summary>
    private List<(string Cle, List<uint> Ids)>? nouveautes;
    private object? nouveautesPour;
    private int versionEnvoyes;
    private int nouveautesVersion = -1;

    /// <summary>Dans combien de secondes la prochaine lecture automatique, ou
    /// moins que zéro si aucune n'est programmée.</summary>
    public double ProchaineLectureDans =>
        prochaineLecture <= 0 ? -1 : Math.Max(0, prochaineLecture - chrono.Elapsed.TotalSeconds);

    /// <summary>Rien n'a encore été envoyé depuis ce plugin pour ce personnage.</summary>
    public bool JamaisEnvoye => ContentId != 0 && !Reglages.Envoyes.ContainsKey(ContentId);

    private void SurConnexion() => prochaineLecture = chrono.Elapsed.TotalSeconds + DelaiConnexion;

    private void SurDeconnexion(int type, int code) => prochaineLecture = 0;

    private void SurZone(uint zone)
    {
        if (!etat.IsLoggedIn) return;
        var bientot = chrono.Elapsed.TotalSeconds + DelaiZone;
        prochaineLecture = prochaineLecture <= 0 ? bientot : Math.Min(prochaineLecture, bientot);
    }

    /// <summary>Ce qui interdit une lecture ou un envoi automatique : le joueur
    /// est occupé à quelque chose que rien ne doit ralentir.</summary>
    private bool Occupe =>
        condition[ConditionFlag.InCombat]
        || condition[ConditionFlag.BoundByDuty]
        || condition[ConditionFlag.WatchingCutscene]
        || condition[ConditionFlag.WatchingCutscene78]
        || condition[ConditionFlag.OccupiedInCutSceneEvent]
        || condition[ConditionFlag.BetweenAreas]
        || condition[ConditionFlag.BetweenAreas51];

    /// <summary>À chaque image du jeu : la lecture avance, et l'automatique décide.</summary>
    private void Tour(IFramework _)
    {
        var maintenant = chrono.Elapsed.TotalSeconds;
        Avancer(maintenant);

        if (aRetenir is { } r)
        {
            aRetenir = null;
            Retenir(r.Pour, r.Envoye);
        }

        if (!Reglages.SyncAuto) return;
        if (ContentId == 0 || Jeton.Length == 0 || Catalogue?.Pret != true) return;
        if (LectureEnCours || EnVerification || EnvoiEnCours) return;
        if (Occupe) return;

        // Une lecture vient de finir : y a-t-il du neuf ? Une seule fois par lecture.
        if (!evalue && Releves.Count > 0)
        {
            evalue = true;
            var neuf = Nouveautes();
            var n = neuf.Sum(x => x.Ids.Count);
            if (n > 0 && maintenant - dernierEnvoiAuto >= EntreDeuxEnvois)
            {
                dernierEnvoiAuto = maintenant;
                journal.Information("synchro automatique : {0} nouveaute(s), envoi", n);
                Envoyer();
            }
            else if (n > 0)
            {
                journal.Information("synchro automatique : {0} nouveaute(s), trop tot pour envoyer", n);
            }
            return;
        }

        if (prochaineLecture > 0 && maintenant >= prochaineLecture)
        {
            prochaineLecture = maintenant + Intervalle;
            journal.Information("synchro automatique : lecture");
            Regarder();
        }
    }

    /// <summary>Ce que les relevés courants ont de plus que le dernier envoi,
    /// par collection. Tout, si rien n'a jamais été envoyé.</summary>
    public List<(string Cle, List<uint> Ids)> Nouveautes()
    {
        if (nouveautes is not null && ReferenceEquals(nouveautesPour, Releves) && nouveautesVersion == versionEnvoyes)
            return nouveautes;
        var sortie = new List<(string Cle, List<uint> Ids)>();
        var id = ContentId;
        if (id != 0)
        {
            Reglages.Envoyes.TryGetValue(id, out var envoyes);
            foreach (var x in Releves)
            {
                if (x.Empeche is not null || x.Cle == "adeposer" || x.Trouves.Count == 0) continue;
                HashSet<uint>? deja = envoyes is not null && envoyes.TryGetValue(x.Cle, out var l) ? [.. l] : null;
                var neuf = deja is null ? [.. x.Trouves] : x.Trouves.Where(t => !deja.Contains(t)).ToList();
                if (neuf.Count > 0) sortie.Add((x.Cle, neuf));
            }
        }
        nouveautes = sortie;
        nouveautesPour = Releves;
        nouveautesVersion = versionEnvoyes;
        return sortie;
    }

    /// <summary>Les listes d'une photo telles qu'elles partent : par collection,
    /// sans les lignes empêchées ni le conseil de rangement.</summary>
    private static Dictionary<string, List<uint>> Photographie(IReadOnlyList<Releve> releves)
    {
        var photo = new Dictionary<string, List<uint>>();
        foreach (var x in releves)
        {
            if (x.Empeche is not null || x.Cle == "adeposer") continue;
            photo[x.Cle] = [.. x.Trouves];
        }
        return photo;
    }

    /// <summary>Range ce qui vient d'être envoyé : collection par collection,
    /// sans toucher à celles que cet envoi ne portait pas.</summary>
    private void Retenir(ulong pour, Dictionary<string, List<uint>> envoye)
    {
        if (pour == 0 || envoye.Count == 0) return;
        if (!Reglages.Envoyes.TryGetValue(pour, out var d)) Reglages.Envoyes[pour] = d = new Dictionary<string, List<uint>>();
        foreach (var (cle, ids) in envoye) d[cle] = ids;
        versionEnvoyes++;
        Enregistrer();
    }
}
