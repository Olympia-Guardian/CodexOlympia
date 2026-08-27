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
/// Tous les textes du plugin, en deux langues.
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
    public static string PageSonde => D("Sonde", "Probe");
    public static string SondeQuoi => D(
        "Page temporaire, et elle ne fait QUE lire : rien n’est cliqué, rien n’est déposé, aucun "
        + "prisme n’est consommé. Ouvre ta coiffeuse mirage, clique « Ranger », et montre-moi ce "
        + "tableau : c’est ce qui me permettra de piloter cette fenêtre correctement.",
        "Temporary page, and it only READS: nothing is clicked, nothing is stored, no prism is "
        + "used. Open your glamour dresser, click Store, and show me this table: it is what lets "
        + "me drive that window correctly.");
    public static string SondeRien => D(
        "Aucune de ces fenêtres n’est ouverte. Ouvre ta coiffeuse mirage, puis « Ranger ».",
        "None of those windows is open. Open your glamour dresser, then Store.");

    public static string AllerConfig => D("Aller à la configuration", "Go to settings");
    public static string CataloguePasPret =>
        D("Le catalogue de l'application n'est pas encore chargé.", "The app catalogue is not loaded yet.");
    public static string Reessayer => D("Réessayer", "Try again");

    public static string Regarder => D("Regarder ce que j'ai", "See what I have");
    public static string RienNePart => D("rien n'est envoyé à cette étape", "nothing is sent at this step");
    public static string Presentation => D(
        "Le plugin lit ce que le jeu tient pour débloqué, te le montre, et n'envoie que si tu " +
        "le lui dis. Rien n'est jamais décoché à ta place.",
        "The plugin reads what the game holds as unlocked, shows it to you, and only sends if " +
        "you say so. Nothing is ever unchecked on your behalf.");

    public static string OnRecupere => D("On récupère tes déblocages", "Fetching your unlocks");
    public static string Lecture => D("lecture", "reading");
    public static string EnAttente => D("en attente", "waiting");

    public static string ColCollection => D("Collection", "Collection");
    public static string ColTrouve => D("Trouvé", "Found");
    public static string NonLu => D("non lu", "not read");
    public static string Relire => D("Relire", "Rescan");
    public static string RelireAide => D(
        "Ouvre d'abord la fenêtre demandée en jeu, puis relis cette collection seule. Le reste du relevé ne bouge pas.",
        "Open the required game window first, then rescan just this collection. The rest of the report stays.");

    public static string AuJournal(int n) => n == 1
        ? D("1 autre se coche dans l'application", "1 more is ticked in the app")
        : D($"{n} autres se cochent dans l'application", $"{n} more are ticked in the app");
    public static string VerifiablesAide => D(
        "Le jeu ne sait répondre que pour les entrées liées à un objet de déblocage.\n" +
        "Les autres ne sont ni ajoutées ni signalées manquantes : elles se cochent\n" +
        "à la main dans l'application, comme avant.",
        "The game can only answer for entries tied to an unlock item.\n" +
        "The others are neither added nor reported missing: tick them by hand\n" +
        "in the app, as before.");
    public static string Verification => D("vérification", "double-checking");
    public static string VerificationAttente => D(
        "vérification en cours, l'envoi attend",
        "double-checking, sending will wait");
    public static string AOuvrirTitre => D("À ouvrir en jeu", "Open in game first");
    public static string AOuvrirAide => D(
        "Le jeu ne charge ces collections qu'à l'ouverture de leur fenêtre : le carnet de " +
        "succès, la coiffeuse mirage et l'armoire chez un rassembleur. Ouvre-les, puis Relire.",
        "The game only loads these collections when their window opens: the achievements " +
        "log, the glamour dresser and the armoire at a Calamity Salvager. Open them, then Rescan.");

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
        "Le plugin et l'application sont gratuits, et le resteront.",
        "The plugin and the app are free, and will stay that way.");

    // ------------------------------------------ le rangement automatique

    public static string RangeurTitre => D("Ranger tout seul", "Store automatically");
    public static string RangeurExperimental => D("EXPÉRIMENTAL", "EXPERIMENTAL");
    public static string RangeurAvertissement => D(
        "C'est la seule chose que ce plugin fasse AGIR dans le jeu : tout le reste se contente " +
        "de lire. Chaque dépôt est un ordre envoyé au serveur. Une opération à la fois, à " +
        "cadence humaine, et tout s'arrête au premier imprévu. Reste devant ta coiffeuse ou ton " +
        "armoire, ouverte, pendant que ça travaille.\n\n" +
        "La conversion en mirage RETIRE les matérias serties, les teintures, les mirages, les " +
        "blasons et certains bonus, et remet la symbiose à zéro. C'est le jeu qui le fait, pas " +
        "le plugin, mais tu dois le savoir avant de lancer.",
        "This is the only thing in this plugin that ACTS on the game: everything else only " +
        "reads. Each deposit is an order sent to the server. One operation at a time, at human " +
        "pace, and everything stops at the first surprise. Stay at your glamour dresser or " +
        "armoire, open, while it works.");
    public static string RangeurRien => D("Rien à ranger d'ici.", "Nothing to store from here.");
    public static string RangeurAMain => D("tu as appuyé sur Arrêter.", "you pressed Stop.");
    public static string RangeurLancer => D("Ranger", "Store");
    public static string RangeurRienAFaire => D(
        "Rien à ranger d’ici. Le rangement ne prend que ce qui est SOUS LA MAIN : tes sacs et ton "
        + "arsenal. Ce qui dort chez un servant ne compte pas, va le chercher d’abord.",
        "Nothing to store from here. Storing only takes what is AT HAND: your bags and your "
        + "armoury. What sits with a retainer does not count: fetch it first.");
    public static string RangeurPartiel => D(
        "Une tenue se dépose même incomplète : elle occupe un emplacement, qu’on la remplisse en "
        + "une fois ou en cinq. Ce qui manque s’y ajoutera plus tard.",
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
            bouts.Add(D($"{armoire} objets pour l’armoire", $"{armoire} items for the armoire"));
        return string.Join(D(", ", ", "), bouts) + ".";
    }
    public static string RangeurAussiArmoire => D(
        "Ranger aussi dans l’armoire ce qu’aucune tenue ne prendra. Sans cette case, seules les "
        + "tenues sont servies : un objet rangé à l’armoire quitte l’inventaire, et la coiffeuse ne "
        + "l’aura plus.",
        "Also store in the armoire whatever no outfit will take. Without this box, only outfits "
        + "are served: an item stored in the armoire leaves your inventory, and the dresser will "
        + "not get it.");
    public static string RangeurStop => D("Arrêter", "Stop");
    public static string RangeurAvance(int fait, int total, string quoi) =>
        D($"{fait}/{total} · {quoi}", $"{fait}/{total} · {quoi}");
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
    public static string RangeurRefus(string quoi) =>
        D($"le jeu a refusé « {quoi} ».", $"the game refused \"{quoi}\".");
    public static string RangeurSelectionVide => D(
        "la confirmation annonçait zéro prisme : aucune pièce n’était sélectionnée. On a répondu "
        + "Non plutôt que de confirmer une conversion vide.",
        "the confirmation said zero prisms: no piece was selected. We answered No rather than "
        + "confirm an empty conversion.");
    public static string RangeurDoublon(string nom) => D(
        $"le jeu allait créer un second ensemble « {nom} ». On a répondu Non : pas de doublon.",
        $"the game was about to create a second {nom} set. We answered No: no duplicates.");
    public static string RangeurRangerFermee => D(
        "la fenêtre « Ranger » de la coiffeuse ne s’ouvre pas.",
        "the dresser’s Store window will not open.");
    public static string RangeurSansCatalogue => D("le catalogue a disparu.", "the catalogue is gone.");
    public static string RangeurSansPrisme => D(
        "il te manque des prismes de mirage. Chaque pièce déposée en consomme un.",
        "you are out of glamour prisms. Each piece deposited uses one.");
    public static string RangeurPrismes(int reste, int besoin) => reste >= besoin
        ? D($"{reste} prismes de mirage en réserve, {besoin} nécessaires.",
            $"{reste} glamour prisms in stock, {besoin} needed.")
        : D($"{reste} prismes de mirage seulement, il en faudrait {besoin} : ça s’arrêtera en route.",
            $"only {reste} glamour prisms, {besoin} needed: it will stop partway.");
    public static string RangeurErreur => D("quelque chose a mal tourné.", "something went wrong.");
    public static string RangeurPasDeQuestion => D(
        "le jeu n’a pas posé la question attendue. Regarde ton écran : une fenêtre attend "
        + "peut-être une réponse que je ne sais pas donner.",
        "the game did not ask the expected question. Look at your screen: a window may be "
        + "waiting for an answer I do not know how to give.");
    public static string RangeurCadence => D(
        "secondes entre deux gestes",
        "seconds between moves");
    public static string RangeurSansEffet => D(
        "le jeu a dit oui sans rien faire.",
        "the game said yes and did nothing.");

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
    public static string ARangerCompte(int pieces, int achevent) => achevent == 0
        ? D($"{pieces} pièces à déposer.", $"{pieces} pieces to deposit.")
        : D($"{pieces} pièces à déposer, dont {achevent} qui achèvent une tenue.",
            $"{pieces} pieces to deposit, {achevent} of which complete an outfit.");
    public static string ARangerAchevent =>
        D("Celles-ci achèvent une tenue", "These complete an outfit");
    public static string ARangerReste => D("Le reste", "The rest");
    public static string DoublesTitre(int n) => D(
        $"{n} pièces en double dans tes sacs", $"{n} duplicate pieces in your bags");
    public static string DoublesAide => D(
        "Elles sont déjà dans ta coiffeuse : celles-ci ne servent plus à rien. Vends-les au "
        + "PNJ ou à ta grande compagnie. Le plugin n’y touche pas.",
        "They are already in your glamour dresser: these are useless now. Sell them to a vendor "
        + "or your Grand Company. The plugin never touches them.");
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

    // ---------------------------------------------- la synchro automatique
    public static string SyncAutoTitre => D("Synchronisation automatique", "Automatic sync");
    public static string SyncAutoExplique => D(
        "Le plugin regarde tout seul à la connexion, au changement de zone et toutes les cinq " +
        "minutes, et envoie dès qu'il y a du neuf depuis le dernier envoi. Jamais en combat, en " +
        "instance ou en cinématique, jamais deux fois en moins d'une minute. Rien n'est jamais " +
        "retiré : c'est la même photo que le bouton, sans le bouton.",
        "The plugin looks on its own at login, on zone change and every five minutes, and sends " +
        "as soon as there is something new since the last send. Never in combat, in a duty or " +
        "in a cutscene, never twice within a minute. Nothing is ever removed: it is the same " +
        "snapshot as the button, without the button.");
    public static string SyncAutoEtat(double dans) => dans < 0
        ? D("Synchro automatique : en attente de la connexion", "Auto sync: waiting for login")
        : dans < 1
            ? D("Synchro automatique : lecture imminente", "Auto sync: about to read")
            : D($"Synchro automatique : prochaine lecture dans {dans:F0} s", $"Auto sync: next read in {dans:F0} s");
    public static string NouveautesTitre(int n) => D(
        $"{n} nouveauté(s) depuis le dernier envoi",
        $"{n} new since the last send");
    public static string NouveautesJamais => D(
        "Rien n'a encore été envoyé depuis ce plugin pour ce personnage : tout ce qui a été lu est à envoyer.",
        "Nothing has been sent from this plugin for this character yet: everything read is pending.");
    public static string EtAutres(int n) => D($"et {n} autre(s)", $"and {n} more");
    public static string RienDeNeuf => D("Rien de neuf depuis le dernier envoi.", "Nothing new since the last send.");

    // --------------------------------------------------------------- le pied
    public static string Discord => "Discord";
    public static string Bugs => D("Signaler un bug", "Report a bug");
    public static string PastillesExplique => D(
        "Une pastille sur les objets de ton sac qu'il reste à déposer.",
        "A dot on the items in your bag that are still waiting to be deposited.");
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
