using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace CodexOlympia;

public enum EtatRangement
{
    Arrete,
    EnMarche,
    Fini,
    Interrompu,
}

/// <summary>Ce qu'il y a a faire, et par quel moyen.</summary>
public enum Moyen
{
    /// <summary>Un objet a ranger dans l'armoire.</summary>
    Armoire,

    /// <summary>Une tenue a deposer d'un bloc dans la coiffeuse.</summary>
    TenueNeuve,

    /// <summary>Des pieces a ajouter a une tenue deja deposee.</summary>
    TenueEntamee,
}

/// <summary>
/// Les temps d'un depot en coiffeuse.
///
/// Le jeu ne sait pas deposer une piece d'un seul appel : il faut ouvrir la
/// fenetre de conversion, lui tendre les pieces, valider, transformer, puis
/// confirmer. C'est la procedure exacte du joueur, et rien ne la raccourcit :
/// « Transformer » appele sans sa fenetre repond oui et ne fait rien.
/// </summary>
public enum Temps
{
    Ouvrir,
    Tendre,
    Valider,
    Transformer,
    Confirmer,

    /// <summary>La question posee juste apres l'ouverture, quand la piece
    /// appartient a un ensemble deja depose : « Ranger et ajouter au mirage
    /// d'ensemble ? ». Elle precede la fenetre de conversion, elle ne la
    /// remplace pas.</summary>
    Question,
    Fini,
}

/// <summary>Une operation, decrite par ce qu'elle vise et jamais par des cases.
///
/// <para><c>Pieces</c> est le nombre d'objets qu'elle deposera : c'est aussi le
/// nombre de prismes de mirage qu'elle consommera, un par piece.</para></summary>
public sealed record Tache(
    Moyen Moyen,
    uint Cible,
    string Nom,
    uint Emplacement = 0,
    int Pieces = 0,
    bool Acheve = false);

/// <summary>
/// Le rangement automatique. FONCTION EXPERIMENTALE.
///
/// <para><b>Ce module agit sur le jeu.</b> Tout le reste du plugin lit la
/// memoire du client, ce qui ne produit aucun paquet. Ici, chaque operation est
/// un ordre envoye au serveur. C'est la seule partie du plugin dont le serveur
/// voit passer quelque chose, et c'est la raison de tout ce qui suit.</para>
///
/// <para><b>Une operation a la fois, a cadence humaine.</b> Une salve a vitesse
/// machine ne ressemble a rien de ce qu'un joueur produit.</para>
///
/// <para><b>Rien n'est memorise par sa case.</b> Une case change des qu'un objet
/// en sort. Chaque tache designe un OBJET, et sa position est retrouvee juste
/// avant d'agir. Ranger le mauvais objet parce qu'on visait une case perimee est
/// exactement l'accident qu'on ne peut pas defaire.</para>
///
/// <para><b>On s'arrete au premier imprevu.</b> Fenetre fermee, depot plein,
/// objet disparu, retour negatif : on cesse et on dit pourquoi.</para>
/// </summary>
public sealed class Rangeur
{
    private readonly IGameInventory sacs;
    private readonly IPluginLog journal;
    private readonly IGameGui gui;
    private readonly Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.MirageStoreSetItem> ensembles;
    private readonly Random hasard = new();

    private readonly List<Tache> file = [];
    private int fait;
    private double prochaine;

    /// <summary>Ou en est la conversion en cours.</summary>
    private Temps temps = Temps.Ouvrir;

    /// <summary>Tours passes a attendre que la question paraisse.</summary>
    private int attenteQuestion;

    /// <summary>Les pieces encore a tendre a la fenetre, pour la tache en cours.</summary>
    private readonly List<uint> aTendre = [];

    /// <summary>Combien de taches on a passees faute de mieux, et combien de
    /// suite : une seule qui coince ne doit pas emporter tout le reste, mais
    /// trois d'affilee veulent dire que quelque chose ne va pas.</summary>
    private int sautes;
    private int sautesDeSuite;

    /// <summary>Combien de taches ont ete passees.</summary>
    public int Sautes => sautes;

    /// <summary>Combien d'emplacements la coiffeuse occupait avant la conversion :
    /// c'est ce qui permet de distinguer un depot d'un oui poli.</summary>
    private int avantConversion;

    /// <summary>Le temps entre deux gestes, en secondes. Regle par le joueur :
    /// large, on voit ce qui se passe ; serre, ca va vite.</summary>
    public double Cadence { get; set; } = 3.0;

    public EtatRangement Etat { get; private set; } = EtatRangement.Arrete;
    public string? Pourquoi { get; private set; }
    public int Faits => fait;
    public int Total => file.Count;
    public Tache? EnCours => Etat == EtatRangement.EnMarche && fait < file.Count ? file[fait] : null;

    public Rangeur(
        IGameInventory sacs,
        IPluginLog journal,
        IGameGui gui,
        Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.MirageStoreSetItem> ensembles)
    {
        this.sacs = sacs;
        this.journal = journal;
        this.gui = gui;
        this.ensembles = ensembles;
    }

    /// <summary>
    /// Les contenants ou le jeu accepte de puiser.
    ///
    /// L'arsenal en fait partie : la fenetre « Ranger » du jeu a un selecteur de
    /// categorie, et l'arsenal y figure. Une version precedente l'avait exclu
    /// apres avoir lu cette fenetre alors qu'elle affichait l'inventaire, et
    /// avoir pris ce qu'elle montrait pour ce qu'elle savait montrer.
    /// </summary>
    private static readonly (GameInventoryType Vue, InventoryType Jeu)[] Puisables =
    [
        (GameInventoryType.Inventory1, InventoryType.Inventory1),
        (GameInventoryType.Inventory2, InventoryType.Inventory2),
        (GameInventoryType.Inventory3, InventoryType.Inventory3),
        (GameInventoryType.Inventory4, InventoryType.Inventory4),
        (GameInventoryType.ArmoryMainHand, InventoryType.ArmoryMainHand),
        (GameInventoryType.ArmoryOffHand, InventoryType.ArmoryOffHand),
        (GameInventoryType.ArmoryHead, InventoryType.ArmoryHead),
        (GameInventoryType.ArmoryBody, InventoryType.ArmoryBody),
        (GameInventoryType.ArmoryHands, InventoryType.ArmoryHands),
        (GameInventoryType.ArmoryLegs, InventoryType.ArmoryLegs),
        (GameInventoryType.ArmoryFeets, InventoryType.ArmoryFeets),
        (GameInventoryType.ArmoryEar, InventoryType.ArmoryEar),
        (GameInventoryType.ArmoryNeck, InventoryType.ArmoryNeck),
        (GameInventoryType.ArmoryWrist, InventoryType.ArmoryWrist),
        (GameInventoryType.ArmoryRings, InventoryType.ArmoryRings),
    ];

    private const uint SeuilHq = 1_000_000;

    /// <summary>Le prisme de mirage. Deposer une piece dans la coiffeuse en
    /// consomme un : sans reserve, le jeu refuse et rien n'explique pourquoi.</summary>
    private const uint Prisme = 21800;

    /// <summary>Combien de prismes le personnage a sous la main.</summary>
    public int Prismes()
    {
        var n = 0;
        foreach (var (vue, _) in Puisables)
        {
            var items = sacs.GetInventoryItems(vue);
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].IsEmpty) continue;
                var id = items[i].ItemId >= SeuilHq ? items[i].ItemId - SeuilHq : items[i].ItemId;
                if (id == Prisme) n += items[i].Quantity;
            }
        }
        return n;
    }

    /// <summary>Ou se trouve cet objet, maintenant. Nul s'il n'y est plus.</summary>
    private (InventoryType Contenant, ushort Case)? Trouver(uint objet)
    {
        foreach (var (vue, jeu) in Puisables)
        {
            var items = sacs.GetInventoryItems(vue);
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].IsEmpty) continue;
                var id = items[i].ItemId >= SeuilHq ? items[i].ItemId - SeuilHq : items[i].ItemId;
                if (id == objet) return (jeu, (ushort)items[i].InventorySlot);
            }
        }
        return null;
    }

    /// <summary>
    /// Batit la liste des operations.
    ///
    /// Toute piece de tenue qu'on a sous la main part vers la coiffeuse, seule
    /// ou accompagnee : le set occupe un emplacement, qu'on le remplisse en une
    /// fois ou en cinq. Si la tenue y est deja, on la complete ; sinon on la
    /// cree. L'armoire ne prend que le reste, et seulement si on le demande.
    /// </summary>
    public unsafe List<Tache> Preparer(Catalogue cat, Coffre coffre, bool aussiArmoire)
    {
        var taches = new List<Tache>();
        var ui = UIState.Instance();
        var mirage = MirageManager.Instance();

        // Les emplacements de la coiffeuse : c'est la qu'on voit si une tenue y
        // est deja, et donc s'il faut la completer plutot que d'en creer une
        // seconde.
        var deposees = new Dictionary<uint, uint>();
        var emplacements = mirage->PrismBoxItemIds;
        for (var i = 0; i < emplacements.Length; i++)
        {
            var v = emplacements[i];
            if (v == 0) continue;
            deposees.TryAdd(v >= SeuilHq ? v - SeuilHq : v, (uint)i);
        }

        // LA COIFFEUSE D'ABORD, et l'ordre n'est pas un detail : un objet range
        // a l'armoire quitte l'inventaire, donc la coiffeuse ne l'aura plus. Une
        // premiere version rangeait l'armoire en tete et vidait les sacs avant
        // que les tenues aient eu leur chance.
        //
        // Les tenues entamees passent avant les neuves : completer un emplacement
        // deja pris n'en consomme pas un second.
        var prises = new HashSet<uint>();
        foreach (var deuxieme in new[] { false, true })
        {
            foreach (var t in cat.Tenues)
            {
                var manquantes = t.Pieces.Where(p => !coffre.Coiffeuse.Contains(p.Objet)).ToList();
                if (manquantes.Count == 0) continue;

                // Ce qu'on a sous la main de cette tenue, meme une seule piece.
                //
                // Une premiere version exigeait la tenue ENTIERE, en croyant
                // qu'un depot partiel gaspillait un emplacement. C'etait faux :
                // le set en occupe UN, qu'on le remplisse en une fois ou en cinq,
                // et le jeu accepte explicitement les emplacements vides. La
                // regle ne faisait que laisser les pieces pourrir dans les sacs.
                // `prises` : une piece deja revendiquee par une tenue de cette
                // passe ne peut pas l'etre par une autre. Deux ensembles
                // partagent parfois une piece — les accessoires Praemagitek de
                // pisteur et d'eclaireur sont les memes objets — mais on n'en
                // possede qu'un exemplaire. Le premier le prend, le second s'en
                // passe et sera cree quand meme, avec ce qui lui reste.
                var enMain = manquantes
                    .Where(p => !prises.Contains(p.Objet) && Trouver(p.Objet) is not null)
                    .ToList();
                if (enMain.Count == 0) continue;

                var entamee = deposees.TryGetValue(t.Id, out var place);
                if (entamee == deuxieme) continue;
                // Une tenue qu'on acheve passe devant : c'est la ou l'effort
                // rapporte, et c'est ce qu'on attend de voir partir en premier.
                var acheve = enMain.Count == manquantes.Count;
                if (entamee)
                    taches.Add(new Tache(Moyen.TenueEntamee, t.Id, t.Nom, place, enMain.Count, acheve));
                else taches.Add(new Tache(Moyen.TenueNeuve, t.Id, t.Nom, 0, enMain.Count, acheve));
                foreach (var p in enMain) prises.Add(p.Objet);
            }
        }

        // L'armoire ensuite, et seulement si on l'a demande : elle range ce
        // qu'aucune tenue ne prendra. Par defaut on n'y touche pas, parce que ce
        // qu'on veut d'abord, ce sont les tenues.
        if (!aussiArmoire) return taches;

        var vus = new HashSet<uint>();
        foreach (var t in cat.Tenues)
        {
            foreach (var p in t.Pieces)
            {
                if (p.Armoire == 0 || prises.Contains(p.Objet) || !vus.Add(p.Armoire)) continue;
                // Deja dans la coiffeuse : la piece qu'on tient est un double.
                // L'armoire n'en veut pas, elle ne sert qu'a garder ce qui n'a
                // pas d'autre place. Ce double-la se vend.
                if (coffre.Coiffeuse.Contains(p.Objet)) continue;
                if (ui->Cabinet.IsItemInCabinet(p.Armoire - 1)) continue;
                if (Trouver(p.Objet) is null) continue;
                taches.Add(new Tache(Moyen.Armoire, p.Armoire, p.Nom));
            }
        }

        // Ce qui acheve une tenue d'abord, le reste ensuite. L'ordre du
        // catalogue ne veut rien dire pour qui regarde travailler.
        return [.. taches.OrderByDescending(t => t.Acheve)];
    }

    public void Demarrer(List<Tache> taches)
    {
        file.Clear();
        file.AddRange(taches);
        fait = 0;
        sautes = 0;
        sautesDeSuite = 0;
        prochaine = 0;
        temps = Temps.Ouvrir;
        aTendre.Clear();
        Pourquoi = null;
        Etat = file.Count == 0 ? EtatRangement.Fini : EtatRangement.EnMarche;
    }

    public void Arreter(string? pourquoi)
    {
        if (Etat != EtatRangement.EnMarche) return;
        Pourquoi = pourquoi;
        Etat = pourquoi is null ? EtatRangement.Fini : EtatRangement.Interrompu;
    }

    /// <summary>Une operation au plus par appel, et seulement quand l'heure est
    /// venue. Appele depuis le fil du jeu.</summary>
    public void Tic(Catalogue? cat, double maintenant)
    {
        if (Etat != EtatRangement.EnMarche) return;
        if (fait >= file.Count)
        {
            Arreter(null);
            return;
        }
        if (maintenant < prochaine) return;
        // Une demi-seconde et des poussieres, et la poussiere varie : une cadence
        // reguliere au millieme ne ressemble a personne.
        // La meme attente partout : on regarde une machine travailler, et une
        // cadence qui varie selon l'etape se lit mal.
        prochaine = maintenant + Cadence + hasard.NextDouble() * 0.3;

        if (cat is null)
        {
            Arreter(Mots.RangeurSansCatalogue);
            return;
        }

        try
        {
            if (!Faire(cat, file[fait])) return;
        }
        catch (Exception e)
        {
            journal.Error(e, "rangement impossible");
            Arreter(Mots.RangeurErreur);
            return;
        }
        fait++;
    }

    /// <summary>Fait une operation. Rend faux et arrete si quelque chose cloche.</summary>
    private unsafe bool Faire(Catalogue cat, Tache tache)
    {
        var ui = UIState.Instance();
        var mirage = MirageManager.Instance();

        if (tache.Moyen == Moyen.Armoire)
        {
            if (!ui->Cabinet.IsCabinetLoaded())
            {
                Arreter(Mots.RangeurArmoireFermee);
                return false;
            }
            // Deja range entre-temps : ce n'est pas une erreur, c'est fait.
            if (ui->Cabinet.IsItemInCabinet(tache.Cible - 1)) return true;
            if (!ui->Cabinet.StoreCabinetItem(tache.Cible - 1))
            {
                Arreter(Mots.RangeurRefus(tache.Nom));
                return false;
            }
            return true;
        }

        if (!mirage->PrismBoxLoaded)
        {
            Arreter(Mots.RangeurCoiffeuseFermee);
            return false;
        }

        var tenue = cat.Tenues.FirstOrDefault(x => x.Id == tache.Cible);
        if (tenue is null) return true;

        return Convertir(tache, tenue);
    }

    /// <summary>
    /// Une conversion, un temps par appel.
    ///
    /// C'est la procedure du joueur, dans son ordre : ouvrir la fenetre sur une
    /// premiere piece, lui tendre les suivantes, valider, transformer, confirmer.
    /// Chaque temps rend faux tant qu'il n'a pas abouti, et la tache ne se compte
    /// pour faite qu'au dernier.
    /// </summary>
    private unsafe bool Convertir(Tache tache, Tenue tenue)
    {
        var mirage = MirageManager.Instance();
        var agent = AgentMiragePrismPrismSetConvert.Instance();
        var rangs = Photo.SlotsDe(ensembles, tenue.Id);

        switch (temps)
        {
            case Temps.Ouvrir:
            {

                // Ce qu'on va tendre : les pieces de la tenue qu'on a sous la
                // main et qui ne sont pas deja dans la coiffeuse.
                aTendre.Clear();
                for (var k = 0; k < 11 && k < rangs.Length; k++)
                {
                    var objet = rangs[k];
                    if (objet == 0) continue;
                    if (tache.Moyen == Moyen.TenueEntamee
                        && mirage->IsSetSlotUnlocked(tache.Emplacement, k)) continue;
                    if (Trouver(objet) is null) continue;
                    aTendre.Add(objet);
                }
                if (aTendre.Count == 0)
                {
                    temps = Temps.Ouvrir;
                    return true;
                }

                var premiere = aTendre[0];
                var ou = Trouver(premiere)!.Value;
                // Les identifiants des deux fenetres du jeu : l'agent en a
                // besoin pour se rattacher a ce qui est ouvert.
                var idCoiffeuse = IdFenetre("MiragePrismPrismBox");
                if (idCoiffeuse == 0)
                {
                    Arreter(Mots.RangeurCoiffeuseFermee);
                    return false;
                }
                // Le geste manuel part de la fenetre « Ranger », jamais
                // d'ailleurs : sans elle, l'agent accepte tout et ne
                // selectionne rien.
                var idCrystallize = IdFenetre("MiragePrismPrismBoxCrystallize");
                if (idCrystallize == 0)
                {
                    AgentMiragePrismPrismBox.Instance()->PopulateCrystallizeAndFireRefresh();
                    if (++attenteQuestion < 4) return false;
                    attenteQuestion = 0;
                    return Passer(Mots.RangeurRangerFermee);
                }
                attenteQuestion = 0;
                if (!agent->Open(premiere, ou.Contenant, ou.Case, idCrystallize, idCoiffeuse, true))
                {
                    Arreter(Mots.RangeurRefus(tache.Nom));
                    return false;
                }
                aTendre.RemoveAt(0);
                avantConversion = Occupees();
                temps = Temps.Question;
                return false;
            }

            case Temps.Tendre:
            {
                if (aTendre.Count == 0)
                {
                    temps = Temps.Valider;
                    return false;
                }
                var objet = aTendre[0];
                var ou = Trouver(objet);
                // Disparue entre-temps : on passe, plutot que de s'arreter pour
                // une piece que le joueur vient de bouger lui-meme.
                if (ou is not null) agent->PopulateHandInItem(ou.Value.Contenant, objet, true);
                aTendre.RemoveAt(0);
                return false;
            }

            case Temps.Valider:
                agent->ValidateItems();
                temps = Temps.Transformer;
                return false;

            case Temps.Transformer:
            {
                // « Transformer ». La fenetre est ouverte et pointee : l'appel a
                // enfin le contexte qui lui manquait.
                //
                // Les tableaux restent fournis : la signature les attend, et
                // passer un pointeur nul a du code natif ne se rattrape pas.
                var contenants = stackalloc InventoryType[11];
                var cases = stackalloc ushort[11];
                for (var k = 0; k < 11; k++)
                {
                    contenants[k] = InventoryType.Invalid;
                    cases[k] = 0;
                    var objet = k < rangs.Length ? rangs[k] : 0;
                    if (objet == 0) continue;
                    if (tache.Moyen == Moyen.TenueEntamee
                        && mirage->IsSetSlotUnlocked(tache.Emplacement, k)) continue;
                    var ou = Trouver(objet);
                    if (ou is null) continue;
                    contenants[k] = ou.Value.Contenant;
                    cases[k] = ou.Value.Case;
                }

                var ok = tache.Moyen == Moyen.TenueNeuve
                    ? mirage->StoreNewOutfit(tenue.Id, contenants, cases)
                    : mirage->StoreExistingOutfit(tache.Emplacement, contenants, cases);
                if (!ok)
                {
                    Arreter(Prismes() < tache.Pieces
                        ? Mots.RangeurSansPrisme
                        : Mots.RangeurRefus(tache.Nom));
                    return false;
                }
                temps = Temps.Confirmer;
                return false;
            }

            case Temps.Question:
            {
                // « Cet objet appartient a un ensemble complet existant. Ranger
                // et ajouter au mirage d'ensemble ? » Elle ne parait pas
                // toujours : son absence n'est pas une erreur.
                Repondre();
                temps = Temps.Tendre;
                return false;
            }

            case Temps.Confirmer:
            {
                // Avant de repondre, LIRE. Une confirmation qui annonce zero
                // prisme est une conversion vide : la selection a echoue, et un
                // Oui la-dessus ne range rien, ou pire. On repond Non et on
                // passe, plutot que de confirmer a l'aveugle.
                var verdict = LireConfirmation();
                if (verdict == Verdict.Vide
                    || (verdict == Verdict.Doublon && tache.Moyen == Moyen.TenueNeuve))
                {
                    RepondreNon();
                    return Passer(verdict == Verdict.Vide
                        ? Mots.RangeurSelectionVide
                        : Mots.RangeurDoublon(tache.Nom));
                }

                // La question peut mettre un instant a paraitre apres
                // « Transformer » : on retente quelques tours avant de conclure.
                if (!Repondre())
                {
                    // Cinq tours : la fenetre peut tarder, et le geste de la case en
                    // consomme un a lui seul.
                    if (++attenteQuestion < 5) return false;
                    attenteQuestion = 0;
                    return Passer(Mots.RangeurPasDeQuestion);
                }
                attenteQuestion = 0;
                temps = Temps.Fini;
                return false;
            }

            case Temps.Fini:
            {
                // Un oui sans effet est un non qui se tait. Une tenue neuve doit
                // avoir pris un emplacement de plus ; sans ca on s'arrete, plutot
                // que d'enchainer quinze conversions qui ne font rien.
                temps = Temps.Ouvrir;
                if (tache.Moyen == Moyen.TenueNeuve && Occupees() == avantConversion)
                    return Passer(Mots.RangeurSansEffet);
                sautesDeSuite = 0;
                return true;
            }

            default:
                temps = Temps.Ouvrir;
                return true;
        }
    }

    /// <summary>
    /// Passe la tache en cours au lieu d'arreter tout.
    ///
    /// « Peu importe l'ordre, tant que tout est mis a la fin » : un accroc sur
    /// une tenue ne doit pas emporter les quinze suivantes. Mais trois de suite
    /// veulent dire que le probleme n'est pas la tenue, et la on s'arrete.
    /// </summary>
    private bool Passer(string pourquoi)
    {
        temps = Temps.Ouvrir;
        aTendre.Clear();
        sautes++;
        sautesDeSuite++;
        journal.Warning("tache passee : {0}", pourquoi);
        if (sautesDeSuite >= 3)
        {
            Arreter(pourquoi);
            return false;
        }
        // Comptee comme faite : elle est derriere nous, c'est ce qui compte.
        return true;
    }

    /// <summary>
    /// Les fenetres de confirmation possibles, dans l'ordre ou on les cherche.
    ///
    /// La confirmation du mirage n'est PAS SelectYesno : le jeu a ses propres
    /// dialogues pour la coiffeuse, et c'est pour ca que repondre a SelectYesno
    /// ne faisait rien — la fenetre visee n'existait pas.
    /// </summary>
    private static readonly string[] Confirmations =
    [
        "MiragePrismPrismSetConvertC",
        "MiragePrismExecute",
        "SelectYesno",
    ];

    /// <summary>Ou en est la reponse : la case d'abord, le bouton ensuite.</summary>
    private bool caseFaite;

    /// <summary>
    /// Repond a la confirmation, en deux gestes comme a la main : cocher la
    /// case « Confirmer » si la fenetre en a une, puis cliquer « Oui ».
    ///
    /// Les deux gestes sont les recettes d'ECommons, la bibliotheque
    /// d'AutoRetainer, reprises de sa source : rejouer l'evenement que le noeud
    /// a lui-meme enregistre. Un tampon de donnees pour la case, rien pour le
    /// bouton, et c'est leur code qui le dit, pas une supposition.
    /// </summary>
    private unsafe bool Repondre()
    {
        AtkUnitBase* fenetre = null;
        foreach (var nom in Confirmations)
        {
            var f = (AtkUnitBase*)gui.GetAddonByName(nom).Address;
            if (f is not null && f->IsVisible)
            {
                fenetre = f;
                break;
            }
        }
        if (fenetre is null) return false;

        // Premier geste : la case, si elle existe et n'est pas cochee.
        if (!caseFaite)
        {
            caseFaite = true;
            var boite = TrouverComposant(fenetre->RootNode, ComponentType.CheckBox);
            if (boite is not null)
            {
                var c = (AtkComponentCheckBox*)boite->Component;
                if (c is not null && !c->IsChecked)
                {
                    var evt = boite->AtkResNode.AtkEventManager.Event;
                    if (evt is not null)
                    {
                        var donnees = stackalloc AtkEventData[1];
                        fenetre->ReceiveEvent(evt->State.EventType, (int)evt->Param, evt, donnees);
                        c->SetChecked(true);
                    }
                    // Le bouton au prochain tour : deux gestes, comme a la main.
                    return false;
                }
            }
        }
        caseFaite = false;

        // Second geste : le bouton dont le clic porte le parametre zero.
        var bouton = TrouverBouton(fenetre->RootNode, 0, out var clic);
        if (bouton is not null && clic is not null)
        {
            var composant = (AtkComponentButton*)bouton->Component;
            if (composant is not null && !composant->IsEnabled)
            {
                var drapeaux = (ushort*)&bouton->AtkResNode.NodeFlags;
                *drapeaux ^= 1 << 5;
            }
            fenetre->ReceiveEvent(clic->State.EventType, (int)clic->Param, clic);
            return true;
        }

        fenetre->FireCallbackInt(0);
        return true;
    }

    private enum Verdict
    {
        Rien,
        Normale,
        Vide,
        Doublon,
    }

    /// <summary>
    /// Ce que la confirmation annonce, lu dans ses propres textes.
    ///
    /// « Utiliser 0 prismes » veut dire qu'aucun objet n'est selectionne, et
    /// « vous possedez deja un ensemble du meme type » qu'un Oui creerait un
    /// doublon. Les deux se lisent, ils ne se devinent pas.
    /// </summary>
    private unsafe Verdict LireConfirmation()
    {
        var fenetre = Confirmation();
        if (fenetre is null) return Verdict.Rien;

        var vide = false;
        var doublon = false;
        for (var i = 0; i < fenetre->AtkValuesCount; i++)
        {
            var v = fenetre->AtkValues[i];
            if (v.Type is not (AtkValueType.String or AtkValueType.ConstString)) continue;
            var texte = v.String.ToString();
            if (string.IsNullOrEmpty(texte)) continue;
            if (texte.Contains("0 prisme", StringComparison.OrdinalIgnoreCase)
                || texte.Contains("0 glamour prism", StringComparison.OrdinalIgnoreCase))
                vide = true;
            if (texte.Contains("possédez déjà un ensemble", StringComparison.OrdinalIgnoreCase)
                || texte.Contains("already own", StringComparison.OrdinalIgnoreCase))
                doublon = true;
        }
        if (vide) return Verdict.Vide;
        if (doublon) return Verdict.Doublon;
        return Verdict.Normale;
    }

    /// <summary>La fenetre de confirmation ouverte, ou rien.</summary>
    private unsafe AtkUnitBase* Confirmation()
    {
        foreach (var nom in Confirmations)
        {
            var f = (AtkUnitBase*)gui.GetAddonByName(nom).Address;
            if (f is not null && f->IsVisible) return f;
        }
        return null;
    }

    /// <summary>Clique « Non » : le bouton dont l'evenement porte le un.</summary>
    private unsafe void RepondreNon()
    {
        var f = Confirmation();
        if (f is null) return;
        var bouton = TrouverBouton(f->RootNode, 1, out var clic);
        if (bouton is not null && clic is not null)
            f->ReceiveEvent(clic->State.EventType, (int)clic->Param, clic);
        else f->FireCallbackInt(1);
    }

    /// <summary>L'evenement « clic » (type 25) enregistre par un noeud.</summary>
    private static unsafe AtkEvent* EvenementClic(AtkComponentNode* noeud)
    {
        var evt = noeud->AtkResNode.AtkEventManager.Event;
        while (evt is not null)
        {
            if ((ushort)evt->State.EventType == 25) return evt;
            evt = evt->NextEvent;
        }
        return null;
    }

    /// <summary>Le bouton dont le clic porte ce parametre : zero est « Oui »,
    /// un est « Non ». C'est le parametre qui nomme un bouton, pas sa place.</summary>
    private static unsafe AtkComponentNode* TrouverBouton(AtkResNode* n, uint param, out AtkEvent* evenement)
    {
        evenement = null;
        while (n is not null)
        {
            if ((ushort)n->Type >= 1000)
            {
                var noeud = (AtkComponentNode*)n;
                var c = noeud->Component;
                if (c is not null)
                {
                    if (c->GetComponentType() == ComponentType.Button)
                    {
                        var evt = EvenementClic(noeud);
                        if (evt is not null && evt->Param == param)
                        {
                            evenement = evt;
                            return noeud;
                        }
                    }
                    var dedans = TrouverBouton(c->UldManager.RootNode, param, out var e1);
                    if (dedans is not null)
                    {
                        evenement = e1;
                        return dedans;
                    }
                }
            }
            var enfant = TrouverBouton(n->ChildNode, param, out var e2);
            if (enfant is not null)
            {
                evenement = e2;
                return enfant;
            }
            n = n->PrevSiblingNode;
        }
        return null;
    }

    /// <summary>Le premier composant d'un type donne, dans l'arbre d'une fenetre.</summary>
    private static unsafe AtkComponentNode* TrouverComposant(AtkResNode* n, ComponentType type)
    {
        while (n is not null)
        {
            if ((ushort)n->Type >= 1000)
            {
                var noeud = (AtkComponentNode*)n;
                var c = noeud->Component;
                if (c is not null)
                {
                    if (c->GetComponentType() == type) return noeud;
                    var dedans = TrouverComposant(c->UldManager.RootNode, type);
                    if (dedans is not null) return dedans;
                }
            }
            var enfant = TrouverComposant(n->ChildNode, type);
            if (enfant is not null) return enfant;
            n = n->PrevSiblingNode;
        }
        return null;
    }

    /// <summary>Combien d'emplacements la coiffeuse occupe.</summary>
    private unsafe int Occupees()
    {
        var n = 0;
        foreach (var v in MirageManager.Instance()->PrismBoxItemIds)
            if (v != 0)
                n++;
        return n;
    }

    /// <summary>L'identifiant d'une fenetre ouverte, ou zero.</summary>
    private unsafe ushort IdFenetre(string nom)
    {
        var a = (AtkUnitBase*)gui.GetAddonByName(nom).Address;
        return a is null || !a->IsVisible ? (ushort)0 : a->Id;
    }
}
