using Dalamud.Game;

namespace CodexOlympiaAuto;

/// <summary>Ce que le joueur a choisi comme langue.</summary>
public enum Langue
{
    /// <summary>Celle du client de jeu.</summary>
    Auto,
    Francais,
    Anglais,
}

/// <summary>
/// Tous les textes du plugin, en deux langues. Des propriétés plutôt qu'un
/// dictionnaire : le compilateur refuse un texte oublié.
/// </summary>
public static class Mots
{
    public static bool Fr { get; private set; } = true;

    public static void Choisir(Langue choix, ClientLanguage jeu)
    {
        Fr = choix switch
        {
            Langue.Francais => true,
            Langue.Anglais => false,
            _ => jeu == ClientLanguage.French,
        };
    }

    private static string D(string fr, string en) => Fr ? fr : en;

    // -------------------------------------------------------------- la fenêtre

    public static string PageARanger => D("À ranger", "To store");
    public static string PageSonde => D("Sonde", "Probe");
    public static string PageConfig => D("Configuration", "Settings");

    public static string Experimental => D(
        "PLUGIN EXPÉRIMENTAL, en cours de développement. Il agit sur le jeu : chaque dépôt est un "
        + "ordre envoyé au serveur. Rien ne se lance sans un geste de ta part, et tout s'arrête au "
        + "premier imprévu.",
        "EXPERIMENTAL PLUGIN, under development. It acts on the game: each deposit is an order sent "
        + "to the server. Nothing starts without you, and everything stops at the first surprise.");

    public static string CataloguePasPret =>
        D("Le catalogue de l'application n'est pas encore chargé.", "The app catalogue is not loaded yet.");
    public static string Reessayer => D("Réessayer", "Try again");

    // ---------------------------------------------------------------- à ranger

    public static string ARangerQuoi => D(
        "Ce que tu possèdes sans l'avoir déposé. Une pièce rangée dans un sac, portée sur toi "
        + "ou confiée à un servant ne compte pour rien tant qu'elle n'est pas dans ta coiffeuse "
        + "ou ton armoire : elle peut se vendre, se jeter, se perdre de vue.",
        "What you own without having deposited it. A piece sitting in a bag, worn, or left with "
        + "a retainer counts for nothing until it reaches your glamour dresser or armoire: it can "
        + "be sold, discarded, forgotten.");
    public static string ARangerRien => D(
        "Rien à ranger : tout ce que tu as sous la main est déjà déposé.",
        "Nothing to store: everything you are carrying is already deposited.");
    public static string ARangerCompte(int pieces, int achevent) => achevent == 0
        ? D($"{pieces} pièces à déposer.", $"{pieces} pieces to deposit.")
        : D($"{pieces} pièces à déposer, dont {achevent} qui achèvent une tenue.",
            $"{pieces} pieces to deposit, {achevent} of which complete an outfit.");
    public static string ARangerAchevent =>
        D("Celles-ci achèvent une tenue", "These complete an outfit");
    public static string ARangerReste => D("Le reste", "The rest");
    public static string ServantsVus(int n) => n == 0
        ? D("Aucun servant consulté : parle-leur une fois pour qu'ils comptent.",
            "No retainer seen yet: talk to them once so they count.")
        : D($"{n} servants connus, d'après la dernière fois que tu leur as parlé.",
            $"{n} retainers known, from the last time you talked to them.");
    public static string DoublesTitre(int n) => D(
        $"{n} pièces en double dans tes sacs", $"{n} duplicate pieces in your bags");
    public static string DoublesAide => D(
        "Elles sont déjà dans ta coiffeuse : celles-ci ne servent plus à rien. Vends-les au "
        + "PNJ ou à ta grande compagnie. Le plugin n'y touche pas.",
        "They are already in your glamour dresser: these are useless now. Sell them to a vendor "
        + "or your Grand Company. The plugin never touches them.");

    public static string OuSac => D("sac", "bag");
    public static string OuArmurerie => D("arsenal", "armoury");
    public static string OuPorte => D("porté", "worn");
    public static string OuCabas => D("cabas", "saddlebag");
    public static string OuServant(string nom) => D($"servant « {nom} »", $"retainer \"{nom}\"");

    // ------------------------------------------------------------ le rangement

    public static string RangeurTitre => D("Ranger tout seul", "Store automatically");
    public static string RangeurExperimental => D("EXPÉRIMENTAL", "EXPERIMENTAL");
    public static string RangeurAvertissement => D(
        "Chaque dépôt suit exactement les fenêtres du jeu, comme si tu cliquais toi-même. Une "
        + "opération à la fois, à cadence réglable, et tout s'arrête au premier imprévu. Reste "
        + "devant ta coiffeuse, ouverte, pendant que ça travaille.\n\n"
        + "La conversion en mirage RETIRE les matérias serties, les teintures, les mirages, les "
        + "blasons et certains bonus, et remet la symbiose à zéro. C'est le jeu qui le fait, pas "
        + "le plugin, mais tu dois le savoir avant de lancer.",
        "Each deposit follows the game's own windows, as if you clicked yourself. One operation "
        + "at a time, at an adjustable pace, and everything stops at the first surprise. Stay at "
        + "your glamour dresser, open, while it works.\n\n"
        + "Glamour conversion REMOVES melded materia, dyes, glamours, crests and some bonuses, "
        + "and resets spiritbond to zero. The game does that, not the plugin, but you must know "
        + "before you start.");
    public static string RangeurRienAFaire => D(
        "Rien à ranger d'ici. Le rangement ne prend que ce qui est SOUS LA MAIN : tes sacs et ton "
        + "arsenal. Ce qui dort chez un servant ne compte pas, va le chercher d'abord.",
        "Nothing to store from here. Storing only takes what is AT HAND: your bags and your "
        + "armoury. What sits with a retainer does not count: fetch it first.");
    public static string RangeurPartiel => D(
        "Une tenue se dépose même incomplète : elle occupe un emplacement, qu'on la remplisse en "
        + "une fois ou en cinq. Ce qui manque s'y ajoutera plus tard.",
        "An outfit can be stored even incomplete: it takes one slot whether you fill it in one go "
        + "or in five. What is missing joins it later.");
    public static string RangeurApercu(int entamees, int neuves, int armoire)
    {
        var bouts = new List<string>();
        if (entamees > 0)
            bouts.Add(entamees == 1
                ? D("1 tenue à compléter", "1 outfit to complete")
                : D($"{entamees} tenues à compléter", $"{entamees} outfits to complete"));
        if (neuves > 0)
            bouts.Add(neuves == 1
                ? D("1 tenue à créer", "1 outfit to create")
                : D($"{neuves} tenues à créer", $"{neuves} outfits to create"));
        if (armoire > 0)
            bouts.Add(D($"{armoire} objets pour l'armoire", $"{armoire} items for the armoire"));
        return string.Join(", ", bouts) + ".";
    }
    public static string RangeurPrismes(int reste, int besoin) => reste >= besoin
        ? D($"{reste} prismes de mirage en réserve, {besoin} nécessaires.",
            $"{reste} glamour prisms in stock, {besoin} needed.")
        : D($"{reste} prismes de mirage seulement, il en faudrait {besoin} : ça s'arrêtera en route.",
            $"only {reste} glamour prisms, {besoin} needed: it will stop partway.");
    public static string RangeurAussiArmoire => D(
        "Ranger aussi dans l'armoire ce qu'aucune tenue ne prendra. Sans cette case, seules les "
        + "tenues sont servies : un objet rangé à l'armoire quitte l'inventaire, et la coiffeuse "
        + "ne l'aura plus.",
        "Also store in the armoire whatever no outfit will take. Without this box, only outfits "
        + "are served: an item stored in the armoire leaves your inventory, and the dresser will "
        + "not get it.");
    public static string RangeurCadence => D("secondes entre deux gestes", "seconds between moves");
    public static string RangeurLancer => D("Ranger", "Store");
    public static string RangeurStop => D("Arrêter", "Stop");
    public static string RangeurAMain => D("tu as appuyé sur Arrêter.", "you pressed Stop.");
    public static string RangeurAvance(int fait, int total, string quoi) => $"{fait}/{total} · {quoi}";
    public static string RangeurFini(int n, int sautes) => sautes == 0
        ? D($"Rangé : {n} opérations.", $"Stored: {n} operations.")
        : D($"Rangé : {n} opérations, dont {sautes} passées.",
            $"Stored: {n} operations, {sautes} skipped.");
    public static string RangeurArrete(string pourquoi) =>
        D("Arrêté : " + pourquoi, "Stopped: " + pourquoi);
    public static string RangeurArmoireFermee =>
        D("ton armoire n'est pas ouverte.", "your armoire is not open.");
    public static string RangeurCoiffeuseFermee =>
        D("ta coiffeuse mirage n'est pas ouverte.", "your glamour dresser is not open.");
    public static string RangeurRangerFermee => D(
        "la fenêtre « Ranger » de la coiffeuse ne s'ouvre pas.",
        "the dresser's Store window will not open.");
    public static string RangeurRefus(string quoi) =>
        D($"le jeu a refusé « {quoi} ».", $"the game refused \"{quoi}\".");
    public static string RangeurPasDeQuestion => D(
        "le jeu n'a pas ouvert la fenêtre attendue.",
        "the game did not open the expected window.");
    public static string RangeurSelectionVide => D(
        "aucune pièce n'a pu être sélectionnée : rien n'a été converti.",
        "no piece could be selected: nothing was converted.");
    public static string RangeurDoublon(string nom) => D(
        $"« {nom} » est déjà dans la coiffeuse : pas de doublon.",
        $"\"{nom}\" is already in the dresser: no duplicates.");
    public static string RangeurSansPrisme => D(
        "il te manque des prismes de mirage. Chaque pièce déposée en consomme un.",
        "you are out of glamour prisms. Each piece deposited uses one.");
    public static string RangeurSansCatalogue => D("le catalogue a disparu.", "the catalogue is gone.");
    public static string RangeurErreur => D("quelque chose a mal tourné.", "something went wrong.");

    // ------------------------------------------------------------------ divers

    public static string SondeQuoi => D(
        "Page de développement, et elle ne fait QUE lire : rien n'est cliqué, rien n'est déposé, "
        + "aucun prisme n'est consommé.",
        "Development page, and it only READS: nothing is clicked, nothing is stored, no prism is "
        + "used.");
    public static string SondeRien => D(
        "Aucune des fenêtres observées n'est ouverte.",
        "None of the watched windows is open.");

    public static string Langue_ => D("Langue", "Language");
    public static string LangueAuto => D("Celle du jeu", "Game language");
    public static string PasDePerso => D("Connecte-toi avec un personnage.", "Log in with a character.");
}
