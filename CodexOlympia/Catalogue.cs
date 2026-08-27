using System.Net.Http;
using System.Text.Json;

namespace CodexOlympia;

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
        "hairstyles", "bardings", "cards", "frames", "spells", "achievements",
        "armoires", "outfits",
    ];

    /// <summary>Les identifiants du catalogue, par collection, dans son ordre.</summary>
    public Dictionary<string, uint[]> Ids { get; } = new();

    /// <summary>L'objet qui déverrouille, quand l'entrée en a un.</summary>
    public Dictionary<string, Dictionary<uint, uint>> Objets { get; } = new();

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
                catch when (File.Exists(fichier))
                {
                    texte = await File.ReadAllTextAsync(fichier);
                }
            }

            if (texte is null) continue;
            cat.Lire(cle, texte);
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
        foreach (var e in liste.EnumerateArray())
        {
            if (!e.TryGetProperty("id", out var ji) || ji.ValueKind != JsonValueKind.Number) continue;
            var id = ji.GetUInt32();
            ids.Add(id);
            if (e.TryGetProperty("itemId", out var jo) && jo.ValueKind == JsonValueKind.Number)
                objets[id] = jo.GetUInt32();
            noms[id] = (Texte(e, "name"), Texte(e, "nameEn"));

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
        Noms[cle] = noms;
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
}
