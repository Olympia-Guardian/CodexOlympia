using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace CodexOlympiaAuto;

/// <summary>
/// La fenêtre du plugin d'automatisation : la page « À ranger » avec le
/// rangement, la sonde de développement, et la configuration.
/// </summary>
public sealed class Fenetre : Window, IDisposable
{
    private readonly Plugin plugin;

    private static readonly Vector4 Or = new(0.84f, 0.70f, 0.42f, 1f);
    private static readonly Vector4 Vert = new(0.45f, 0.78f, 0.45f, 1f);
    private static readonly Vector4 Ambre = new(0.98f, 0.70f, 0.10f, 1f);
    private static readonly Vector4 Gris = new(0.54f, 0.53f, 0.51f, 1f);

    public Fenetre(Plugin plugin) : base("Codex Olympia Automatisation###codex-olympia-auto")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 420),
            MaximumSize = new Vector2(1400, 1400),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("pages")) return;
        if (ImGui.BeginTabItem(Mots.PageARanger))
        {
            PageARanger();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem(Mots.PageSonde))
        {
            PageSonde();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem(Mots.PageConfig))
        {
            PageConfiguration();
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    // ------------------------------------------------------------- à ranger

    private void PageARanger()
    {
        ImGui.Spacing();

        if (plugin.ContentId == 0)
        {
            ImGui.TextColored(Ambre, Mots.PasDePerso);
            return;
        }
        if (plugin.Catalogue?.Pret != true)
        {
            ImGui.TextColored(Ambre, Mots.CataloguePasPret);
            if (ImGui.Button(Mots.Reessayer)) plugin.RechargerCatalogue();
            return;
        }

        var egarees = plugin.ARanger();
        if (egarees.Count == 0)
        {
            ImGui.TextColored(Vert, Mots.ARangerRien);
            ImGui.Spacing();
            ImGui.TextColored(Gris, Mots.ServantsVus(plugin.ServantsDuPerso().Count));
            Doubles();
            return;
        }

        var achevent = egarees.Count(e => e.Acheve);
        ImGui.TextColored(Or, Mots.ARangerCompte(egarees.Count, achevent));
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Mots.ARangerQuoi + "\n\n" + Mots.ServantsVus(plugin.ServantsDuPerso().Count));
        ImGui.Spacing();

        if (!ImGui.BeginChild("aranger", new Vector2(0, 0), false)) return;

        Rangement(egarees);

        if (achevent > 0)
        {
            ImGui.TextColored(Or, Mots.ARangerAchevent);
            Groupe(egarees.Where(e => e.Acheve), true);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        var reste = egarees.Where(e => !e.Acheve).ToList();
        if (reste.Count > 0)
        {
            ImGui.TextColored(Gris, Mots.ARangerReste);
            Groupe(reste, false);
        }

        Doubles();
        ImGui.EndChild();
    }

    private void Doubles()
    {
        var doubles = plugin.Doubles();
        if (doubles.Count == 0) return;
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.SetNextItemOpen(false, ImGuiCond.FirstUseEver);
        if (!ImGui.CollapsingHeader($"{Mots.DoublesTitre(doubles.Count)}###doubles")) return;
        ImGui.TextColored(Gris, Mots.DoublesAide);
        ImGui.Spacing();
        if (!ImGui.BeginTable("dbl", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp)) return;
        ImGui.TableSetupColumn("##p", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("##t", ImGuiTableColumnFlags.WidthStretch, 1f);
        foreach (var d in doubles.OrderBy(x => x.Tenue, StringComparer.CurrentCultureIgnoreCase))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(d.Nom);
            ImGui.TableNextColumn();
            ImGui.TextColored(Gris, d.Tenue);
        }
        ImGui.EndTable();
    }

    /// <summary>Les pièces regroupées par endroit, un endroit par pliage.</summary>
    private void Groupe(IEnumerable<Egaree> pieces, bool ouvert)
    {
        foreach (var lot in pieces.GroupBy(e => e.Ou).OrderByDescending(g => g.Count()))
        {
            ImGui.SetNextItemOpen(ouvert, ImGuiCond.FirstUseEver);
            if (!ImGui.CollapsingHeader($"{lot.Key}  ({lot.Count()})###{lot.Key}{ouvert}")) continue;
            if (!ImGui.BeginTable($"t{lot.Key}{ouvert}", 2,
                    ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                continue;
            ImGui.TableSetupColumn("##piece", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##tenue", ImGuiTableColumnFlags.WidthStretch, 1f);
            foreach (var e in lot.OrderBy(x => x.Tenue, StringComparer.CurrentCultureIgnoreCase))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(e.Nom);
                ImGui.TableNextColumn();
                ImGui.TextColored(Gris, e.Tenue);
            }
            ImGui.EndTable();
            ImGui.Spacing();
        }
    }

    // ----------------------------------------------------------- le rangement

    private void Rangement(IReadOnlyList<Egaree> egarees)
    {
        var r = plugin.Rangeur;

        if (r.Etat == EtatRangement.EnMarche)
        {
            var quoi = r.EnCours?.Nom ?? string.Empty;
            ImGui.TextColored(Or, Mots.RangeurAvance(r.Faits, r.Total, quoi) + Points());
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Or);
            ImGui.ProgressBar(r.Total == 0 ? 0f : (float)r.Faits / r.Total, new Vector2(-1, 6), string.Empty);
            ImGui.PopStyleColor();
            if (ImGui.Button(Mots.RangeurStop)) r.Arreter(Mots.RangeurAMain);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            return;
        }

        if (r.Etat == EtatRangement.Interrompu && r.Pourquoi is not null)
        {
            ImGui.TextColored(Ambre, Mots.RangeurArrete(r.Pourquoi));
            ImGui.Spacing();
        }

        if (!ImGui.CollapsingHeader($"{Mots.RangeurTitre}  [{Mots.RangeurExperimental}]###rangeur"))
            return;

        ImGui.TextColored(Ambre, Mots.RangeurAvertissement);
        ImGui.Spacing();

        var cat = plugin.Catalogue;
        var coffre = plugin.Coffre();
        if (cat is null || coffre is null) return;

        var aussi = plugin.Reglages.RangerArmoire;
        if (ImGui.Checkbox("##armoire", ref aussi))
        {
            plugin.Reglages.RangerArmoire = aussi;
            plugin.Enregistrer();
        }
        ImGui.SameLine();
        ImGui.TextWrapped(Mots.RangeurAussiArmoire);
        ImGui.Spacing();

        var apercu = r.Preparer(cat, coffre, plugin.Reglages.RangerArmoire);
        if (apercu.Count == 0)
        {
            ImGui.TextColored(Gris, Mots.RangeurRienAFaire);
            return;
        }

        var entamees = apercu.Count(t => t.Moyen == Moyen.TenueEntamee);
        var neuves = apercu.Count(t => t.Moyen == Moyen.TenueNeuve);
        var armoire = apercu.Count(t => t.Moyen == Moyen.Armoire);
        ImGui.TextColored(Or, Mots.RangeurApercu(entamees, neuves, armoire));
        ImGui.TextColored(Gris, Mots.RangeurPartiel);

        var besoin = apercu.Sum(t => t.Pieces);
        if (besoin > 0)
        {
            var prismes = r.Prismes();
            ImGui.TextColored(prismes >= besoin ? Gris : Ambre, Mots.RangeurPrismes(prismes, besoin));
        }
        ImGui.Spacing();

        ImGui.SetNextItemWidth(180);
        var cadence = plugin.Reglages.CadenceRangement;
        if (ImGui.SliderFloat("##cadence", ref cadence, 0.5f, 6f, "%.1f s"))
        {
            plugin.Reglages.CadenceRangement = cadence;
            plugin.Enregistrer();
        }
        ImGui.SameLine();
        ImGui.TextColored(Gris, Mots.RangeurCadence);
        ImGui.Spacing();

        if (ImGui.Button(Mots.RangeurLancer))
        {
            r.Cadence = plugin.Reglages.CadenceRangement;
            r.Demarrer(apercu);
        }
        if (r.Etat == EtatRangement.Fini && r.Total > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(Vert, Mots.RangeurFini(r.Faits, r.Sautes));
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Le releve sert au survol du panneau, rien de plus pour l'instant.
        _ = egarees;
    }

    // --------------------------------------------------------------- la sonde

    private void PageSonde()
    {
        ImGui.Spacing();
        ImGui.TextWrapped(Mots.SondeQuoi);
        ImGui.Spacing();

        var vu = false;
        foreach (var nom in Sonde.Fenetres)
        {
            var valeurs = Sonde.Lire(plugin.InterfaceJeu, nom, 200);
            if (valeurs is null) continue;
            vu = true;

            var textes = Sonde.Textes(plugin.InterfaceJeu, nom, 120);
            if (textes is { Count: > 0 })
            {
                ImGui.TextColored(Or, $"{nom} · textes  ({textes.Count})");
                foreach (var t in textes) ImGui.TextColored(Gris, $"{t.Type}  #{t.Index}  {t.Contenu}");
                ImGui.Spacing();
            }

            ImGui.TextColored(Or, $"{nom} · valeurs  ({valeurs.Count})");
            if (ImGui.BeginTable($"s{nom}", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("##i", ImGuiTableColumnFlags.WidthFixed, 44);
                ImGui.TableSetupColumn("##t", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableSetupColumn("##v", ImGuiTableColumnFlags.WidthStretch);
                foreach (var v in valeurs)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextColored(Gris, v.Index.ToString());
                    ImGui.TableNextColumn();
                    ImGui.TextColored(Gris, v.Type);
                    ImGui.TableNextColumn();
                    ImGui.Text(v.Contenu);
                }
                ImGui.EndTable();
            }
            ImGui.Spacing();
        }

        if (!vu) ImGui.TextColored(Ambre, Mots.SondeRien);
    }

    // -------------------------------------------------------- la configuration

    private void PageConfiguration()
    {
        ImGui.Spacing();
        ImGui.TextColored(Ambre, Mots.Experimental);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(Or, Mots.Langue_);
        ImGui.SetNextItemWidth(200);
        var choix = (int)plugin.Reglages.Langue;
        if (ImGui.Combo("##langue", ref choix, $"{Mots.LangueAuto}\0Français\0English\0"))
        {
            plugin.ChoisirLangue((Langue)choix);
        }
    }

    /// <summary>« . », « .. », « ... » : une attente qui se voit.</summary>
    private static string Points() => new('.', 1 + (int)(ImGui.GetTime() * 3) % 3);
}
