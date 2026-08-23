using System.Numerics;
using Dalamud.Bindings.ImGui;
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
        if (ImGui.BeginTabItem(Mots.PageARanger))
        {
            PageARanger();
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

        Tableau(releves);
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

    private void Tableau(IReadOnlyList<Releve> releves)
    {
        const ImGuiTableFlags style = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH
            | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX;
        if (!ImGui.BeginTable("releves", 4, style)) return;

        ImGui.TableSetupColumn(Mots.ColCollection, ImGuiTableColumnFlags.WidthFixed, 190);
        ImGui.TableSetupColumn(Mots.ColTrouve, ImGuiTableColumnFlags.WidthFixed, 96);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 110);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        var attendu = plugin.EnCours;
        foreach (var (cle, nom) in Mots.Collections)
        {
            var x = releves.FirstOrDefault(v => v.Cle == cle);
            // Pendant la lecture, les lignes pas encore faites restent visibles :
            // le tableau garde sa hauteur, et on voit ce qu'il reste a venir.
            var aVenir = x is null;
            if (aVenir && !plugin.LectureEnCours) continue;

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            if (aVenir || x!.Empeche is not null) ImGui.TextColored(Gris, nom);
            else ImGui.Text(nom);

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            if (aVenir)
            {
                if (cle == attendu) ImGui.TextColored(Or, Mots.Lecture + Points());
                else ImGui.TextColored(Gris, Mots.EnAttente);
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                continue;
            }
            if (x!.Empeche is not null) ImGui.TextColored(Ambre, Mots.NonLu);
            else ImGui.TextColored(x.Trouves.Count > 0 ? Vert : Gris, $"{x.Trouves.Count} / {x.Total}");

            // La barre ne dit rien de neuf : elle rend la colonne lisible d'un
            // coup d'oeil, ce qu'une colonne de fractions ne fait pas.
            ImGui.TableNextColumn();
            if (x!.Empeche is null && x.Total > 0)
            {
                var part = Math.Clamp((float)x.Trouves.Count / x.Total, 0f, 1f);
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, x.Trouves.Count > 0 ? Vert : Gris);
                ImGui.ProgressBar(part, new Vector2(100, 6), string.Empty);
                ImGui.PopStyleColor();
            }

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            if (x!.Empeche is not null) ImGui.TextWrapped(x.Empeche);
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

    // ------------------------------------------------------- ce qui traine

    /// <summary>
    /// Ce qu'on possède sans l'avoir déposé.
    ///
    /// Elle ne coche rien et n'envoie rien : c'est une liste de courses. Un objet
    /// qui traîne dans un sac peut se vendre ou se jeter, le compter comme acquis
    /// serait promettre ce qu'on ne peut pas tenir.
    /// </summary>
    private void PageARanger()
    {
        ImGui.Spacing();

        if (plugin.Coffre is null)
        {
            ImGui.TextWrapped(Mots.ARangerQuoi);
            ImGui.Spacing();
            ImGui.TextColored(Ambre, Mots.ARangerDAbord);
            ImGui.Spacing();
            if (ImGui.Button(Mots.Regarder))
            {
                plugin.Regarder();
                ongletVoulu = 0;
            }
            return;
        }

        var egarees = plugin.ARanger();
        if (egarees.Count == 0)
        {
            ImGui.TextColored(Vert, Mots.ARangerRien);
            ImGui.Spacing();
            ImGui.TextColored(Gris, Mots.ServantsVus(plugin.ServantsDuPerso().Count));
            return;
        }

        // Une ligne de compte, et l'explication au survol : elle se lit une fois,
        // pas à chaque ouverture.
        var achevent = egarees.Count(e => e.Acheve);
        ImGui.TextColored(Or, Mots.ARangerCompte(egarees.Count, achevent));
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        Survol(Mots.ARangerQuoi + "\n\n" + Mots.ServantsVus(plugin.ServantsDuPerso().Count));
        ImGui.Spacing();

        Rangement();

        if (!ImGui.BeginChild("aranger", new Vector2(0, 0), false)) return;

        // D'abord ce qui paie : les tenues qu'un rangement achève. Ensuite le
        // reste, groupé par ENDROIT, parce qu'on range en allant quelque part.
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

        // Ce qu'on tient alors que c'est deja depose : le plugin n'y touche
        // jamais, il le nomme pour qu'on sache que ca ne sert plus a rien.
        var doubles = plugin.Doubles;
        if (doubles.Count > 0)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.SetNextItemOpen(false, ImGuiCond.FirstUseEver);
            if (ImGui.CollapsingHeader($"{Mots.DoublesTitre(doubles.Count)}###doubles"))
            {
                ImGui.TextColored(Gris, Mots.DoublesAide);
                ImGui.Spacing();
                if (ImGui.BeginTable("dbl", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                {
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
            }
        }
        ImGui.EndChild();
    }

    /// <summary>
    /// Le rangement automatique, replié et signalé pour ce qu'il est.
    ///
    /// C'est la seule commande du plugin qui agisse sur le jeu. Elle ne se
    /// déclenche pas par mégarde : il faut déplier, lire, et appuyer.
    /// </summary>
    private void Rangement()
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
        var coffre = plugin.Coffre;
        if (cat is null || coffre is null) return;

        // La liste se bâtit au moment où on la demande : un inventaire d'il y a
        // trente secondes ne vaut rien pour décider de déplacer des objets.
        var aussi = plugin.Reglages.RangerArmoire;
        if (ImGui.Checkbox("##armoire", ref aussi))
        {
            plugin.Reglages.RangerArmoire = aussi;
            plugin.Enregistrer();
        }
        ImGui.SameLine();
        ImGui.TextWrapped(Mots.RangeurAussiArmoire);
        ImGui.Spacing();

        // Ce que le bouton ferait, avant qu'on l'appuie. Sans ça, un rangement
        // qui n'a rien à ranger ressemble à un bouton cassé.
        var apercu = plugin.Apercu();
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
        ImGui.Spacing();

        if (ImGui.Button(Mots.RangeurLancer)) r.Demarrer(apercu);
        if (r.Etat == EtatRangement.Fini && r.Total > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(Vert, Mots.RangeurFini(r.Faits));
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
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
        var pastilles = r.Pastilles;
        if (ImGui.Checkbox("##pastilles", ref pastilles))
        {
            r.Pastilles = pastilles;
            plugin.Enregistrer();
        }
        ImGui.SameLine();
        ImGui.TextWrapped(Mots.PastillesExplique);

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
