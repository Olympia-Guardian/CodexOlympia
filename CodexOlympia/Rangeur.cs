using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
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
/// Les temps d'un depot en coiffeuse. C'est la procedure du joueur, et rien ne
/// la raccourcit : une version precedente appelait les fonctions internes du
/// jeu sans passer par ses fenetres, et creait des ensembles vides.
/// </summary>
public enum Temps
{
    /// <summary>Ouvrir la conversion d'ensemble sur une piece qu'on possede.</summary>
    Ouvrir,

    /// <summary>Attendre de voir ce que le jeu ouvre : la fenetre de
    /// conversion, ou une simple question.</summary>
    Guichet,

    /// <summary>Remplir les cases de la fenetre, une par tour, en deux clics
    /// comme a la main.</summary>
    Tendre,

    /// <summary>Le bouton « Transformer » de la fenetre.</summary>
    Transformer,

    /// <summary>Repondre a la confirmation.</summary>
    Confirmer,

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
/// un ordre envoye au serveur.</para>
///
/// <para><b>Tout passe par les fenetres du jeu</b>, jamais par ses fonctions
/// internes. La mecanique vient de la source de YesAlready et d'ECommons, qui
/// automatisent ces memes fenetres depuis des annees : remplir les cases par le
/// menu contextuel, cliquer « Transformer », repondre a la confirmation par le
/// rappel de la fenetre. Une version precedente court-circuitait tout cela et
/// creait des ensembles vides sans rien demander.</para>
///
/// <para><b>Une operation a la fois, a cadence reglable</b>, et un accroc fait
/// passer la tache, pas tout arreter ; trois d'affilee veulent dire autre
/// chose, et la on s'arrete en disant pourquoi.</para>
/// </summary>
public sealed class Rangeur
{
    private readonly IGameInventory sacs;
    private readonly IPluginLog journal;
    private readonly IGameGui gui;
    private readonly Random hasard = new();

    private readonly List<Tache> file = [];
    private int fait;
    private double prochaine;

    /// <summary>Ou en est la conversion en cours.</summary>
    private Temps temps = Temps.Ouvrir;

    /// <summary>Tours passes a attendre qu'une fenetre paraisse.</summary>
    private int attente;

    /// <summary>Les pieces de la tache en cours qu'on possede et qui ne sont
    /// pas deposees. Sert a ouvrir, et a boucler quand le jeu procede piece par
    /// piece.</summary>
    private readonly List<uint> aOuvrir = [];

    /// <summary>Combien de taches on a passees, et combien de suite.</summary>
    private int sautes;
    private int sautesDeSuite;

    public int Sautes => sautes;

    /// <summary>Le temps entre deux gestes, en secondes. Regle par le joueur :
    /// large, on voit ce qui se passe ; serre, ca va vite.</summary>
    public double Cadence { get; set; } = 3.0;

    public EtatRangement Etat { get; private set; } = EtatRangement.Arrete;
    public string? Pourquoi { get; private set; }
    public int Faits => fait;
    public int Total => file.Count;
    public Tache? EnCours => Etat == EtatRangement.EnMarche && fait < file.Count ? file[fait] : null;

    public Rangeur(IGameInventory sacs, IPluginLog journal, IGameGui gui)
    {
        this.sacs = sacs;
        this.journal = journal;
        this.gui = gui;
    }

    /// <summary>Les contenants ou le jeu accepte de puiser : les sacs et
    /// l'arsenal, comme le selecteur de la fenetre « Ranger ».</summary>
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

    /// <summary>Le prisme de mirage. Deposer une piece en consomme un.</summary>
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

        // LA COIFFEUSE D'ABORD : un objet range a l'armoire quitte l'inventaire,
        // donc la coiffeuse ne l'aurait plus. Les tenues entamees passent avant
        // les neuves : completer un emplacement deja pris n'en consomme pas un
        // second.
        var prises = new HashSet<uint>();
        foreach (var deuxieme in new[] { false, true })
        {
            foreach (var t in cat.Tenues)
            {
                var manquantes = t.Pieces.Where(p => !coffre.Coiffeuse.Contains(p.Objet)).ToList();
                if (manquantes.Count == 0) continue;

                // `prises` : une piece deja revendiquee par une tenue de cette
                // passe ne peut pas l'etre par une autre. Deux ensembles
                // partagent parfois une piece, mais on n'en possede qu'un
                // exemplaire : le premier le prend, le second s'en passe.
                var enMain = manquantes
                    .Where(p => !prises.Contains(p.Objet) && Trouver(p.Objet) is not null)
                    .ToList();
                if (enMain.Count == 0) continue;

                var acheve = enMain.Count == manquantes.Count;
                var entamee = deposees.TryGetValue(t.Id, out var place);
                if (entamee == deuxieme) continue;
                if (entamee)
                    taches.Add(new Tache(Moyen.TenueEntamee, t.Id, t.Nom, place, enMain.Count, acheve));
                else taches.Add(new Tache(Moyen.TenueNeuve, t.Id, t.Nom, 0, enMain.Count, acheve));
                foreach (var p in enMain) prises.Add(p.Objet);
            }
        }

        // L'armoire ensuite, et seulement si on le demande. Une piece deja dans
        // la coiffeuse n'y va jamais : celle qu'on tient est un double, et un
        // double se vend.
        if (!aussiArmoire) return taches;

        var vus = new HashSet<uint>();
        foreach (var t in cat.Tenues)
        {
            foreach (var p in t.Pieces)
            {
                if (p.Armoire == 0 || prises.Contains(p.Objet) || !vus.Add(p.Armoire)) continue;
                if (coffre.Coiffeuse.Contains(p.Objet)) continue;
                if (ui->Cabinet.IsItemInCabinet(p.Armoire - 1)) continue;
                if (Trouver(p.Objet) is null) continue;
                taches.Add(new Tache(Moyen.Armoire, p.Armoire, p.Nom));
            }
        }

        return taches;
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
        attente = 0;
        aOuvrir.Clear();
        Pourquoi = null;
        Etat = file.Count == 0 ? EtatRangement.Fini : EtatRangement.EnMarche;
    }

    public void Arreter(string? pourquoi)
    {
        if (Etat != EtatRangement.EnMarche) return;
        Pourquoi = pourquoi;
        Etat = pourquoi is null ? EtatRangement.Fini : EtatRangement.Interrompu;
    }

    /// <summary>Passe la tache en cours au lieu d'arreter tout. Trois de suite
    /// veulent dire que le probleme n'est pas la tenue, et la on s'arrete.</summary>
    private bool Passer(string pourquoi)
    {
        temps = Temps.Ouvrir;
        attente = 0;
        aOuvrir.Clear();
        sautes++;
        sautesDeSuite++;
        journal.Warning("tache passee : {0}", pourquoi);
        if (sautesDeSuite >= 3)
        {
            Arreter(pourquoi);
            return false;
        }
        return true;
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

    /// <summary>Fait un temps d'une operation. Rend vrai quand la tache est
    /// terminee.</summary>
    private unsafe bool Faire(Catalogue cat, Tache tache)
    {
        var ui = UIState.Instance();

        if (tache.Moyen == Moyen.Armoire)
        {
            if (!ui->Cabinet.IsCabinetLoaded())
            {
                Arreter(Mots.RangeurArmoireFermee);
                return false;
            }
            if (ui->Cabinet.IsItemInCabinet(tache.Cible - 1)) return true;
            if (!ui->Cabinet.StoreCabinetItem(tache.Cible - 1))
            {
                Arreter(Mots.RangeurRefus(tache.Nom));
                return false;
            }
            return true;
        }

        return Convertir(cat, tache);
    }

    /// <summary>Une conversion d'ensemble, un temps par appel, par les fenetres
    /// du jeu et par elles seules.</summary>
    private unsafe bool Convertir(Catalogue cat, Tache tache)
    {
        switch (temps)
        {
            case Temps.Ouvrir:
            {
                if (Fenetre("MiragePrismPrismBox") is null)
                {
                    Arreter(Mots.RangeurCoiffeuseFermee);
                    return false;
                }
                // Le geste manuel part de la fenetre « Ranger » : sans elle,
                // l'agent accepte tout et ne selectionne rien.
                if (Fenetre("MiragePrismPrismBoxCrystallize") is null)
                {
                    AgentMiragePrismPrismBox.Instance()->PopulateCrystallizeAndFireRefresh();
                    if (++attente < 4) return false;
                    attente = 0;
                    return Passer(Mots.RangeurRangerFermee);
                }
                attente = 0;

                if (aOuvrir.Count == 0)
                {
                    var tenue = cat.Tenues.FirstOrDefault(x => x.Id == tache.Cible);
                    if (tenue is null) return true;
                    var mirage = MirageManager.Instance();
                    foreach (var p in tenue.Pieces)
                    {
                        var dedans = false;
                        foreach (var v in mirage->PrismBoxItemIds)
                        {
                            if (v == 0) continue;
                            if ((v >= SeuilHq ? v - SeuilHq : v) == p.Objet)
                            {
                                dedans = true;
                                break;
                            }
                        }
                        if (!dedans && Trouver(p.Objet) is not null) aOuvrir.Add(p.Objet);
                    }
                    if (aOuvrir.Count == 0) return true;
                }

                var piece = aOuvrir[0];
                var ou = Trouver(piece);
                if (ou is null)
                {
                    aOuvrir.RemoveAt(0);
                    return false;
                }
                var idRanger = Fenetre("MiragePrismPrismBoxCrystallize")->Id;
                var idCoiffeuse = Fenetre("MiragePrismPrismBox")->Id;
                AgentMiragePrismPrismSetConvert.Instance()->Open(
                    piece, ou.Value.Contenant, ou.Value.Case, idRanger, idCoiffeuse, true);
                temps = Temps.Guichet;
                return false;
            }

            case Temps.Guichet:
            {
                // Ce que le jeu a ouvert decide de la suite : sa fenetre de
                // conversion, ou une simple question quand la piece rejoint un
                // ensemble deja complet.
                if (Fenetre("MiragePrismPrismSetConvert") is not null)
                {
                    attente = 0;
                    aOuvrir.Clear();
                    temps = Temps.Tendre;
                    return false;
                }
                if (Confirmation() is not null)
                {
                    attente = 0;
                    RepondreOui();
                    aOuvrir.RemoveAt(0);
                    // Piece suivante par le meme chemin, ou tache finie.
                    temps = aOuvrir.Count > 0 ? Temps.Ouvrir : Temps.Fini;
                    return false;
                }
                if (++attente < 4) return false;
                attente = 0;
                return Passer(Mots.RangeurPasDeQuestion);
            }

            case Temps.Tendre:
            {
                var convert = Fenetre("MiragePrismPrismSetConvert");
                if (convert is null) return Passer(Mots.RangeurPasDeQuestion);

                // Le noeud 12 : « cet ensemble est deja dans la coiffeuse ».
                // C'est le jeu qui le dit, et on le croit : pas de doublon.
                var deja = convert->GetNodeById(12);
                if (deja is not null && deja->IsVisible())
                {
                    Fermer(convert);
                    return Passer(Mots.RangeurDoublon(tache.Nom));
                }

                var compte = (int)Valeur(convert, 20);
                var cible = -1;
                for (var i = 0; i < compte; i++)
                {
                    // L'etat de chaque case : 0 piece manquante, 2 a remplir,
                    // 3 remplie, 6 deja dans l'ensemble.
                    if (Valeur(convert, 21 + i * 7 + 6) == 2)
                    {
                        cible = i;
                        break;
                    }
                }

                if (cible < 0)
                {
                    var remplies = 0;
                    for (var i = 0; i < compte; i++)
                        if (Valeur(convert, 21 + i * 7 + 6) == 3)
                            remplies++;
                    if (remplies == 0)
                    {
                        Fermer(convert);
                        return Passer(Mots.RangeurSelectionVide);
                    }
                    temps = Temps.Transformer;
                    return false;
                }

                // Les deux clics du geste manuel : le premier ouvre le menu de
                // la case, le second y choisit la piece.
                var menu = Fenetre("ContextIconMenu");
                if (menu is null)
                {
                    Rappeler(convert, 13, cible);
                }
                else
                {
                    var icone = Valeur(convert, 21 + cible * 7 + 1);
                    RappelerChoix(menu, icone);
                }
                return false;
            }

            case Temps.Transformer:
            {
                var convert = Fenetre("MiragePrismPrismSetConvert");
                if (convert is null) return Passer(Mots.RangeurPasDeQuestion);
                // « Transformer » est le composant 27 de la fenetre, « Fermer »
                // le 26. C'est la source de YesAlready qui les nomme.
                var bouton = convert->GetComponentButtonById(27);
                if (bouton is null || !bouton->IsEnabled)
                {
                    Fermer(convert);
                    return Passer(Prismes() == 0 ? Mots.RangeurSansPrisme : Mots.RangeurSelectionVide);
                }
                CliquerBouton(convert, bouton);
                temps = Temps.Confirmer;
                return false;
            }

            case Temps.Confirmer:
            {
                var fenetre = Confirmation();
                if (fenetre is null)
                {
                    if (++attente < 5) return false;
                    attente = 0;
                    return Passer(Mots.RangeurPasDeQuestion);
                }
                attente = 0;

                // Avant de repondre, LIRE. Zero prisme veut dire conversion
                // vide, et « vous possedez deja un ensemble » un doublon.
                var verdict = LireConfirmation(fenetre);
                if (verdict != Verdict.Normale)
                {
                    RepondreNon();
                    return Passer(verdict == Verdict.Vide
                        ? Mots.RangeurSelectionVide
                        : Mots.RangeurDoublon(tache.Nom));
                }

                RepondreOui();
                temps = Temps.Fini;
                return false;
            }

            default:
                temps = Temps.Ouvrir;
                sautesDeSuite = 0;
                return true;
        }
    }

    // ---------------------------------------------------------- les fenetres

    /// <summary>Les confirmations possibles, dans l'ordre ou on les cherche.</summary>
    private static readonly string[] Confirmations =
    [
        "MiragePrismPrismSetConvertC",
        "MiragePrismExecute",
        "SelectYesno",
    ];

    private unsafe AtkUnitBase* Fenetre(string nom)
    {
        var f = (AtkUnitBase*)gui.GetAddonByName(nom).Address;
        return f is not null && f->IsVisible ? f : null;
    }

    private unsafe AtkUnitBase* Confirmation()
    {
        foreach (var nom in Confirmations)
        {
            var f = Fenetre(nom);
            if (f is not null) return f;
        }
        return null;
    }

    private enum Verdict
    {
        Normale,
        Vide,
        Doublon,
    }

    /// <summary>Ce que la confirmation annonce, lu dans ses propres textes.
    /// Zero prisme et doublon se lisent, ils ne se devinent pas.</summary>
    private static unsafe Verdict LireConfirmation(AtkUnitBase* fenetre)
    {
        for (var i = 0; i < fenetre->AtkValuesCount; i++)
        {
            var v = fenetre->AtkValues[i];
            if (v.Type is not (AtkValueType.String or AtkValueType.ConstString)) continue;
            var texte = v.String.ToString();
            if (string.IsNullOrEmpty(texte)) continue;
            if (texte.Contains("0 prisme", StringComparison.OrdinalIgnoreCase)
                || texte.Contains("0 glamour prism", StringComparison.OrdinalIgnoreCase))
                return Verdict.Vide;
            if (texte.Contains("possédez déjà un ensemble", StringComparison.OrdinalIgnoreCase)
                || texte.Contains("already own", StringComparison.OrdinalIgnoreCase))
                return Verdict.Doublon;
        }
        return Verdict.Normale;
    }

    /// <summary>
    /// Repond a la confirmation ouverte.
    ///
    /// Les dialogues du mirage se repondent par le rappel de la fenetre, zero
    /// pour oui, un pour non, avec l'etat mis a jour : c'est mot pour mot ce
    /// que fait YesAlready sur ces memes fenetres. SelectYesno garde sa recette
    /// a elle, le clic du bouton.
    /// </summary>
    private unsafe void RepondreOui() => Repondre(0);

    private unsafe void RepondreNon() => Repondre(1);

    private unsafe void Repondre(int reponse)
    {
        var yesno = Fenetre("SelectYesno");
        if (yesno is not null)
        {
            var bouton = TrouverBouton(yesno->RootNode, (uint)reponse, out var clic);
            if (bouton is not null && clic is not null)
            {
                var composant = (AtkComponentButton*)bouton->Component;
                if (composant is not null && !composant->IsEnabled)
                {
                    var drapeaux = (ushort*)&bouton->AtkResNode.NodeFlags;
                    *drapeaux ^= 1 << 5;
                }
                yesno->ReceiveEvent(clic->State.EventType, (int)clic->Param, clic);
                return;
            }
        }

        var fenetre = Confirmation();
        if (fenetre is null) return;
        Rappeler(fenetre, reponse);
    }

    /// <summary>Le rappel d'une fenetre, valeurs entieres, etat mis a jour.</summary>
    private static unsafe void Rappeler(AtkUnitBase* fenetre, params int[] valeurs)
    {
        var v = stackalloc AtkValue[valeurs.Length];
        for (var i = 0; i < valeurs.Length; i++)
        {
            v[i].Type = AtkValueType.Int;
            v[i].Int = valeurs[i];
        }
        fenetre->FireCallback((uint)valeurs.Length, v, true);
    }

    /// <summary>Le choix dans le menu contextuel d'une case : les cinq valeurs
    /// viennent de la source de YesAlready, l'icone de la case au milieu.</summary>
    private static unsafe void RappelerChoix(AtkUnitBase* menu, uint icone)
    {
        var v = stackalloc AtkValue[5];
        v[0].Type = AtkValueType.Int;
        v[0].Int = 0;
        v[1].Type = AtkValueType.Int;
        v[1].Int = 0;
        v[2].Type = AtkValueType.UInt;
        v[2].UInt = icone;
        v[3].Type = AtkValueType.UInt;
        v[3].UInt = 0;
        v[4].Type = AtkValueType.Int;
        v[4].Int = 0;
        menu->FireCallback(5, v, true);
    }

    /// <summary>Ferme la fenetre de conversion par son bouton, le composant 26.</summary>
    private unsafe void Fermer(AtkUnitBase* convert)
    {
        var bouton = convert->GetComponentButtonById(26);
        if (bouton is not null && bouton->IsEnabled) CliquerBouton(convert, bouton);
    }

    /// <summary>Clique un bouton comme le jeu se clique lui-meme : en rejouant
    /// l'evenement que le bouton a enregistre.</summary>
    private static unsafe void CliquerBouton(AtkUnitBase* fenetre, AtkComponentButton* bouton)
    {
        var noeud = &bouton->AtkComponentBase.OwnerNode->AtkResNode;
        var evt = noeud->AtkEventManager.Event;
        if (evt is null) return;
        fenetre->ReceiveEvent(evt->State.EventType, (int)evt->Param, evt);
    }

    /// <summary>Une valeur entiere d'une fenetre, ou zero.</summary>
    private static unsafe uint Valeur(AtkUnitBase* fenetre, int i)
    {
        if (i < 0 || i >= fenetre->AtkValuesCount) return 0;
        var v = fenetre->AtkValues[i];
        return v.Type switch
        {
            AtkValueType.UInt => v.UInt,
            AtkValueType.Int => (uint)v.Int,
            _ => 0,
        };
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
}
