using System.Collections.Concurrent;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;

namespace CodexOlympia;

/// <summary>Un endroit du monde : le territoire, sa carte, la position, qui s'y
/// trouve et comment l'endroit s'appelle.</summary>
public sealed record Lieu(uint Territoire, uint Carte, float X, float Z, string Qui, string Ou);

/// <summary>
/// Où se trouve ce qui s'obtient, quand le jeu le sait (PLG-R50).
///
/// Deux sources, toutes deux dans le client. Une carte de Triple Triade gagnée
/// contre un personnage porte, dans sa table, la position exacte de ce
/// personnage. Un objet vendu contre des gils ou échangé chez un marchand mène
/// à la boutique, la boutique au marchand, et le marchand à sa position : la
/// table des positions en connaît moins de la moitié, les fichiers de zone
/// presque tous. Ce qui reste sans lieu n'offre pas de geste.
///
/// L'index des marchands se bâtit une fois, en arrière-plan, parce qu'il lit
/// un fichier par zone : une seconde, que le joueur ne doit pas sentir.
/// </summary>
public sealed class Lieux
{
    /// <summary>L'icône du drapeau que le jeu pose sur la carte.</summary>
    private const uint Drapeau = 60561;

    /// <summary>Le type d'une ligne de position qui désigne un personnage.</summary>
    private const byte PositionDePersonnage = 8;

    private readonly IDataManager donnees;
    private readonly IPluginLog journal;

    /// <summary>De l'objet vers les marchands qui le vendent ou l'échangent.</summary>
    private readonly ConcurrentDictionary<uint, List<uint>> marchands = new();

    /// <summary>Du marchand vers sa position dans le monde.</summary>
    private readonly ConcurrentDictionary<uint, (uint Territoire, uint Carte, float X, float Z)> positions = new();

    private volatile bool pret;

    public Lieux(IDataManager donnees, IPluginLog journal)
    {
        this.donnees = donnees;
        this.journal = journal;
    }

    /// <summary>Vrai quand l'index des marchands est bâti.</summary>
    public bool Pret => pret;

    /// <summary>Bâtit l'index des marchands, hors du fil du jeu.</summary>
    public void Batir() => _ = Task.Run(() =>
    {
        try
        {
            var debut = DateTime.UtcNow;
            Boutiques();
            Personnages();
            pret = true;
            journal.Information(
                "lieux : {0} objets chez un marchand, {1} marchands placés, en {2} ms",
                marchands.Count, positions.Count, (int)(DateTime.UtcNow - debut).TotalMilliseconds);
        }
        catch (Exception e)
        {
            journal.Warning("lieux : index des marchands incomplet : {0}", e.Message);
            pret = true;
        }
    });

    /// <summary>Le lieu d'une entrée de la galerie, ou rien quand le jeu ne le
    /// sait pas. <paramref name="objet"/> est l'objet qui la déverrouille, zéro
    /// quand elle n'en a pas.</summary>
    public Lieu? Pour(string cle, uint id, uint objet)
    {
        try
        {
            if (cle == "cards") return Carte(id);
            return objet != 0 ? Marchand(objet) : null;
        }
        catch (Exception e)
        {
            journal.Warning("lieu de {0} {1} illisible : {2}", cle, id, e.Message);
            return null;
        }
    }

    /// <summary>Pose le drapeau et ouvre la carte dessus.</summary>
    public static unsafe void Montrer(Lieu lieu)
    {
        var agent = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance();
        if (agent is null) return;
        agent->SetFlagMapMarker(lieu.Territoire, lieu.Carte, lieu.X, lieu.Z, Drapeau);
        agent->OpenMapByMapId(lieu.Carte, lieu.Territoire, true);
    }

    // ------------------------------------------------------- les cartes

    /// <summary>Les façons d'obtenir une carte qui désignent un personnage et
    /// sa position : le duel, et l'achat chez lui.</summary>
    private static bool ParPersonnage(uint facon) => facon is 6 or 10;

    private Lieu? Carte(uint id)
    {
        var carte = donnees.GetExcelSheet<TripleTriadCardResident>().GetRowOrDefault(id);
        if (carte is null || !ParPersonnage(carte.Value.AcquisitionType.RowId)) return null;

        var position = donnees.GetExcelSheet<Level>().GetRowOrDefault(carte.Value.Location.RowId);
        if (position is null || position.Value.Territory.RowId == 0) return null;

        var qui = donnees.GetExcelSheet<ENpcResident>().GetRowOrDefault(carte.Value.Acquisition.RowId)
            ?.Singular.ExtractText() ?? string.Empty;
        return Depuis(position.Value.Territory.RowId, position.Value.Map.RowId,
            position.Value.X, position.Value.Z, qui);
    }

    // ---------------------------------------------------- les marchands

    private Lieu? Marchand(uint objet)
    {
        if (!pret || !marchands.TryGetValue(objet, out var vendeurs)) return null;
        foreach (var vendeur in vendeurs)
        {
            if (!positions.TryGetValue(vendeur, out var p)) continue;
            var qui = donnees.GetExcelSheet<ENpcResident>().GetRowOrDefault(vendeur)
                ?.Singular.ExtractText() ?? string.Empty;
            return Depuis(p.Territoire, p.Carte, p.X, p.Z, qui);
        }
        return null;
    }

    /// <summary>De l'objet vers les boutiques, puis des boutiques vers les
    /// marchands. Une boutique et le marchand qui la tient portent le même
    /// numéro dans la table des personnages : c'est ce numéro qu'on suit.</summary>
    private void Boutiques()
    {
        var parBoutique = new Dictionary<uint, List<uint>>();

        foreach (var boutique in donnees.GetSubrowExcelSheet<GilShopItem>())
            foreach (var article in boutique)
                Ajouter(parBoutique, boutique.RowId, article.Item.RowId);

        foreach (var boutique in donnees.GetExcelSheet<SpecialShop>())
            foreach (var article in boutique.Item)
                foreach (var recu in article.ReceiveItems)
                    Ajouter(parBoutique, boutique.RowId, recu.Item.RowId);

        foreach (var personnage in donnees.GetExcelSheet<ENpcBase>())
        {
            foreach (var donnee in personnage.ENpcData)
            {
                if (!parBoutique.TryGetValue(donnee.RowId, out var objets)) continue;
                foreach (var objet in objets)
                    marchands.GetOrAdd(objet, _ => []).Add(personnage.RowId);
            }
        }
    }

    private static void Ajouter(Dictionary<uint, List<uint>> ou, uint boutique, uint objet)
    {
        if (objet == 0) return;
        if (!ou.TryGetValue(boutique, out var liste)) ou[boutique] = liste = [];
        liste.Add(objet);
    }

    /// <summary>La position des personnages : d'abord la table des positions,
    /// puis les fichiers de zone pour ceux qu'elle ne connaît pas.</summary>
    private void Personnages()
    {
        foreach (var ligne in donnees.GetExcelSheet<Level>())
        {
            if (ligne.Type != PositionDePersonnage || ligne.Territory.RowId == 0) continue;
            positions.TryAdd(ligne.Object.RowId, (ligne.Territory.RowId, ligne.Map.RowId, ligne.X, ligne.Z));
        }

        foreach (var territoire in donnees.GetExcelSheet<TerritoryType>())
        {
            var bg = territoire.Bg.ExtractText();
            if (bg.Length == 0 || territoire.Map.RowId == 0) continue;
            var dossier = bg[..(bg.LastIndexOf('/') + 1)];
            LgbFile? fichier;
            try
            {
                fichier = donnees.GetFile<LgbFile>("bg/" + dossier + "planevent.lgb");
            }
            catch
            {
                continue;
            }
            if (fichier is null) continue;
            foreach (var couche in fichier.Layers)
                foreach (var o in couche.InstanceObjects)
                {
                    if (o.AssetType != LayerEntryType.EventNPC) continue;
                    var id = ((LayerCommon.ENPCInstanceObject)o.Object).ParentData.ParentData.BaseId;
                    positions.TryAdd(id,
                        (territoire.RowId, territoire.Map.RowId, o.Transform.Translation.X, o.Transform.Translation.Z));
                }
        }
    }

    // ------------------------------------------------------- le lieu

    private Lieu? Depuis(uint territoire, uint carte, float x, float z, string qui)
    {
        if (carte == 0)
            carte = donnees.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoire)?.Map.RowId ?? 0;
        if (carte == 0) return null;
        var ou = donnees.GetExcelSheet<Map>().GetRowOrDefault(carte)?.PlaceName.ValueNullable?.Name.ExtractText()
            ?? string.Empty;
        return new Lieu(territoire, carte, x, z, Majuscule(qui), ou);
    }

    private static string Majuscule(string t) =>
        t.Length == 0 ? t : char.ToUpper(t[0]) + t[1..];
}
