using Dalamud.Bindings.ImGui;

namespace CodexOlympia.Ui;

/// <summary>
/// Le mouvement : ce qui empêche une valeur de sauter d'un état à l'autre.
///
/// Rien n'est animé pour le plaisir. Une jauge qui se remplit d'un coup fait
/// douter de ce qu'on vient de lire ; une jauge qui glisse en un tiers de
/// seconde se laisse suivre. L'état vit dans un dictionnaire, par identifiant
/// ImGui, pour que les éléments qui dessinent n'aient rien à retenir.
/// </summary>
internal static class Mouvement
{
    private static readonly Dictionary<int, float> valeurs = new();

    /// <summary>Le temps écoulé depuis l'image précédente, borné : un à-coup
    /// du jeu ne doit pas téléporter une animation.</summary>
    private static float Delta => MathF.Min(ImGui.GetIO().DeltaTime, 0.05f);

    /// <summary>Le joueur a-t-il demandé moins de mouvement ? Dalamud le sait,
    /// on le respecte : tout arrive alors à destination tout de suite.</summary>
    private static bool Calme => Plugin.MoinsDeMouvement;

    public static int Cle(string id) => unchecked((int)ImGui.GetID(id));

    /// <summary>Approche une valeur de sa cible, à vitesse indépendante du
    /// nombre d'images par seconde.</summary>
    public static float Vers(string id, float cible, float vitesse = 12f)
    {
        var cle = Cle(id);
        if (Calme || !valeurs.TryGetValue(cle, out var courant))
        {
            valeurs[cle] = cible;
            return cible;
        }
        var suivant = courant + (cible - courant) * (1f - MathF.Exp(-vitesse * Delta));
        if (MathF.Abs(suivant - cible) < 0.0005f) suivant = cible;
        valeurs[cle] = suivant;
        return suivant;
    }

    /// <summary>Le survol, adouci : de 0 à 1 et retour.</summary>
    public static float Survol(string id, bool dessus) => Vers(id, dessus ? 1f : 0f, 18f);

    /// <summary>Un battement de 0 à 1 et retour, sur l'horloge du monde : ce
    /// qui fait respirer un point d'état.</summary>
    public static float Battement(double periodeMs = 1800)
    {
        if (Calme) return 0.5f;
        var t = (Environment.TickCount % periodeMs) / periodeMs;
        return (float)((Math.Sin(t * Math.PI * 2.0) + 1.0) * 0.5);
    }

    /// <summary>Une dent de scie de 0 à 1 : ce qui fait tourner une comète.</summary>
    public static float Phase(double periodeMs)
    {
        if (Calme) return 0f;
        return (float)((Environment.TickCount % periodeMs) / periodeMs);
    }
}
