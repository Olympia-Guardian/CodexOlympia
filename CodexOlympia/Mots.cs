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
    public static string BestiaireNonCharge => D(
        "le jeu n'a pas chargé le bestiaire : connecte un personnage, puis regarde à nouveau",
        "the game has not loaded the bestiary: log in a character, then look again");
    public static string BestiaireFormeInattendue => D(
        "le bestiaire du jeu n'a pas la forme attendue, sans doute depuis un patch : rien n'est envoyé. " +
        "/codex bestiaire écrit le détail dans le journal Dalamud.",
        "the game's bestiary does not have the expected shape, probably since a patch: nothing is sent. " +
        "/codex bestiaire writes the details to the Dalamud log.");
    public static string BestiaireLu(int trouves, int total, string noms) => D(
        $"bestiaire : {trouves} bêtes lues sur {total}" + (noms.Length > 0 ? $" : {noms}" : "") +
        ". Le détail est dans le journal Dalamud (/xllog).",
        $"bestiary: {trouves} beasts read out of {total}" + (noms.Length > 0 ? $": {noms}" : "") +
        ". Details are in the Dalamud log (/xllog).");
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
    public static string NouveautesTitre(int n) => D(
        $"{n} nouveauté(s) depuis le dernier envoi",
        $"{n} new since the last send");
    public static string NouveautesJamais => D(
        "Rien n'a encore été envoyé depuis ce plugin pour ce personnage : tout ce qui a été lu est à envoyer.",
        "Nothing has been sent from this plugin for this character yet: everything read is pending.");
    public static string EtAutres(int n) => D($"et {n} autre(s)", $"and {n} more");
    public static string RienDeNeuf => D("Rien de neuf depuis le dernier envoi.", "Nothing new since the last send.");

    // ------------------------------------------------- la fenêtre habillée
    // (PLG-R41 à R43 : l'état en deux mots, les étapes du geste, les compteurs.)

    public static string PageAPropos => D("À propos", "About");
    public static string Fermer => D("Fermer", "Close");

    public static string EtatJeton => D("Jeton manquant", "No token");
    public static string EtatPerso => D("Pas de personnage", "No character");
    public static string EtatLecture => D("Lecture en cours", "Reading");
    public static string EtatEnvoi => D("Envoi", "Sending");
    public static string EtatPret => D("Prêt à envoyer", "Ready to send");
    public static string EtatAJour => D("À jour", "Up to date");
    public static string EtatARegarder => D("À regarder", "Not read yet");

    public static string EtapeRegarder => D("Regarder", "Read");
    public static string EtapeVerifier => D("Vérifier", "Check");
    public static string EtapeEnvoyer => D("Envoyer", "Send");

    public static string MotCollections => D("Collections", "Collections");
    public static string MotNouveautes => D("Nouveautés", "New");
    public static string MotDernierEnvoi => D("Dernier envoi", "Last send");
    public static string MotSyncAuto => D("Synchro auto", "Auto sync");
    public static string MotNonLues => D("Non lues", "Not read");

    public static string ToutesLues => D("toutes lues", "all read");
    public static string ResteN(int n) => n == 1 ? D("1 en attente", "1 waiting") : D($"{n} en attente", $"{n} waiting");
    public static string DepuisEnvoi => D("depuis l'envoi", "since the send");
    public static string ApresLecture => D("après la lecture", "after the read");
    public static string Jamais => D("jamais", "never");
    public static string SyncEteinte => D("éteinte", "off");
    public static string SyncAllumee => D("allumée", "on");
    public static string FenetreAOuvrir => D("fenêtre à ouvrir", "window to open");
    public static string LuesSeules => D("Lues toutes seules", "Read on their own");
    public static string PasEncoreLues(int n) => n == 1
        ? D("1 entrée que le jeu n'a pas encore chargée : on relit dans un instant.",
            "1 entry the game has not loaded yet: we will read again in a moment.")
        : D($"{n} entrées que le jeu n'a pas encore chargées : on relit dans un instant.",
            $"{n} entries the game has not loaded yet: we will read again in a moment.");
    public static string CeQuiAttend => D("Ce qui attend", "What is waiting");
    public static string NCollections(int n) => D($"{n} collection(s)", $"{n} collection(s)");
    public static string RegarderCourt => D("Regarder", "Read");
    public static string EnvoyerN(int n) => D($"Envoyer {n} nouveauté(s)", $"Send {n} new");

    /// <summary>Un moment passé, dit en gros : « il y a 2 h ».</summary>
    public static string IlYA(long? quand)
    {
        if (quand is null or <= 0) return Jamais;
        var ecart = TimeSpan.FromMilliseconds(Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - quand.Value));
        if (ecart.TotalMinutes < 1) return D("à l'instant", "just now");
        if (ecart.TotalHours < 1) return D($"il y a {ecart.TotalMinutes:F0} min", $"{ecart.TotalMinutes:F0} min ago");
        if (ecart.TotalDays < 1) return D($"il y a {ecart.TotalHours:F0} h", $"{ecart.TotalHours:F0} h ago");
        return ecart.TotalDays < 30
            ? D($"il y a {ecart.TotalDays:F0} j", $"{ecart.TotalDays:F0} d ago")
            : D("il y a longtemps", "a long time ago");
    }

    /// <summary>Une attente courte, dite en gros : « dans 4 min ».</summary>
    public static string Dans(double secondes)
    {
        if (secondes < 0) return D("à la connexion", "at login");
        if (secondes < 60) return D($"dans {secondes:F0} s", $"in {secondes:F0} s");
        return D($"dans {secondes / 60:F0} min", $"in {secondes / 60:F0} min");
    }

    // --------------------------------------------------------------- le pied
    public static string Discord => "Discord";
    public static string Bugs => D("Signaler un bug", "Report a bug");
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
        ("beastmaster", D("Bestiaire du dresseur", "Beastmaster bestiary")),
        ("achievements", D("Succès", "Achievements")),
        ("quests", D("Quêtes", "Quests")),
        ("armoires", D("Armoire", "Armoire")),
        ("outfitpieces", D("Pièces de tenue", "Outfit pieces")),
        ("outfits", D("Tenues entières", "Complete outfits")),
    ];
}
