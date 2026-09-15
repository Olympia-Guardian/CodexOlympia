using System.Net.Http;
using System.Text.Json;
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
public sealed partial class Plugin : IDalamudPlugin
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
    private readonly ICondition condition;
    private readonly IFramework cadre;
    public ITextureProvider Textures { get; }

    private readonly WindowSystem fenetres = new("CodexOlympia");
    private readonly Fenetre fenetre;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public Reglages Reglages { get; }

    /// <summary>Le nom et le visage du personnage, tels que l'application les
    /// montre (PLG-R40). Demandes a l'ouverture de la fenetre, une fois.</summary>
    public Visage Visage { get; }

    public Catalogue? Catalogue { get; private set; }

    /// <summary>Ce qui existe, lu dans les tables du client (PLG-R46). Bati une
    /// fois au demarrage : les tables ne bougent pas d'une session a l'autre.</summary>
    /// <summary>Ce que le jeu répond pour la galerie, collection par collection,
    /// gardé d'une image sur l'autre : la question est brève mais il y en a neuf
    /// cents, et la fenêtre se redessine soixante fois par seconde. Une valeur
    /// nulle dit que le jeu n'a pas de question à numéro pour cette
    /// collection.</summary>
    private readonly Dictionary<string, HashSet<uint>?> galerie = new();

    /// <summary>Ce que la galerie montre d'une collection : les entrées
    /// visibles, et celles que le joueur possède parmi elles.</summary>
    public sealed record VueGalerie(List<Entree> Entrees, HashSet<uint> Mien);

    private readonly Dictionary<string, VueGalerie> vues = new();

    /// <summary>
    /// Ce que la galerie a à montrer pour une collection.
    ///
    /// Un joueur peut demander à ne pas voir ce qui ne s'obtient qu'en boutique
    /// en ligne. Le tri se fait ici, une fois par collection et par lecture,
    /// pour que la grille et le compteur disent la même chose : un compteur qui
    /// annonce plus d'entrées que la grille n'en dessine est un compteur faux.
    /// </summary>
    public VueGalerie Vue(string cle)
    {
        if (vues.TryGetValue(cle, out var deja)) return deja;

        var toutes = Tables.TryGetValue(cle, out var l) ? l : [];
        var mien = GalerieAMoi(cle) ?? DepuisReleve(cle);

        VueGalerie vue;
        if (!Reglages.CacherBoutique || Catalogue is not { } cat)
        {
            vue = new VueGalerie(toutes, mien);
        }
        else
        {
            var gardees = new List<Entree>(toutes.Count);
            var aMoi = new HashSet<uint>();
            foreach (var x in toutes)
            {
                if (cat.Detail(cle, NumeroCatalogue(cle, x.Id))?.Boutique == true) continue;
                gardees.Add(x);
                if (mien.Contains(x.Id)) aMoi.Add(x.Id);
            }
            vue = new VueGalerie(gardees, aMoi);
        }

        vues[cle] = vue;
        return vue;
    }

    /// <summary>Ce que la dernière lecture a trouvé, pour les collections dont
    /// le jeu n'a pas de question à numéro.</summary>
    private HashSet<uint> DepuisReleve(string cle)
    {
        var r = Releves.FirstOrDefault(x => x.Cle == cle);
        return r is null ? [] : [.. r.Trouves];
    }

    /// <summary>Ce que le joueur possède parmi ce que la galerie montre, ou rien
    /// quand le jeu ne sait pas répondre par numéro.</summary>
    public HashSet<uint>? GalerieAMoi(string cle)
    {
        if (galerie.TryGetValue(cle, out var deja)) return deja;
        var vu = Tables.TryGetValue(cle, out var entrees) ? Photo.Possedes(cle, entrees, donnees) : null;
        galerie[cle] = vu;
        return vu;
    }

    /// <summary>
    /// Fait essayer un objet au personnage (PLG-R51).
    ///
    /// Le jeu empile ce qu'on lui donne : rappeler la méthode pièce après
    /// pièce habille la silhouette de toute la tenue. Rien n'est acheté, rien
    /// n'est équipé, et le joueur referme la fenêtre quand il veut.
    /// </summary>
    public void Essayer(uint objet)
    {
        if (objet == 0) return;
        FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentTryon.TryOn(0u, objet, 0, 0, 0, false);
    }

    /// <summary>Après une lecture, ce qui était gardé ne vaut plus.</summary>
    public void OublierGalerie()
    {
        galerie.Clear();
        vues.Clear();
        pontLunettes = null;
        pontCoiffures = null;
    }

    /// <summary>Du numéro de modèle de lunettes vers le numéro du catalogue.</summary>
    private Dictionary<uint, uint>? pontLunettes;

    /// <summary>
    /// Le numéro du catalogue pour une entrée de la galerie.
    ///
    /// Les deux numérotations coïncident partout, sauf pour les lunettes : le
    /// catalogue les numérote à sa façon, la galerie par la ligne du modèle
    /// dans la table du jeu. Sans ce pont, la fiche d'une paire de lunettes
    /// n'aurait ni patch ni source.
    /// </summary>
    public uint NumeroCatalogue(string cle, uint id)
    {
        if (cle == "facewear")
        {
            pontLunettes ??= BatirPontLunettes();
            return pontLunettes.GetValueOrDefault(id, id);
        }
        if (cle == "hairstyles")
        {
            pontCoiffures ??= BatirPontCoiffures();
            return pontCoiffures.GetValueOrDefault(id, id);
        }
        return id;
    }

    /// <summary>Du numero de brochure vers le numero du catalogue.</summary>
    private Dictionary<uint, uint>? pontCoiffures;

    private Dictionary<uint, uint> BatirPontCoiffures()
    {
        var pont = new Dictionary<uint, uint>();
        if (Catalogue is not { } cat || !cat.Objets.TryGetValue("hairstyles", out var liens)) return pont;
        foreach (var (idCatalogue, objet) in liens)
            if (objet != 0) pont[objet] = idCatalogue;
        return pont;
    }

    private Dictionary<uint, uint> BatirPontLunettes()
    {
        var pont = new Dictionary<uint, uint>();
        if (Catalogue is not { } cat || !cat.Objets.TryGetValue("facewear", out var liens)) return pont;
        var objets = donnees.GetExcelSheet<Item>();
        foreach (var (idCatalogue, objet) in liens)
        {
            if (objet == 0) continue;
            var numero = objets.GetRowOrDefault(objet)?.AdditionalData.RowId ?? 0;
            if (numero != 0) pont[numero] = idCatalogue;
        }
        return pont;
    }

    /// <summary>Où se trouve ce qui s'obtient, quand le jeu le sait (PLG-R50).</summary>
    public Lieux Lieux { get; }

    /// <summary>Ce que les tables du client savent dire.</summary>
    public Jeu Jeu { get; private set; } = new(
        new Dictionary<string, List<Entree>>(),
        new Dictionary<uint, uint[]>(),
        new Dictionary<uint, Entree>(),
        new Dictionary<uint, uint>());

    public IReadOnlyDictionary<string, List<Entree>> Tables { get; private set; } =
        new Dictionary<string, List<Entree>>();
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

    /// <summary>
    /// La boîte aux lettres du fil du jeu : un fil de fond ne touche jamais au
    /// plugin ni au jeu, il dépose ici et <see cref="Tour"/> exécute à l'image
    /// suivante. Dalamud n'accepte le journal de discussion que de là.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentQueue<System.Action> boite = new();

    /// <summary>Fait faire ceci au prochain tour, sur le fil du jeu.</summary>
    private void SurLeFilDuJeu(System.Action quoi) => boite.Enqueue(quoi);

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
        etape == "outfitpieces" ? ["outfitpieces", "outfits"] : [etape];

    /// <summary>Vrai si cette collection attend son tour dans la file.</summary>
    public bool EnFile(string cle) => file.Contains(EtapeDe(cle));

    /// <summary>Quand la revérification doit partir, en secondes de jeu. Zéro :
    /// aucune n'est due.</summary>
    private double reverifieA;

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

    /// <summary>L'empreinte de la passe précédente : ce que chaque collection
    /// douteuse avait trouvé, regardé, et n'avait pas su lire. Vide avant la
    /// première passe.</summary>
    private string empreinte = string.Empty;

    /// <summary>Vrai tant que la chaîne de revérification n'a pas conclu : rien ne
    /// part pendant ce temps, un relevé à zéro pourrait être un relevé en retard.</summary>
    public bool EnVerification { get; private set; }

    /// <summary>Calculées une fois par passe : le tri compare des listes de
    /// milliers d'entrées, et la fenêtre le demande pour chaque tuile et à
    /// chaque image.</summary>
    private readonly HashSet<string> douteuses = [];

    public bool Douteuse(string cle) => douteuses.Contains(cle);

    /// <summary>Refait le tri, à la fin d'une passe de lecture.</summary>
    private void RecalculerDouteuses()
    {
        douteuses.Clear();
        foreach (var cle in Photo.Ordre)
            if (SentLeRetard(cle))
                douteuses.Add(cle);
    }

    /// <summary>Trois signes de retard : une collection lue entièrement vide
    /// (PLG-R33), une portée effondrée (PLG-R44), ou ce qui est déjà parti qui
    /// ne se retrouve pas (PLG-R45).</summary>
    private bool SentLeRetard(string cle)
    {
        var r = Releves.FirstOrDefault(x => x.Cle == cle);
        if (r is null || r.Empeche is not null || r.Total == 0) return false;
        // Une entrée que le jeu n'a pas su donner : lecture en retard (PLG-R44).
        if (r.NonLues > 0) return true;
        // Vide alors que le catalogue en connaît : une liste pas encore
        // remplie répond zéro quelle que soit la question.
        if (r.Trouves.Count == 0) return true;
        // Et, pour toutes : ce qui est déjà parti doit se retrouver (PLG-R45).
        return PerdDuDejaEnvoye(r);
    }

    /// <summary>
    /// La lecture retrouve-t-elle ce qui est déjà parti (PLG-R45) ? Un
    /// déverrouillage acquis le reste.
    ///
    /// Deux garde-fous : une entrée hors de la portée déclarée ne prouve rien,
    /// et une entrée retirée du catalogue entre deux patchs ferait douter pour
    /// toujours.
    /// </summary>
    private bool PerdDuDejaEnvoye(Releve r)
    {
        if (ContentId == 0
            || !Reglages.Envoyes.TryGetValue(ContentId, out var parCollection)
            || !parCollection.TryGetValue(r.Cle, out var envoyes)
            || envoyes.Count == 0)
            return false;
        if (Catalogue is null || !Catalogue.Ids.TryGetValue(r.Cle, out var connus)) return false;

        var catalogue = new HashSet<uint>(connus);
        var vus = new HashSet<uint>(r.Trouves);
        var portee = r.Portee is null ? null : new HashSet<uint>(r.Portee);
        foreach (var id in envoyes)
        {
            if (vus.Contains(id)) continue;
            if (!catalogue.Contains(id)) continue;
            if (portee is not null && !portee.Contains(id)) continue;
            return true;
        }
        return false;
    }

    public Retour? Dernier { get; private set; }
    public bool EnvoiEnCours { get; private set; }

    /// <summary>Zero tant qu'aucun personnage n'est connecte.</summary>
    public ulong ContentId => etat.IsLoggedIn ? perso.ContentId : 0;

    /// <summary>Le jeton du personnage connecte, ou rien.</summary>
    public string Jeton =>
        ContentId != 0 && Reglages.Jetons.TryGetValue(ContentId, out var j) ? j : string.Empty;

    /// <summary>Le reglage d'accessibilite de Dalamud, lu par le dessin, qui
    /// n'a pas acces au plugin (PLG-R41).</summary>
    public static bool MoinsDeMouvement { get; private set; }

    public Plugin(
        IDalamudPluginInterface pi,
        ICommandManager commandes,
        IClientState etat,
        IPlayerState perso,
        IDataManager donnees,
        IPluginLog journal,
        IChatGui discussion,
        IGameInventory sacs,
        ITextureProvider textures,
        ICondition condition,
        IFramework cadre)
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
        this.condition = condition;
        this.cadre = cadre;

        Reglages = pi.GetPluginConfig() as Reglages ?? new Reglages();
        Mots.Choisir(Reglages.Langue, etat.ClientLanguage);
        MoinsDeMouvement = pi.UiBuilder.ShouldUseReducedMotion;

        Visage = new Visage(http, textures, journal, SurLeFilDuJeu);
        Lieux = new Lieux(donnees, journal);
        Lieux.Batir();
        try
        {
            // Chaque collection se batit pour son compte : une table que le
            // client ne decrit plus pareil ne doit emporter qu'elle (PLG-R59).
            Jeu = CodexOlympia.Tables.Batir(donnees, journal);
            Tables = Jeu.Collections;
        }
        catch (Exception e)
        {
            // Le filet de securite : meme un echec que la construction n'a pas
            // su rattraper n'empeche pas la synchronisation.
            journal.Error(e, "tables du jeu illisibles");
        }
        fenetre = new Fenetre(this);
        fenetres.AddWindow(fenetre);

        pi.UiBuilder.Draw += fenetres.Draw;
        pi.UiBuilder.OpenMainUi += Ouvrir;
        pi.UiBuilder.OpenConfigUi += Ouvrir;
        sacs.ItemAdded += PieceArrivee;
        // La lecture avance fenetre ouverte ou non : la synchro automatique
        // ne peut pas dependre d'une fenetre qu'on n'ouvre plus.
        cadre.Update += Tour;
        etat.Login += SurConnexion;
        etat.Logout += SurDeconnexion;
        etat.TerritoryChanged += SurZone;
        if (etat.IsLoggedIn) SurConnexion();

        commandes.AddHandler(Commande, new CommandInfo((_, args) => Commander(args))
        {
            HelpMessage = Mots.AideCommande,
        });

        RechargerCatalogue();
    }

    public void Dispose()
    {
        cadre.Update -= Tour;
        etat.Login -= SurConnexion;
        etat.Logout -= SurDeconnexion;
        etat.TerritoryChanged -= SurZone;
        sacs.ItemAdded -= PieceArrivee;
        commandes.RemoveHandler(Commande);
        pi.UiBuilder.Draw -= fenetres.Draw;
        pi.UiBuilder.OpenMainUi -= Ouvrir;
        pi.UiBuilder.OpenConfigUi -= Ouvrir;
        fenetres.RemoveAllWindows();
        fenetre.Dispose();
        Visage.Dispose();
        http.Dispose();
    }

    private void Ouvrir()
    {
        RetenirLeNom();
        fenetre.IsOpen = true;
    }

    /// <summary>/codex ouvre la fenetre ; /codex bestiaire ecrit la lecture du
    /// bestiaire dans le journal de discussion, et son detail brut dans le
    /// journal Dalamud. C'est le geste qui sert a confirmer, en jeu, la forme
    /// du module que FFXIVClientStructs ne decrit pas encore.</summary>
    private void Commander(string args)
    {
        if (args.Trim().Equals("bestiaire", StringComparison.OrdinalIgnoreCase))
        {
            DiagnostiquerBestiaire();
            return;
        }
        Ouvrir();
    }

    private void DiagnostiquerBestiaire()
    {
        var cat = Catalogue;
        if (cat is null)
        {
            discussion.Print("[Codex Olympia] " + Mots.CatalogueAbsent);
            return;
        }
        journal.Information("bestiaire :\n" + Photo.DiagnosticBestiaire(cat));
        discussion.Print("[Codex Olympia] " + Photo.ResumeBestiaire(cat));
    }

    public void Enregistrer() => pi.SavePluginConfig(Reglages);

    /// <summary>Change la langue de la fenetre et s'en souvient.</summary>
    public void ChoisirLangue(Langue l)
    {
        Reglages.Langue = l;
        Mots.Choisir(l, etat.ClientLanguage);
        Enregistrer();
    }

    /// <summary>Le nom du personnage sert d'etiquette, rien de plus.</summary>
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
                var neuf = await Catalogue.Charger(http, Site.Catalogue, cache);
                SurLeFilDuJeu(() =>
                {
                    Catalogue = neuf;
                    // Ce que les tables du jeu n'ont pas su batir, le catalogue
                    // le complete maintenant qu'il est la.
                    Tables = CodexOlympia.Tables.Completer(Jeu.Collections, neuf);
                    journal.Information("catalogue charge ({0})", neuf.Date);
                });
            }
            catch (Exception e)
            {
                journal.Error(e, "catalogue illisible");
            }
        });
    }

    /// <summary>La date du catalogue chez l'application, ou rien si le reseau
    /// manque : dans ce cas on garde ce qu'on a.</summary>
    private async Task<string> DateDistante()
    {
        try
        {
            using var doc = JsonDocument.Parse(await http.GetStringAsync($"{Site.Catalogue}/meta.json"));
            return doc.RootElement.GetProperty("updatedAt").GetString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>Vrai le temps de verifier la date du catalogue avant une lecture.</summary>
    private bool rafraichit;

    /// <summary>Relit le catalogue s'il a change chez l'application, puis
    /// regarde. La date de meta.json d'abord, le catalogue entier seulement si
    /// elle a bouge : une ronde de nuit passee pendant la session laisserait
    /// sinon lire le jeu avec la liste de la veille.</summary>
    public void RegarderAJour()
    {
        if (LectureEnCours || rafraichit) return;
        rafraichit = true;
        _ = Task.Run(async () =>
        {
            try
            {
                var cat = Catalogue;
                var distante = await DateDistante();
                if (cat is null || (distante.Length > 0 && distante != cat.Date))
                {
                    var cache = Path.Combine(pi.GetPluginConfigDirectory(), "catalogue");
                    var neuf = await Catalogue.Charger(http, Site.Catalogue, cache);
                    SurLeFilDuJeu(() =>
                    {
                        Catalogue = neuf;
                        journal.Information("catalogue relu avant lecture ({0})", neuf.Date);
                    });
                }
            }
            catch (Exception e)
            {
                journal.Error(e, "catalogue illisible");
            }
            finally
            {
                SurLeFilDuJeu(() =>
                {
                    rafraichit = false;
                    Regarder();
                });
            }
        });
    }

    /// <summary>Ouvre une lecture complete. Rien ne part : on montre d'abord.</summary>
    public void Regarder()
    {
        var cat = Catalogue;
        if (cat is null || !cat.Pret || LectureEnCours) return;
        Dernier = null;
        Releves = [];
        Coffre = null;
        evalue = false;
        file.Clear();
        foreach (var cle in Photo.Ordre) file.Enqueue(cle);
        Faites = 0;
        AFaire = file.Count;
        prochaine = 0;
        relectures = 0;
        empreinte = string.Empty;
        EnVerification = true;
        reverifieA = -1; // a programmer quand la lecture aura fini
    }

    /// <summary>
    /// Relit sans geste ce qui sent le retard, jusqu'au plafond. Sur toutes les
    /// collections : le retard n'est pas une affaire de collection mais de
    /// moment.
    ///
    /// La chaine s'arrete aussi quand une passe ne change rien (PLG-R56). Une
    /// revérification attend que le jeu finisse de charger ; deux passes qui
    /// disent la meme chose, a deux secondes d'intervalle, disent qu'il ne
    /// charge plus rien, et une troisieme ferait attendre le joueur pour la
    /// meme reponse. Le plafond reste, pour ce qui changerait a chaque fois
    /// sans jamais se stabiliser.
    /// </summary>
    private void Reverifier()
    {
        RecalculerDouteuses();
        var maintenant = Empreinte();
        var fige = douteuses.Count > 0 && maintenant == empreinte;
        empreinte = maintenant;

        if (douteuses.Count == 0 || fige || relectures >= MaxRelectures)
        {
            // Plus rien a relire, plus rien qui bouge, ou plus le droit : la
            // chaine conclut.
            if (douteuses.Count > 0)
                journal.Information("revérification : lecture tenue pour bonne après {0} relectures ({1}){2}",
                    relectures, string.Join(", ", douteuses), fige ? ", plus rien ne bougeait" : "");
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

    /// <summary>Ce que les collections douteuses disent en ce moment : de quoi
    /// reconnaitre une passe qui n'a rien appris.</summary>
    private string Empreinte() =>
        string.Join(";", Releves
            .Where(r => douteuses.Contains(r.Cle))
            .OrderBy(r => r.Cle, StringComparer.Ordinal)
            .Select(r => $"{r.Cle}:{r.Trouves.Count}:{r.NonLues}:{r.Portee?.Count ?? -1}"));

    /// <summary>Relit UNE collection, pour celles que le jeu ne charge qu'a
    /// l'ouverture de leur fenetre. L'armoire, les pieces et les tenues se
    /// relisent ensemble : les trois sortent du meme coffre.</summary>
    public void Relire(string cle)
    {
        var cat = Catalogue;
        if (cat is null || !cat.Pret || LectureEnCours) return;
        Dernier = null;
        evalue = false;
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
                donnees.GetExcelSheet<Item>(),
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
            if (file.Count == 0)
            {
                OublierGalerie();
                RecalculerDouteuses();
                if (EnVerification) reverifieA = -1;
            }
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

    public void Envoyer()
    {
        if (EnvoiEnCours || Releves.Count == 0) return;
        var jeton = Jeton;
        if (jeton.Length == 0) return;

        EnvoiEnCours = true;
        var aEnvoyer = Releves;
        var pour = ContentId;
        _ = Task.Run(async () =>
        {
            Retour retour;
            try
            {
                retour = await Envoi.Deposer(http, jeton, aEnvoyer);
            }
            catch (Exception e)
            {
                journal.Error(e, "envoi impossible");
                retour = new Retour(false, Mots.ServeurInjoignable(e.Message), [], []);
            }
            // Le fil reseau ne touche a rien : il rapporte, et le fil du jeu
            // range. Le journal de discussion, surtout, ne s'ecrit que d'ici.
            SurLeFilDuJeu(() =>
            {
                EnvoiEnCours = false;
                Dernier = retour;
                if (!retour.Ok) return;
                discussion.Print("[Codex Olympia] " + retour.Message);
                // Ce qui vient de partir devient la reference du neuf.
                Retenir(pour, Photographie(aEnvoyer));
            });
        });
    }

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
