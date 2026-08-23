using Dalamud.Game;

namespace CodexOlympia;

/// <summary>Ce que le joueur a choisi comme langue.</summary>
public enum Langue
{
    /// <summary>Celle du client de jeu.</summary>
    Auto,
    Francais,
    Anglais,
}

/// <summary>
/// Tous les textes du greffon, en deux langues.
///
/// Des propriétés plutôt qu'un dictionnaire de clés : le compilateur refuse
/// alors un texte oublié, là où une clé manquante ne se voit qu'à l'exécution,
/// et seulement si quelqu'un ouvre cet écran-là dans cette langue-là.
///
/// La langue suit le client de jeu par défaut. Quelqu'un qui joue en anglais
/// depuis dix ans lit son jeu en anglais : lui imposer du français parce que sa
/// machine est française serait un contresens.
/// </summary>
public static class Mots
{
    public static bool Fr { get; private set; } = true;

    /// <summary>Fixe la langue. `Auto` suit le client, et tout ce qui n'est pas
    /// français passe en anglais : c'est la langue commune du jeu.</summary>
    public static void Choisir(Langue choix, ClientLanguage jeu)
    {
        Fr = choix switch
        {
            Langue.Francais => true,
            Langue.Anglais => false,
            _ => jeu == ClientLanguage.French,
        };
        collections = null;
    }

    private static string D(string fr, string en) => Fr ? fr : en;

    // ------------------------------------------------------------- la fenêtre

    public static string PageSync => D("Synchronisation", "Sync");
    public static string PageConfig => D("Configuration", "Settings");

    public static string AllerConfig => D("Aller à la configuration", "Go to settings");
    public static string CataloguePasPret =>
        D("Le catalogue de l'application n'est pas encore chargé.", "The app catalogue is not loaded yet.");
    public static string Reessayer => D("Réessayer", "Try again");

    public static string Regarder => D("Regarder ce que j'ai", "See what I have");
    public static string RienNePart => D("rien n'est envoyé à cette étape", "nothing is sent at this step");
    public static string Presentation => D(
        "Le greffon lit ce que le jeu tient pour débloqué, te le montre, et n'envoie que si tu " +
        "le lui dis. Rien n'est jamais décoché à ta place.",
        "The plugin reads what the game holds as unlocked, shows it to you, and only sends if " +
        "you say so. Nothing is ever unchecked on your behalf.");

    public static string OnRecupere => D("On récupère tes déblocages", "Fetching your unlocks");
    public static string Lecture => D("lecture", "reading");
    public static string EnAttente => D("en attente", "waiting");

    public static string ColCollection => D("Collection", "Collection");
    public static string ColTrouve => D("Trouvé", "Found");
    public static string NonLu => D("non lu", "not read");

    public static string Verifiables(int n, int total) =>
        D($"{n} sur {total} vérifiables", $"{n} of {total} checkable");
    public static string VerifiablesAide => D(
        "Le jeu ne sait pas répondre pour le reste de cette collection.\n" +
        "Ces entrées-là sont laissées tranquilles : ni ajoutées, ni signalées manquantes.",
        "The game cannot answer for the rest of this collection.\n" +
        "Those entries are left alone: neither added, nor reported missing.");

    public static string AjoutSeulement => D("ajout seulement", "adds only");
    public static string AjoutSeulementAide => D(
        "Cette collection se constate dans un dépôt : la coiffeuse mirage ou l'armoire.\n" +
        "On y voit ce qui s'y trouve, jamais ce qui n'y est pas. Une pièce peut dormir\n" +
        "dans un sac ou chez un servant. Rien ne sera donc signalé comme manquant.",
        "This collection is observed in a deposit: the glamour dresser or the armoire.\n" +
        "You see what is in there, never what is not. A piece may sit in a bag or with\n" +
        "a retainer. Nothing will ever be reported as missing.");

    public static string PieceQuiDort(int n) => n == 1
        ? D("Une pièce de tenue dort dans ton armoire.", "One outfit piece sits in your armoire.")
        : D($"{n} pièces de tenue dorment dans ton armoire.",
            $"{n} outfit pieces sit in your armoire.");
    public static string PieceQuiDortAide => D(
        "Dépose-les dans la coiffeuse pour pouvoir t'en servir.",
        "Move them to the glamour dresser to actually use them.");

    public static string NonLues(int n) => n == 1
        ? D("Une collection n'a pas pu être lue : elle ne sera pas envoyée.",
            "One collection could not be read: it will not be sent.")
        : D($"{n} collections n'ont pas pu être lues : elles ne seront pas envoyées.",
            $"{n} collections could not be read: they will not be sent.");

    public static string EnvoiEnCours => D("envoi en cours...", "sending...");
    public static string Envoyer => D("Envoyer à Codex Olympia", "Send to Codex Olympia");
    public static string Ajoute => D("Ajouté", "Added");
    public static string ATrancher => D("À trancher dans l'application", "To settle in the app");

    // ------------------------------------------------------ la configuration

    public static string PasDePerso => D("Connecte-toi avec un personnage.", "Log in with a character.");
    public static string JetonDe(string nom) => D($"Le jeton de {nom}", $"{nom}'s token");
    public static string JetonExplique => D(
        "Il se crée dans Codex Olympia, page de compte, section « Plugin Codex Olympia " +
        "Dalamud ». Choisis ce personnage au moment de le créer, colle-le ici, et c'est tout. " +
        "Il ne sait faire qu'une chose : déposer une photo de tes déblocages. Il ne peut ni " +
        "lire ton compte, ni le modifier, ni l'effacer.",
        "You create it in Codex Olympia, Account page, section \"Codex Olympia Dalamud plugin\". " +
        "Pick this character when you create it, paste it here, and that is all. It can do " +
        "exactly one thing: drop off a snapshot of your unlocks. It cannot read your account, " +
        "change it, or delete it.");
    public static string ColleJeton => D("colle ton jeton ici", "paste your token here");
    public static string JetonRange => D("jeton enregistré pour ce personnage", "token saved for this character");
    public static string PasDeJeton => D("aucun jeton pour ce personnage", "no token for this character");

    public static string Langue_ => D("Langue", "Language");
    public static string LangueAuto => D("Celle du jeu", "Game language");

    public static string ManqueJeton => D("Il manque le jeton de ce personnage.", "This character has no token.");
    public static string ManquePerso => D("Aucun personnage connecté.", "No character logged in.");

    // ------------------------------------------------------------ la lecture

    public static string CatalogueAbsent => D("catalogue absent", "catalogue missing");
    public static string OuvreSucces => D(
        "ouvre ton carnet de succès une fois, puis regarde à nouveau",
        "open your achievements log once, then look again");
    public static string OuvreArmoire => D(
        "ouvre une fois ton armoire chez un rassembleur pour que le jeu la charge",
        "open your armoire at a Calamity Salvager once so the game loads it");
    public static string OuvreCoiffeuse => D(
        "ouvre ta coiffeuse mirage une fois, puis regarde à nouveau : le jeu ne charge son " +
        "contenu qu'à ce moment-là",
        "open your glamour dresser once, then look again: the game only loads its contents then");
    public static string OuvreLesDeux => D(
        "ouvre ta coiffeuse mirage et ton armoire chez un rassembleur, puis regarde à nouveau",
        "open your glamour dresser and your armoire at a Calamity Salvager, then look again");
    public static string TenuesDeduites => D("elles se déduisent des pièces", "derived from the pieces");
    public static string Depots(int coiffeuse, string armoire) =>
        D($"coiffeuse : {coiffeuse} objets, armoire : {armoire}",
            $"dresser: {coiffeuse} items, armoire: {armoire}");
    public static string ArmoireNonChargee => D("non chargée", "not loaded");
    public static string ArmoirePieces(int n) => D($"{n} pièces", $"{n} pieces");

    // -------------------------------------------------------------- l'envoi

    public static string RienAEnvoyer =>
        D("rien à envoyer : aucune collection n'a pu être lue", "nothing to send: no collection could be read");
    public static string ServeurInjoignable(string quoi) =>
        D("le serveur est injoignable : " + quoi, "the server is unreachable: " + quoi);
    public static string EnvoyeAvecRapport => D(
        "envoyé. Le rapport t'attend dans les notifications de Codex Olympia.",
        "sent. The report is waiting in your Codex Olympia notifications.");
    public static string EnvoyeRienDeNeuf => D(
        "envoyé. Rien de nouveau : l'application savait déjà tout ça.",
        "sent. Nothing new: the app already knew all of it.");
    public static string ReponseIllisible =>
        D("le serveur a répondu quelque chose d'illisible", "the server replied with something unreadable");
    public static string JetonRefuse => D(
        "jeton refusé. Il a peut-être été révoqué : refais-en un dans la page de compte.",
        "token refused. It may have been revoked: make a new one on the account page.");
    public static string PersoNonVerifie => D(
        "le personnage de ce jeton n'est plus vérifié sur ton compte.",
        "this token's character is no longer verified on your account.");
    public static string JetonSansPerso => D(
        "ce jeton date d'avant et ne désigne aucun personnage. Révoque-le et refais-en un.",
        "this token predates the change and names no character. Revoke it and make a new one.");
    public static string TropDEnvois =>
        D("trop d'envois d'affilée. Laisse passer un moment.", "too many sends in a row. Wait a moment.");
    public static string PhotoRefusee(string quoi) =>
        D("photo refusée : " + quoi, "snapshot refused: " + quoi);
    public static string ServeurRepondu(int code) =>
        D($"le serveur a répondu {code}.", $"the server replied {code}.");
    public static string LectureEchouee(string quoi) =>
        D("la lecture du jeu a échoué : " + quoi, "reading the game failed: " + quoi);

    public static string AideCommande =>
        D("Ouvre la fenetre de synchronisation Codex Olympia.", "Opens the Codex Olympia sync window.");

    public static string Soutien => D("Offrir un café", "Buy a coffee");
    public static string SoutienAide => D(
        "Le greffon et l'application sont gratuits, et le resteront.",
        "The plugin and the app are free, and will stay that way.");

    // ---------------------------------------------------------- a ranger

    public static string PageARanger => D("À ranger", "To store");
    public static string ARangerQuoi => D(
        "Ce que tu possèdes sans l'avoir déposé. Une pièce rangée dans un sac, portée sur toi " +
        "ou confiée à un servant ne compte pour rien tant qu'elle n'est pas dans ta coiffeuse " +
        "ou ton armoire : elle peut se vendre, se jeter, se perdre de vue.",
        "What you own without having deposited it. A piece sitting in a bag, worn, or left with " +
        "a retainer counts for nothing until it reaches your glamour dresser or armoire: it can " +
        "be sold, discarded, forgotten.");
    public static string ARangerDAbord => D(
        "Regarde d'abord ce que tu as : cette page compare tes sacs à tes dépôts.",
        "Look at what you have first: this page compares your bags with your deposits.");
    public static string ARangerRien => D(
        "Rien à ranger : tout ce que tu as sous la main est déjà déposé.",
        "Nothing to store: everything you are carrying is already deposited.");
    public static string ARangerCompte(int tenues, int pieces) => D(
        $"{pieces} pièces à déposer, réparties sur {tenues} tenues.",
        $"{pieces} pieces to deposit, across {tenues} outfits.");
    public static string ARangerComplete => D("complète la tenue", "completes the outfit");
    public static string ARangerEtat(int deposees, int total) =>
        D($"{deposees}/{total} déposées", $"{deposees}/{total} deposited");
    public static string ServantsVus(int n) => n == 0
        ? D("Aucun servant consulté : parle-leur une fois pour qu'ils comptent.",
            "No retainer seen yet: talk to them once so they count.")
        : D($"{n} servants connus, d'après la dernière fois que tu leur as parlé.",
            $"{n} retainers known, from the last time you talked to them.");

    public static string OuSac => D("sac", "bag");
    public static string OuArmurerie => D("armurerie", "armoury");
    public static string OuPorte => D("porté", "worn");
    public static string OuCabas => D("cabas", "saddlebag");
    public static string OuServant(string nom) => D($"servant « {nom} »", $"retainer \"{nom}\"");

    public static string AvisPiece(string piece, string tenue) => D(
        $"« {piece} » fait partie de la tenue « {tenue} ». Dépose-la dans ta coiffeuse pour qu'elle compte.",
        $"\"{piece}\" belongs to the \"{tenue}\" outfit. Put it in your glamour dresser so it counts.");
    public static string AvisTitre => D("Prévenir en jeu", "Notify in game");
    public static string AvisExplique => D(
        "Un mot dans le journal quand tu obtiens une pièce de tenue que tu n'as pas encore déposée.",
        "A line in the chat log when you get an outfit piece you have not deposited yet.");

    // ------------------------------------------------------- les collections

    /// <summary>Le nom lisible de chaque collection, dans l'ordre d'affichage.
    ///
    /// Gardée d'un appel sur l'autre : cette table est parcourue à chaque image
    /// du jeu, et la reconstruire soixante fois par seconde pour un texte qui ne
    /// change qu'au changement de langue serait du gâchis pur.</summary>
    public static (string Cle, string Nom)[] Collections => collections ??= Batir();

    private static (string Cle, string Nom)[]? collections;

    private static (string Cle, string Nom)[] Batir() =>
    [
        ("mounts", D("Montures", "Mounts")),
        ("minions", D("Mascottes", "Minions")),
        ("orchestrions", D("Rouleaux d'orchestrion", "Orchestrion rolls")),
        ("emotes", D("Emotes", "Emotes")),
        ("hairstyles", D("Coiffures", "Hairstyles")),
        ("fashions", D("Accessoires de mode", "Fashion accessories")),
        ("facewear", D("Lunettes", "Facewear")),
        ("bardings", D("Bardes", "Bardings")),
        ("cards", D("Cartes de Triple Triade", "Triple Triad cards")),
        ("frames", D("Portraits", "Portrait frames")),
        ("spells", D("Sorts bleus", "Blue magic spells")),
        ("achievements", D("Succès", "Achievements")),
        ("armoires", D("Armoire", "Armoire")),
        ("outfitpieces", D("Pièces de tenue", "Outfit pieces")),
        ("outfits", D("Tenues entières", "Complete outfits")),
    ];
}
