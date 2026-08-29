// Le catalogue de l'application : CodexOlympia seul.
//
// repo.json liste tous les plugins de la maison, y compris ceux qui automatisent
// le jeu. L'application, elle, ne doit proposer que le plugin de
// synchronisation : c'est le seul dont elle ait besoin, et le seul qu'elle
// assume en public. Ce script extrait son entrée dans codex-olympia.json, que
// l'application donne comme adresse de dépôt Dalamud.
//
//   node scripts/catalogue-app.mjs     à relancer après chaque bump de repo.json
import { readFileSync, writeFileSync } from 'node:fs'

const tous = JSON.parse(readFileSync('repo.json', 'utf8'))
const seul = tous.filter((p) => p.InternalName === 'CodexOlympia')
if (seul.length !== 1) throw new Error(`repo.json : ${seul.length} entrée(s) CodexOlympia, une attendue`)
writeFileSync('codex-olympia.json', JSON.stringify(seul, null, 2) + '\n')
console.log(`codex-olympia.json : CodexOlympia ${seul[0].AssemblyVersion}`)
