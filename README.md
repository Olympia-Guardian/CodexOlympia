# Codex Olympia — plugins Dalamud

Deux plugins, deux métiers, un dépôt.

**Codex Olympia Synchronisation** lit ce que ton personnage a débloqué dans
FINAL FANTASY XIV, tenues et armoire comprises, et l'envoie à
[Codex Olympia](https://olympia-guardian.github.io/) à la demande. Il prévient
aussi dans le journal quand une pièce de tenue arrive dans tes sacs.

**Codex Olympia Automatisation** (EXPÉRIMENTAL, en cours de développement)
compare tes sacs, ton arsenal et tes servants à ta coiffeuse, liste ce qui
reste à déposer, et le range pour toi en suivant exactement les fenêtres du
jeu. Commande : `/codexauto`.

L'adresse du dépôt à coller dans Dalamud est la même pour les deux :

```
https://raw.githubusercontent.com/Olympia-Guardian/codex-olympia-dalamud/main/repo.json
```

La suite de cette notice décrit le plugin de synchronisation.

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
3. Dans Codex Olympia, page **Compte**, section *Plugin Codex Olympia Dalamud* :
   crée un jeton **en choisissant ton personnage**, et copie-le. Il ne s'affiche
   qu'une fois.
4. Dans le jeu : `/codex`, onglet **Configuration**, colle le jeton.

C'est tout : le jeton désigne lui-même le personnage qu'il alimente. Un
personnage de plus, un jeton de plus, et le plugin garde celui de chacun.

## La langue

La fenêtre suit la langue de ton client de jeu : français si tu joues en
français, anglais sinon. Tu peux forcer l'une ou l'autre dans l'onglet
**Configuration**.

## Le jeton

Un jeton de synchronisation ne sait faire qu'une chose : **déposer une photo**,
et pour **un seul personnage**. Il ne peut ni lire ton compte, ni le modifier,
ni l'effacer, ni voir tes groupes ou tes contacts, ni toucher à tes autres
personnages. Il est écrit en clair dans le fichier de configuration du plugin,
comme n'importe quel réglage : c'est précisément pour ça qu'il ne donne aucun
autre droit.

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

Le tableau annonce deux bornes, et elles n'ont rien à voir.

**« 145 sur 398 vérifiables »** : le catalogue ne donne pas d'objet déverrouillant
à toutes les entrées de cette collection, donc le jeu ne sait pas répondre pour le
reste. Ces entrées-là sont laissées tranquilles, ni ajoutées ni signalées.

**« ajout seulement »** : cette collection se constate dans un dépôt, la coiffeuse
ou l'armoire. On y voit ce qui s'y trouve, jamais ce qui n'y est pas. Rien ne sera
donc jamais signalé comme manquant.

Certaines lectures demandent que le jeu ait chargé la donnée au moins une fois
dans la session : ouvre ton armoire chez un rassembleur et ta coiffeuse mirage,
puis relance la lecture. Le tableau te dit quoi ouvrir.

## Ce qui reste à ranger

Une troisième page liste ce que tu possèdes **sans l'avoir déposé** : les pièces
de tenue qui dorment dans tes sacs, ton armurerie, ton cabas, sur toi, ou chez un
servant. Elles ne comptent pour rien tant qu'elles ne sont pas dans ta coiffeuse
ou ton armoire, parce qu'un objet qui traîne peut se vendre ou se jeter.

Les tenues qu'un seul rangement achève passent en tête : c'est là que l'effort
rapporte le plus.

Le sac d'un servant n'est lisible **que pendant qu'on lui parle**. Le plugin
retient donc ce qu'il a vu la dernière fois. Un servant à qui tu n'as jamais
parlé depuis l'installation ne compte pas.

Et quand une pièce de tenue arrive dans tes sacs, un mot dans le journal te dit
à quelle tenue elle appartient et où la mettre. Ça se coupe dans la
configuration.

Dans ton sac, une **pastille** marque les objets qu'il reste à déposer. Le jeu
ne dit pas quel sac une grille affiche : le plugin le reconnaît en comparant les
icônes affichées à celles de chaque sac. Quand rien ne correspond, il ne dessine
rien plutôt que de dessiner faux.

## Ranger tout seul (expérimental)

Sur la page « À ranger », replié, un bouton dépose pour toi ce que tu as sous la
main : les tenues complètes, et les pièces manquantes des tenues déjà déposées.
Une tenue déjà dans la coiffeuse est **complétée**, jamais dupliquée, et les
tenues entamées passent avant les neuves parce que les compléter ne consomme pas
un emplacement de plus.

**La coiffeuse d'abord, toujours.** Un objet rangé à l'armoire quitte
l'inventaire, donc la coiffeuse ne l'aura plus. Une case à cocher, décochée par
défaut, ajoute l'armoire pour ce qu'aucune tenue ne prendra. Une pièce déjà dans
la coiffeuse n'y va jamais : celle qu'on tient est un double, et un double se
vend.

La page liste d'ailleurs ces doubles à part, sans jamais y toucher. Seuls les
sacs comptent : une pièce à l'armurerie est un glamour monté sur un job, et la
dire en trop pousserait à vendre ce qu'on porte tous les jours.

**C'est la seule chose que ce plugin fasse agir dans le jeu.** Tout le reste se
contente de lire la mémoire du client, ce qui ne produit aucun paquet et
n'existe pas pour le serveur. Ici, chaque dépôt est un ordre envoyé. Sache-le
avant de t'en servir.

Quatre garde-fous :

- une opération à la fois, espacée d'une demi-seconde plus une variation ;
- aucune case n'est mémorisée : chaque tâche vise un objet, et sa position est
  retrouvée juste avant d'agir, parce qu'une case change dès qu'un objet en sort ;
- arrêt au premier imprévu, avec la raison affichée ;
- une tenue se dépose **même incomplète** : elle occupe un emplacement, qu'on la
  remplisse en une fois ou en cinq, et ce qui manque s'y ajoutera plus tard.

Le dépôt suit exactement la procédure manuelle : ouvrir la fenêtre de conversion
sur une première pièce, lui tendre les suivantes, valider, transformer,
confirmer. Rien ne la raccourcit, « Transformer » appelé sans sa fenêtre répond
oui et ne fait rien.

**La conversion retire les matérias, les teintures, les mirages et les blasons**,
et remet la symbiose à zéro. C'est le jeu qui le fait, pas le plugin, mais
sache-le avant de lancer.

**Il faut des prismes de mirage** : chaque pièce déposée dans la coiffeuse en
consomme un. Le plugin compte ta réserve et te dit avant de partir s'il en
manque, plutôt que de s'arrêter en route sans expliquer pourquoi.

Reste devant ta coiffeuse ou ton armoire, ouverte, pendant que ça travaille. Le
rangement ne prend que ce qui est **sous la main**, tes sacs et ton arsenal : ce
qui dort chez un servant, va le chercher d'abord.

Une pièce qui n'appartient à aucune tenue et n'a pas de case d'armoire reste où
elle est : le jeu n'offre pas d'appel propre pour déposer une pièce seule dans la
coiffeuse.

## Ce qu'il ne lit pas

- **Les reliques.** Le jeu ne tient pas l'état d'avancement d'une relique. Le
  déduire d'un succès serait deviner, et deviner faux se paie en travail perdu.
- **L'équipement de raid.** Même raison.
- **Ce qui vit dans un inventaire.** Une pièce d'équipement se vend, se jette,
  dort chez un servant. Le plugin ne constate que les **dépôts définitifs** :
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
Le contrat porte sur la photo reçue, pas sur ce plugin : une autre source
pourrait l'envoyer demain sans que rien ne change côté serveur.
