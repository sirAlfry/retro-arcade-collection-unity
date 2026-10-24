using System;
using System.Collections.Generic;

namespace RetroArcade.Logic
{
    /// <summary>Un tetromino in campo: tipo ('I', 'J', ...), posizione e rotazione (quarti di giro orari).</summary>
    public sealed class TetrisPiece
    {
        public readonly char Kind;
        public int X, Y, Rotation;

        public TetrisPiece(char kind, int x, int y, int rotation = 0)
        {
            Kind = kind;
            X = x;
            Y = y;
            Rotation = rotation;
        }

        public int[][] Matrix => TetrisLogic.Rotated(Kind, Rotation);

        /// <summary>Coordinate di campo (x, y) dei quattro quadratini.</summary>
        public List<Int2> Cells()
        {
            var cells = new List<Int2>(4);
            var m = Matrix;
            for (int r = 0; r < m.Length; r++)
                for (int c = 0; c < m[r].Length; c++)
                    if (m[r][c] != 0) cells.Add(new Int2(X + c, Y + r));
            return cells;
        }
    }

    /// <summary>
    /// Tutte le regole del Tetris "moderno" (porting di <c>TetrisLogic</c> di <c>tetris.py</c>), senza grafica:
    /// <list type="bullet">
    /// <item>generatore 7-bag (niente più 5 pezzi S di fila);</item>
    /// <item>rotazione SRS con le tabelle ufficiali dei wall kick;</item>
    /// <item>lock delay di 0,5 s con "move reset" (massimo 15 rinvii);</item>
    /// <item>hold, pezzo fantasma, coda di anteprima;</item>
    /// <item>punteggio guideline (100/300/500/800 × livello, punti per soft/hard drop);</item>
    /// <item>curva di gravità guideline, un livello ogni 10 linee.</item>
    /// </list>
    /// Si guida con i metodi (Move, Rotate, HardDrop...) e con <see cref="Update"/>.
    /// </summary>
    public sealed class TetrisLogic
    {
        public const int Columns = 10, RowsCount = 20;
        public const float LockDelay = 0.5f;
        public const int MaxLockResets = 15;
        public const float ClearAnimTime = 0.32f;

        /// <summary>Orientamento di partenza di ogni pezzo (riquadri SRS).</summary>
        public static readonly Dictionary<char, int[][]> Shapes = new Dictionary<char, int[][]>
        {
            ['I'] = new[] { new[] { 0, 0, 0, 0 }, new[] { 1, 1, 1, 1 }, new[] { 0, 0, 0, 0 }, new[] { 0, 0, 0, 0 } },
            ['J'] = new[] { new[] { 1, 0, 0 }, new[] { 1, 1, 1 }, new[] { 0, 0, 0 } },
            ['L'] = new[] { new[] { 0, 0, 1 }, new[] { 1, 1, 1 }, new[] { 0, 0, 0 } },
            ['O'] = new[] { new[] { 1, 1 }, new[] { 1, 1 } },
            ['S'] = new[] { new[] { 0, 1, 1 }, new[] { 1, 1, 0 }, new[] { 0, 0, 0 } },
            ['T'] = new[] { new[] { 0, 1, 0 }, new[] { 1, 1, 1 }, new[] { 0, 0, 0 } },
            ['Z'] = new[] { new[] { 1, 1, 0 }, new[] { 0, 1, 1 }, new[] { 0, 0, 0 } },
        };

        /// <summary>Ordine dei pezzi in un sacchetto (come <c>list(SHAPES)</c>).</summary>
        public static readonly char[] Kinds = { 'I', 'J', 'L', 'O', 'S', 'T', 'Z' };

        /// <summary>Colori 0xRRGGBB dei pezzi.</summary>
        public static readonly Dictionary<char, uint> Colors = new Dictionary<char, uint>
        {
            ['I'] = 0x00e6ff, ['J'] = 0x3c6eff, ['L'] = 0xff961e, ['O'] = 0xffe128,
            ['S'] = 0x3ce65a, ['T'] = 0xb446ff, ['Z'] = 0xff3c50,
        };

        // Wall kick SRS (dx, dy) con la y verso l'ALTO, come nelle tabelle ufficiali.
        private static readonly Dictionary<(int, int), Int2[]> KicksJLSTZ = new Dictionary<(int, int), Int2[]>
        {
            [(0, 1)] = K((0, 0), (-1, 0), (-1, 1), (0, -2), (-1, -2)),
            [(1, 0)] = K((0, 0), (1, 0), (1, -1), (0, 2), (1, 2)),
            [(1, 2)] = K((0, 0), (1, 0), (1, -1), (0, 2), (1, 2)),
            [(2, 1)] = K((0, 0), (-1, 0), (-1, 1), (0, -2), (-1, -2)),
            [(2, 3)] = K((0, 0), (1, 0), (1, 1), (0, -2), (1, -2)),
            [(3, 2)] = K((0, 0), (-1, 0), (-1, -1), (0, 2), (-1, 2)),
            [(3, 0)] = K((0, 0), (-1, 0), (-1, -1), (0, 2), (-1, 2)),
            [(0, 3)] = K((0, 0), (1, 0), (1, 1), (0, -2), (1, -2)),
        };

        private static readonly Dictionary<(int, int), Int2[]> KicksI = new Dictionary<(int, int), Int2[]>
        {
            [(0, 1)] = K((0, 0), (-2, 0), (1, 0), (-2, -1), (1, 2)),
            [(1, 0)] = K((0, 0), (2, 0), (-1, 0), (2, 1), (-1, -2)),
            [(1, 2)] = K((0, 0), (-1, 0), (2, 0), (-1, 2), (2, -1)),
            [(2, 1)] = K((0, 0), (1, 0), (-2, 0), (1, -2), (-2, 1)),
            [(2, 3)] = K((0, 0), (2, 0), (-1, 0), (2, 1), (-1, -2)),
            [(3, 2)] = K((0, 0), (-2, 0), (1, 0), (-2, -1), (1, 2)),
            [(3, 0)] = K((0, 0), (1, 0), (-2, 0), (1, -2), (-2, 1)),
            [(0, 3)] = K((0, 0), (-1, 0), (2, 0), (-1, 2), (2, -1)),
        };

        private static Int2[] K(params (int x, int y)[] v)
        {
            var r = new Int2[v.Length];
            for (int i = 0; i < v.Length; i++) r[i] = new Int2(v[i].x, v[i].y);
            return r;
        }

        public static readonly int[] LineScores = { 0, 100, 300, 500, 800 };
        public static readonly string[] LineNames = { "", "SINGLE", "DOUBLE", "TRIPLE", "TETRIS!" };

        /// <summary>Ruota una matrice di 90° in senso orario.</summary>
        public static int[][] Rotate(int[][] shape)
        {
            int rows = shape.Length, cols = shape[0].Length;
            var r = new int[cols][];
            for (int c = 0; c < cols; c++)
            {
                r[c] = new int[rows];
                for (int i = 0; i < rows; i++) r[c][i] = shape[rows - 1 - i][c];
            }
            return r;
        }

        private static readonly Dictionary<(char, int), int[][]> RotCache = new Dictionary<(char, int), int[][]>();

        /// <summary>Matrice del pezzo dopo <paramref name="rotation"/> quarti di giro orari.</summary>
        public static int[][] Rotated(char kind, int rotation)
        {
            rotation = ((rotation % 4) + 4) % 4;
            if (kind == 'O') rotation = 0;
            if (RotCache.TryGetValue((kind, rotation), out var m)) return m;
            m = Shapes[kind];
            for (int i = 0; i < rotation; i++) m = Rotate(m);
            RotCache[(kind, rotation)] = m;
            return m;
        }

        /// <summary>Secondi per riga al livello dato (curva della guideline).</summary>
        public static float GravityInterval(int level)
        {
            level = Math.Max(1, level);
            return (float)Math.Max(0.02, Math.Pow(0.8 - (level - 1) * 0.007, level - 1));
        }

        // ── Stato ──

        private readonly Random _rng;
        public readonly int Cols, Rows;
        /// <summary>[riga, colonna]: tipo del pezzo che occupa la casella, '\0' = vuota.</summary>
        public char[,] Board;
        private readonly List<char> _bag = new List<char>();
        public readonly List<char> Queue = new List<char>();
        public TetrisPiece Piece;
        public char? Hold;
        public bool CanHold = true;
        public int Score, Lines, Level = 1;
        public bool GameOver;
        public float GravityTimer, LockTimer;
        public int LockResets;
        public int LowestY = -99;
        public List<int> ClearingRows = new List<int>();
        public float ClearTimer;
        private readonly List<string> _events = new List<string>();

        public TetrisLogic(Random rng = null, int cols = Columns, int rows = RowsCount)
        {
            _rng = rng ?? new Random();
            Cols = cols;
            Rows = rows;
            Board = new char[rows, cols];
            FillQueue();
            Spawn();
        }

        // ── Pezzi ──

        private char NextFromBag()
        {
            if (_bag.Count == 0)
            {
                _bag.AddRange(Kinds);
                for (int i = _bag.Count - 1; i > 0; i--) // Fisher-Yates, come random.shuffle
                {
                    int j = _rng.Next(i + 1);
                    (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
                }
            }
            char k = _bag[_bag.Count - 1];
            _bag.RemoveAt(_bag.Count - 1);
            return k;
        }

        private void FillQueue()
        {
            while (Queue.Count < 5) Queue.Add(NextFromBag());
        }

        /// <summary>Mette un nuovo pezzo in cima. False (e fine partita) se non c'è spazio.</summary>
        public bool Spawn(char? kind = null)
        {
            char k;
            if (kind.HasValue) k = kind.Value;
            else
            {
                k = Queue[0];
                Queue.RemoveAt(0);
                FillQueue();
            }
            int width = Shapes[k][0].Length;
            int x = k != 'O' ? (Cols - width) / 2 : Cols / 2 - 1;
            int y = k == 'I' ? -1 : 0;
            Piece = new TetrisPiece(k, x, y);
            GravityTimer = 0f;
            LockTimer = 0f;
            LockResets = 0;
            LowestY = Piece.Y;
            if (Collides(Piece.Matrix, x, y))
            {
                GameOver = true;
                _events.Add("gameover");
                return false;
            }
            return true;
        }

        /// <summary>
        /// True se la matrice in (x, y) tocca un muro, il fondo o un blocco. Le righe sopra il campo (y &lt; 0)
        /// contano come vuote, così i pezzi possono ruotare anche se sporgono in alto.
        /// </summary>
        public bool Collides(int[][] matrix, int x, int y)
        {
            for (int r = 0; r < matrix.Length; r++)
                for (int c = 0; c < matrix[r].Length; c++)
                {
                    if (matrix[r][c] == 0) continue;
                    int bx = x + c, by = y + r;
                    if (bx < 0 || bx >= Cols || by >= Rows) return true;
                    if (by >= 0 && Board[by, bx] != '\0') return true;
                }
            return false;
        }

        public bool OnGround => Piece != null && Collides(Piece.Matrix, Piece.X, Piece.Y + 1);

        private void AfterSuccessfulMove()
        {
            if (Piece == null) return;
            if (Piece.Y > LowestY)
            {
                LowestY = Piece.Y;
                LockResets = 0;
            }
            if (LockResets < MaxLockResets)
            {
                LockTimer = 0f;
                LockResets++;
            }
        }

        // ── Azioni ──

        public bool Move(int dx)
        {
            var p = Piece;
            if (p == null || GameOver || Collides(p.Matrix, p.X + dx, p.Y)) return false;
            p.X += dx;
            AfterSuccessfulMove();
            _events.Add("move");
            return true;
        }

        /// <summary>Ruota in senso orario (1) o antiorario (-1) provando i wall kick SRS.</summary>
        public bool Rotate(int direction = 1)
        {
            var p = Piece;
            if (p == null || GameOver || p.Kind == 'O') return false;
            int newRot = ((p.Rotation + direction) % 4 + 4) % 4;
            var matrix = Rotated(p.Kind, newRot);
            var table = p.Kind == 'I' ? KicksI : KicksJLSTZ;
            foreach (var kick in table[(p.Rotation, newRot)])
            {
                int nx = p.X + kick.X, ny = p.Y - kick.Y;
                if (Collides(matrix, nx, ny)) continue;
                p.X = nx;
                p.Y = ny;
                p.Rotation = newRot;
                AfterSuccessfulMove();
                _events.Add("rotate");
                return true;
            }
            return false;
        }

        /// <summary>Scende di una riga (+1 punto).</summary>
        public bool SoftDrop()
        {
            var p = Piece;
            if (p == null || GameOver || Collides(p.Matrix, p.X, p.Y + 1)) return false;
            p.Y++;
            Score++;
            GravityTimer = 0f;
            AfterSuccessfulMove();
            return true;
        }

        public int GhostY()
        {
            var p = Piece;
            if (p == null) return 0;
            int y = p.Y;
            while (!Collides(p.Matrix, p.X, y + 1)) y++;
            return y;
        }

        /// <summary>Caduta immediata fino in fondo e blocco (+2 punti per riga).</summary>
        public int HardDrop()
        {
            var p = Piece;
            if (p == null || GameOver) return 0;
            int distance = GhostY() - p.Y;
            p.Y += distance;
            Score += 2 * distance;
            _events.Add("harddrop");
            Lock();
            return distance;
        }

        /// <summary>Scambia il pezzo con quello in "hold" (una volta per pezzo).</summary>
        public bool HoldPiece()
        {
            if (Piece == null || !CanHold || GameOver) return false;
            char current = Piece.Kind;
            if (Hold == null)
            {
                Hold = current;
                Spawn();
            }
            else
            {
                char swap = Hold.Value;
                Hold = current;
                Spawn(swap);
            }
            CanHold = false;
            _events.Add("hold");
            return true;
        }

        // ── Regole ──

        private bool RowFull(int y)
        {
            for (int x = 0; x < Cols; x++) if (Board[y, x] == '\0') return false;
            return true;
        }

        private void Lock()
        {
            var p = Piece;
            if (p == null) return;
            bool aboveTop = false;
            foreach (var c in p.Cells())
            {
                if (c.Y < 0) aboveTop = true;
                else Board[c.Y, c.X] = p.Kind;
            }
            Piece = null;
            CanHold = true;
            _events.Add("lock");
            if (aboveTop) // "lock out": il pezzo si è fermato fuori dal campo
            {
                GameOver = true;
                _events.Add("gameover");
                return;
            }
            var full = new List<int>();
            for (int y = 0; y < Rows; y++) if (RowFull(y)) full.Add(y);
            if (full.Count > 0)
            {
                ClearingRows = full;
                ClearTimer = ClearAnimTime;
                int n = full.Count;
                Score += LineScores[Math.Min(n, 4)] * Level;
                Lines += n;
                _events.Add("lines" + n);
                int newLevel = 1 + Lines / 10;
                if (newLevel > Level)
                {
                    Level = newLevel;
                    _events.Add("levelup");
                }
            }
            else
            {
                Spawn();
            }
        }

        private void FinishClear()
        {
            var keep = new List<char[]>();
            for (int y = 0; y < Rows; y++)
            {
                if (ClearingRows.Contains(y)) continue;
                var row = new char[Cols];
                for (int x = 0; x < Cols; x++) row[x] = Board[y, x];
                keep.Add(row);
            }
            var board = new char[Rows, Cols];
            int offset = Rows - keep.Count; // righe vuote in cima
            for (int i = 0; i < keep.Count; i++)
                for (int x = 0; x < Cols; x++)
                    board[offset + i, x] = keep[i][x];
            Board = board;
            ClearingRows = new List<int>();
            Spawn();
        }

        /// <summary>Avanza il tempo (secondi): gravità, lock delay e animazione delle righe.</summary>
        public void Update(float dt, bool softDrop = false)
        {
            if (GameOver) return;
            if (ClearingRows.Count > 0)
            {
                ClearTimer -= dt;
                if (ClearTimer <= 0f) FinishClear();
                return;
            }
            if (Piece == null) return;
            float interval = GravityInterval(Level);
            if (softDrop) interval = Math.Min(interval, 0.035f);
            GravityTimer += dt;
            while (GravityTimer >= interval && Piece != null)
            {
                GravityTimer -= interval;
                if (OnGround)
                {
                    GravityTimer = 0f;
                    break;
                }
                Piece.Y++;
                if (softDrop) Score++;
                if (Piece.Y > LowestY)
                {
                    LowestY = Piece.Y;
                    LockResets = 0;
                    LockTimer = 0f;
                }
            }
            if (Piece != null && OnGround)
            {
                LockTimer += dt;
                if (LockTimer >= LockDelay) Lock();
            }
            else
            {
                LockTimer = 0f;
            }
        }

        /// <summary>Restituisce (e svuota) gli eventi prodotti dall'ultima chiamata.</summary>
        public List<string> PopEvents()
        {
            var e = new List<string>(_events);
            _events.Clear();
            return e;
        }
    }
}
