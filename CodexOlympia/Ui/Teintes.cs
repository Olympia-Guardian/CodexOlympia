using System.Numerics;

namespace CodexOlympia.Ui;

/// <summary>
/// Les couleurs de l'application, telles quelles (PLG-R41).
///
/// Ce sont les jetons de <c>src/styles.css</c>, recopiés à l'identique : qui
/// ouvre le plugin doit reconnaître le site. Deux langages de couleur, qu'on ne
/// mélange jamais :
///
/// <list type="bullet">
/// <item>l'<b>or</b> est la marque, et n'habille que ce qui agit ;</item>
/// <item>le <b>bleu</b> est l'avancement, et rien d'autre.</item>
/// </list>
///
/// Le vert, l'ambre et le rouge gardent le sens qu'ils ont sur le site : fini,
/// à regarder, arrêté. Et aucune couleur ne porte seule une information : elle
/// double toujours un mot (PLG-R43).
/// </summary>
internal static class Teintes
{
    /// <summary>Une couleur du site, écrite comme sur le site.</summary>
    private static Vector4 Hex(uint rvb, float a = 1f) => new(
        ((rvb >> 16) & 0xFF) / 255f,
        ((rvb >> 8) & 0xFF) / 255f,
        (rvb & 0xFF) / 255f,
        a);

    // Les fonds, du plus profond au plus clair.
    public static readonly Vector4 Page = Hex(0x0D0D0D);
    public static readonly Vector4 Surface = Hex(0x1A1A19);
    public static readonly Vector4 Surface2 = Hex(0x232322);
    public static readonly Vector4 Filet = Hex(0x2C2C2A);

    // Les encres, de la plus forte à la plus discrète.
    public static readonly Vector4 Encre = Hex(0xFFFFFF);
    public static readonly Vector4 Encre2 = Hex(0xC3C2B7);
    public static readonly Vector4 Discret = Hex(0x898781);

    // La marque, puis les quatre sens.
    public static readonly Vector4 Or = Hex(0xD6B26B);
    public static readonly Vector4 Bleu = Hex(0x3987E5);
    public static readonly Vector4 Vert = Hex(0x0CA30C);
    public static readonly Vector4 Ambre = Hex(0xFAB219);
    public static readonly Vector4 Rouge = Hex(0xD03B3B);

    /// <summary>L'encre posée sur l'or : le site écrit ses boutons principaux
    /// en sombre sur l'or, comme les marches d'un podium.</summary>
    public static readonly Vector4 SurOr = Hex(0x171716);

    // Les arrondis du site : 12 pour une carte, 8 pour une tuile ou un bouton.
    public const float RondCarte = 12f;
    public const float RondTuile = 8f;
    public const float RondFenetre = 12f;

    public static Vector4 Alpha(Vector4 c, float a) => c with { W = a };

    public static Vector4 Eclaircir(Vector4 c, float t) => Vector4.Lerp(c, Vector4.One, t) with { W = c.W };

    public static Vector4 Assombrir(Vector4 c, float t) => Vector4.Lerp(c, Vector4.Zero, t) with { W = c.W };

    /// <summary>Une couleur teintée d'une autre, sans changer sa
    /// transparence : de quoi border une carte de son accent.</summary>
    public static Vector4 Melanger(Vector4 fond, Vector4 accent, float part)
        => Vector4.Lerp(fond, accent, part) with { W = fond.W };

    /// <summary>
    /// La couleur d'un avancement, exactement comme l'application la choisit
    /// (<c>JaugesCollections.tsx</c>) : fini en vert, puis par tiers, rouge,
    /// ambre, bleu. Un joueur qui passe du site au jeu retrouve ses couleurs.
    /// </summary>
    public static Vector4 Avancement(int fait, int total)
    {
        if (total > 0 && fait >= total) return Vert;
        var part = total > 0 ? (float)fait / total : 0f;
        return part < 1f / 3f ? Rouge : part < 2f / 3f ? Ambre : Bleu;
    }
}
