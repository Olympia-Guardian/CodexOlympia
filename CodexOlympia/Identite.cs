using System.Net.Http;
using System.Text.Json;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace CodexOlympia;

/// <summary>
/// Qui est le personnage de ce jeton, du point de vue de l'application
/// (PLG-R40).
///
/// Le plugin ne lit pas le compte : il demande une seule chose, l'identité du
/// personnage que son jeton désigne, pour pouvoir mettre un nom et un visage
/// dans sa fenêtre. Pas une collection.
/// </summary>
public sealed record Identite(
    long CharId,
    string Nom,
    string? Monde,
    string? Avatar,
    string? Portrait,
    string? Decoupe,
    long? Derniere);

/// <summary>
/// Le visage du personnage : l'identité, et l'image une fois arrivée.
///
/// Trois adresses sont essayées dans l'ordre (PLG-R43) : le portrait découpé
/// que l'application pose dans ses cadres, le portrait du Lodestone, puis
/// l'avatar rond. Ce qui n'arrive pas ne laisse pas de trou, on descend d'un
/// cran ; si rien n'arrive, la fenêtre se passe d'image et n'en parle pas.
/// </summary>
public sealed class Visage : IDisposable
{
    private readonly HttpClient http;
    private readonly ITextureProvider textures;
    private readonly IPluginLog journal;

    /// <summary>Le personnage pour lequel tout ceci a été demandé.</summary>
    public ulong Pour { get; private set; }

    public Identite? Qui { get; private set; }

    public IDalamudTextureWrap? Image { get; private set; }

    /// <summary>Une demande est en route : on n'en lance pas une seconde.</summary>
    private bool enCours;

    /// <summary>Le moment (secondes du chrono du plugin) avant lequel on ne
    /// redemande rien. Un serveur injoignable ne doit pas être martelé.</summary>
    private double prochaineTentative;

    public Visage(HttpClient http, ITextureProvider textures, IPluginLog journal)
    {
        this.http = http;
        this.textures = textures;
        this.journal = journal;
    }

    /// <summary>
    /// Repart de zéro : changement de personnage, ou de jeton.
    ///
    /// L'ancienne image n'est pas jetée tout de suite : la fenêtre la dessine
    /// peut-être à cet instant, sur un autre fil. On la met de côté, et tout
    /// part ensemble quand le plugin se décharge. Un joueur change de
    /// personnage quelques fois par session, jamais mille.
    /// </summary>
    public void Oublier()
    {
        if (Image is not null) anciennes.Add(Image);
        Image = null;
        Qui = null;
        Pour = 0;
        prochaineTentative = 0;
    }

    private readonly List<IDalamudTextureWrap> anciennes = [];

    /// <summary>
    /// Demande l'identité si elle manque. Appelé depuis le fil du jeu, à
    /// l'ouverture de la fenêtre : rien ne part tant que personne ne regarde.
    /// </summary>
    public void Assurer(ulong contentId, string jeton, double maintenant)
    {
        if (contentId == 0 || jeton.Length == 0) return;
        if (Pour != contentId) Oublier();
        if (Qui is not null || enCours || maintenant < prochaineTentative) return;

        Pour = contentId;
        enCours = true;
        // Un quart d'heure avant de réessayer : l'identité ne bouge pas, et
        // une panne ne mérite pas une requête par seconde.
        prochaineTentative = maintenant + 900;
        _ = Task.Run(async () =>
        {
            try
            {
                var qui = await Demander(jeton);
                if (qui is null) return;
                if (Pour != contentId) return;
                Qui = qui;
                var image = await PremiereImage(qui);
                if (Pour != contentId)
                {
                    image?.Dispose();
                    return;
                }
                Image = image;
            }
            catch (Exception e)
            {
                journal.Warning(e, "identite du personnage : pas cette fois");
            }
            finally
            {
                enCours = false;
            }
        });
    }

    private async Task<Identite?> Demander(string jeton)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, Site.Serveur + "/plugin/moi");
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + jeton);
        using var rep = await http.SendAsync(req);
        if (!rep.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await rep.Content.ReadAsStringAsync());
        var r = doc.RootElement;
        string? Mot(string cle) =>
            r.TryGetProperty(cle, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        return new Identite(
            r.TryGetProperty("charId", out var c) && c.TryGetInt64(out var id) ? id : 0,
            Mot("name") ?? string.Empty,
            Mot("monde"),
            Mot("avatar"),
            Mot("portrait"),
            Mot("decoupe"),
            r.TryGetProperty("derniere", out var d) && d.TryGetInt64(out var ms) ? ms : null);
    }

    /// <summary>La première des trois adresses qui répond avec une image.</summary>
    private async Task<IDalamudTextureWrap?> PremiereImage(Identite qui)
    {
        foreach (var adresse in new[] { qui.Decoupe, qui.Portrait, qui.Avatar })
        {
            if (string.IsNullOrEmpty(adresse)) continue;
            try
            {
                using var rep = await http.GetAsync(adresse);
                if (!rep.IsSuccessStatusCode) continue;
                var octets = await rep.Content.ReadAsByteArrayAsync();
                if (octets.Length == 0) continue;
                return await textures.CreateFromImageAsync(octets);
            }
            catch (Exception e)
            {
                journal.Debug(e, $"portrait injoignable : {adresse}");
            }
        }
        return null;
    }

    public void Dispose()
    {
        Image?.Dispose();
        foreach (var vieille in anciennes) vieille.Dispose();
        anciennes.Clear();
    }
}
