using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;

namespace CodexOlympia;

/// <summary>
/// La fenêtre du plugin, en deux pages.
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
    private readonly Plugin plugin;

    // Les teintes : discrètes, et jamais seules à porter l'information. Chaque
    // couleur double un mot, elle ne le remplace pas.
    private static readonly Vector4 Or = new(0.84f, 0.70f, 0.42f, 1f);
    private static readonly Vector4 Vert = new(0.45f, 0.78f, 0.45f, 1f);
    private static readonly Vector4 Ambre = new(0.98f, 0.70f, 0.10f, 1f);
    private static readonly Vector4 Gris = new(0.54f, 0.53f, 0.51f, 1f);

    public Fenetre(Plugin plugin) : base("Codex Olympia###codex-olympia")
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
        // La lecture se fait ici, une collection par image : c'est le fil du jeu,
        // le seul d'ou sa mémoire se lit sans risque.
        plugin.Avancer(ImGui.GetTime());

        if (!ImGui.BeginTabBar("pages")) return;
        if (ImGui.BeginTabItem(Mots.PageSync))
        {
            PageSynchronisation();
            ImGui.EndTabItem();
        }
        // Le bouton « Aller a la configuration » n'a de sens que s'il y va.
        var force = ongletVoulu == 1 ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        ongletVoulu = -1;
        if (ImGui.BeginTabItem(Mots.PageConfig, force))
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
            if (ImGui.Button(Mots.AllerConfig)) ongletVoulu = 1;
            return;
        }

        var nom = plugin.Reglages.Noms.TryGetValue(plugin.ContentId, out var n) ? n : "ce personnage";
        ImGui.TextColored(Or, nom);

        if (plugin.Catalogue?.Pret != true)
        {
            ImGui.Spacing();
            ImGui.TextColored(Ambre, Mots.CataloguePasPret);
            if (ImGui.Button(Mots.Reessayer)) plugin.RechargerCatalogue();
            return;
        }

        var lecture = plugin.LectureEnCours;

        ImGui.Spacing();
        ImGui.BeginDisabled(lecture);
        if (ImGui.Button(Mots.Regarder, new Vector2(180, 28))) plugin.Regarder();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Gris, Mots.RienNePart);

        var releves = plugin.Releves;
        if (releves.Count == 0 && !lecture)
        {
            ImGui.Spacing();
            ImGui.TextWrapped(Mots.Presentation);
            return;
        }

        ImGui.Spacing();
        if (lecture)
        {
            // Le tableau se remplit de haut en bas : on voit ce qui est fait, ce
            // qui est en train de se faire, et ce qui attend.
            ImGui.TextColored(Or, Mots.OnRecupere + Points());
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Or);
            ImGui.ProgressBar(
                plugin.AFaire == 0 ? 0f : (float)plugin.Faites / plugin.AFaire,
                new Vector2(-1, 6),
                string.Empty);
            ImGui.PopStyleColor();
            ImGui.Spacing();
        }

        Cartes(releves);
        ImGui.Spacing();

        if (lecture) return;

        // Une piece rangee a l'armoire est possedee, mais elle ne sert a aucun
        // glamour tant qu'elle dort la-bas. Le dire ici evite d'aller chercher
        // l'information ailleurs, et n'a rien a faire dans la photo : c'est un
        // conseil, pas un fait de collection.
        var aDeposer = releves.FirstOrDefault(r => r.Cle == "adeposer");
        if (aDeposer is not null && aDeposer.Trouves.Count > 0)
        {
            ImGui.TextColored(Or, Mots.PieceQuiDort(aDeposer.Trouves.Count));
            ImGui.TextColored(Gris, Mots.PieceQuiDortAide);
            ImGui.Spacing();
        }

        // Une ligne empêchée est une lecture qu'on refuse de faire, pas une
        // erreur : elle mérite d'être dite avant le bouton, jamais après.
        var bloquees = releves.Count(r => r.Empeche is not null);
        if (bloquees > 0)
        {
            ImGui.TextColored(Ambre, Mots.NonLues(bloquees));
            ImGui.Spacing();
        }

        if (plugin.EnvoiEnCours)
        {
            ImGui.TextColored(Gris, Mots.EnvoiEnCours);
        }
        else if (ImGui.Button(Mots.Envoyer, new Vector2(220, 30)))
        {
            plugin.Envoyer();
        }

        Retour();
    }

    /// <summary>L'icone du jeu de chaque collection : les memes que dans
    /// l'application, pour qu'on se repere d'un ecran a l'autre.</summary>
    private static readonly Dictionary<string, uint> Icones = new()
    {
        ["mounts"] = 58,
        ["minions"] = 59,
        ["orchestrions"] = 67,
        ["emotes"] = 9,
        ["hairstyles"] = 26178,
        ["fashions"] = 86,
        ["facewear"] = 92,
        ["bardings"] = 49,
        ["cards"] = 27661,
        ["frames"] = 88,
        ["spells"] = 78,
        ["achievements"] = 6,
        ["armoires"] = 52,
        ["outfitpieces"] = 2,
        ["outfits"] = 32,
    };

    private void Icone(string cle, float taille)
    {
        if (!Icones.TryGetValue(cle, out var id))
        {
            ImGui.Dummy(new Vector2(taille, taille));
            return;
        }
        var image = plugin.Textures.GetFromGameIcon(new GameIconLookup(id)).GetWrapOrEmpty();
        ImGui.Image(image.Handle, new Vector2(taille, taille));
    }

    /// <summary>
    /// Le releve en cartes, une par collection : icone, compte, jauge. Une
    /// collection que le jeu n'avait pas chargee porte son conseil au survol et
    /// un bouton pour la relire seule, une fois la bonne fenetre ouverte en jeu.
    /// </summary>
    /// <summary>Les collections que le jeu ne charge qu'a l'ouverture de leur
    /// fenetre. Elles se presentent a part, sous le mot qui dit quoi ouvrir.</summary>
    private static readonly string[] AOuvrir = ["achievements", "armoires", "outfitpieces", "outfits"];

    private void Cartes(IReadOnlyList<Releve> releves)
    {
        var directes = Mots.Collections.Where(c => !AOuvrir.Contains(c.Cle)).ToList();
        var aOuvrir = Mots.Collections.Where(c => AOuvrir.Contains(c.Cle)).ToList();

        Grille(releves, directes);

        // Le mot est visible d'emblee, pas cache dans le survol d'un bouton :
        // c'est lui qui evite de croire a une panne.
        ImGui.Spacing();
        ImGui.TextColored(Or, Mots.AOuvrirTitre);
        ImGui.PushTextWrapPos(0);
        ImGui.TextColored(Gris, Mots.AOuvrirAide);
        ImGui.PopTextWrapPos();
        ImGui.Spacing();
        Grille(releves, aOuvrir);
    }

    private void Grille(IReadOnlyList<Releve> releves, IReadOnlyList<(string Cle, string Nom)> collections)
    {
        const float gap = 8f;
        const float hauteur = 112f;
        var large = ImGui.GetContentRegionAvail().X;
        var parLigne = Math.Max(2, (int)((large + gap) / (196f + gap)));
        var wCarte = (large - gap * (parLigne - 1)) / parLigne;

        var attendu = plugin.EnCours;
        var lecture = plugin.LectureEnCours;
        var i = 0;
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 8f);
        foreach (var (cle, nom) in collections)
        {
            var x = releves.FirstOrDefault(v => v.Cle == cle);
            // Pendant une lecture, les cartes a venir restent visibles : on voit
            // ce qui reste. Hors lecture, une collection jamais lue n'a rien a
            // montrer.
            if (x is null && !plugin.EnFile(cle)) continue;

            if (i % parLigne != 0) ImGui.SameLine(0, gap);
            i++;

            ImGui.BeginChild($"carte-{cle}", new Vector2(wCarte, hauteur), true,
                ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoScrollbar);

            Icone(cle, 22f);
            ImGui.SameLine(0, 7);
            var y = ImGui.GetCursorPosY();
            ImGui.SetCursorPosY(y + 3f);
            if (x is null || x.Empeche is not null) ImGui.TextColored(Gris, nom);
            else ImGui.TextUnformatted(nom);
            Survol(nom);

            ImGui.Spacing();
            if (x is null)
            {
                ImGui.SetWindowFontScale(1.3f);
                if (Plugin.EtapeDe(cle) == attendu) ImGui.TextColored(Or, Mots.Lecture + Points());
                else ImGui.TextColored(Gris, Mots.EnAttente);
                ImGui.SetWindowFontScale(1f);
            }
            else if (x.Empeche is not null)
            {
                ImGui.SetWindowFontScale(1.3f);
                ImGui.TextColored(Ambre, Mots.NonLu);
                ImGui.SetWindowFontScale(1f);
                Survol(x.Empeche);
                ImGui.Spacing();
                ImGui.BeginDisabled(lecture);
                if (ImGui.Button($"{Mots.Relire}##{cle}", new Vector2(-1, 0))) plugin.Relire(cle);
                ImGui.EndDisabled();
                Survol(x.Empeche + "\n\n" + Mots.RelireAide);
            }
            else
            {
                ImGui.SetWindowFontScale(1.3f);
                ImGui.TextColored(x.Trouves.Count > 0 ? Vert : Gris, $"{x.Trouves.Count} / {x.Total}");
                ImGui.SetWindowFontScale(1f);
                if (plugin.EnFile(cle))
                {
                    ImGui.SameLine();
                    ImGui.TextColored(Or, Mots.Verification + Points());
                }
                if (x.Total > 0)
                {
                    var part = Math.Clamp((float)x.Trouves.Count / x.Total, 0f, 1f);
                    ImGui.PushStyleColor(ImGuiCol.PlotHistogram, x.Trouves.Count > 0 ? Vert : Gris);
                    ImGui.ProgressBar(part, new Vector2(-1, 5), string.Empty);
                    ImGui.PopStyleColor();
                }
                ImGui.Spacing();
                Limitation(x);
            }

            ImGui.EndChild();
        }
        ImGui.PopStyleVar();
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
                ImGui.TextColored(Gris, Mots.Verifiables(x.Portee?.Count ?? 0, x.Total));
                Survol(Mots.VerifiablesAide);
                break;
            case Limite.Depot:
                ImGui.TextColored(Gris, Mots.AjoutSeulement);
                Survol(Mots.AjoutSeulementAide);
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

    /// <summary>« . », « .. », « ... » : une attente qui se voit, sans exiger du
    /// jeu un glyphe qu'il n'a peut-être pas.</summary>
    private static string Points() => new('.', 1 + (int)(ImGui.GetTime() * 3) % 3);

    private static void Survol(string texte)
    {
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(texte);
    }

    private void Retour()
    {
        if (plugin.Dernier is not { } retour) return;
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextColored(retour.Ok ? Vert : Ambre, string.Empty);
        ImGui.SameLine(0, 0);
        ImGui.TextWrapped(retour.Message);
        if (!retour.Ok) return;

        if (retour.Ajouts.Count > 0)
        {
            ImGui.TextColored(Vert, Mots.Ajoute);
            ImGui.SameLine();
            ImGui.TextWrapped(string.Join(", ", retour.Ajouts.Select(a => $"{a.Value} {Lisible(a.Key)}")));
        }
        if (retour.Ecarts.Count > 0)
        {
            ImGui.TextColored(Ambre, Mots.ATrancher);
            ImGui.SameLine();
            ImGui.TextWrapped(string.Join(", ", retour.Ecarts.Select(a => $"{a.Value} {Lisible(a.Key)}")));
        }
    }

    // -------------------------------------------------- la page qu'on ouvre une fois

    /// <summary>Vaut 1 quand un bouton demande à passer sur la configuration.</summary>
    private int ongletVoulu = -1;

    private void PageConfiguration()
    {
        var r = plugin.Reglages;
        ImGui.Spacing();

        var contentId = plugin.ContentId;
        if (contentId == 0)
        {
            ImGui.TextColored(Ambre, Mots.PasDePerso);
            return;
        }

        // Un jeton par personnage, et c'est le seul champ de cette page. Le
        // jeton désigne lui-même le personnage qu'il alimente : rien d'autre
        // n'est à recopier depuis l'application.
        var nom = r.Noms.TryGetValue(contentId, out var n) ? n : "?";
        ImGui.TextColored(Or, Mots.JetonDe(nom));
        ImGui.TextWrapped(Mots.JetonExplique);
        ImGui.Spacing();
        ImGui.SetNextItemWidth(-1);
        var jeton = plugin.Jeton;
        if (ImGui.InputTextWithHint("##jeton", Mots.ColleJeton, ref jeton, 200,
                ImGuiInputTextFlags.Password))
        {
            plugin.PoserJeton(jeton);
        }
        ImGui.TextColored(plugin.Jeton.Length > 0 ? Vert : Ambre,
            plugin.Jeton.Length > 0 ? Mots.JetonRange : Mots.PasDeJeton);

        // La langue suit le client de jeu, sauf demande contraire : quelqu'un qui
        // joue en anglais depuis dix ans lit son jeu en anglais.
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextColored(Or, Mots.AvisTitre);
        var avis = r.AvisEnJeu;
        if (ImGui.Checkbox("##avis", ref avis))
        {
            r.AvisEnJeu = avis;
            plugin.Enregistrer();
        }
        ImGui.SameLine();
        ImGui.TextWrapped(Mots.AvisExplique);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(Or, Mots.Langue_);
        ImGui.SetNextItemWidth(200);
        var choix = (int)r.Langue;
        if (ImGui.Combo("##langue", ref choix, $"{Mots.LangueAuto}\0Français\0English\0"))
        {
            plugin.ChoisirLangue((Langue)choix);
        }

        // En bas, discret : ce plugin est gratuit et le restera.
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        if (ImGui.Button(Mots.Soutien)) Dalamud.Utility.Util.OpenLink(Cafe);
        ImGui.SameLine();
        ImGui.TextColored(Gris, Mots.SoutienAide);
    }

    /// <summary>Le pot à café de l'auteur de l'application.</summary>
    private const string Cafe = "https://buymeacoffee.com/derp4kiin";

    // ------------------------------------------------------------------ menu

    /// <summary>Ce qui empêche encore d'envoyer, dit en une phrase.</summary>
    private string? Manque()
    {
        if (plugin.ContentId == 0) return Mots.ManquePerso;
        if (plugin.Jeton.Length == 0) return Mots.ManqueJeton;
        return null;
    }

    private static string Lisible(string cle) =>
        Mots.Collections.FirstOrDefault(n => n.Cle == cle).Nom?.ToLowerInvariant() ?? cle;

}
