using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.Exd;
using FFXIVClientStructs.STD;

namespace CodexOlympia;

/// <summary>Ce que vaut, en ce moment, le rapport du front bozjien que le jeu
/// tient pour le personnage connecté.</summary>
public enum EtatDuRapport
{
    /// <summary>Le plugin ne sait pas où le lire : une signature introuvable,
    /// ou un décalage qui ne tombe pas à sa place. Relire n'y changerait
    /// rien.</summary>
    Illisible,

    /// <summary>Le serveur ne l'a pas encore envoyé depuis la connexion.</summary>
    EnAttente,

    /// <summary>Ce sont encore les notes du personnage précédent.</summary>
    DuPrecedent,

    /// <summary>Il est là, et ce sont les siennes.</summary>
    Pret,
}

/// <summary>
/// Le rapport du front bozjien, les cinquante notes du front (PLG-R78).
///
/// <para><b>Le client ne le garde pas d'une session à l'autre.</b> Le serveur
/// l'envoie quand on le lui demande, et le jeu ne le demande que la première
/// fois qu'il doit dire d'une note du front si elle est enregistrée, à son
/// infobulle. Avant cette demande, chaque note se lit « absente », exactement
/// comme une note jamais enregistrée. Dalamud le sait, et ne répond pas pour
/// ces notes-là.</para>
///
/// <para>Le jeu range ce rapport à côté de la carte d'aventurier, dans une
/// partie de l'état de l'interface que FFXIVClientStructs ne décrit pas : un
/// octet par ligne de la table du rapport (non nul : la note est enregistrée),
/// et une carte qui tient, par type d'action d'objet, l'état de la demande au
/// serveur : zéro, rien de demandé depuis la connexion ; un, demandé ; deux,
/// reçu. Les notes y sont sous le type 19743, que Dalamud nomme FieldNotes.
/// D'après le désassemblage, la connexion remet cet état à zéro.</para>
///
/// <para>Le plugin retrouve ces deux places par les deux fonctions du jeu qui
/// les lisent, une signature chacune : leur déplacement donne le décalage dans
/// l'état de l'interface. Un patch peut déplacer l'une ou l'autre : la
/// signature ne se retrouve plus, ou tombe ailleurs, et le rapport se dit
/// illisible au lieu de lire une adresse au hasard. Rien n'est lu tant que les
/// deux décalages ne sont pas à leur place.</para>
///
/// <para><b>Rien n'est écrit dans la mémoire du jeu, mais une demande part</b> :
/// le plugin fait le geste de l'infobulle. Il montre au jeu une note du front,
/// le jeu voit qu'il n'a rien demandé et envoie sa demande habituelle, puis
/// note l'attente ; la réponse arrive un instant plus tard. Ce geste se fait
/// au début d'une lecture, sur le fil du jeu, jamais pendant le dessin de la
/// fenêtre, et au plus une fois par connexion.</para>
///
/// <para>Le désassemblage du client du 16 octobre 2026 l'a montré, et les deux
/// signatures y sont uniques. Rien de tout ça n'a encore été vu en jeu.</para>
/// </summary>
public static unsafe class Bozja
{
    /// <summary>La collection du rapport, telle que l'application la déclare.</summary>
    public const string Cle = "field_records";

    /// <summary>Le type d'action des objets « Notes du front bozjien » : c'est
    /// aussi la clé sous laquelle le jeu range l'état de la demande.</summary>
    public const ushort ActionDesNotes = 19743;

    /// <summary>Le nombre d'octets du rapport, un par ligne de sa table, de la
    /// ligne zéro à la ligne cinquante-quatre. Les cinquante notes en occupent
    /// les lignes une à cinquante.</summary>
    public const int Lignes = 55;

    /// <summary>La fonction qui rend l'octet d'une ligne : l'entier à
    /// <see cref="DeplacementNotes"/> est le décalage du rapport dans l'état de
    /// l'interface.</summary>
    private const string SignatureNotes = "8B 02 0F B6 84 08 ?? ?? ?? ?? C3";

    private const int DeplacementNotes = 6;

    /// <summary>La fonction qui change l'état d'une demande : l'entier à
    /// <see cref="DeplacementDemandes"/> est le décalage de la carte des
    /// demandes dans l'état de l'interface.</summary>
    private const string SignatureDemandes = "44 0F B7 0A 48 8B 91 ?? ?? ?? ?? 48 8B CA 48 8B 42 08";

    private const int DeplacementDemandes = 7;

    /// <summary>L'état d'une demande dont la réponse est arrivée.</summary>
    private const int Recu = 2;

    /// <summary>Combien de fois le plugin montre une note au jeu, au plus, dans
    /// une même connexion, quand la demande ne part pas.</summary>
    private const int EssaisParConnexion = 3;

    /// <summary>Une carte des demandes en compte une par type d'action qui en
    /// fait ; au-delà, ce n'est pas elle qu'on regarde.</summary>
    private const long DemandesAuPlus = 256;

    /// <summary>Le décalage du rapport dans l'état de l'interface, zéro tant
    /// qu'il n'est pas trouvé.</summary>
    private static int notes;

    /// <summary>Le décalage de la carte des demandes, zéro tant qu'il n'est pas
    /// trouvé.</summary>
    private static int demandes;

    /// <summary>Vrai si le plugin sait où lire le rapport et l'état de sa
    /// demande.</summary>
    public static bool Lisible => notes != 0 && demandes != 0;

    /// <summary>Vrai une fois que la demande de ce plugin est partie, depuis la
    /// connexion : le jeu a noté l'attente après qu'on lui a montré une
    /// note.</summary>
    private static bool demandee;

    /// <summary>Les notes montrées au jeu depuis la connexion.</summary>
    private static int essais;

    /// <summary>Ce que le journal a déjà dit, pour ne pas le répéter à chaque
    /// lecture.</summary>
    private static bool sansNote, sansFonction, carteDite;

    /// <summary>Le personnage dont le rapport est reconnu, zéro sinon.</summary>
    private static ulong reconnuPour;

    /// <summary>L'empreinte de ce que le dernier personnage a laissé.</summary>
    private static ulong laissees;

    /// <summary>Le personnage à qui ce rapport appartient, zéro sinon.</summary>
    private static ulong laisseesPar;

    /// <summary>
    /// Cherche les deux places, au démarrage. Une signature introuvable, ou
    /// un décalage qui ne tombe pas à sa place, laisse le rapport illisible et
    /// le dit une fois au journal.
    /// </summary>
    public static void Trouver(ISigScanner memoire, IPluginLog journal)
    {
        var n = Decalage(memoire, SignatureNotes, DeplacementNotes);
        var d = Decalage(memoire, SignatureDemandes, DeplacementDemandes);
        if (n == 0 || d == 0)
        {
            journal.Warning("rapport du front bozjien introuvable (notes {0}, demandes {1}) : il ne se lira pas",
                n == 0 ? "absentes" : "trouvées", d == 0 ? "absentes" : "trouvées");
            return;
        }
        if (!AuBonEndroit(n, d))
        {
            journal.Warning("rapport du front bozjien hors de sa place (notes 0x{0:X}, demandes 0x{1:X}) : il ne se lira pas",
                n, d);
            return;
        }
        notes = n;
        demandes = d;
        journal.Information("rapport du front bozjien : notes à 0x{0:X}, demandes à 0x{1:X} de l'état de l'interface",
            n, d);
    }

    /// <summary>Le décalage que porte l'instruction trouvée par cette
    /// signature, ou zéro.</summary>
    private static int Decalage(ISigScanner memoire, string signature, int deplacement)
    {
        try
        {
            if (!memoire.TryScanText(signature, out var adresse) || adresse == 0) return 0;
            return *(int*)(adresse + deplacement);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Vrai si les deux décalages tombent là où le client du 16 octobre 2026
    /// les met : dans le trou que FFXIVClientStructs laisse entre la carte
    /// d'aventurier et les réglages de l'écran de sélection, le rapport avant
    /// la carte des demandes, qui est alignée comme un pointeur. Une signature
    /// qui tomberait sur une autre instruction après un patch donnerait un
    /// décalage n'importe où : lire une carte à une adresse fausse ferait
    /// tomber le jeu.
    /// </summary>
    private static bool AuBonEndroit(int n, int d)
    {
        var ui = UIState.Instance();
        if (ui == null) return false;
        var depart = (long)((byte*)&ui->CharaCard - (byte*)ui) + sizeof(CharaCard);
        var borne = (long)((byte*)&ui->ClientSelectDataConfigFlags - (byte*)ui);
        return n >= depart && n + Lignes <= d && d % 8 == 0 && d + sizeof(StdMap<ushort, int>) <= borne;
    }

    /// <summary>L'état de la demande du rapport : zéro, un ou deux, ou rien
    /// quand la carte ne ressemble pas à une carte. Une carte sans le type des
    /// notes dit zéro : rien n'a été demandé.</summary>
    private static int? Demande(UIState* ui)
    {
        var carte = (StdMap<ushort, int>*)((byte*)ui + demandes);
        var tete = carte->WithOps.Tree.Head;
        // La tête d'une carte du jeu n'est jamais une entrée : elle se reconnaît
        // à sa marque, et ses entrées se comptent en dizaines.
        if (tete == null || !tete->_Isnil || carte->LongCount < 0 || carte->LongCount > DemandesAuPlus)
            return null;
        return carte->TryGetValuePointer(ActionDesNotes, out var valeur) ? *valeur : 0;
    }

    /// <summary>
    /// Montre une note du front au jeu, pour qu'il demande le rapport au
    /// serveur. Appelée au début d'une lecture qui lit le rapport, sur le fil
    /// du jeu, jamais pendant le dessin de la fenêtre.
    ///
    /// <para>Rien ne part si le jeu a déjà demandé (l'état n'est plus zéro),
    /// ni si la demande de ce plugin est déjà partie depuis la connexion. La
    /// ligne de la note se demande au client : elle peut ne pas être chargée,
    /// et ce qu'il rend alors peut être la ligne d'un autre objet (PLG-R55).
    /// Une ligne absente ne coûte rien, on réessaiera à la lecture suivante.
    /// Une ligne rendue se vérifie à son effet : la demande est partie si
    /// l'état est passé de zéro à un. Sinon, ce n'était pas une note, et la
    /// lecture suivante montre la note d'après, au plus trois fois par
    /// connexion.</para>
    /// </summary>
    public static void Demander(IPluginLog journal)
    {
        try
        {
            if (!Lisible || demandee || essais >= EssaisParConnexion) return;
            var ps = PlayerState.Instance();
            var ui = UIState.Instance();
            if (ps == null || !ps->IsLoaded || ui == null) return;
            // Déjà demandé par le jeu, ou une carte qui ne se lit pas : il n'y
            // a rien à faire partir.
            if (Demande(ui) != 0) return;

            var objets = Tables.NotesDuFront;
            if (objets.Count == 0)
            {
                if (!sansNote) journal.Warning("rapport du front bozjien : aucune note du front dans la table des objets, rien à demander");
                sansNote = true;
                return;
            }
            // Les fonctions du jeu introuvables, un patch ayant déplacé leur
            // signature : les appeler lèverait.
            if ((nint)ExdModule.MemberFunctionPointers.GetItemRowById == 0
                || (nint)UIState.MemberFunctionPointers.IsItemActionUnlocked == 0)
            {
                if (!sansFonction) journal.Warning("rapport du front bozjien : fonctions du jeu introuvables, rien à demander");
                sansFonction = true;
                return;
            }

            var objet = objets[essais % objets.Count];
            var ligne = ExdModule.GetItemRowById(objet);
            if (ligne == null) return;
            essais++;
            ui->IsItemActionUnlocked(ligne);
            var apres = Demande(ui);
            if (apres is 1 or Recu)
            {
                demandee = true;
                journal.Information("rapport du front bozjien demandé au serveur (objet {0})", objet);
            }
            else
            {
                journal.Warning("rapport du front bozjien : la demande n'est pas partie (objet {0}, état {1}), essai {2} sur {3}",
                    objet, apres?.ToString() ?? "illisible", essais, EssaisParConnexion);
            }
        }
        catch (Exception e)
        {
            journal.Warning("rapport du front bozjien : demande impossible : {0}", e.Message);
        }
    }

    /// <summary>
    /// Ce que vaut le rapport pour ce personnage. Ne demande rien, et peut
    /// donc se poser pendant le dessin de la fenêtre ; la lecture passe son
    /// journal, le dessin non.
    ///
    /// <para>Un rapport reçu sans que ce plugin l'ait demandé depuis la
    /// connexion peut, en principe, être celui du personnage précédent : le
    /// désassemblage dit que la connexion remet l'état de la demande à zéro,
    /// mais rien ne l'a encore montré en jeu. L'empreinte de ce qu'un
    /// personnage laisse se prend donc à sa déconnexion, comme pour les
    /// carnets (Marques), et un autre personnage dont le rapport a exactement
    /// cette empreinte, non nulle, ne conclut rien. Si la connexion remet bien
    /// tout à zéro, la garde ne sert jamais : ce plugin a fait la demande, ou
    /// le rapport a changé.</para>
    /// </summary>
    public static EtatDuRapport Etat(ulong perso, IPluginLog? journal = null)
    {
        if (!Lisible) return EtatDuRapport.Illisible;
        var ui = UIState.Instance();
        if (ui == null) return EtatDuRapport.Illisible;
        var etat = Demande(ui);
        if (etat is null)
        {
            if (journal is not null && !carteDite)
                journal.Warning("rapport du front bozjien : la carte des demandes ne ressemble pas à une carte, rien n'est lu");
            if (journal is not null) carteDite = true;
            return EtatDuRapport.Illisible;
        }
        if (etat != Recu) return EtatDuRapport.EnAttente;
        if (perso != 0 && perso == reconnuPour) return EtatDuRapport.Pret;
        var comparer = !demandee && laisseesPar != 0 && laisseesPar != perso;
        if (comparer)
        {
            var empreinte = Empreinte(ui);
            if (empreinte != 0 && empreinte == laissees) return EtatDuRapport.DuPrecedent;
        }
        reconnuPour = perso;
        return EtatDuRapport.Pret;
    }

    /// <summary>Vrai si la note de ce numéro (celui du catalogue) est
    /// enregistrée, faux sinon, rien quand le numéro n'a pas de ligne dans le
    /// rapport. Le jeu marque ses notes par ligne, et dix-sept des cinquante
    /// numéros ne sont pas leur ligne : la table de Tables fait le
    /// pont.</summary>
    public static bool? Enregistree(uint numero)
    {
        if (!Lisible || !Tables.LignesDuRapport.TryGetValue(numero, out var ligne) || ligne >= Lignes) return null;
        var ui = UIState.Instance();
        if (ui == null) return null;
        return *((byte*)ui + notes + ligne) != 0;
    }

    /// <summary>Une somme des notes enregistrées, indépendante de leur ordre ;
    /// zéro sans aucune.</summary>
    private static ulong Empreinte(UIState* ui)
    {
        var somme = 0UL;
        var octets = (byte*)ui + notes;
        for (var i = 0u; i < Lignes; i++)
        {
            if (octets[i] == 0) continue;
            var x = (i + 1UL) * 0x9E3779B97F4A7C15UL;
            somme += x ^ (x >> 31);
        }
        return somme;
    }

    /// <summary>
    /// La déconnexion : ce que ce personnage laisse se retient, et la demande
    /// se refera à la connexion suivante. Un rapport qui n'est pas reçu n'est
    /// à personne ; un rapport qui n'a pas bougé reste à celui qui l'a laissé,
    /// comme les marques des carnets.
    /// </summary>
    public static void Deconnexion(ulong perso)
    {
        try
        {
            var ui = UIState.Instance();
            if (Lisible && perso != 0 && ui != null && Demande(ui) == Recu)
            {
                var empreinte = Empreinte(ui);
                if (empreinte != laissees)
                {
                    laissees = empreinte;
                    laisseesPar = perso;
                }
            }
        }
        catch
        {
            // Rien à retenir : la garde ne fera que comparer à moins.
        }
        demandee = false;
        essais = 0;
        reconnuPour = 0;
    }
}
