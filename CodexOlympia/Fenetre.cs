using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace CodexOlympia;

/// <summary>
/// La fenêtre du greffon, en deux pages.
///
/// <b>Synchronisation</b> est celle qu'on ouvre tous les jours : un bouton, un
/// tableau, un envoi. <b>Configuration</b> est celle qu'on ouvre une fois.
/// Les mélanger obligeait à passer devant un jeton et une adresse de serveur
/// pour atteindre le seul bouton qui sert.
///
/// L'ordre de la page principale compte : on regarde, on lit, on envoie. Rien ne
/// part tant que le joueur n'a pas vu ce qui partira, et c'est la seule
/// protection contre une lecture qui se tromperait : lui seul reconnaît ses
/// propres chiffres.
/// </summary>
public sealed class Fenetre : Window, IDisposable
{
    private readonly Greffon greffon;

    // Les teintes : discrètes, et jamais seules à porter l'information. Chaque
    // couleur double un mot, elle ne le remplace pas.
    private static readonly Vector4 Or = new(0.84f, 0.70f, 0.42f, 1f);
    private static readonly Vector4 Vert = new(0.45f, 0.78f, 0.45f, 1f);
    private static readonly Vector4 Ambre = new(0.98f, 0.70f, 0.10f, 1f);
    private static readonly Vector4 Gris = new(0.54f, 0.53f, 0.51f, 1f);

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
            MinimumSize = new Vector2(560, 420),
            MaximumSize = new Vector2(1400, 1400),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("pages")) return;
        if (ImGui.BeginTabItem("Synchronisation"))
        {
            PageSynchronisation();
            ImGui.EndTabItem();
        }
        // Le bouton « Aller a la configuration » n'a de sens que s'il y va.
        var force = ongletVoulu == 1 ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        ongletVoulu = -1;
        if (ImGui.BeginTabItem("Configuration", force))
        {
            PageConfiguration();
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    // ------------------------------------------------------- la page du jour

    private void PageSynchronisation()
    {
        ImGui.Spacing();

        var manque = Manque();
        if (manque is not null)
        {
            ImGui.TextColored(Ambre, manque);
            ImGui.Spacing();
            if (ImGui.Button("Aller à la configuration")) ongletVoulu = 1;
            return;
        }

        var nom = greffon.Reglages.Noms.TryGetValue(greffon.ContentId, out var n) ? n : "ce personnage";
        ImGui.TextColored(Or, nom);

        if (greffon.Catalogue?.Pret != true)
        {
            ImGui.Spacing();
            ImGui.TextColored(Ambre, "Le catalogue de l'application n'est pas encore chargé.");
            if (ImGui.Button("Réessayer")) greffon.RechargerCatalogue();
            return;
        }

        ImGui.Spacing();
        if (ImGui.Button("Regarder ce que j'ai", new Vector2(180, 28))) greffon.Regarder();
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Gris, "rien n'est envoyé à cette étape");

        var releves = greffon.Releves;
        if (releves.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextWrapped(
                "Le greffon lit ce que le jeu tient pour débloqué, te le montre, et n'envoie " +
                "que si tu le lui dis. Rien n'est jamais décoché à ta place.");
            return;
        }

        ImGui.Spacing();
        Tableau(releves);
        ImGui.Spacing();

        // Une piece rangee a l'armoire est possedee, mais elle ne sert a aucun
        // glamour tant qu'elle dort la-bas. Le dire ici evite d'aller chercher
        // l'information ailleurs, et n'a rien a faire dans la photo : c'est un
        // conseil, pas un fait de collection.
        var aDeposer = releves.FirstOrDefault(r => r.Cle == "adeposer");
        if (aDeposer is not null && aDeposer.Trouves.Count > 0)
        {
            ImGui.TextColored(Or, aDeposer.Trouves.Count == 1
                ? "Une pièce de tenue dort dans ton armoire."
                : $"{aDeposer.Trouves.Count} pièces de tenue dorment dans ton armoire.");
            ImGui.TextColored(Gris, "Dépose-les dans la coiffeuse pour pouvoir t'en servir.");
            ImGui.Spacing();
        }

        // Une ligne empêchée est une lecture qu'on refuse de faire, pas une
        // erreur : elle mérite d'être dite avant le bouton, jamais après.
        var bloquees = releves.Count(r => r.Empeche is not null);
        if (bloquees > 0)
        {
            ImGui.TextColored(Ambre, bloquees == 1
                ? "Une collection n'a pas pu être lue : elle ne sera pas envoyée."
                : $"{bloquees} collections n'ont pas pu être lues : elles ne seront pas envoyées.");
            ImGui.Spacing();
        }

        if (greffon.EnvoiEnCours)
        {
            ImGui.TextColored(Gris, "envoi en cours...");
        }
        else if (ImGui.Button("Envoyer à Codex Olympia", new Vector2(220, 30)))
        {
            greffon.Envoyer();
        }

        Retour();
    }

    private void Tableau(IReadOnlyList<Releve> releves)
    {
        const ImGuiTableFlags style = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH
            | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX;
        if (!ImGui.BeginTable("releves", 4, style)) return;

        ImGui.TableSetupColumn("Collection", ImGuiTableColumnFlags.WidthFixed, 190);
        ImGui.TableSetupColumn("Trouvé", ImGuiTableColumnFlags.WidthFixed, 96);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 110);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        foreach (var (cle, nom) in Noms)
        {
            var x = releves.FirstOrDefault(v => v.Cle == cle);
            if (x is null) continue;

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            if (x.Empeche is not null) ImGui.TextColored(Gris, nom);
            else ImGui.Text(nom);

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            if (x.Empeche is not null) ImGui.TextColored(Ambre, "non lu");
            else ImGui.TextColored(x.Trouves.Count > 0 ? Vert : Gris, $"{x.Trouves.Count} / {x.Total}");

            // La barre ne dit rien de neuf : elle rend la colonne lisible d'un
            // coup d'oeil, ce qu'une colonne de fractions ne fait pas.
            ImGui.TableNextColumn();
            if (x.Empeche is null && x.Total > 0)
            {
                var part = Math.Clamp((float)x.Trouves.Count / x.Total, 0f, 1f);
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, x.Trouves.Count > 0 ? Vert : Gris);
                ImGui.ProgressBar(part, new Vector2(100, 6), string.Empty);
                ImGui.PopStyleColor();
            }

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            if (x.Empeche is not null) ImGui.TextWrapped(x.Empeche);
            else Limitation(x);
        }
        ImGui.EndTable();
    }

    /// <summary>
    /// Ce qui borne une lecture, dit en trois mots avec l'explication au survol.
    ///
    /// Les deux bornes n'ont rien à voir et les confondre trompe : l'une dit que
    /// le jeu ne sait pas répondre pour toutes les entrées, l'autre qu'on
    /// regarde un dépôt, où l'on voit ce qui s'y trouve et jamais ce qui n'y est
    /// pas.
    /// </summary>
    private void Limitation(Releve x)
    {
        switch (x.Limite)
        {
            case Limite.Capacite:
                ImGui.TextColored(Gris, $"{x.Portee?.Count ?? 0} sur {x.Total} vérifiables");
                Survol(
                    "Le jeu ne sait pas répondre pour le reste de cette collection.\n" +
                    "Ces entrées-là sont laissées tranquilles : ni ajoutées, ni signalées manquantes.");
                break;
            case Limite.Depot:
                ImGui.TextColored(Gris, "ajout seulement");
                Survol(
                    "Cette collection se constate dans un dépôt : la coiffeuse mirage ou l'armoire.\n" +
                    "On y voit ce qui s'y trouve, jamais ce qui n'y est pas. Une pièce peut dormir\n" +
                    "dans un sac ou chez un servant. Rien ne sera donc signalé comme manquant.");
                if (x.Note is not null)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(Gris, $"· {x.Note}");
                }
                break;
            default:
                if (x.Note is not null) ImGui.TextColored(Gris, x.Note);
                break;
        }
    }

    private static void Survol(string texte)
    {
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(texte);
    }

    private void Retour()
    {
        if (greffon.Dernier is not { } retour) return;
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextColored(retour.Ok ? Vert : Ambre, string.Empty);
        ImGui.SameLine(0, 0);
        ImGui.TextWrapped(retour.Message);
        if (!retour.Ok) return;

        if (retour.Ajouts.Count > 0)
        {
            ImGui.TextColored(Vert, "Ajouté");
            ImGui.SameLine();
            ImGui.TextWrapped(string.Join(", ", retour.Ajouts.Select(a => $"{a.Value} {Lisible(a.Key)}")));
        }
        if (retour.Ecarts.Count > 0)
        {
            ImGui.TextColored(Ambre, "À trancher dans l'application");
            ImGui.SameLine();
            ImGui.TextWrapped(string.Join(", ", retour.Ecarts.Select(a => $"{a.Value} {Lisible(a.Key)}")));
        }
    }

    // -------------------------------------------------- la page qu'on ouvre une fois

    /// <summary>Vaut 1 quand un bouton demande à passer sur la configuration.</summary>
    private int ongletVoulu = -1;

    private void PageConfiguration()
    {
        var r = greffon.Reglages;
        ImGui.Spacing();

        var contentId = greffon.ContentId;
        if (contentId == 0)
        {
            ImGui.TextColored(Ambre, "Connecte-toi avec un personnage.");
            return;
        }

        // Un jeton par personnage, et c'est le seul champ de cette page. Le
        // jeton désigne lui-même le personnage qu'il alimente : rien d'autre
        // n'est à recopier depuis l'application.
        var nom = r.Noms.TryGetValue(contentId, out var n) ? n : "ce personnage";
        ImGui.TextColored(Or, $"Le jeton de {nom}");
        ImGui.TextWrapped(
            "Il se crée dans Codex Olympia, page de compte, section « Plugin Codex Olympia " +
            "Dalamud ». Choisis ce personnage au moment de le créer, colle-le ici, et c'est " +
            "tout. Il ne sait faire qu'une chose : déposer une photo de tes déblocages. Il " +
            "ne peut ni lire ton compte, ni le modifier, ni l'effacer.");
        ImGui.Spacing();
        ImGui.SetNextItemWidth(-1);
        var jeton = greffon.Jeton;
        if (ImGui.InputTextWithHint("##jeton", "colle ton jeton ici", ref jeton, 200,
                ImGuiInputTextFlags.Password))
        {
            greffon.PoserJeton(jeton);
        }
        ImGui.TextColored(greffon.Jeton.Length > 0 ? Vert : Ambre,
            greffon.Jeton.Length > 0
                ? "jeton enregistré pour ce personnage"
                : "aucun jeton pour ce personnage");
    }

    // ------------------------------------------------------------------ menu

    /// <summary>Ce qui empêche encore d'envoyer, dit en une phrase.</summary>
    private string? Manque()
    {
        if (greffon.ContentId == 0) return "Aucun personnage connecté.";
        if (greffon.Jeton.Length == 0) return "Il manque le jeton de ce personnage.";
        return null;
    }

    private static string Lisible(string cle) =>
        Noms.FirstOrDefault(n => n.Cle == cle).Nom?.ToLowerInvariant() ?? cle;

}
