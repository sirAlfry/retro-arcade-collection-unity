# 🛠️ Strumenti (fuori da Unity)

Due progetti .NET che **non fanno parte del gioco** ma lo rendono più facile da verificare.
Servono solo il [.NET SDK](https://dotnet.microsoft.com/download) 8 o superiore (con il 9 vengono compilati per net9.0).

## `LogicTests/` — i test della logica con `dotnet test`

```bash
dotnet test tools/LogicTests
```

Compila i file di `Assets/RetroArcade/Logic` e `Assets/RetroArcade/Tests/EditMode` **così come sono** (link, non copie)
e li esegue con NUnit 3. Sono gli stessi test che trovi in Unity in *Window ▸ General ▸ Test Runner ▸ EditMode*.
Il linguaggio è bloccato a C# 9, come in Unity 6: se scrivi sintassi più nuova te ne accorgi subito.

## `PreviewRenderer/` — screenshot e "fuzz" senza aprire Unity

```bash
dotnet run --project tools/PreviewRenderer -c Release -- screenshots     # 23 PNG 1600×1200 in ./screenshots
dotnet run --project tools/PreviewRenderer -c Release -- --fuzz 120      # input casuale, 120 s simulati × 5 semi per scena
```

Esegue le **vere** scene C# (`Assets/RetroArcade/Runtime/Games`) con:

- `UnityShim.cs`: un mini-`UnityEngine` in puro C# con i soli tipi usati dalle scene (`Vector2`, `Mathf`, `Rect`, ...);
- `SoftwareRaster.cs`: un rasterizzatore software che disegna i triangoli prodotti da `RetroCanvas`
  (stesso atlante dei font, stesso alpha blending) e salva PNG senza librerie esterne.

`ArcadeApp` e `CrtPresenter` restano fuori perché parlano con la GPU: gli screenshot mostrano quindi l'immagine
"pulita" (modalità CRT *Off*). La modalità `--fuzz` preme tasti e clicca a caso disegnando ogni frame e
segnala qualsiasi eccezione: è un ottimo paracadute quando modifichi un gioco.

Gli screenshot non sono foto "messe in posa": ogni scena viene **giocata** da un piccolo copione (tasti e clic simulati)
e, per Tetris e Snake, da due bot che stanno in `Program.cs` (`PlayTetris` sceglie la mossa migliore con
`TetrisLogic`, `PlaySnake` raggiunge la mela col percorso più breve). Un filtro facoltativo genera solo alcune immagini:

```bash
dotnet run --project tools/PreviewRenderer -c Release -- screenshots puzzle   # solo 18_puzzle15 e 19_puzzle15_risolto
```

## `crt_preview.py` — l'effetto CRT su uno screenshot

```bash
pip install numpy pillow
python tools/crt_preview.py screenshots/01_launcher.png docs/screenshots/00_launcher_crt.png
```

Applica a un'immagine la **stessa formula** di `RetroCRT.shader` in modalità *Full* (curvatura, aberrazione cromatica,
glow, scanline, vignetta): è così che è stata creata l'immagine in cima al README.

