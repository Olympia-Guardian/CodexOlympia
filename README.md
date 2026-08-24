# Codex Olympia Synchronisation

Plugin Dalamud pour FINAL FANTASY XIV. Il lit ce que ton personnage a débloqué,
collections, tenues et armoire comprises, et l'envoie à
[Codex Olympia](https://olympia-guardian.github.io/) quand tu le demandes.

Rien ne part sans ton geste, et rien n'est jamais décoché à ta place : les
écarts sont rapportés dans l'application, et c'est toi qui tranches. Il prévient
aussi dans le journal quand une pièce de tenue arrive dans tes sacs.

Ce plugin est **exclusif à Codex Olympia** : il ne parle qu'au serveur du site
et ne lit que son catalogue, les adresses sont figées dans le code. **Sans
compte sur l'application, il ne sert à rien** : c'est là que le jeton se crée.

## English

Dalamud plugin for FINAL FANTASY XIV. It reads what your character has
unlocked, collections, outfits and armoire included, and sends it to
[Codex Olympia](https://olympia-guardian.github.io/) when you ask. Nothing is
sent without your action, and nothing is ever unchecked on your behalf.

Exclusive to the Codex Olympia website: without an account on the app, this
plugin is useless. Create a sync token on the Account page, then paste it in
`/codex`, Settings tab. The window follows your game language (French or
English).

## Installation

1. Dans le jeu : `/xlsettings` → onglet **Experimental** → ajoute cette adresse
   à *Custom Plugin Repositories* :

   ```
   https://raw.githubusercontent.com/Olympia-Guardian/CodexOlympia/main/repo.json
   ```

2. `/xlplugins` → cherche **Codex Olympia Synchronisation** → installe.
3. Dans [Codex Olympia](https://olympia-guardian.github.io/account), page
   **Compte** : crée un jeton en choisissant ton personnage, et copie-le.
   Il ne s'affiche qu'une fois.
4. Dans le jeu : `/codex` → onglet **Configuration** → colle le jeton.

## Développement

Développé avec assistance IA, niveau « Copilot » de la
[politique de Dalamud](https://dalamud.dev/plugin-publishing/ai-policy/) :
le comportement, les décisions et les tests en jeu sont humains.

Sous licence [MIT](LICENSE).
