using System.Numerics;

namespace CodexOlympia.Ui;

/// <summary>
/// Les jetons de <c>src/styles.css</c>, recopiés à l'identique (PLG-R41).
///
/// Deux langages de couleur qu'on ne mélange jamais : l'or est la marque et
/// n'habille que ce qui agit, le bleu est l'avancement et rien d'autre. Le
/// vert, l'ambre et le rouge gardent le sens du site. Aucune couleur ne porte
/// seule une information (PLG-R43).
/// </summary>
internal static class Teintes
{
    private static Vector4 Hex(uint rvb, float a = 1f) => new(
        ((rvb >> 16) & 0xFF) / 255f,
        ((rvb >> 8) & 0xFF) / 255f,
        (rvb & 0xFF) / 255f,
        a);

    public static readonly Vector4 Page = Hex(0x0D0D0D);
    public static readonly Vector4 Surface = Hex(0x1A1A19);
    public static readonly Vector4 Surface2 = Hex(0x232322);
    public static readonly Vector4 Filet = Hex(0x2C2C2A);

    public static readonly Vector4 Encre = Hex(0xFFFFFF);
    public static readonly Vector4 Encre2 = Hex(0xC3C2B7);
    public static readonly Vector4 Discret = Hex(0x898781);

    public static readonly Vector4 Or = Hex(0xD6B26B);
    public static readonly Vector4 Bleu = Hex(0x3987E5);
    public static readonly Vector4 Vert = Hex(0x0CA30C);
    public static readonly Vector4 Ambre = Hex(0xFAB219);
    public static readonly Vector4 Rouge = Hex(0xD03B3B);

    /// <summary>L'encre posée sur l'or : le site écrit ses boutons principaux
    /// en sombre.</summary>
    public static readonly Vector4 SurOr = Hex(0x171716);

    public const float RondCarte = 12f;
    public const float RondTuile = 8f;
    public const float RondFenetre = 12f;

    public static Vector4 Alpha(Vector4 c, float a) => c with { W = a };

    public static Vector4 Eclaircir(Vector4 c, float t) => Vector4.Lerp(c, Vector4.One, t) with { W = c.W };

    public static Vector4 Melanger(Vector4 fond, Vector4 accent, float part)
        => Vector4.Lerp(fond, accent, part) with { W = fond.W };

    /// <summary>La couleur d'un avancement, comme l'application la choisit
    /// (<c>JaugesCollections.tsx</c>) : fini en vert, puis par tiers.</summary>
    public static Vector4 Avancement(int fait, int total)
    {
        if (total > 0 && fait >= total) return Vert;
        var part = total > 0 ? (float)fait / total : 0f;
        return part < 1f / 3f ? Rouge : part < 2f / 3f ? Ambre : Bleu;
    }
}
