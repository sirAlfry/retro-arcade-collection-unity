# Changelog

Tutte le modifiche rilevanti della versione Unity. Il formato segue [Keep a Changelog](https://keepachangelog.com/it/1.1.0/).

## [3.0.0] — 2026-10-24

Allineamento completo alla [versione Python v3](https://github.com/sirAlfry/retro-arcade-collection) (pygame-ce).

### Aggiunto
- **Creare l'eseguibile con un clic**: *Tools ▸ Retro Arcade ▸ Crea eseguibile Windows / Linux / app macOS*
  (nome, icona, finestra 4:3 ridimensionabile, niente splash, zip pronto da condividere, record conservati tra una build e l'altra).
  Anche da riga di comando con `-executeMethod RetroArcade.EditorTools.RetroArcadeBuild.BuildWindowsCli`.
- Launcher come in Python: sfondo synthwave con griglia prospettica e stelle, tessere con miniature animate,
  pannello informativo con descrizione/comandi/record, crediti, avvio diretto con `1`…`9`, `M` audio, `V` filtro CRT.
- Menu comuni di Python: pausa con `ESC`/`P` (Riprendi · Ricomincia · Audio · Filtro CRT · Torna al menu · Esci,
  scorciatoie `R` `M` `Q`), schermata di fine partita con "nuovo record", menu di scelta (difficoltà, griglie…).
- Font bitmap 5×7 della versione Python, palette neon, particelle, testi fluttuanti, screen shake.
- `F11`/`Alt+Invio` schermo intero, `F12` screenshot, notifiche ("toast") in basso a destra.
- `scores.json` con lo stesso formato e le stesse chiavi di Python (scrittura atomica, file corrotto messo da parte);
  `PlayerPrefs` nel browser.
- Gli effetti sonori di `sfx.py` (37), sintetizzati in codice.
- Logica pura e testata per tutti i giochi: `GameAI` (Minimax, Alpha-Beta), `TetrisLogic` (SRS, 7-bag, lock delay),
  `SnakeLogic`, `MazeLogic`, `MemoryLogic`, `Puzzle15Logic`, `PRect`/`ArcadePhysics`, livelli di Breakout, sprite e scudi
  di Space Invaders: **110 test NUnit**.
- Giochi riscritti seguendo Python: Tris (3 livelli di CPU, 2 giocatori), Snake (frutto bonus), Tetris (hold, ghost, auto-shift DAS/ARR,
  3 anteprime), Pong (CPU che prevede i rimbalzi), Breakout (5 livelli, power-up), Space Invaders (scudi, UFO, marcia a
  4 note), Maze Runner (4 difficoltà, buio con torcia, aiuto `H`), Memory (3 griglie, flip, combo), Puzzle 15
  (scorrimento di intere file, tessere al posto giusto evidenziate).
- `PreviewRenderer`: 23 screenshot giocati da copioni e bot; `tools/crt_preview.py` per l'immagine CRT del README.

### Modificato
- `RetroCanvas`: linee spesse con la stessa geometria di pygame, clip con ritaglio preciso, gradienti, poligoni, archi.
- Il filtro CRT si accende/spegne con `V` (come in Python); `F1` resta per provare le varianti Off/Soft/Full.

### Rimosso
- La vecchia logica derivata dalla versione Phaser (`TicTacToeAI`, `TetrisGame`, `MazeGenerator`, `Puzzle15Board`)
  e i relativi test, sostituiti da quelli nuovi.
- Il tasto `H` "torna al menu" della prima versione: ora si torna al menu dal menu di pausa (`ESC` ▸ `M`),
  e `H` è l'aiuto del labirinto, come in Python.

## [1.0.0]

Primo porting in Unity 6 dalla [versione Phaser 4](https://github.com/sirAlfry/retro-arcade-collection-phaser):
renderer immediate-mode, shader CRT, audio procedurale, record in `PlayerPrefs`, test della logica,
`PreviewRenderer` e workflow CI. Corretti 9 bug dell'originale JavaScript, tra cui il Puzzle 15 che generava solo
scacchiere irrisolvibili.
