using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace CodexOlympia;

/// <summary>
/// La fenêtre du greffon.
///
/// Elle tient en trois temps, et l'ordre compte : on règle une fois, on regarde,
/// puis on envoie. Rien ne part tant que le joueur n'a pas vu ce qui partira.
/// C'est la seule protection contre une lecture qui se tromperait : le joueur
/// reconnaît ses propres chiffres.
/// </summary>
public sealed class Fenetre : Window, IDisposable
{
    private readonly Greffon greffon;

    /// <summary>Le nom lisible de chaque collection, dans l'ordre d'affichage.</summary>
    private static readonly (string Cle, string Nom)[] Noms =
    [
        ("mounts", "Montures"),
        ("minions", "Mascottes"),
        ("orchestrions", "Rouleaux d'orchestrion"),
        ("emotes", "Emotes"),
        ("hairstyles", "Coiffures"),
        ("fashions", "Accessoires de mode"),
        ("facewear", "Lunettes"),
        ("bardings", "Bardes"),
        ("cards", "Cartes de Triple Triade"),
        ("frames", "Portraits"),
        ("spells", "Sorts bleus"),
        ("achievements", "Succès"),
        ("armoires", "Armoire"),
        ("outfitpieces", "Pièces de tenue"),
        ("outfits", "Tenues entières"),
    ];

    public Fenetre(Greffon greffon) : base("Codex Olympia###codex-olympia")
    {
        this.greffon = greffon;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 360),
            MaximumSize = new Vector2(1400, 1400),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        Reglages();
        ImGui.Separator();
        Lecture();
    }

    private void Reglages()
    {
        var r = greffon.Reglages;

        if (ImGui.CollapsingHeader("Réglages"))
        {
            ImGui.TextWrapped(
                "Le jeton se crée dans Codex Olympia, page de compte. Il ne sait faire " +
                "qu'une chose : déposer une photo de tes déblocages. Il ne peut ni lire " +
                "ton compte, ni le modifier, ni l'effacer.");

            var jeton = r.Jeton;
            if (ImGui.InputText("Jeton", ref jeton, 200, ImGuiInputTextFlags.Password))
            {
                r.Jeton = jeton.Trim();
                greffon.Enregistrer();
            }

            var serveur = r.Serveur;
            if (ImGui.InputText("Serveur", ref serveur, 200))
            {
                r.Serveur = serveur.Trim();
                greffon.Enregistrer();
            }
            ImGui.Spacing();
        }

        var contentId = greffon.ContentId;
        if (contentId == 0)
        {
            ImGui.TextWrapped("Connecte-toi avec un personnage pour continuer.");
            return;
        }

        var nom = r.Noms.TryGetValue(contentId, out var n) ? n : "ce personnage";
        r.Personnages.TryGetValue(contentId, out var lodestone);
        var saisie = lodestone == 0 ? string.Empty : lodestone.ToString();

        ImGui.Text($"Personnage : {nom}");
        ImGui.SetNextItemWidth(180);
        if (ImGui.InputText("Identifiant Lodestone", ref saisie, 12, ImGuiInputTextFlags.CharsDecimal))
        {
            if (uint.TryParse(saisie, out var id) && id > 0) r.Personnages[contentId] = id;
            else r.Personnages.Remove(contentId);
            greffon.Enregistrer();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Le jeu ne connaît pas le Lodestone : c'est à toi de faire le lien, une " +
                "fois par personnage.\nOuvre ta fiche sur le Lodestone : le nombre à la " +
                "fin de l'adresse est cet identifiant.");
        }
    }

    private void Lecture()
    {
        var pret = greffon.Catalogue?.Pret == true;
        if (!pret)
        {
            ImGui.TextWrapped("Le catalogue de l'application n'est pas encore chargé.");
            if (ImGui.Button("Réessayer")) greffon.RechargerCatalogue();
            return;
        }

        if (ImGui.Button("Regarder ce que j'ai")) greffon.Regarder();
        ImGui.SameLine();
        ImGui.TextDisabled("rien n'est envoyé à cette étape");

        var releves = greffon.Releves;
        if (releves.Count == 0) return;

        ImGui.Spacing();
        if (ImGui.BeginTable("releves", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Collection", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Trouvé", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            foreach (var (cle, nom) in Noms)
            {
                var x = releves.FirstOrDefault(v => v.Cle == cle);
                if (x is null) continue;
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(nom);
                ImGui.TableNextColumn();
                if (x.Empeche is not null) ImGui.TextDisabled("non lu");
                else ImGui.Text($"{x.Trouves.Count} / {x.Total}");
                ImGui.TableNextColumn();
                if (x.Empeche is not null) ImGui.TextWrapped(x.Empeche);
                else if (x.Portee is not null) ImGui.TextDisabled($"{x.Portee.Count} entrées interrogeables");
                else if (x.Note is not null) ImGui.TextDisabled(x.Note);
            }
            ImGui.EndTable();
        }

        ImGui.Spacing();
        var manque = Manque();
        if (manque is not null)
        {
            ImGui.TextWrapped(manque);
            return;
        }

        if (greffon.EnvoiEnCours)
        {
            ImGui.TextDisabled("envoi en cours...");
        }
        else if (ImGui.Button("Envoyer à Codex Olympia"))
        {
            greffon.Envoyer();
        }

        if (greffon.Dernier is { } retour)
        {
            ImGui.Spacing();
            ImGui.TextWrapped(retour.Message);
            if (retour.Ok && retour.Ajouts.Count > 0)
            {
                ImGui.TextWrapped("Ajouté : " + string.Join(", ", retour.Ajouts.Select(
                    a => $"{a.Value} {Lisible(a.Key)}")));
            }
            if (retour.Ok && retour.Ecarts.Count > 0)
            {
                ImGui.TextWrapped("À trancher dans l'application : " + string.Join(", ", retour.Ecarts.Select(
                    a => $"{a.Value} {Lisible(a.Key)}")));
            }
        }
    }

    /// <summary>Ce qui empêche encore d'envoyer, dit en une phrase.</summary>
    private string? Manque()
    {
        if (greffon.Reglages.Jeton.Length == 0) return "Il manque le jeton, dans les réglages.";
        if (greffon.ContentId == 0) return "Il manque un personnage connecté.";
        if (!greffon.Reglages.Personnages.ContainsKey(greffon.ContentId))
            return "Il manque l'identifiant Lodestone de ce personnage.";
        return null;
    }

    private static string Lisible(string cle) =>
        Noms.FirstOrDefault(n => n.Cle == cle).Nom?.ToLowerInvariant() ?? cle;
}
