using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace CodexOlympia.Ui;

/// <summary>
/// Le thème poussé dans ImGui avant de dessiner, retiré après.
///
/// La marge de fenêtre est à zéro : la barre de titre, le rail et le pied
/// touchent les bords, et chaque page remet la sienne.
/// </summary>
internal static class Theme
{
    public static IDisposable Pousser()
    {
        var e = Peinture.Echelle;
        var styles = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, Teintes.RondFenetre * e)
            .Push(ImGuiStyleVar.WindowBorderSize, 1f)
            .Push(ImGuiStyleVar.WindowPadding, Vector2.Zero)
            .Push(ImGuiStyleVar.ChildRounding, Teintes.RondTuile * e)
            .Push(ImGuiStyleVar.ChildBorderSize, 0f)
            .Push(ImGuiStyleVar.FrameRounding, Teintes.RondTuile * e)
            .Push(ImGuiStyleVar.FrameBorderSize, 1f)
            .Push(ImGuiStyleVar.FramePadding, new Vector2(9f, 7f) * e)
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 8f) * e)
            .Push(ImGuiStyleVar.ItemInnerSpacing, new Vector2(7f, 5f) * e)
            .Push(ImGuiStyleVar.ScrollbarSize, 9f * e)
            .Push(ImGuiStyleVar.ScrollbarRounding, 5f * e)
            .Push(ImGuiStyleVar.PopupRounding, Teintes.RondTuile * e)
            .Push(ImGuiStyleVar.WindowMinSize, new Vector2(420f, 320f) * e);

        var couleurs = ImRaii.PushColor(ImGuiCol.WindowBg, Teintes.Page)
            .Push(ImGuiCol.ChildBg, Vector4.Zero)
            .Push(ImGuiCol.PopupBg, Teintes.Surface)
            .Push(ImGuiCol.Border, Teintes.Filet)
            .Push(ImGuiCol.Text, Teintes.Encre)
            .Push(ImGuiCol.TextDisabled, Teintes.Discret)
            .Push(ImGuiCol.FrameBg, Teintes.Surface2)
            .Push(ImGuiCol.FrameBgHovered, Teintes.Melanger(Teintes.Surface2, Teintes.Or, 0.12f))
            .Push(ImGuiCol.FrameBgActive, Teintes.Melanger(Teintes.Surface2, Teintes.Or, 0.2f))
            .Push(ImGuiCol.Button, Teintes.Surface2)
            .Push(ImGuiCol.ButtonHovered, Teintes.Melanger(Teintes.Surface2, Teintes.Or, 0.18f))
            .Push(ImGuiCol.ButtonActive, Teintes.Melanger(Teintes.Surface2, Teintes.Or, 0.28f))
            .Push(ImGuiCol.Header, Teintes.Surface2)
            .Push(ImGuiCol.HeaderHovered, Teintes.Surface2)
            .Push(ImGuiCol.HeaderActive, Teintes.Surface2)
            .Push(ImGuiCol.ScrollbarBg, Vector4.Zero)
            .Push(ImGuiCol.ScrollbarGrab, Teintes.Filet)
            .Push(ImGuiCol.ScrollbarGrabHovered, Teintes.Alpha(Teintes.Or, 0.4f))
            .Push(ImGuiCol.ScrollbarGrabActive, Teintes.Alpha(Teintes.Or, 0.6f))
            .Push(ImGuiCol.ResizeGrip, Vector4.Zero)
            .Push(ImGuiCol.ResizeGripHovered, Teintes.Alpha(Teintes.Or, 0.25f))
            .Push(ImGuiCol.ResizeGripActive, Teintes.Alpha(Teintes.Or, 0.4f))
            .Push(ImGuiCol.CheckMark, Teintes.Or)
            .Push(ImGuiCol.Separator, Teintes.Filet)
            .Push(ImGuiCol.PlotHistogram, Teintes.Bleu);

        return new Portee(styles, couleurs);
    }

    /// <summary>Les deux piles se rendent dans l'ordre inverse : couleurs
    /// d'abord, styles ensuite.</summary>
    private sealed class Portee(IDisposable styles, IDisposable couleurs) : IDisposable
    {
        public void Dispose()
        {
            couleurs.Dispose();
            styles.Dispose();
        }
    }
}
