using System.Net.Http;
using Dalamud.Game.Command;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace CodexOlympia;

/// <summary>
/// Le greffon.
///
/// Il ne surveille rien et n'envoie rien de lui-même. Il attend qu'on le lui
/// demande, lit le jeu, montre ce qu'il a lu, et n'envoie que si on le lui dit.
/// C'est volontaire : une synchronisation qui part toute seule est une
/// synchronisation qu'on ne relit jamais.
/// </summary>
public sealed class Greffon : IDalamudPlugin
{
    private const string Commande = "/codex";

    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commandes;
    private readonly IClientState etat;
    private readonly IPlayerState perso;
    private readonly IDataManager donnees;
    private readonly IPluginLog journal;
    private readonly IChatGui discussion;
    private readonly IGameInventory sacs;
    private readonly IFramework cadence;

    private readonly WindowSystem fenetres = new("CodexOlympia");
    private readonly Fenetre fenetre;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public Reglages Reglages { get; }
    public Catalogue? Catalogue { get; private set; }
    public List<Releve> Releves { get; private set; } = [];
    /// <summary>Ce que la derniere lecture a vu dans les depots.</summary>
    public Coffre? Coffre { get; private set; }

    /// <summary>Combien d'etapes la lecture en cours a franchies, et sur combien.
    /// Zero sur zero quand rien n'est en cours.</summary>
    public int Faites { get; private set; }
    public int AFaire { get; private set; }
    public bool LectureEnCours => AFaire > 0 && Faites < AFaire;
    /// <summary>La collection en train d'etre lue, ou rien.</summary>
    public string? EnCours => LectureEnCours ? Photo.Ordre[Faites] : null;
    public Retour? Dernier { get; private set; }
    public bool EnvoiEnCours { get; private set; }

    /// <summary>Zero tant qu'aucun personnage n'est connecte.</summary>
    public ulong ContentId => etat.IsLoggedIn ? perso.ContentId : 0;

    /// <summary>Le jeton du personnage connecte, ou rien.</summary>
    public string Jeton =>
        ContentId != 0 && Reglages.Jetons.TryGetValue(ContentId, out var j) ? j : string.Empty;

    private List<TenueARanger> aRanger = [];
    private double prochainARanger;

    /// <summary>
    /// Ce que le personnage a sous la main et n'a pas depose.
    ///
    /// Le calcul parcourt les six mille pieces du catalogue : le refaire soixante
    /// fois par seconde pour une liste qui bouge quand on ramasse un objet serait
    /// du gachis. Deux fois par seconde suffit largement a suivre un inventaire.
    /// </summary>
    public List<TenueARanger> ARanger()
    {
        var cat = Catalogue;
        var coffre = Coffre;
        if (cat is null || !cat.Pret || coffre is null) return [];

        var maintenant = Environment.TickCount64 / 1000.0;
        if (maintenant < prochainARanger) return aRanger;
        prochainARanger = maintenant + 0.5;
        try
        {
            aRanger = CodexOlympia.ARanger.Calculer(cat, coffre, Sacs.Miennes(sacs), ServantsDuPerso());
        }
        catch (Exception e)
        {
            journal.Error(e, "lecture des sacs impossible");
        }
        return aRanger;
    }

    public Dictionary<string, uint[]> ServantsDuPerso() =>
        Reglages.Servants.TryGetValue(ContentId, out var s) ? s : [];

    /// <summary>Quand on a regarde le servant ouvert pour la derniere fois.</summary>
    private double prochainServant;

    /// <summary>
    /// Retenir ce que porte le servant a qui on parle.
    ///
    /// Le jeu ne charge son sac que pendant la conversation : c'est la seule
    /// fenetre pendant laquelle on peut voir quoi que ce soit. On la saisit, une
    /// fois par seconde, et on ne garde que les pieces de tenue.
    /// </summary>
    private void Veiller(IFramework _)
    {
        var maintenant = Environment.TickCount64 / 1000.0;
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

    /// <summary>
    /// Un mot quand une piece de tenue arrive dans les sacs.
    ///
    /// Elle n'est pas cochee pour autant : un objet qui traine peut se vendre ou
    /// se jeter. Le message dit ou la mettre pour qu'elle compte, et rien de
    /// plus.
    /// </summary>
    private void PieceArrivee(GameInventoryEvent quoi, InventoryEventArgs e)
    {
        if (!Reglages.AvisEnJeu) return;
        var cat = Catalogue;
        if (cat is null || !cat.Pret) return;
        var id = e.Item.ItemId >= 1_000_000 ? e.Item.ItemId - 1_000_000 : e.Item.ItemId;
        if (id == 0) return;
        // Deja depose : il n'y a rien a aller ranger.
        if (Coffre is not null && (Coffre.Coiffeuse.Contains(id) || Coffre.Armoire.Contains(id))) return;

        foreach (var t in cat.Tenues)
        {
            foreach (var p in t.Pieces)
            {
                if (p.Objet != id) continue;
                discussion.Print("[Codex Olympia] " + Mots.AvisPiece(p.Nom, t.Nom));
                return;
            }
        }
    }

    /// <summary>Range le jeton du personnage connecte. Vide = on l'oublie.</summary>
    public void PoserJeton(string valeur)
    {
        if (ContentId == 0) return;
        var net = valeur.Trim();
        if (net.Length == 0) Reglages.Jetons.Remove(ContentId);
        else Reglages.Jetons[ContentId] = net;
        Enregistrer();
    }

    public Greffon(
        IDalamudPluginInterface pi,
        ICommandManager commandes,
        IClientState etat,
        IPlayerState perso,
        IDataManager donnees,
        IPluginLog journal,
        IChatGui discussion,
        IGameInventory sacs,
        IFramework cadence)
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

        Reglages = pi.GetPluginConfig() as Reglages ?? new Reglages();
        Mots.Choisir(Reglages.Langue, etat.ClientLanguage);

        fenetre = new Fenetre(this);
        fenetres.AddWindow(fenetre);

        pi.UiBuilder.Draw += fenetres.Draw;
        pi.UiBuilder.OpenMainUi += Ouvrir;
        pi.UiBuilder.OpenConfigUi += Ouvrir;

        commandes.AddHandler(Commande, new CommandInfo((_, _) => Ouvrir())
        {
            HelpMessage = Mots.AideCommande,
        });

        sacs.ItemAdded += PieceArrivee;
        cadence.Update += Veiller;

        RechargerCatalogue();
    }

    public void Dispose()
    {
        sacs.ItemAdded -= PieceArrivee;
        cadence.Update -= Veiller;
        commandes.RemoveHandler(Commande);
        pi.UiBuilder.Draw -= fenetres.Draw;
        pi.UiBuilder.OpenMainUi -= Ouvrir;
        pi.UiBuilder.OpenConfigUi -= Ouvrir;
        fenetres.RemoveAllWindows();
        fenetre.Dispose();
        http.Dispose();
    }

    private void Ouvrir()
    {
        RetenirLeNom();
        fenetre.IsOpen = true;
    }

    public void Enregistrer() => pi.SavePluginConfig(Reglages);

    /// <summary>Change la langue de la fenetre et s'en souvient.</summary>
    public void ChoisirLangue(Langue l)
    {
        Reglages.Langue = l;
        Mots.Choisir(l, etat.ClientLanguage);
        Enregistrer();
    }

    /// <summary>Le nom du personnage sert d'étiquette, rien de plus : il dit au
    /// joueur de quel jeton il est en train de parler.</summary>
    private void RetenirLeNom()
    {
        var id = ContentId;
        if (id == 0) return;
        var monde = perso.HomeWorld.ValueNullable?.Name.ExtractText() ?? "?";
        var nom = $"{perso.CharacterName} ({monde})";
        if (Reglages.Noms.TryGetValue(id, out var vieux) && vieux == nom) return;
        Reglages.Noms[id] = nom;
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

    /// <summary>Ouvre une lecture. Rien ne part : on montre d'abord.</summary>
    public void Regarder()
    {
        var cat = Catalogue;
        if (cat is null || !cat.Pret) return;
        Dernier = null;
        Releves = [];
        Coffre = null;
        Faites = 0;
        AFaire = Photo.Ordre.Length;
        prochaine = 0;
        // Les depots vont changer : ce qui restait a ranger n'est plus a jour.
        aRanger = [];
        prochainARanger = 0;
    }

    /// <summary>Quand la prochaine etape a le droit de partir, en secondes.</summary>
    private double prochaine;

    /// <summary>Le temps qu'on laisse a chaque collection.
    ///
    ///  La lecture est bien plus rapide que ca : quatorze collections seraient
    ///  lues en un battement de cil, et le tableau apparaitrait tout fait sans
    ///  qu'on ait rien vu se passer. Une cadence lisible vaut mieux qu'une
    ///  vitesse dont personne ne profite. </summary>
    private const double Cadence = 0.11;

    /// <summary>Avance la lecture d'au plus une etape. Appelee a chaque image,
    /// depuis le fil du jeu : c'est le seul endroit d'ou la memoire du jeu se
    /// lit sans risque.</summary>
    public void Avancer(double maintenant)
    {
        if (!LectureEnCours) return;
        if (maintenant < prochaine) return;
        prochaine = maintenant + Cadence;

        var cat = Catalogue;
        if (cat is null || !cat.Pret)
        {
            AFaire = 0;
            return;
        }
        var cle = Photo.Ordre[Faites];
        try
        {
            var coffre = Coffre;
            Releves.AddRange(
                Photo.Etape(
                    cle,
                    cat,
                    donnees.GetExcelSheet<AozAction>(),
                    donnees.GetExcelSheet<MirageStoreSetItem>(),
                    ref coffre));
            Coffre = coffre;
            Faites++;
        }
        catch (Exception e)
        {
            journal.Error(e, "lecture du jeu impossible ({0})", cle);
            Releves = [];
            AFaire = 0;
            Dernier = new Retour(false, Mots.LectureEchouee(e.Message), [], []);
        }
    }

    public void Envoyer()
    {
        if (EnvoiEnCours || Releves.Count == 0) return;
        var jeton = Jeton;
        if (jeton.Length == 0) return;

        EnvoiEnCours = true;
        var aEnvoyer = Releves;
        _ = Task.Run(async () =>
        {
            try
            {
                Dernier = await Envoi.Deposer(http, Reglages, jeton, aEnvoyer);
                if (Dernier.Ok) discussion.Print("[Codex Olympia] " + Dernier.Message);
            }
            catch (Exception e)
            {
                journal.Error(e, "envoi impossible");
                Dernier = new Retour(false, Mots.ServeurInjoignable(e.Message), [], []);
            }
            finally
            {
                EnvoiEnCours = false;
            }
        });
    }
}
