using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace CodexOlympia;

/// <summary>La réponse du serveur, réduite à ce qui se montre.</summary>
public sealed record Retour(
    bool Ok,
    string Message,
    Dictionary<string, int> Ajouts,
    Dictionary<string, int> Ecarts);

/// <summary>
/// L'envoi de la photo.
///
/// Une seule route, un seul verbe. Le greffon ne sait rien lire du compte : il
/// dépose, et l'application se charge du reste.
/// </summary>
public static class Envoi
{
    public static async Task<Retour> Deposer(
        HttpClient http, Reglages r, string jeton, IReadOnlyList<Releve> releves)
    {
        var collections = new Dictionary<string, List<uint>>();
        var portee = new Dictionary<string, List<uint>>();
        foreach (var x in releves)
        {
            if (x.Empeche is not null) continue;
            // « adeposer » est un conseil pour le joueur, pas une collection :
            // le serveur n'en a jamais entendu parler et n'a pas a en entendre
            // parler.
            if (x.Cle == "adeposer") continue;
            collections[x.Cle] = x.Trouves;
            if (x.Portee is not null) portee[x.Cle] = x.Portee;
        }
        if (collections.Count == 0)
            return new Retour(false, Mots.RienAEnvoyer, [], []);

        // Le personnage n'est pas dit ici : le jeton le porte. Une photo ne
        // choisit pas qui elle alimente.
        var corps = new Dictionary<string, object> { ["collections"] = collections };
        if (portee.Count > 0) corps["portee"] = portee;

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{r.Serveur.TrimEnd('/')}/plugin/photo")
        {
            Content = new StringContent(JsonSerializer.Serialize(corps), Encoding.UTF8, "application/json"),
        };
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + jeton);

        HttpResponseMessage rep;
        try
        {
            rep = await http.SendAsync(req);
        }
        catch (Exception e)
        {
            return new Retour(false, Mots.ServeurInjoignable(e.Message), [], []);
        }

        var texte = await rep.Content.ReadAsStringAsync();
        if (!rep.IsSuccessStatusCode)
            return new Retour(false, Expliquer(rep.StatusCode, texte), [], []);

        try
        {
            using var doc = JsonDocument.Parse(texte);
            var ajouts = Compter(doc.RootElement, "ajouts");
            var ecarts = Compter(doc.RootElement, "ecarts");
            var rapport = doc.RootElement.TryGetProperty("rapport", out var jr)
                          && jr.ValueKind == JsonValueKind.String;
            var message = rapport ? Mots.EnvoyeAvecRapport : Mots.EnvoyeRienDeNeuf;
            return new Retour(true, message, ajouts, ecarts);
        }
        catch
        {
            return new Retour(false, Mots.ReponseIllisible, [], []);
        }
    }

    /// <summary>Un code d'erreur ne dit rien à personne : on le traduit.</summary>
    private static string Expliquer(HttpStatusCode code, string texte) => code switch
    {
        HttpStatusCode.Unauthorized => Mots.JetonRefuse,
        HttpStatusCode.Forbidden => Mots.PersoNonVerifie,
        HttpStatusCode.Conflict => Mots.JetonSansPerso,
        HttpStatusCode.TooManyRequests => Mots.TropDEnvois,
        (HttpStatusCode)422 => Mots.PhotoRefusee(texte),
        _ => Mots.ServeurRepondu((int)code),
    };

    private static Dictionary<string, int> Compter(JsonElement racine, string champ)
    {
        var sortie = new Dictionary<string, int>();
        if (!racine.TryGetProperty(champ, out var o) || o.ValueKind != JsonValueKind.Object) return sortie;
        foreach (var p in o.EnumerateObject())
        {
            sortie[p.Name] = p.Value.ValueKind switch
            {
                JsonValueKind.Number => p.Value.GetInt32(),
                JsonValueKind.Array => p.Value.GetArrayLength(),
                _ => 0,
            };
        }
        return sortie;
    }
}
