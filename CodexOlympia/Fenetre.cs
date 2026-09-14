using System.Numerics;
using CodexOlympia.Ui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace CodexOlympia;

/// <summary>
/// La fenêtre, habillée comme l'application (PLG-R41 à R43).
///
/// Elle dessine tout elle-même, barre de titre comprise : Dalamud ne lui met
/// plus de cadre et ImGui plus de marge.
///
/// L'ordre de la page du jour ne change pas : on regarde, on lit, on envoie.
/// Rien ne part tant que le joueur n'a pas vu ce qui partira, et c'est la seule
/// protection contre une lecture qui se tromperait.
/// </summary>
public sealed class Fenetre : Window, IDisposable
{
    private readonly Plugin plugin;

    /// <summary>La page ouverte : 0 le jour, 1 les réglages, 2 à propos.</summary>
    private int page;

    private IDisposable? theme;

    private static float E => Peinture.Echelle;

    private const float HTitre = 46f;
    private const float HPied = 54f;
    private const float LRail = 54f;
    private const float Marge = 14f;

    public Fenetre(Plugin plugin) : base(
        "Codex Olympia###codex-olympia",
        ImGuiWindowFlags.NoTitleBar
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoScrollWithMouse
        | ImGuiWindowFlags.NoCollapse)
    {
        this.plugin = plugin;
        Size = new Vector2(820, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 420),
            MaximumSize = new Vector2(1600, 1600),
        };
        // Les gadgets de Dalamud se poseraient sur notre barre de titre.
        AllowPinning = false;
        AllowClickthrough = false;
    }

    public void Dispose() { }

    public override void PreDraw() => theme = Theme.Pousser();

    public override void PostDraw()
    {
        theme?.Dispose();
        theme = null;
    }

    public override void Draw()
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetWindowPos();
        var taille = ImGui.GetWindowSize();

        Ambiance(dl, pos, pos + taille);
        BarreDeTitre(dl, pos, taille.X);

        var hautCorps = pos.Y + HTitre * E;
        var hCorps = MathF.Max(40f, taille.Y - (HTitre + HPied) * E);
        Rail(dl, new Vector2(pos.X, hautCorps), hCorps);
        Corps(new Vector2(pos.X + LRail * E, hautCorps), new Vector2(taille.X - LRail * E, hCorps));
        Pied(dl, new Vector2(pos.X, pos.Y + taille.Y - HPied * E), taille.X);
    }

    private static void Ambiance(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var l = max.X - min.X;
        var h = max.Y - min.Y;
        dl.PushClipRect(min, max, true);
        Peinture.Tache(dl, min + new Vector2(l * 0.14f, h * 0.02f), l * 0.38f, Teintes.Or, 0.05f);
        Peinture.Tache(dl, min + new Vector2(l * 0.92f, h * 0.10f), l * 0.42f, Teintes.Bleu, 0.035f);
        dl.PopClipRect();
    }

    private void BarreDeTitre(ImDrawListPtr dl, Vector2 pos, float largeur)
    {
        var h = HTitre * E;
        var fin = pos + new Vector2(largeur, h);
        Peinture.Plein(dl, pos, fin, Teintes.Surface, Teintes.RondFenetre * E, ImDrawFlags.RoundCornersTop);
        Peinture.Filet(dl, new Vector2(pos.X, fin.Y - 0.5f), new Vector2(fin.X, fin.Y - 0.5f));

        // Sans barre de titre, ImGui ne déplace plus la fenêtre : on le fait.
        var largeurPrise = largeur - 70f * E;
        ImGui.SetCursorScreenPos(pos);
        ImGui.InvisibleButton("##deplacer", new Vector2(MathF.Max(1f, largeurPrise), h));
        if (ImGui.IsItemActive())
        {
            var delta = ImGui.GetIO().MouseDelta;
            if (delta != Vector2.Zero) ImGui.SetWindowPos(ImGui.GetWindowPos() + delta, ImGuiCond.Always);
        }

        var cote = 24f * E;
        var logo = new Vector2(pos.X + 14f * E, pos.Y + (h - cote) * 0.5f);
        Peinture.Degrade(dl, logo, logo + new Vector2(cote), new Vector4(0.23f, 0.20f, 0.15f, 1f),
            Teintes.Surface, 6f * E);
        Peinture.Contour(dl, logo, logo + new Vector2(cote), Teintes.Alpha(Teintes.Or, 0.45f), 6f * E);
        Texte.Milieu("C", logo, logo + new Vector2(cote), Teintes.Or);

        var x = logo.X + cote + 10f * E;
        var nom = "Codex Olympia";
        Texte.A(nom, new Vector2(x, pos.Y + (h - ImGui.GetTextLineHeight()) * 0.5f), Teintes.Encre);
        x += Texte.Mesurer(nom).X + 10f * E;

        var (mot, teinte, bat) = Etat();
        ImGui.SetCursorScreenPos(new Vector2(x, pos.Y + (h - (ImGui.GetTextLineHeight() + 6f * E)) * 0.5f));
        Pieces.Pastille(mot, teinte, bat);

        var bouton = 26f * E;
        ImGui.SetCursorScreenPos(new Vector2(fin.X - 12f * E - bouton, pos.Y + (h - bouton) * 0.5f));
        if (BoutonIcone("##fermer", FontAwesomeIcon.Times, bouton, Mots.Fermer)) IsOpen = false;
    }

    /// <summary>L'état en deux mots, lisible sans entrer dans la fenêtre
    /// (PLG-R41).</summary>
    private (string Mot, Vector4 Teinte, bool Bat) Etat()
    {
        if (plugin.ContentId == 0) return (Mots.EtatPerso, Teintes.Discret, false);
        if (plugin.Jeton.Length == 0) return (Mots.EtatJeton, Teintes.Ambre, false);
        if (plugin.LectureEnCours) return (Mots.EtatLecture, Teintes.Bleu, true);
        if (plugin.EnvoiEnCours) return (Mots.EtatEnvoi, Teintes.Bleu, true);
        if (plugin.Releves.Count == 0) return (Mots.EtatARegarder, Teintes.Discret, false);
        var neuf = plugin.Nouveautes().Sum(n => n.Ids.Count);
        return neuf > 0 ? (Mots.EtatPret, Teintes.Or, false) : (Mots.EtatAJour, Teintes.Vert, false);
    }

    private static bool BoutonIcone(string id, FontAwesomeIcon icone, float cote, string aide)
    {
        var origine = ImGui.GetCursorScreenPos();
        var g = Pieces.Zone(id, new Vector2(cote));
        var dl = ImGui.GetWindowDrawList();
        var chaud = Mouvement.Survol(id + "#survol", g.Dessus);
        if (chaud > 0.01f)
            Peinture.Plein(dl, origine, origine + new Vector2(cote),
                Teintes.Alpha(Teintes.Surface2, chaud), 6f * E);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            Texte.Milieu(icone.ToIconString(), origine, origine + new Vector2(cote),
                Vector4.Lerp(Teintes.Discret, Teintes.Encre, chaud));
        }
        Pieces.Infobulle(aide);
        return g.Clic;
    }

    private void Rail(ImDrawListPtr dl, Vector2 pos, float hauteur)
    {
        var l = LRail * E;
        Peinture.Plein(dl, pos, pos + new Vector2(l, hauteur), Teintes.Surface, 0f);
        Peinture.Filet(dl, new Vector2(pos.X + l - 0.5f, pos.Y + 8f * E),
            new Vector2(pos.X + l - 0.5f, pos.Y + hauteur - 8f * E));

        var cote = 38f * E;
        var y = pos.Y + 10f * E;
        (FontAwesomeIcon, string)[] pages =
        [
            (FontAwesomeIcon.SyncAlt, Mots.PageSync),
            (FontAwesomeIcon.SlidersH, Mots.PageConfig),
            (FontAwesomeIcon.InfoCircle, Mots.PageAPropos),
        ];
        for (var i = 0; i < pages.Length; i++)
        {
            var (icone, nom) = pages[i];
            var origine = new Vector2(pos.X + (l - cote) * 0.5f, y);
            ImGui.SetCursorScreenPos(origine);
            var g = Pieces.Zone($"##rail{i}", new Vector2(cote));
            var actif = page == i;
            var chaud = Mouvement.Survol($"##rail{i}#survol", g.Dessus);
            if (actif || chaud > 0.01f)
                Peinture.Plein(dl, origine, origine + new Vector2(cote),
                    Teintes.Alpha(Teintes.Surface2, actif ? 1f : chaud * 0.7f), Teintes.RondTuile * E);
            if (actif)
                Peinture.Plein(dl, new Vector2(pos.X, origine.Y + 8f * E),
                    new Vector2(pos.X + 3f * E, origine.Y + cote - 8f * E), Teintes.Or, 2f * E);
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                Texte.Milieu(icone.ToIconString(), origine, origine + new Vector2(cote),
                    actif ? Teintes.Or : Vector4.Lerp(Teintes.Discret, Teintes.Encre2, chaud));
            }
            Pieces.Infobulle(nom);
            if (g.Clic) page = i;
            y += cote + 4f * E;
        }
    }

    private void Corps(Vector2 pos, Vector2 taille)
    {
        ImGui.SetCursorScreenPos(pos);
        using var marge = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(Marge, Marge) * E);
        using var enfant = ImRaii.Child("##page", taille, false, ImGuiWindowFlags.AlwaysUseWindowPadding);
        if (!enfant) return;
        switch (page)
        {
            case 1:
                PageReglages();
                break;
            case 2:
                PageAPropos();
                break;
            default:
                PageDuJour();
                break;
        }
    }

    private void PageDuJour()
    {
        if (Manque() is { } manque)
        {
            CarteMot(manque, Teintes.Ambre);
            return;
        }
        if (plugin.Catalogue?.Pret != true)
        {
            CarteMot(Mots.CataloguePasPret, Teintes.Ambre);
            return;
        }

        CarteDeTete();
        ImGui.Dummy(new Vector2(0, 4f * E));
        Compteurs();
        ImGui.Dummy(new Vector2(0, 4f * E));

        if (plugin.Releves.Count == 0 && !plugin.LectureEnCours)
        {
            ImGui.PushTextWrapPos(0);
            ImGui.TextColored(Teintes.Encre2, Mots.Presentation);
            ImGui.PopTextWrapPos();
            return;
        }

        PiecesQuiDorment();
        CeQuiAttend();
        Collections();
        Retour();
    }

    /// <summary>Le conseil des pièces qui dorment (PLG-R42) : un conseil, pas
    /// un fait de collection, il n'entre jamais dans la photo.</summary>
    private void PiecesQuiDorment()
    {
        var dort = plugin.Releves.FirstOrDefault(r => r.Cle == "adeposer");
        if (dort is null || dort.Trouves.Count == 0) return;
        CarteTitree(Mots.PieceQuiDort(dort.Trouves.Count), Mots.PieceQuiDortAide, null);
    }

    /// <summary>Une carte qui ne porte qu'une phrase. Le geste qui répare est
    /// dans le pied.</summary>
    private void CarteMot(string mot, Vector4 teinte)
    {
        var large = ImGui.GetContentRegionAvail().X;
        var origine = ImGui.GetCursorScreenPos();
        var h = Texte.HauteurRepliee(mot, large - 32f * E) + 28f * E;
        ImGui.Dummy(new Vector2(large, h));
        var dl = ImGui.GetWindowDrawList();
        Peinture.Carte(dl, origine, origine + new Vector2(large, h), Teintes.RondCarte * E);
        Texte.Coupe(mot, origine + new Vector2(16f * E, 14f * E), large - 32f * E, teinte);
    }

    /// <summary>La carte de tête (PLG-R42) : le portrait, le nom, où en est la
    /// lecture, les étapes, et l'anneau de l'avancement.</summary>
    private void CarteDeTete()
    {
        var large = ImGui.GetContentRegionAvail().X;
        var origine = ImGui.GetCursorScreenPos();
        var h = 112f * E;
        ImGui.Dummy(new Vector2(large, h));
        var fin = origine + new Vector2(large, h);
        var dl = ImGui.GetWindowDrawList();
        Peinture.Carte(dl, origine, fin, Teintes.RondCarte * E);

        var x = origine.X + 16f * E;

        // Pas de portrait, pas de trou ni de mot d'excuse (PLG-R43).
        if (plugin.Visage.Image is { } image)
        {
            var lp = 64f * E;
            var hp = 88f * E;
            var coin = new Vector2(x, origine.Y + (h - hp) * 0.5f);
            Peinture.ImageCouvrante(dl, image.Handle, image.Width, image.Height,
                coin, coin + new Vector2(lp, hp), Teintes.RondTuile * E);
            Peinture.Contour(dl, coin, coin + new Vector2(lp, hp), Teintes.Filet, Teintes.RondTuile * E);
            x += lp + 14f * E;
        }

        var lues = plugin.Releves.Count(r => r.Cle != "adeposer" && r.Empeche is null);
        var total = Math.Max(1, Mots.Collections.Length);
        var rayon = 34f * E;
        var centre = new Vector2(fin.X - 22f * E - rayon, origine.Y + h * 0.5f);
        var part = Mouvement.Vers("##anneau-tete", Math.Clamp((float)lues / total, 0f, 1f));
        Pieces.Anneau(centre, rayon, 6f * E, part, Teintes.Avancement(lues, total),
            $"{lues} / {total}", Mots.MotCollections, plugin.LectureEnCours);

        var droite = centre.X - rayon - 16f * E;
        var largeurTexte = MathF.Max(60f * E, droite - x);

        var nom = plugin.Visage.Qui?.Nom
                  ?? (plugin.Reglages.Noms.TryGetValue(plugin.ContentId, out var n) ? n : string.Empty);
        var monde = plugin.Visage.Qui?.Monde;
        var y = origine.Y + 18f * E;
        Texte.A(Texte.Tronquer(nom, largeurTexte), new Vector2(x, y), Teintes.Or);
        if (!string.IsNullOrEmpty(monde))
        {
            var apres = x + Texte.Mesurer(Texte.Tronquer(nom, largeurTexte)).X + 8f * E;
            if (apres + Texte.Mesurer(monde).X < droite) Texte.A(monde, new Vector2(apres, y), Teintes.Discret);
        }
        y += ImGui.GetTextLineHeight() + 4f * E;

        Texte.A(Texte.Tronquer(Phrase(), largeurTexte), new Vector2(x, y), Teintes.Encre2);

        Pieces.Etapes(new Vector2(x, fin.Y - 18f * E - Pieces.HauteurEtapes()), largeurTexte, Etapes());
    }

    /// <summary>Où l'on en est, en une phrase.</summary>
    private string Phrase()
    {
        if (plugin.LectureEnCours)
        {
            var quoi = plugin.EnCours;
            var nom = Mots.Collections.FirstOrDefault(c => Plugin.EtapeDe(c.Cle) == quoi).Nom;
            return nom is null ? Mots.OnRecupere + Points() : $"{Mots.Lecture} : {nom.ToLowerInvariant()}{Points()}";
        }
        if (plugin.EnvoiEnCours) return Mots.EnvoiEnCours;
        if (plugin.EnVerification) return Mots.VerificationAttente + Points();
        if (plugin.Releves.Count == 0) return Mots.Presentation;
        var neuf = plugin.Nouveautes().Sum(x => x.Ids.Count);
        return neuf > 0 ? Mots.NouveautesTitre(neuf) : Mots.RienDeNeuf;
    }

    private List<(string, Pieces.Etat)> Etapes()
    {
        var lu = plugin.Releves.Count > 0;
        var neuf = lu && !plugin.LectureEnCours ? plugin.Nouveautes().Sum(x => x.Ids.Count) : 0;
        Pieces.Etat regarder, verifier, envoyer;
        if (plugin.LectureEnCours)
        {
            regarder = Pieces.Etat.EnCours;
            verifier = envoyer = Pieces.Etat.AVenir;
        }
        else if (!lu)
        {
            regarder = Pieces.Etat.AVenir;
            verifier = envoyer = Pieces.Etat.AVenir;
        }
        else if (plugin.EnVerification)
        {
            regarder = Pieces.Etat.Faite;
            verifier = Pieces.Etat.EnCours;
            envoyer = Pieces.Etat.AVenir;
        }
        else
        {
            regarder = Pieces.Etat.Faite;
            verifier = Pieces.Etat.Faite;
            envoyer = plugin.EnvoiEnCours || neuf > 0 ? Pieces.Etat.EnCours : Pieces.Etat.Faite;
        }
        return
        [
            (Mots.EtapeRegarder, regarder),
            (Mots.EtapeVerifier, verifier),
            (Mots.EtapeEnvoyer, envoyer),
        ];
    }

    private void Compteurs()
    {
        var ecart = 8f * E;
        var large = ImGui.GetContentRegionAvail().X;
        var l = (large - ecart * 3f) / 4f;

        var lues = plugin.Releves.Count(r => r.Cle != "adeposer" && r.Empeche is null);
        var total = Mots.Collections.Length;
        var reste = Math.Max(0, total - lues);
        Pieces.TuileStat("##c1", Mots.MotCollections, $"{lues} / {total}",
            reste == 0 ? Mots.ToutesLues : Mots.ResteN(reste),
            reste == 0 ? Teintes.Vert : Teintes.Bleu, l);
        ImGui.SameLine(0, ecart);

        var enLecture = plugin.LectureEnCours;
        var neuf = enLecture ? -1 : plugin.Nouveautes().Sum(x => x.Ids.Count);
        Pieces.TuileStat("##c2", Mots.MotNouveautes, neuf < 0 ? "—" : neuf.ToString(),
            neuf < 0 ? Mots.ApresLecture : Mots.DepuisEnvoi,
            neuf > 0 ? Teintes.Or : Teintes.Discret, l);
        ImGui.SameLine(0, ecart);

        Pieces.TuileStat("##c3", Mots.MotDernierEnvoi, Mots.IlYA(plugin.Visage.Qui?.Derniere), string.Empty,
            plugin.Visage.Qui?.Derniere is > 0 ? Teintes.Vert : Teintes.Discret, l);
        ImGui.SameLine(0, ecart);

        var bloquees = plugin.Releves.Count(r => r.Empeche is not null);
        if (bloquees > 0)
        {
            Pieces.TuileStat("##c4", Mots.MotNonLues, bloquees.ToString(), Mots.FenetreAOuvrir, Teintes.Ambre, l);
        }
        else if (plugin.Reglages.SyncAuto)
        {
            Pieces.TuileStat("##c4", Mots.MotSyncAuto, Mots.Dans(plugin.ProchaineLectureDans), Mots.SyncAllumee,
                Teintes.Bleu, l);
        }
        else
        {
            Pieces.TuileStat("##c4", Mots.MotSyncAuto, Mots.SyncEteinte, string.Empty, Teintes.Discret, l);
        }
    }

    /// <summary>Ce qui attend d'être envoyé (PLG-R38) : avant le bouton qui
    /// l'envoie, jamais après.</summary>
    private void CeQuiAttend()
    {
        if (plugin.LectureEnCours) return;
        var neuf = plugin.Nouveautes();
        var total = neuf.Sum(n => n.Ids.Count);
        if (total == 0) return;

        Titre(Mots.CeQuiAttend, Mots.NouveautesTitre(total));
        var large = ImGui.GetContentRegionAvail().X;
        var origine = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);
        ImGui.SetCursorScreenPos(origine + new Vector2(14f * E, 12f * E));
        ImGui.BeginGroup();
        if (plugin.JamaisEnvoye)
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + large - 28f * E);
            ImGui.TextColored(Teintes.Encre2, Mots.NouveautesJamais);
            ImGui.PopTextWrapPos();
        }
        else
        {
            const int parCollection = 8;
            var cat = plugin.Catalogue;
            foreach (var (cle, ids) in neuf)
            {
                var noms = ids.Take(parCollection).Select(id => cat?.Nom(cle, id) ?? $"#{id}");
                var suite = ids.Count > parCollection ? " " + Mots.EtAutres(ids.Count - parCollection) : string.Empty;
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + large - 28f * E);
                ImGui.TextColored(Teintes.Discret, $"{Lisible(cle)} ({ids.Count}) : ");
                ImGui.SameLine(0, 0);
                ImGui.TextColored(Teintes.Encre2, string.Join(", ", noms) + suite);
                ImGui.PopTextWrapPos();
            }
        }
        ImGui.EndGroup();
        var bas = ImGui.GetItemRectMax().Y + 12f * E;
        dl.ChannelsSetCurrent(0);
        Peinture.Carte(dl, origine, new Vector2(origine.X + large, bas), Teintes.RondCarte * E,
            Teintes.Surface, Teintes.Alpha(Teintes.Or, 0.28f));
        dl.ChannelsMerge();
        ImGui.SetCursorScreenPos(new Vector2(origine.X, bas));
        ImGui.Dummy(new Vector2(large, 6f * E));
    }

    private static void Titre(string mot, string aide)
    {
        ImGui.Dummy(new Vector2(0, 2f * E));
        var origine = ImGui.GetCursorScreenPos();
        var h = ImGui.GetTextLineHeight();
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, h + 4f * E));
        Texte.PetitesCapitales(mot, origine, Teintes.Encre2);
        if (aide.Length > 0)
            Texte.A(aide, new Vector2(origine.X + Texte.LargeurPetitesCapitales(mot) + 10f * E, origine.Y),
                Teintes.Discret);
    }

    /// <summary>Celles que le jeu ne charge qu'à l'ouverture de leur fenêtre :
    /// elles se présentent à part (PLG-R34).</summary>
    private static readonly string[] AOuvrir = ["achievements", "armoires", "outfitpieces", "outfits"];

    private void Collections()
    {
        var directes = Mots.Collections.Where(c => !AOuvrir.Contains(c.Cle)).ToList();
        var aOuvrir = Mots.Collections.Where(c => AOuvrir.Contains(c.Cle)).ToList();

        Titre(Mots.LuesSeules, Mots.NCollections(directes.Count));
        Grille(directes);

        Titre(Mots.AOuvrirTitre, Mots.NCollections(aOuvrir.Count));
        ImGui.PushTextWrapPos(0);
        ImGui.TextColored(Teintes.Discret, Mots.AOuvrirAide);
        ImGui.PopTextWrapPos();
        ImGui.Dummy(new Vector2(0, 2f * E));
        Grille(aOuvrir);
    }

    private void Grille(IReadOnlyList<(string Cle, string Nom)> collections)
    {
        var ecart = 8f * E;
        var large = ImGui.GetContentRegionAvail().X;
        var colonnes = Math.Max(1, (int)((large + ecart) / (228f * E + ecart)));
        var l = (large - ecart * (colonnes - 1)) / colonnes;

        var i = 0;
        foreach (var (cle, nom) in collections)
        {
            var x = plugin.Releves.FirstOrDefault(v => v.Cle == cle);
            // En lecture, les collections à venir gardent leur tuile.
            if (x is null && !plugin.EnFile(cle)) continue;
            if (i % colonnes != 0) ImGui.SameLine(0, ecart);
            i++;
            Tuile(cle, nom, x, l);
        }
        if (i == 0) ImGui.TextColored(Teintes.Discret, Mots.EnAttente);
    }

    /// <summary>La tuile d'une collection, celle de Mon Codex (PLG-R42). Une
    /// collection non lue devient elle-même le bouton qui la relit : un bouton
    /// de plus ne tiendrait pas à cette taille.</summary>
    private void Tuile(string cle, string nom, Releve? x, float largeur)
    {
        var h = 62f * E;
        var origine = ImGui.GetCursorScreenPos();
        var empechee = x?.Empeche is not null;
        var relisible = empechee && !plugin.LectureEnCours;
        var g = relisible
            ? Pieces.Zone($"##tuile-{cle}", new Vector2(largeur, h))
            : default;
        if (!relisible) ImGui.Dummy(new Vector2(largeur, h));
        var fin = origine + new Vector2(largeur, h);
        var dl = ImGui.GetWindowDrawList();

        var attente = x is null;
        var finie = x is not null && !empechee && Lisibles(x) > 0 && x.Trouves.Count >= Lisibles(x);
        var bord = finie ? Teintes.Alpha(Teintes.Vert, 0.35f)
            : empechee ? Teintes.Alpha(Teintes.Ambre, 0.35f)
            : Teintes.Filet;
        var chaud = relisible ? Mouvement.Survol($"##tuile-{cle}#survol", g.Dessus) : 0f;
        Peinture.Carte(dl, origine, fin, Teintes.RondTuile * E,
            Teintes.Melanger(Teintes.Surface2, Teintes.Encre, chaud * 0.06f), bord);

        var rayon = 19f * E;
        var centre = new Vector2(origine.X + 10f * E + rayon + 2f * E, origine.Y + h * 0.5f);
        var fait = x?.Trouves.Count ?? 0;
        var lisibles = x is null ? 0 : Lisibles(x);
        var teinte = attente || empechee ? Teintes.Filet : Teintes.Avancement(fait, lisibles);
        var part = lisibles > 0 && !empechee ? Math.Clamp((float)fait / lisibles, 0f, 1f) : 0f;
        Peinture.Anneau(dl, centre, rayon, 3.5f * E, part, teinte);
        if (attente && plugin.EnFile(cle) && Plugin.EtapeDe(cle) == plugin.EnCours)
            Peinture.Comete(dl, centre, rayon, 3.5f * E, Teintes.Bleu);
        Icone(dl, cle, centre, 22f * E, attente || empechee ? 0.45f : 1f);

        var x0 = centre.X + rayon + 12f * E;
        var largeurTexte = fin.X - 10f * E - x0;
        var y = origine.Y + 13f * E;
        Texte.A(Texte.Tronquer(nom, largeurTexte), new Vector2(x0, y),
            attente || empechee ? Teintes.Discret : Teintes.Encre);
        y += ImGui.GetTextLineHeight() + 2f * E;

        if (attente)
        {
            Texte.A(plugin.EnFile(cle) && Plugin.EtapeDe(cle) == plugin.EnCours
                ? Mots.Lecture + Points()
                : Mots.EnAttente, new Vector2(x0, y), Teintes.Discret);
        }
        else if (empechee)
        {
            Texte.A(Texte.Tronquer(Mots.NonLu, largeurTexte), new Vector2(x0, y), Teintes.Ambre);
        }
        else
        {
            var compte = $"{fait} / {lisibles}";
            Texte.A(Texte.Tronquer(compte, largeurTexte), new Vector2(x0, y),
                fait > 0 ? Teintes.Encre2 : Teintes.Discret);
            if (plugin.EnFile(cle) || (plugin.EnVerification && plugin.Douteuse(cle)))
            {
                var apres = x0 + Texte.Mesurer(compte).X + 8f * E;
                if (apres < fin.X - 10f * E) Texte.A(Mots.Verification + Points(), new Vector2(apres, y), Teintes.Or);
            }
        }

        Pieces.Infobulle(Aide(cle, nom, x, relisible));
        if (relisible && g.Clic) plugin.Relire(cle);
    }

    /// <summary>Ce que le plugin sait lire : le catalogue entier, ou la portée
    /// déclarée. Dire « 0 / 398 » à qui possède tout ce qui se lit serait
    /// faux.</summary>
    private static int Lisibles(Releve x) =>
        x.Limite == Limite.Capacite && x.Portee is not null ? x.Portee.Count : x.Total;

    /// <summary>Ce que la tuile n'a pas la place de dire.</summary>
    private static string Aide(string cle, string nom, Releve? x, bool relisible)
    {
        var lignes = new List<string> { nom };
        if (x is null) return string.Join("\n", lignes);
        if (x.Empeche is not null)
        {
            lignes.Add(x.Empeche);
            if (relisible) lignes.Add(Mots.RelireAide);
            return string.Join("\n\n", lignes);
        }
        switch (x.Limite)
        {
            case Limite.Capacite:
                // Deux raisons de sortir de la portée, une seule se coche à
                // la main (PLG-R44).
                var hors = x.Total - (x.Portee?.Count ?? 0) - x.NonLues;
                if (hors > 0)
                {
                    lignes.Add(Mots.AuJournal(hors));
                    lignes.Add(Mots.VerifiablesAide);
                }
                if (x.NonLues > 0) lignes.Add(Mots.PasEncoreLues(x.NonLues));
                break;
            case Limite.Depot:
                lignes.Add(Mots.AjoutSeulement);
                lignes.Add(Mots.AjoutSeulementAide);
                break;
        }
        if (x.Note is not null) lignes.Add(x.Note);
        _ = cle;
        return string.Join("\n\n", lignes);
    }

    /// <summary>Les mêmes icônes que l'application, pour se repérer d'un écran
    /// à l'autre.</summary>
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
        ["beastmaster"] = 62143,
        ["achievements"] = 6,
        ["quests"] = 61412,
        ["armoires"] = 52,
        ["outfitpieces"] = 2,
        ["outfits"] = 32,
    };

    private void Icone(ImDrawListPtr dl, string cle, Vector2 centre, float cote, float alpha)
    {
        if (!Icones.TryGetValue(cle, out var id)) return;
        var image = plugin.Textures.GetFromGameIcon(new GameIconLookup(id)).GetWrapOrEmpty();
        var demi = new Vector2(cote * 0.5f);
        dl.AddImage(image.Handle, centre - demi, centre + demi, Vector2.Zero, Vector2.One,
            Peinture.Col(new Vector4(1f, 1f, 1f, alpha)));
    }

    /// <summary>Ce que le serveur a répondu au dernier envoi.</summary>
    private void Retour()
    {
        if (plugin.Dernier is not { } retour) return;
        ImGui.Dummy(new Vector2(0, 4f * E));
        var large = ImGui.GetContentRegionAvail().X;
        var origine = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);
        ImGui.SetCursorScreenPos(origine + new Vector2(14f * E, 12f * E));
        ImGui.BeginGroup();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + large - 28f * E);
        ImGui.TextColored(retour.Ok ? Teintes.Encre : Teintes.Ambre, retour.Message);
        if (retour.Ok && retour.Ajouts.Count > 0)
        {
            ImGui.TextColored(Teintes.Vert, Mots.Ajoute);
            ImGui.SameLine();
            ImGui.TextColored(Teintes.Encre2,
                string.Join(", ", retour.Ajouts.Select(a => $"{a.Value} {Lisible(a.Key)}")));
        }
        if (retour.Ok && retour.Ecarts.Count > 0)
        {
            ImGui.TextColored(Teintes.Ambre, Mots.ATrancher);
            ImGui.SameLine();
            ImGui.TextColored(Teintes.Encre2,
                string.Join(", ", retour.Ecarts.Select(a => $"{a.Value} {Lisible(a.Key)}")));
        }
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();
        var bas = ImGui.GetItemRectMax().Y + 12f * E;
        dl.ChannelsSetCurrent(0);
        Peinture.Carte(dl, origine, new Vector2(origine.X + large, bas), Teintes.RondCarte * E);
        dl.ChannelsMerge();
        ImGui.SetCursorScreenPos(new Vector2(origine.X, bas));
        ImGui.Dummy(new Vector2(large, 4f * E));
    }

    private void Pied(ImDrawListPtr dl, Vector2 pos, float largeur)
    {
        var h = HPied * E;
        var fin = pos + new Vector2(largeur, h);
        Peinture.Plein(dl, pos, fin, Teintes.Surface, Teintes.RondFenetre * E, ImDrawFlags.RoundCornersBottom);
        Peinture.Filet(dl, new Vector2(pos.X, pos.Y + 0.5f), new Vector2(fin.X, pos.Y + 0.5f));

        var y = pos.Y + (h - 34f * E) * 0.5f;
        ImGui.SetCursorScreenPos(new Vector2(pos.X + Marge * E, y));
        switch (page)
        {
            case 1:
                PiedReglages();
                break;
            case 2:
                PiedAPropos(fin);
                break;
            default:
                PiedDuJour(fin);
                break;
        }
    }

    private void PiedDuJour(Vector2 fin)
    {
        if (plugin.ContentId == 0)
        {
            Note(fin, Mots.ManquePerso);
            return;
        }
        if (plugin.Jeton.Length == 0)
        {
            if (Pieces.BoutonOr("##aller-reglages", Mots.AllerConfig)) page = 1;
            Note(fin, Mots.ManqueJeton);
            return;
        }
        if (plugin.Catalogue?.Pret != true)
        {
            if (Pieces.BoutonFantome("##reessayer", Mots.Reessayer)) plugin.RechargerCatalogue();
            return;
        }
        if (plugin.LectureEnCours)
        {
            Pieces.BoutonOr("##lecture", Mots.EtatLecture, 0f, false);
            Note(fin, Phrase());
            return;
        }
        if (plugin.EnvoiEnCours || plugin.EnVerification)
        {
            Pieces.BoutonOr("##attente", Mots.Envoyer, 0f, false);
            Note(fin, Phrase());
            return;
        }

        var neuf = plugin.Releves.Count > 0 ? plugin.Nouveautes().Sum(x => x.Ids.Count) : 0;
        if (plugin.Releves.Count == 0)
        {
            if (Pieces.BoutonOr("##regarder", Mots.Regarder)) plugin.RegarderAJour();
            Note(fin, Mots.RienNePart);
            return;
        }
        if (neuf > 0)
        {
            if (Pieces.BoutonOr("##envoyer", Mots.EnvoyerN(neuf))) plugin.Envoyer();
            ImGui.SameLine(0, 8f * E);
            if (Pieces.BoutonFantome("##regarder2", Mots.RegarderCourt)) plugin.RegarderAJour();
            Note(fin, Mots.RienNePart);
            return;
        }
        if (Pieces.BoutonFantome("##regarder3", Mots.Regarder)) plugin.RegarderAJour();
        Note(fin, Mots.RienDeNeuf);
    }

    private void PiedReglages()
    {
        var jeton = plugin.Jeton.Length > 0;
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(jeton ? Teintes.Vert : Teintes.Ambre, jeton ? Mots.JetonRange : Mots.PasDeJeton);
    }

    private static void PiedAPropos(Vector2 fin)
    {
        if (Pieces.BoutonFantome("##discord", Mots.Discord, 0f, true, 34f,
                new Vector4(0.45f, 0.50f, 0.98f, 1f)))
            Dalamud.Utility.Util.OpenLink(Discord);
        ImGui.SameLine(0, 8f * E);
        if (Pieces.BoutonFantome("##bugs", Mots.Bugs, 0f, true, 34f, Teintes.Rouge))
            Dalamud.Utility.Util.OpenLink(Bugs);
        ImGui.SameLine(0, 8f * E);
        if (Pieces.BoutonFantome("##cafe", Mots.Soutien, 0f, true, 34f, Teintes.Vert))
            Dalamud.Utility.Util.OpenLink(Cafe);
        Pieces.Infobulle(Mots.SoutienAide);
        _ = fin;
    }

    /// <summary>Le mot du pied, à droite : il rassure, il ne demande rien.</summary>
    private static void Note(Vector2 fin, string mot)
    {
        var t = Texte.Mesurer(mot);
        var x = fin.X - Marge * E - t.X;
        var y = fin.Y - (HPied * E + t.Y) * 0.5f;
        if (x > ImGui.GetCursorScreenPos().X + 12f * E) Texte.A(mot, new Vector2(x, y), Teintes.Discret);
    }

    private void PageReglages()
    {
        var r = plugin.Reglages;
        if (plugin.ContentId == 0)
        {
            CarteMot(Mots.PasDePerso, Teintes.Ambre);
            return;
        }

        var nom = r.Noms.TryGetValue(plugin.ContentId, out var n) ? n : "?";
        CarteTitree(Mots.JetonDe(nom), Mots.JetonExplique, () =>
        {
            ImGui.SetNextItemWidth(-1);
            var jeton = plugin.Jeton;
            if (ImGui.InputTextWithHint("##jeton", Mots.ColleJeton, ref jeton, 200, ImGuiInputTextFlags.Password))
                plugin.PoserJeton(jeton);
        });

        CarteTitree(Mots.SyncAutoTitre, Mots.SyncAutoExplique, null, () =>
        {
            var auto = r.SyncAuto;
            if (Pieces.Interrupteur("##auto", ref auto))
            {
                r.SyncAuto = auto;
                plugin.Enregistrer();
            }
        });

        CarteTitree(Mots.AvisTitre, Mots.AvisExplique, null, () =>
        {
            var avis = r.AvisEnJeu;
            if (Pieces.Interrupteur("##avis", ref avis))
            {
                r.AvisEnJeu = avis;
                plugin.Enregistrer();
            }
        });

        CarteTitree(Mots.Langue_, string.Empty, () =>
        {
            ImGui.SetNextItemWidth(220f * E);
            var choix = (int)r.Langue;
            if (ImGui.Combo("##langue", ref choix, $"{Mots.LangueAuto}\0Français\0English\0"))
                plugin.ChoisirLangue((Langue)choix);
        });
    }

    /// <summary>
    /// Une carte titrée : réglages, conseils et page « à propos » en sont tous
    /// faits.
    ///
    /// La hauteur n'est pas connue d'avance — le texte se replie selon la
    /// largeur — alors on dessine le contenu d'abord sur un calque, on mesure,
    /// et on peint le fond derrière. C'est la recette des cartes d'ImGui.
    /// </summary>
    private void CarteTitree(string titre, string aide, Action? dessous, Action? aCote = null)
    {
        var large = ImGui.GetContentRegionAvail().X;
        var origine = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);
        ImGui.SetCursorScreenPos(origine + new Vector2(14f * E, 12f * E));
        ImGui.BeginGroup();
        if (aCote is not null)
        {
            aCote();
            ImGui.SameLine(0, 10f * E);
        }
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Teintes.Or, titre);
        if (aide.Length > 0)
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + large - 28f * E);
            ImGui.TextColored(Teintes.Discret, aide);
            ImGui.PopTextWrapPos();
        }
        if (dessous is not null)
        {
            ImGui.Dummy(new Vector2(0, 2f * E));
            dessous();
        }
        ImGui.EndGroup();
        var bas = ImGui.GetItemRectMax().Y + 12f * E;
        dl.ChannelsSetCurrent(0);
        Peinture.Carte(dl, origine, new Vector2(origine.X + large, bas), Teintes.RondCarte * E);
        dl.ChannelsMerge();
        ImGui.SetCursorScreenPos(new Vector2(origine.X, bas));
        ImGui.Dummy(new Vector2(large, 8f * E));
    }

    /// <summary>Le Discord de l'appli, où l'on pose ses questions.</summary>
    private const string Discord = "https://discord.gg/vG4sMjjHFy";

    /// <summary>Le forum des bugs, sur ce Discord : un post par bug.</summary>
    private const string Bugs = "https://discord.com/channels/1542459890524749864/1542472939427864706";

    /// <summary>Le pot à café de l'auteur de l'application.</summary>
    private const string Cafe = "https://buymeacoffee.com/derp4kiin";

    private void PageAPropos()
    {
        CarteTitree("Codex Olympia", Mots.Presentation, () =>
        {
            if (Pieces.BoutonFantome("##site", "codex-olympia.com", 0f, true, 34f, Teintes.Or))
                Dalamud.Utility.Util.OpenLink("https://codex-olympia.com/");
        });
        CarteTitree(Mots.SyncAutoTitre, Mots.SyncAutoExplique, null);
        CarteTitree(Mots.AOuvrirTitre, Mots.AOuvrirAide, null);

        // La version, tout en bas, discrète : ce qu'on demande quand on
        // signale un bug.
        var version = typeof(Fenetre).Assembly.GetName().Version;
        if (version is not null)
        {
            ImGui.Dummy(new Vector2(0, 2f * E));
            ImGui.TextColored(Teintes.Discret, $"v{version.Major}.{version.Minor}.{version.Build}");
        }
    }

    /// <summary>« . », « .. », « ... » : une attente qui se voit, sans exiger
    /// du jeu un glyphe qu'il n'a peut-être pas.</summary>
    private static string Points() => new('.', 1 + (int)(ImGui.GetTime() * 3) % 3);

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
