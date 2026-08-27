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
/// Le plugin de synchronisation.
///
/// Il ne surveille rien et n'envoie rien de lui-meme. Il attend qu'on le lui
/// demande, lit le jeu, montre ce qu'il a lu, et n'envoie que si on le lui dit.
/// Le rangement automatique est l'affaire de l'autre plugin, Codex Olympia
/// Automatisation : celui-ci ne fait qu'ajouter un mot dans le journal quand
/// une piece de tenue arrive.
/// </summary>
public sealed class Plugin : IDalamudPlugin
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
    public ITextureProvider Textures { get; }

    private readonly WindowSystem fenetres = new("CodexOlympia");
    private readonly Fenetre fenetre;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public Reglages Reglages { get; }
    public Catalogue? Catalogue { get; private set; }
    public List<Releve> Releves { get; private set; } = [];
    /// <summary>Ce que la derniere lecture a vu dans les depots.</summary>
    public Coffre? Coffre { get; private set; }

    /// <summary>Les depots tels que la derniere photo les a vus, relus des
    /// reglages pour le personnage connecte : la memoire qui survit a la
    /// session. L'avis en jeu s'en sert tant qu'aucune photo n'a ete reprise.</summary>
    private Coffre? depotsRetenus;

    /// <summary>Le personnage pour lequel <see cref="depotsRetenus"/> a ete relu.</summary>
    private ulong depotsPour;

    /// <summary>Les etapes qui restent a lire. La lecture complete y met tout
    /// Photo.Ordre ; une relecture ciblee n'y met qu'une collection et ce qui
    /// depend d'elle.</summary>
    private readonly Queue<string> file = new();

    /// <summary>Combien d'etapes la lecture en cours a franchies, et sur combien.
    /// Zero sur zero quand rien n'est en cours.</summary>
    public int Faites { get; private set; }
    public int AFaire { get; private set; }
    public bool LectureEnCours => file.Count > 0;
    /// <summary>L'etape en train d'etre lue, ou rien.</summary>
    public string? EnCours => file.Count > 0 ? file.Peek() : null;

    /// <summary>L'etape qui lira cette collection : les tenues et les pieces
    /// sortent de l'etape « outfitpieces », tout le reste porte son nom.</summary>
    public static string EtapeDe(string cle) => cle == "outfits" ? "outfitpieces" : cle;

    /// <summary>Ce qu'une etape produit comme releves : c'est ce qu'il faut
    /// retirer avant de la rejouer, sans toucher au reste.</summary>
    private static string[] EmisPar(string etape) =>
        etape == "outfitpieces" ? ["outfitpieces", "outfits", "adeposer"] : [etape];

    /// <summary>Vrai si cette collection attend son tour dans la file.</summary>
    public bool EnFile(string cle) => file.Contains(EtapeDe(cle));

    /// <summary>Quand la revérification doit partir, en secondes de jeu. Zéro :
    /// aucune n'est due.</summary>
    private double reverifieA;

    /// <summary>Les collections lues objet par objet : le jeu leur répond parfois
    /// « rien » à la première lecture après la connexion, et tout à la seconde.
    /// Plutôt que de demander au joueur de relancer, on relit nous-mêmes.</summary>
    private static readonly string[] ParObjet = ["hairstyles", "facewear", "bardings", "frames"];

    /// <summary>Le délai entre deux relectures : assez pour que le jeu ait fini
    /// de charger ce qu'il chargeait, assez court pour qu'on ne l'attende pas.</summary>
    private const double DelaiReverification = 2.0;

    /// <summary>Combien de relectures avant de conclure que zéro est la vraie
    /// réponse. Un personnage qui n'a aucune barde renverra zéro pour toujours,
    /// et rien ne distingue ce zéro-là d'une table pas encore chargée : sans
    /// plafond, on relirait jusqu'à la déconnexion.</summary>
    private const int MaxRelectures = 8;

    /// <summary>Relectures automatiques faites depuis la dernière lecture complète.</summary>
    private int relectures;

    /// <summary>Vrai tant que la chaîne de revérification n'a pas conclu : rien ne
    /// part pendant ce temps, un relevé à zéro pourrait être un relevé en retard.</summary>
    public bool EnVerification { get; private set; }

    /// <summary>Une collection lue à zéro alors que le jeu savait répondre : celle
    /// que la chaîne relit.</summary>
    public bool Douteuse(string cle) =>
        ParObjet.Contains(cle)
        && Releves.Any(r => r.Cle == cle && r.Empeche is null && r.Trouves.Count == 0 && r.Total > 0);
    public Retour? Dernier { get; private set; }
    public bool EnvoiEnCours { get; private set; }

    /// <summary>Zero tant qu'aucun personnage n'est connecte.</summary>
    public ulong ContentId => etat.IsLoggedIn ? perso.ContentId : 0;

    /// <summary>Le jeton du personnage connecte, ou rien.</summary>
    public string Jeton =>
        ContentId != 0 && Reglages.Jetons.TryGetValue(ContentId, out var j) ? j : string.Empty;

    public Plugin(
        IDalamudPluginInterface pi,
        ICommandManager commandes,
        IClientState etat,
        IPlayerState perso,
        IDataManager donnees,
        IPluginLog journal,
        IChatGui discussion,
        IGameInventory sacs,
        ITextureProvider textures)
    {
        Textures = textures;
        this.pi = pi;
        this.commandes = commandes;
        this.etat = etat;
        this.perso = perso;
        this.donnees = donnees;
        this.journal = journal;
        this.discussion = discussion;
        this.sacs = sacs;

        Reglages = pi.GetPluginConfig() as Reglages ?? new Reglages();
        Mots.Choisir(Reglages.Langue, etat.ClientLanguage);

        fenetre = new Fenetre(this);
        fenetres.AddWindow(fenetre);

        pi.UiBuilder.Draw += fenetres.Draw;
        pi.UiBuilder.OpenMainUi += Ouvrir;
        pi.UiBuilder.OpenConfigUi += Ouvrir;
        sacs.ItemAdded += PieceArrivee;

        commandes.AddHandler(Commande, new CommandInfo((_, _) => Ouvrir())
        {
            HelpMessage = Mots.AideCommande,
        });

        RechargerCatalogue();
    }

    public void Dispose()
    {
        sacs.ItemAdded -= PieceArrivee;
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

    /// <summary>Le nom du personnage sert d'etiquette, rien de plus : il dit au
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

    /// <summary>Range le jeton du personnage connecte. Vide = on l'oublie.</summary>
    public void PoserJeton(string valeur)
    {
        if (ContentId == 0) return;
        var net = valeur.Trim();
        if (net.Length == 0) Reglages.Jetons.Remove(ContentId);
        else Reglages.Jetons[ContentId] = net;
        Enregistrer();
    }

    public void RechargerCatalogue()
    {
        var cache = Path.Combine(pi.GetPluginConfigDirectory(), "catalogue");
        _ = Task.Run(async () =>
        {
            try
            {
                Catalogue = await Catalogue.Charger(http, Site.Catalogue, cache);
                journal.Information("catalogue charge ({0})", Catalogue.Date);
            }
            catch (Exception e)
            {
                journal.Error(e, "catalogue illisible");
            }
        });
    }

    // ------------------------------------------------------------- la lecture

    /// <summary>Ouvre une lecture complete. Rien ne part : on montre d'abord.</summary>
    public void Regarder()
    {
        var cat = Catalogue;
        if (cat is null || !cat.Pret || LectureEnCours) return;
        Dernier = null;
        Releves = [];
        Coffre = null;
        file.Clear();
        foreach (var cle in Photo.Ordre) file.Enqueue(cle);
        Faites = 0;
        AFaire = file.Count;
        prochaine = 0;
        relectures = 0;
        EnVerification = true;
        reverifieA = -1; // a programmer quand la lecture aura fini
    }

    /// <summary>Relit sans geste les collections lues vides alors que le jeu
    /// savait répondre, et recommence tant qu'elles reviennent vides, jusqu'au
    /// plafond. Le cas rapporté est celui où la portée revient VIDE : la table
    /// des objets du jeu n'était pas encore en mémoire, aucune entrée n'a pu
    /// être lue, et une lecture plus tard les trouve toutes.</summary>
    private void Reverifier()
    {
        var douteuses = ParObjet.Where(Douteuse).ToList();
        if (douteuses.Count == 0 || relectures >= MaxRelectures)
        {
            // Plus rien a relire, ou plus le droit : la chaine conclut.
            if (douteuses.Count > 0)
                journal.Information("revérification : zéro confirmé après {0} relectures ({1})",
                    relectures, string.Join(", ", douteuses));
            EnVerification = false;
            return;
        }
        relectures++;
        foreach (var cle in douteuses) file.Enqueue(cle);
        Faites = 0;
        AFaire = file.Count;
        prochaine = 0;
        journal.Information("revérification {0}/{1} : {2}", relectures, MaxRelectures, string.Join(", ", douteuses));
    }

    /// <summary>
    /// Relit UNE collection, sans toucher au reste du releve.
    ///
    /// C'est la reponse aux collections que le jeu ne charge qu'a la demande :
    /// le joueur ouvre la fenetre voulue (carnet de succes, coiffeuse chez un
    /// rassembleur), puis relit juste cette carte. L'armoire, les pieces et les
    /// tenues se relisent ensemble : les trois sortent du meme coffre.
    /// </summary>
    public void Relire(string cle)
    {
        var cat = Catalogue;
        if (cat is null || !cat.Pret || LectureEnCours) return;
        Dernier = null;
        var etape = EtapeDe(cle);
        if (etape is "armoires" or "outfitpieces")
        {
            file.Enqueue("armoires");
            file.Enqueue("outfitpieces");
        }
        else
        {
            file.Enqueue(etape);
        }
        Faites = 0;
        AFaire = file.Count;
        prochaine = 0;
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
        if (!LectureEnCours)
        {
            if (!EnVerification) return;
            // Une passe vient de finir : la suivante part dans un instant.
            if (reverifieA < 0) reverifieA = maintenant + DelaiReverification;
            else if (reverifieA > 0 && maintenant >= reverifieA)
            {
                reverifieA = 0;
                Reverifier();
            }
            return;
        }
        if (maintenant < prochaine) return;
        prochaine = maintenant + Cadence;

        var cat = Catalogue;
        if (cat is null || !cat.Pret)
        {
            file.Clear();
            AFaire = 0;
            return;
        }
        var cle = file.Peek();
        try
        {
            var coffre = Coffre;
            var produits = Photo.Etape(
                cle,
                cat,
                donnees.GetExcelSheet<AozAction>(),
                donnees.GetExcelSheet<MirageStoreSetItem>(),
                ref coffre);
            // Une relecture remplace ce que l'etape avait produit la premiere
            // fois : deux releves de la meme collection seraient un mensonge.
            var anciens = EmisPar(cle);
            Releves.RemoveAll(r => anciens.Contains(r.Cle));
            Releves.AddRange(produits);
            Coffre = coffre;
            if (cle == "armoires" && coffre is not null) RetenirDepots(coffre);
            file.Dequeue();
            Faites++;
            if (file.Count == 0 && EnVerification) reverifieA = -1;
        }
        catch (Exception e)
        {
            journal.Error(e, "lecture du jeu impossible ({0})", cle);
            Releves = [];
            file.Clear();
            AFaire = 0;
            reverifieA = 0;
            EnVerification = false;
            Dernier = new Retour(false, Mots.LectureEchouee(e.Message), [], []);
        }
    }

    // --------------------------------------------------------------- l'envoi

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
                Dernier = await Envoi.Deposer(http, jeton, aEnvoyer);
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

    // --------------------------------------------------------------- l'alerte

    /// <summary>Les conteneurs ou un objet « arrive » vraiment chez le joueur :
    /// ses sacs et son arsenal. Les autres (coffre de compagnie, servants,
    /// cabas) levent le meme evenement quand ils se CHARGENT, en faisant
    /// defiler tout leur contenu comme autant d'arrivees : ouvrir le coffre de
    /// la compagnie libre en debut de session conseillait de deposer des pieces
    /// qui n'ont jamais quitte sa page.</summary>
    private static readonly HashSet<GameInventoryType> SacsDuJoueur =
    [
        GameInventoryType.Inventory1,
        GameInventoryType.Inventory2,
        GameInventoryType.Inventory3,
        GameInventoryType.Inventory4,
        GameInventoryType.ArmoryMainHand,
        GameInventoryType.ArmoryOffHand,
        GameInventoryType.ArmoryHead,
        GameInventoryType.ArmoryBody,
        GameInventoryType.ArmoryHands,
        GameInventoryType.ArmoryLegs,
        GameInventoryType.ArmoryFeets,
        GameInventoryType.ArmoryEar,
        GameInventoryType.ArmoryNeck,
        GameInventoryType.ArmoryWrist,
        GameInventoryType.ArmoryRings,
    ];

    /// <summary>Retient ce que la photo vient de voir dans les depots, pour le
    /// personnage connecte. Un depot que le jeu n'avait pas charge ne remplace
    /// jamais ce qu'on en savait : la coiffeuse se lit vide tant qu'elle n'a pas
    /// ete ouverte (la lecture tient deja ce vide pour « pas ouverte »), et une
    /// memoire effacee par une lecture a vide serait exactement le bug qu'on
    /// corrige.</summary>
    private void RetenirDepots(Coffre coffre)
    {
        var id = ContentId;
        if (id == 0) return;
        if (!Reglages.Depots.TryGetValue(id, out var d)) Reglages.Depots[id] = d = new Depots();
        var change = false;
        if (coffre.Coiffeuse.Count > 0)
        {
            d.Coiffeuse = [.. coffre.Coiffeuse];
            change = true;
        }
        if (coffre.ArmoireLue)
        {
            d.Armoire = [.. coffre.Armoire];
            change = true;
        }
        if (!change) return;
        depotsRetenus = new Coffre([.. d.Coiffeuse], [.. d.Armoire], true);
        depotsPour = id;
        Enregistrer();
    }

    /// <summary>La memoire des depots du personnage connecte, ou rien. Relue
    /// des reglages quand le personnage change : pas besoin d'ecouter la
    /// connexion, l'identifiant suffit.</summary>
    private Coffre? DepotsRetenus()
    {
        var id = ContentId;
        if (id == 0) return null;
        if (depotsPour != id)
        {
            depotsRetenus = Reglages.Depots.TryGetValue(id, out var d)
                ? new Coffre([.. d.Coiffeuse], [.. d.Armoire], true)
                : null;
            depotsPour = id;
        }
        return depotsRetenus;
    }

    private static bool Deposee(Coffre? c, uint id) =>
        c is not null && (c.Coiffeuse.Contains(id) || c.Armoire.Contains(id));

    /// <summary>
    /// Un mot quand une piece de tenue arrive dans les sacs.
    ///
    /// Elle n'est pas cochee pour autant : un objet qui traine peut se vendre ou
    /// se jeter. Le message dit ou la mettre pour qu'elle compte, et rien de
    /// plus. Il se tait pour ce que la derniere photo, de cette session ou d'une
    /// precedente, a deja vu range.
    /// </summary>
    private void PieceArrivee(GameInventoryEvent quoi, InventoryEventArgs e)
    {
        if (!Reglages.AvisEnJeu) return;
        if (!SacsDuJoueur.Contains(e.Item.ContainerType)) return;
        var cat = Catalogue;
        if (cat is null || !cat.Pret) return;
        var id = e.Item.ItemId >= 1_000_000 ? e.Item.ItemId - 1_000_000 : e.Item.ItemId;
        if (id == 0) return;
        // Deja depose : il n'y a rien a aller ranger. La photo de cette session
        // fait foi si elle existe, et la memoire des sessions passees repond
        // pour ce qu'elle n'a pas pu voir.
        if (Deposee(Coffre, id) || Deposee(DepotsRetenus(), id)) return;

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
}
