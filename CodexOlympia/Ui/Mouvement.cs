using Dalamud.Bindings.ImGui;

namespace CodexOlympia.Ui;

/// <summary>
/// Ce qui empêche une valeur de sauter d'un état à l'autre.
///
/// L'état vit dans un dictionnaire, par identifiant ImGui, pour que les
/// éléments qui dessinent n'aient rien à retenir entre deux images.
/// </summary>
internal static class Mouvement
{
    private static readonly Dictionary<int, float> valeurs = new();

    /// <summary>Borné : un à-coup du jeu ne doit pas téléporter une
    /// animation.</summary>
    private static float Delta => MathF.Min(ImGui.GetIO().DeltaTime, 0.05f);

    /// <summary>Le réglage d'accessibilité de Dalamud : tout arrive alors à
    /// destination tout de suite.</summary>
    private static bool Calme => Plugin.MoinsDeMouvement;

    public static int Cle(string id) => unchecked((int)ImGui.GetID(id));

    /// <summary>Approche une cible, à vitesse indépendante du nombre d'images
    /// par seconde.</summary>
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

    public static float Survol(string id, bool dessus) => Vers(id, dessus ? 1f : 0f, 18f);

    /// <summary>Un battement de 0 à 1 et retour, sur l'horloge du monde.</summary>
    public static float Battement(double periodeMs = 1800)
    {
        if (Calme) return 0.5f;
        var t = (Environment.TickCount % periodeMs) / periodeMs;
        return (float)((Math.Sin(t * Math.PI * 2.0) + 1.0) * 0.5);
    }

    /// <summary>Une dent de scie de 0 à 1.</summary>
    public static float Phase(double periodeMs)
    {
        if (Calme) return 0f;
        return (float)((Environment.TickCount % periodeMs) / periodeMs);
    }
}
