namespace CodexOlympia;

/// <summary>Une pièce qu'on possède sans l'avoir déposée.</summary>
public sealed record Egaree(uint Objet, string Nom, string Ou, string Tenue, bool Acheve);

/// <summary>
/// Ce qu'il reste à ranger.
///
/// Le rapprochement tient en une phrase : parmi les pièces qu'une tenue attend,
/// certaines ne sont dans aucun dépôt, et on les a pourtant sous la main.
///
/// La liste est rendue à plat, pièce par pièce. Le regroupement se fait à
/// l'affichage, et il se fait par ENDROIT : on range en allant quelque part,
/// pas en pensant à une tenue. Une liste triée par tenue obligeait à faire le
/// tri soi-même pour savoir quoi prendre chez quel servant.
///
/// Rien n'est coché ni envoyé. Un objet qui traîne dans un sac peut se vendre ou
/// se jeter : le compter comme acquis serait promettre ce qu'on ne peut pas
/// tenir.
/// </summary>
public static class ARanger
{
    public static List<Egaree> Calculer(
        Catalogue cat,
        Coffre coffre,
        IReadOnlyList<Trouvaille> sousLaMain,
        IReadOnlyDictionary<string, uint[]> servants)
    {
        // Où chaque objet se trouve. Un objet peut être à deux endroits : on
        // garde le premier, la liste sert à aller le chercher, pas à recenser.
        var ou = new Dictionary<uint, string>();
        foreach (var t in sousLaMain) ou.TryAdd(t.Objet, t.Ou);
        foreach (var (nom, objets) in servants)
            foreach (var o in objets)
                ou.TryAdd(o, Mots.OuServant(nom));

        var sortie = new List<Egaree>();
        foreach (var tenue in cat.Tenues)
        {
            var manquantes = new List<(uint Objet, string Nom, string Ou)>();
            var introuvables = 0;
            foreach (var p in tenue.Pieces)
            {
                if (coffre.Coiffeuse.Contains(p.Objet) || coffre.Armoire.Contains(p.Objet)) continue;
                if (ou.TryGetValue(p.Objet, out var endroit)) manquantes.Add((p.Objet, p.Nom, endroit));
                else introuvables++;
            }
            if (manquantes.Count == 0) continue;

            // Une tenue qu'un seul rangement achève : c'est là que l'effort
            // rapporte, et c'est la seule chose qui mérite d'être signalée.
            var acheve = introuvables == 0;
            foreach (var (objet, nom, endroit) in manquantes)
                sortie.Add(new Egaree(objet, nom, endroit, tenue.Nom, acheve));
        }
        return sortie;
    }
}
