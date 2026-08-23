namespace CodexOlympia;

/// <summary>Une pièce qu'on possède sans l'avoir déposée, et où elle dort.</summary>
public sealed record Egaree(uint Objet, string Nom, string Ou);

/// <summary>Une tenue dont il manque des pièces qu'on a pourtant quelque part.</summary>
public sealed record TenueARanger(uint Id, string Nom, int Total, int Deposees, List<Egaree> Egarees);

/// <summary>
/// Ce qu'il reste à ranger.
///
/// Le rapprochement tient en une phrase : parmi les pièces qu'une tenue attend,
/// certaines ne sont dans aucun dépôt, et on les a pourtant sous la main. Ce
/// sont celles-là qu'il faut déposer, et c'est tout ce que cette page dit.
///
/// Elle ne coche rien et n'envoie rien. Un objet qui traîne dans un sac peut se
/// vendre ou se jeter : le compter comme acquis serait promettre ce qu'on ne
/// peut pas tenir.
/// </summary>
public static class ARanger
{
    public static List<TenueARanger> Calculer(
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

        var sortie = new List<TenueARanger>();
        foreach (var tenue in cat.Tenues)
        {
            var deposees = 0;
            var egarees = new List<Egaree>();
            foreach (var p in tenue.Pieces)
            {
                if (coffre.Coiffeuse.Contains(p.Objet) || coffre.Armoire.Contains(p.Objet))
                {
                    deposees++;
                    continue;
                }
                if (ou.TryGetValue(p.Objet, out var endroit))
                    egarees.Add(new Egaree(p.Objet, p.Nom, endroit));
            }
            if (egarees.Count > 0)
                sortie.Add(new TenueARanger(tenue.Id, tenue.Nom, tenue.Pieces.Count, deposees, egarees));
        }

        // Les tenues qu'on peut compléter d'un coup passent devant : ce sont
        // celles où le rangement rapporte le plus.
        return
        [
            .. sortie.OrderByDescending(t => t.Deposees + t.Egarees.Count == t.Total)
                .ThenByDescending(t => t.Egarees.Count)
                .ThenBy(t => t.Nom, StringComparer.CurrentCultureIgnoreCase),
        ];
    }
}
