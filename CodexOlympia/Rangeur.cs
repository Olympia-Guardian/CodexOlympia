using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

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

/// <summary>Une operation, decrite par ce qu'elle vise et jamais par des cases.
///
/// <para><c>Pieces</c> est le nombre d'objets qu'elle deposera : c'est aussi le
/// nombre de prismes de mirage qu'elle consommera, un par piece.</para></summary>
public sealed record Tache(Moyen Moyen, uint Cible, string Nom, uint Emplacement = 0, int Pieces = 0);

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
    private readonly Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.MirageStoreSetItem> ensembles;
    private readonly Random hasard = new();

    private readonly List<Tache> file = [];
    private int fait;
    private double prochaine;

    public EtatRangement Etat { get; private set; } = EtatRangement.Arrete;
    public string? Pourquoi { get; private set; }
    public int Faits => fait;
    public int Total => file.Count;
    public Tache? EnCours => Etat == EtatRangement.EnMarche && fait < file.Count ? file[fait] : null;

    public Rangeur(
        IGameInventory sacs,
        IPluginLog journal,
        Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.MirageStoreSetItem> ensembles)
    {
        this.sacs = sacs;
        this.journal = journal;
        this.ensembles = ensembles;
    }

    /// <summary>Les contenants ou le jeu accepte de puiser pour un depot.</summary>
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
                var enMain = manquantes.Where(p => Trouver(p.Objet) is not null).ToList();
                if (enMain.Count == 0) continue;

                var entamee = deposees.TryGetValue(t.Id, out var place);
                if (entamee == deuxieme) continue;
                if (entamee)
                    taches.Add(new Tache(Moyen.TenueEntamee, t.Id, t.Nom, place, enMain.Count));
                else taches.Add(new Tache(Moyen.TenueNeuve, t.Id, t.Nom, 0, enMain.Count));
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

        return taches;
    }

    public void Demarrer(List<Tache> taches)
    {
        file.Clear();
        file.AddRange(taches);
        fait = 0;
        prochaine = 0;
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
        prochaine = maintenant + 0.5 + hasard.NextDouble() * 0.4;

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

        // Onze emplacements, dans l'ordre de la feuille du jeu. Ce qu'on ne
        // depose pas reste `Invalid` et case zero, comme le jeu l'attend.
        var contenants = stackalloc InventoryType[11];
        var cases = stackalloc ushort[11];
        var rangs = Photo.SlotsDe(ensembles, tenue.Id);
        var quelquechose = false;
        for (var k = 0; k < 11; k++)
        {
            contenants[k] = InventoryType.Invalid;
            cases[k] = 0;
            var objet = k < rangs.Length ? rangs[k] : 0;
            if (objet == 0) continue;
            // Une piece deja dans la coiffeuse ne se redepose pas.
            if (tache.Moyen == Moyen.TenueEntamee && mirage->IsSetSlotUnlocked(tache.Emplacement, k)) continue;
            var ou = Trouver(objet);
            if (ou is null) continue;
            contenants[k] = ou.Value.Contenant;
            cases[k] = ou.Value.Case;
            quelquechose = true;
        }
        if (!quelquechose) return true;

        var ok = tache.Moyen == Moyen.TenueNeuve
            ? mirage->StoreNewOutfit(tenue.Id, contenants, cases)
            : mirage->StoreExistingOutfit(tache.Emplacement, contenants, cases);
        if (!ok)
        {
            // La cause la plus frequente, et la seule qui ne se devine pas.
            Arreter(Prismes() < tache.Pieces ? Mots.RangeurSansPrisme : Mots.RangeurRefus(tache.Nom));
            return false;
        }
        return true;
    }
}
