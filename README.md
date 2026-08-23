# Codex Olympia — greffon Dalamud

Lit ce que ton personnage a débloqué dans FINAL FANTASY XIV et l'envoie à
[Codex Olympia](https://olympia-guardian.github.io/), à la demande.

## Ce qu'il fait

Tu cliques, il regarde, il te montre, tu envoies. C'est tout.

- **Il n'envoie jamais rien de lui-même.** Pas de minuterie, pas de
  synchronisation de fond, pas de rapport journalier. Une synchronisation qui
  part toute seule est une synchronisation qu'on ne relit jamais.
- **Il te montre avant d'envoyer.** Le tableau donne, collection par collection,
  ce qu'il a trouvé et sur combien. Si un chiffre te paraît faux, tu n'envoies
  pas.
- **Il n'écrit rien dans le jeu.** Il lit, il envoie, c'est tout. Ce qui n'est
  pas encore chargé, il te demande de l'ouvrir : il ne le demande pas au client
  à ta place.
- **Il ne décoche rien.** Ce que l'application a en plus de ce qu'il a trouvé
  devient un écart, rapporté dans les notifications de Codex Olympia. C'est toi
  qui tranches, là-bas, à tête reposée.

## Installation

1. Dans le jeu : `/xlsettings`, onglet **Experimental**, ajoute ce dépôt à
   *Custom Plugin Repositories* :

   ```
   https://raw.githubusercontent.com/Olympia-Guardian/codex-olympia-dalamud/main/repo.json
   ```

2. `/xlplugins`, cherche **Codex Olympia**, installe.
3. Dans Codex Olympia, page **Compte**, section *Greffon de synchronisation* :
   crée un jeton et copie-le. Il ne s'affiche qu'une fois.
4. Dans le jeu : `/codex`, ouvre les **Réglages**, colle le jeton.
5. Toujours dans `/codex` : colle l'**identifiant Lodestone** de ton personnage.
   Le jeu ne connaît pas le Lodestone, c'est à toi de faire le lien. Ouvre ta
   fiche sur le Lodestone : le nombre à la fin de l'adresse est cet identifiant.

## Le jeton

Un jeton de synchronisation ne sait faire qu'une chose : **déposer une photo**.
Il ne peut ni lire ton compte, ni le modifier, ni l'effacer, ni voir tes groupes
ou tes contacts. Il est écrit en clair dans le fichier de configuration du
greffon, comme n'importe quel réglage : c'est précisément pour ça qu'il ne donne
aucun autre droit.

Il se révoque depuis la page de compte, sans toucher à tes sessions.

## Ce qu'il lit, et comment

| Collection | Lu depuis |
| --- | --- |
| Montures, mascottes, rouleaux, emotes, accessoires de mode, cartes | l'état de déverrouillage tenu par le jeu |
| Succès | la liste des succès accomplis |
| Armoire | le contenu de ton armoire |
| Coiffures, lunettes, bardes, portraits | l'objet qui les déverrouille |
| Sorts bleus | le carnet de mage bleu |
| Pièces de tenue, tenues entières | la coiffeuse mirage et l'armoire |

Trois de ces collections ne sont lisibles qu'en partie : le catalogue ne donne
pas d'objet déverrouillant à toutes leurs entrées. Le greffon **déclare alors ce
qu'il a regardé**, et l'application ne conclut rien sur le reste. Le tableau te
le dit, ligne par ligne.

Certaines lectures demandent que le jeu ait chargé la donnée au moins une fois
dans la session : ouvre ton armoire chez un rassembleur et ta coiffeuse mirage,
puis relance la lecture. Le tableau te dit quoi ouvrir.

## Ce qu'il ne lit pas

- **Les reliques.** Le jeu ne tient pas l'état d'avancement d'une relique. Le
  déduire d'un succès serait deviner, et deviner faux se paie en travail perdu.
- **L'équipement de raid.** Même raison.
- **Ce qui vit dans un inventaire.** Une pièce d'équipement se vend, se jette,
  dort chez un servant. Le greffon ne constate que les **dépôts définitifs** :
  la coiffeuse et l'armoire. Un dépôt prouve la possession, il ne prouve jamais
  l'absence, et c'est pour ça que l'application dit « non trouvée » et jamais
  « tu ne l'as pas ».

## Compiler

Il faut le SDK .NET 10 et une installation de Dalamud (XIVLauncher).

```
dotnet build -c Release CodexOlympia/CodexOlympia.csproj
```

Le paquet se trouve dans `CodexOlympia/bin/Release/CodexOlympia/latest.zip`.

## Le contrat

Le comportement attendu est écrit dans la spécification de l'application,
module 13 (`spec/13-plugin.md` du dépôt
[olympia-guardian.github.io](https://github.com/Olympia-Guardian/olympia-guardian.github.io)).
Le contrat porte sur la photo reçue, pas sur ce greffon : une autre source
pourrait l'envoyer demain sans que rien ne change côté serveur.
