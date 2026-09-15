using System.Net.Http;
using System.Text.Json;

namespace CodexOlympia;

/// <summary>Une façon d'obtenir une entrée, telle que l'application la publie.
/// <c>Genre</c> est la famille (succès, donjon, boutique...), en anglais, parce
/// que c'est une clé et non une phrase ; <c>Fr</c> et <c>En</c> sont la phrase.</summary>
public sealed record Source(string Genre, string Fr, string En)
{
    public string Phrase
    {
        get
        {
            var t = Mots.Fr ? Fr : En;
            if (t.Length == 0) t = Mots.Fr ? En : Fr;
            return t;
        }
    }
}

/// <summary>
/// Ce que le jeu ne sait pas dire d'une entrée (PLG-R47).
///
/// Le client connaît le nom, l'icône et si c'est débloqué ; il ne connaît ni le
/// patch d'arrivée, ni le fait qu'une entrée ne s'obtienne plus, ni une phrase
/// qui dise comment l'avoir. Ces trois-là viennent de l'application, et leur
/// absence n'empêche rien : la fiche s'ouvre sans elles.
/// </summary>
public sealed record Detail(
    string Patch,
    bool Inobtenable,
    string NoticeFr,
    string NoticeEn,
    IReadOnlyList<Source> Sources)
{
    /// <summary>La notice de l'objet, dans la langue de la fenêtre.</summary>
    public string Notice
    {
        get
        {
            var t = Mots.Fr ? NoticeFr : NoticeEn;
            return t.Length > 0 ? t : (Mots.Fr ? NoticeEn : NoticeFr);
        }
    }

    /// <summary>La boutique en ligne, que le jeu ne distingue pas d'un déblocage
    /// ordinaire une fois l'objet reçu.</summary>
    public bool Boutique => Sources.Any(x => x.Genre == "Premium");
}

/// <summary>Où en est un événement, à l'horloge du joueur.</summary>
public enum Vie
{
    AVenir,
    EnCours,
    /// <summary>Fini, mais l'échange de ses récompenses reste ouvert jusqu'à un
    /// patch qui n'est pas encore paru.</summary>
    Echange,
    Termine,
}

/// <summary>
/// Un événement du jeu tel que l'application le publie : ses dates, et ce
/// qu'il donne.
///
/// Le catalogue marque « plus obtenable » ce qui vient d'un événement passé,
/// et il a raison la plupart du temps. Mais une fête revient, et une
/// collection Mog Mog offre pendant six semaines une monture que le catalogue
/// dit perdue. L'événement en cours l'emporte sur la marque.
/// </summary>
public sealed record Evenement(
    string Slug,
    string NomFr,
    string NomEn,
    string Debut,
    string Fin,
    string FinPatch,
    string EchangeJusqua,
    int? Annee)
{
    public string Nom => Mots.Fr ? (NomFr.Length > 0 ? NomFr : NomEn) : (NomEn.Length > 0 ? NomEn : NomFr);

    /// <summary>Le statut, calculé comme le site le calcule (EVT-R7) : à l'horloge,
    /// et aux patchs parus quand une fin ne tient pas à une date.</summary>
    public Vie Statut(DateTime maintenant, Func<string, bool> patchParu)
    {
        if (Debut.Length == 0)
            return Annee is { } a && a >= maintenant.Year ? Vie.AVenir : Vie.Termine;
        if (!DateTime.TryParse(Debut, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var debut))
            return Vie.Termine;
        if (maintenant < debut) return Vie.AVenir;
        if (Fin.Length > 0)
        {
            if (DateTime.TryParse(Fin, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var fin)
                && maintenant <= fin)
                return Vie.EnCours;
        }
        else if (FinPatch.Length > 0 && !patchParu(FinPatch))
        {
            return Vie.EnCours;
        }
        if (EchangeJusqua.Length > 0 && !patchParu(EchangeJusqua)) return Vie.Echange;
        return Vie.Termine;
    }
}

/// <summary>Une pièce de tenue : son objet, sa case d'armoire s'il y en a une,
/// et son nom dans les deux langues.</summary>
public sealed record Piece(uint Objet, uint Armoire, string Fr, string En)
{
    public string Nom => Mots.Fr ? Fr : En;
}

/// <summary>Une tenue et les pièces qui la composent.</summary>
public sealed record Tenue(uint Id, string Fr, string En, IReadOnlyList<Piece> Pieces)
{
    public string Nom => Mots.Fr ? Fr : En;
}

/// <summary>
/// Le catalogue de l'application, tel qu'elle le publie.
///
/// Le plugin ne tient aucune liste à lui. Il demande à l'application ce qu'elle
/// connaît, puis interroge le jeu sur chacune de ces entrées. Un objet ajouté au
/// catalogue est donc pris en compte sans qu'on retouche au plugin, et un
/// identifiant que l'application ignore n'est jamais envoyé.
/// </summary>
public sealed class Catalogue
{
    /// <summary>Les collections que le plugin sait lire, et leur fichier.</summary>
    public static readonly string[] Cles =
    [
        "mounts", "minions", "orchestrions", "emotes", "fashions", "facewear",
        "hairstyles", "bardings", "cards", "frames", "spells", "beastmaster", "achievements",
        "quests", "armoires", "outfits",
    ];

    /// <summary>Les identifiants du catalogue, par collection, dans son ordre.</summary>
    public Dictionary<string, uint[]> Ids { get; } = new();

    /// <summary>Les variantes d'une entrée, par collection : une quête de
    /// départ existe en plusieurs exemplaires selon la ville ou la classe, le
    /// catalogue n'en garde qu'une, et elle est faite dès que l'une l'est.</summary>
    public Dictionary<string, Dictionary<uint, uint[]>> Variantes { get; } = new();

    /// <summary>Les mandats, parmi les quêtes : le jeu les tient à part et ne
    /// répond pas à la même question pour eux.</summary>
    public HashSet<uint> Mandats { get; } = [];

    /// <summary>L'objet qui déverrouille, quand l'entrée en a un.</summary>
    public Dictionary<string, Dictionary<uint, uint>> Objets { get; } = new();

    /// <summary>Le patch, l'inobtenable et les sources, pour les collections que
    /// la galerie montre. Les autres n'en gardent pas : un succès porte sa phrase
    /// comme les autres, et il y en a vingt-quatre mille.</summary>
    public Dictionary<string, Dictionary<uint, Detail>> Details { get; } = new();

    /// <summary>Les collections dont on garde le détail en mémoire.</summary>
    private static readonly HashSet<string> Detaillees =
    [
        "mounts", "minions", "orchestrions", "emotes", "fashions", "bardings", "cards",
        "facewear", "spells", "beastmaster", "outfits", "armoires", "hairstyles", "frames",
    ];

    /// <summary>Ce que l'application sait d'une entrée, ou rien.</summary>
    public Detail? Detail(string cle, uint id) =>
        Details.TryGetValue(cle, out var d) && d.TryGetValue(id, out var v) ? v : null;

    /// <summary>Les événements que l'application connaît, et ce que chacun donne.</summary>
    public List<Evenement> Evenements { get; } = [];

    private readonly Dictionary<(string Cle, uint Id), List<Evenement>> donnePar = new();

    /// <summary>Les patchs que le catalogue connaît : un patch y figure dès qu'une
    /// entrée le porte, et c'est ainsi que le site juge qu'un patch est paru.</summary>
    private readonly HashSet<string> patchs = [];

    private bool PatchParu(string patch)
    {
        if (!double.TryParse(patch, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var cible))
            return false;
        foreach (var p in patchs)
            if (double.TryParse(p, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) && v >= cible)
                return true;
        return false;
    }

    /// <summary>
    /// L'événement vivant qui donne cette entrée, ou rien : en cours d'abord, puis
    /// l'échange encore ouvert, puis celui qui vient. Un événement terminé ne
    /// compte pas, c'est justement lui que la marque « plus obtenable » vise.
    /// </summary>
    public (Evenement Ev, Vie Vie)? EvenementVivant(string cle, uint id)
    {
        if (!donnePar.TryGetValue((cle, id), out var liste)) return null;
        var maintenant = DateTime.UtcNow;
        (Evenement, Vie)? meilleur = null;
        foreach (var ev in liste)
        {
            var vie = ev.Statut(maintenant, PatchParu);
            if (vie == Vie.Termine) continue;
            if (meilleur is null || Rang(vie) < Rang(meilleur.Value.Item2)) meilleur = (ev, vie);
        }
        return meilleur;

        static int Rang(Vie v) => v switch { Vie.EnCours => 0, Vie.Echange => 1, _ => 2 };
    }

    /// <summary>Vrai quand l'entrée ne s'obtient plus pour de bon : la marque du
    /// catalogue, moins ce qu'un événement vivant redonne.</summary>
    public bool Inobtenable(string cle, uint id) =>
        Detail(cle, id)?.Inobtenable == true && EvenementVivant(cle, id) is null;

    /// <summary>Le nom de chaque entrée, dans les deux langues, par collection :
    /// pour dire « Colibri callado » plutôt que « monture 435 » quand on liste
    /// ce qui attend d'être envoyé.</summary>
    public Dictionary<string, Dictionary<uint, (string Fr, string En)>> Noms { get; } = new();

    /// <summary>Le nom d'une entrée dans la langue de la fenêtre, ou son numéro
    /// si le catalogue ne le connaît pas.</summary>
    public string Nom(string cle, uint id)
    {
        if (Noms.TryGetValue(cle, out var noms) && noms.TryGetValue(id, out var n))
        {
            var nom = Mots.Fr ? n.Fr : n.En;
            if (nom.Length == 0) nom = Mots.Fr ? n.En : n.Fr;
            if (nom.Length > 0) return nom;
        }
        return $"#{id}";
    }

    /// <summary>Le nom d'une entrée dans l'autre langue, ou rien quand c'est le
    /// même mot : le tiroir de l'application le montre sous le nom.</summary>
    public string AutreNom(string cle, uint id)
    {
        if (!Noms.TryGetValue(cle, out var noms) || !noms.TryGetValue(id, out var n)) return string.Empty;
        return Mots.Fr ? n.En : n.Fr;
    }

    /// <summary>Les tenues et leurs pièces.</summary>
    public List<Tenue> Tenues { get; } = [];

    public string Date { get; private set; } = "";

    public bool Pret => Ids.Count == Cles.Length;

    /// <summary>
    /// Va chercher le catalogue, et le garde sur le disque. Le réseau peut
    /// manquer : dans ce cas on se sert de ce qu'on a déjà, plutôt que de refuser
    /// de fonctionner.
    /// </summary>
    public static async Task<Catalogue> Charger(HttpClient http, string racine, string cache)
    {
        var cat = new Catalogue();
        Directory.CreateDirectory(cache);

        // La date du catalogue distant décide si ce qu'on a en cache est périmé.
        var distant = "";
        try
        {
            using var doc = JsonDocument.Parse(await http.GetStringAsync($"{racine}/meta.json"));
            distant = doc.RootElement.GetProperty("updatedAt").GetString() ?? "";
        }
        catch
        {
            // Tant pis : on se contentera du cache.
        }

        var marque = Path.Combine(cache, "date.txt");
        var local = File.Exists(marque) ? File.ReadAllText(marque) : "";
        var perime = distant.Length > 0 && distant != local;

        foreach (var cle in Cles)
        {
            var fichier = Path.Combine(cache, cle + ".json");
            string? texte = null;
            if (!perime && File.Exists(fichier))
            {
                texte = await File.ReadAllTextAsync(fichier);
            }
            else
            {
                try
                {
                    texte = await http.GetStringAsync($"{racine}/{cle}.json");
                    await File.WriteAllTextAsync(fichier, texte);
                }
                catch
                {
                    // Réseau absent, ou fichier pas encore publié : ce qu'on a
                    // en cache, sinon rien pour cette collection.
                    if (File.Exists(fichier)) texte = await File.ReadAllTextAsync(fichier);
                }
            }

            if (texte is null)
            {
                // Une collection sans catalogue est une collection vide : on
                // ne lit rien pour elle et on n'envoie rien, mais les autres
                // continuent. Elle ne doit pas bloquer le plugin entier.
                cat.Ids[cle] = [];
                cat.Objets[cle] = new();
                cat.Noms[cle] = new();
                cat.Variantes[cle] = new();
                continue;
            }
            cat.Lire(cle, texte);
        }

        // Les événements, à part : leur absence n'empêche rien, la galerie s'en
        // tient alors à la marque du catalogue.
        {
            var fichier = Path.Combine(cache, "events.json");
            string? texte = null;
            if (!perime && File.Exists(fichier))
            {
                texte = await File.ReadAllTextAsync(fichier);
            }
            else
            {
                try
                {
                    texte = await http.GetStringAsync($"{racine}/events.json");
                    await File.WriteAllTextAsync(fichier, texte);
                }
                catch
                {
                    if (File.Exists(fichier)) texte = await File.ReadAllTextAsync(fichier);
                }
            }
            if (texte is not null)
            {
                try
                {
                    cat.LireEvenements(texte);
                }
                catch
                {
                    // Un fichier qui a changé de forme ne fait pas tomber le catalogue.
                }
            }
        }

        if (perime && cat.Pret) File.WriteAllText(marque, distant);
        cat.Date = distant.Length > 0 ? distant : local;
        return cat;
    }

    /// <summary>Un champ texte, vide plutôt qu'absent : un nom manquant ne doit
    /// pas faire tomber la lecture du catalogue entier.</summary>
    private static string Texte(JsonElement e, string champ) =>
        e.TryGetProperty(champ, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? string.Empty
            : string.Empty;

    private void Lire(string cle, string texte)
    {
        using var doc = JsonDocument.Parse(texte);
        var liste = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement
            : doc.RootElement.EnumerateObject().First().Value;

        var ids = new List<uint>();
        var objets = new Dictionary<uint, uint>();
        var noms = new Dictionary<uint, (string Fr, string En)>();
        var variantes = new Dictionary<uint, uint[]>();
        var details = Detaillees.Contains(cle) ? new Dictionary<uint, Detail>() : null;
        foreach (var e in liste.EnumerateArray())
        {
            if (!e.TryGetProperty("id", out var ji) || ji.ValueKind != JsonValueKind.Number) continue;
            var id = ji.GetUInt32();
            ids.Add(id);
            if (e.TryGetProperty("itemId", out var jo) && jo.ValueKind == JsonValueKind.Number)
                objets[id] = jo.GetUInt32();
            noms[id] = (Texte(e, "name"), Texte(e, "nameEn"));
            if (e.TryGetProperty("ids", out var jv) && jv.ValueKind == JsonValueKind.Array)
            {
                var v = new List<uint>();
                foreach (var x in jv.EnumerateArray())
                    if (x.ValueKind == JsonValueKind.Number) v.Add(x.GetUInt32());
                if (v.Count > 1) variantes[id] = [.. v];
            }
            if (e.TryGetProperty("leve", out var jl) && jl.ValueKind == JsonValueKind.True) Mandats.Add(id);

            if (details is not null)
            {
                var d = LireDetail(e);
                details[id] = d;
                if (d.Patch.Length > 0) patchs.Add(d.Patch);
            }

            if (cle != "outfits") continue;
            if (!e.TryGetProperty("pieces", out var jp) || jp.ValueKind != JsonValueKind.Array) continue;
            var pieces = new List<Piece>();
            foreach (var p in jp.EnumerateArray())
            {
                if (!p.TryGetProperty("id", out var pi) || pi.ValueKind != JsonValueKind.Number) continue;
                var arm = p.TryGetProperty("armoireId", out var pa) && pa.ValueKind == JsonValueKind.Number
                    ? pa.GetUInt32()
                    : 0u;
                pieces.Add(new Piece(pi.GetUInt32(), arm, Texte(p, "name"), Texte(p, "nameEn")));
            }
            if (pieces.Count > 0)
                Tenues.Add(new Tenue(id, Texte(e, "name"), Texte(e, "nameEn"), pieces));
        }

        Ids[cle] = [.. ids];
        Objets[cle] = objets;
        if (details is not null) Details[cle] = details;
        Noms[cle] = noms;
        Variantes[cle] = variantes;
        // Les pièces n'ont pas de fichier à elles : elles vivent dans les tenues.
        if (cle == "outfits")
        {
            var pieces = new Dictionary<uint, (string Fr, string En)>();
            foreach (var t in Tenues)
                foreach (var p in t.Pieces)
                    pieces[p.Objet] = (p.Fr, p.En);
            Noms["outfitpieces"] = pieces;
        }
    }

    private void LireEvenements(string texte)
    {
        using var doc = JsonDocument.Parse(texte);
        var liste = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement
            : doc.RootElement.EnumerateObject().First().Value;
        foreach (var e in liste.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Object) continue;
            int? annee = e.TryGetProperty("annee", out var ja) && ja.ValueKind == JsonValueKind.Number
                ? ja.GetInt32()
                : null;
            var ev = new Evenement(
                Texte(e, "slug"), Texte(e, "nomFr"), Texte(e, "nomEn"),
                Texte(e, "debut"), Texte(e, "fin"), Texte(e, "finPatch"), Texte(e, "echangeJusqua"), annee);
            Evenements.Add(ev);

            if (e.TryGetProperty("vedette", out var jv) && jv.ValueKind == JsonValueKind.Object) Donne(ev, jv);
            if (e.TryGetProperty("recompenses", out var jr) && jr.ValueKind == JsonValueKind.Array)
                foreach (var r in jr.EnumerateArray())
                    if (r.ValueKind == JsonValueKind.Object) Donne(ev, r);
        }
    }

    private void Donne(Evenement ev, JsonElement recompense)
    {
        var cle = Texte(recompense, "kind");
        if (cle.Length == 0) return;
        if (!recompense.TryGetProperty("id", out var ji) || ji.ValueKind != JsonValueKind.Number) return;
        var id = ji.GetUInt32();
        if (!donnePar.TryGetValue((cle, id), out var liste)) donnePar[(cle, id)] = liste = [];
        liste.Add(ev);
    }

    private static Detail LireDetail(JsonElement e)
    {
        var sources = new List<Source>();
        if (e.TryGetProperty("sources", out var js) && js.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in js.EnumerateArray())
            {
                if (x.ValueKind != JsonValueKind.Object) continue;
                var genre = Texte(x, "type");
                var fr = Texte(x, "text");
                var en = Texte(x, "textEn");
                if (fr.Length == 0 && en.Length == 0 && genre.Length == 0) continue;
                sources.Add(new Source(genre, fr, en));
            }
        }
        var inobtenable = e.TryGetProperty("unobtainable", out var ju)
            && ju.ValueKind == JsonValueKind.True;
        return new Detail(
            Texte(e, "patch"), inobtenable,
            Texte(e, "description"), Texte(e, "descriptionEn"), sources);
    }
}
