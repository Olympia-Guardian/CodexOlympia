using System.Net.Http;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace CodexOlympiaAuto;

/// <summary>
/// Le plugin d'automatisation. EXPERIMENTAL, en cours de developpement.
///
/// Il fait une seule chose : ranger les pieces de tenue dans la coiffeuse, en
/// suivant exactement les fenetres du jeu. Il ne parle jamais au serveur de
/// Codex Olympia : la synchronisation est l'affaire de l'autre plugin.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    private const string Commande = "/codexauto";

    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commandes;
    private readonly IClientState etat;
    private readonly IPlayerState perso;
    private readonly IDataManager donnees;
    private readonly IPluginLog journal;
    private readonly IChatGui discussion;
    private readonly IGameInventory sacs;
    private readonly IFramework cadence;
    private readonly IGameGui interfaceJeu;

    private readonly WindowSystem fenetres = new("CodexOlympiaAuto");
    private readonly Fenetre fenetre;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public Reglages Reglages { get; }
    public Rangeur Rangeur { get; }
    public Catalogue? Catalogue { get; private set; }
    public IGameGui InterfaceJeu => interfaceJeu;

    public ulong ContentId => etat.IsLoggedIn ? perso.ContentId : 0;

    public Plugin(
        IDalamudPluginInterface pi,
        ICommandManager commandes,
        IClientState etat,
        IPlayerState perso,
        IDataManager donnees,
        IPluginLog journal,
        IChatGui discussion,
        IGameInventory sacs,
        IFramework cadence,
        IGameGui interfaceJeu)
    {
        this.pi = pi;
        this.commandes = commandes;
        this.etat = etat;
        this.perso = perso;
        this.donnees = donnees;
        this.journal = journal;
        this.discussion = discussion;
        this.sacs = sacs;
        this.cadence = cadence;
        this.interfaceJeu = interfaceJeu;

        Reglages = pi.GetPluginConfig() as Reglages ?? new Reglages();
        Mots.Choisir(Reglages.Langue, etat.ClientLanguage);
        Rangeur = new Rangeur(sacs, journal, interfaceJeu);

        fenetre = new Fenetre(this);
        fenetres.AddWindow(fenetre);

        pi.UiBuilder.Draw += fenetres.Draw;
        pi.UiBuilder.OpenMainUi += Ouvrir;
        pi.UiBuilder.OpenConfigUi += Ouvrir;
        cadence.Update += Veiller;

        commandes.AddHandler(Commande, new CommandInfo((_, _) => Ouvrir())
        {
            HelpMessage = "Ouvre la fenetre d'automatisation Codex Olympia.",
        });

        RechargerCatalogue();
    }

    public void Dispose()
    {
        cadence.Update -= Veiller;
        commandes.RemoveHandler(Commande);
        pi.UiBuilder.Draw -= fenetres.Draw;
        pi.UiBuilder.OpenMainUi -= Ouvrir;
        pi.UiBuilder.OpenConfigUi -= Ouvrir;
        fenetres.RemoveAllWindows();
        fenetre.Dispose();
        http.Dispose();
    }

    private void Ouvrir() => fenetre.IsOpen = true;

    public void Enregistrer() => pi.SavePluginConfig(Reglages);

    public void ChoisirLangue(Langue l)
    {
        Reglages.Langue = l;
        Mots.Choisir(l, etat.ClientLanguage);
        Enregistrer();
    }

    public void RechargerCatalogue()
    {
        var cache = Path.Combine(pi.GetPluginConfigDirectory(), "catalogue");
        _ = Task.Run(async () =>
        {
            try
            {
                Catalogue = await Catalogue.Charger(http, Reglages.Catalogue.TrimEnd('/'), cache);
                journal.Information("catalogue charge ({0})", Catalogue.Date);
            }
            catch (Exception e)
            {
                journal.Error(e, "catalogue illisible");
            }
        });
    }

    // ------------------------------------------------------------- la veille

    private double prochainServant;
    private bool ditFini = true;

    /// <summary>Le rangement avance ici, et la memoire des servants se remplit
    /// pendant qu'on leur parle : c'est la seule fenetre pour les voir.</summary>
    private void Veiller(IFramework _)
    {
        var maintenant = Environment.TickCount64 / 1000.0;
        Rangeur.Tic(Catalogue, maintenant);
        if (Rangeur.Etat == EtatRangement.Fini && !ditFini)
        {
            ditFini = true;
            discussion.Print("[Codex Olympia Auto] " + Mots.RangeurFini(Rangeur.Faits, Rangeur.Sautes));
        }
        else if (Rangeur.Etat == EtatRangement.EnMarche)
        {
            ditFini = false;
        }

        if (maintenant < prochainServant) return;
        prochainServant = maintenant + 1.0;

        var cat = Catalogue;
        if (cat is null || !cat.Pret || ContentId == 0) return;
        string? nom;
        try
        {
            nom = Sacs.ServantOuvert();
        }
        catch
        {
            return;
        }
        if (nom is null) return;

        var connues = new HashSet<uint>();
        foreach (var t in cat.Tenues)
            foreach (var p in t.Pieces)
                connues.Add(p.Objet);

        var vus = Sacs.ChezLeServant(sacs).Where(connues.Contains).ToArray();
        if (!Reglages.Servants.TryGetValue(ContentId, out var chez))
        {
            chez = [];
            Reglages.Servants[ContentId] = chez;
        }
        if (chez.TryGetValue(nom, out var avant) && avant.SequenceEqual(vus)) return;
        chez[nom] = vus;
        Enregistrer();
    }

    // ------------------------------------------------------------ les listes

    private Coffre? coffre;
    private List<Egaree> aRanger = [];
    private List<Egaree> doubles = [];
    private double prochainCalcul;

    public Dictionary<string, uint[]> ServantsDuPerso() =>
        Reglages.Servants.TryGetValue(ContentId, out var s) ? s : [];

    /// <summary>Les depots, relus deux fois par seconde : la memoire du jeu se
    /// lit vite, et une liste a jour vaut mieux qu'un bouton de plus.</summary>
    public Coffre? Coffre()
    {
        Calculer();
        return coffre;
    }

    public List<Egaree> ARanger()
    {
        Calculer();
        return aRanger;
    }

    public List<Egaree> Doubles()
    {
        Calculer();
        return doubles;
    }

    private void Calculer()
    {
        var cat = Catalogue;
        if (cat is null || !cat.Pret || ContentId == 0) return;
        var maintenant = Environment.TickCount64 / 1000.0;
        if (maintenant < prochainCalcul) return;
        prochainCalcul = maintenant + 0.5;
        try
        {
            coffre = Depots.Lire(cat, donnees.GetExcelSheet<MirageStoreSetItem>());
            var main = Sacs.Miennes(sacs);
            aRanger = CodexOlympiaAuto.ARanger.Calculer(cat, coffre, main, ServantsDuPerso());
            doubles = CodexOlympiaAuto.ARanger.Doubles(cat, coffre, main);
        }
        catch (Exception e)
        {
            journal.Error(e, "lecture des depots impossible");
        }
    }
}
