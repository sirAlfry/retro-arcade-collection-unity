using System.Collections.Generic;

namespace RetroArcade.Logic
{
    /// <summary>Una casella del Tris: riga e colonna 0..2.</summary>
    public readonly struct Cell : System.IEquatable<Cell>
    {
        public readonly int Row, Col;
        public Cell(int row, int col) { Row = row; Col = col; }
        public bool Equals(Cell o) => Row == o.Row && Col == o.Col;
        public override bool Equals(object o) => o is Cell c && Equals(c);
        public override int GetHashCode() => Row * 3 + Col;
        public override string ToString() => $"({Row}, {Col})";
    }

    /// <summary>
    /// Stato del Tris per <see cref="GameAI{TMove}"/>: porting di <c>TicTacToeState</c>.
    /// Celle: 1 = X, -1 = O, 0 = vuota. Immutabile: ogni mossa crea un nuovo stato.
    /// </summary>
    public sealed class TicTacToeState : IGameState<Cell>
    {
        public const int X = 1, O = -1, Empty = 0;

        private readonly int[] _cells; // 9 celle riga per riga
        private List<Cell> _legal;

        public int CurrentPlayer { get; }

        public TicTacToeState(int currentPlayer = X) : this(new int[9], currentPlayer) { }

        /// <param name="board">Matrice 3×3 (viene copiata).</param>
        public TicTacToeState(int[,] board, int currentPlayer) : this(Flatten(board), currentPlayer) { }

        private TicTacToeState(int[] cells, int currentPlayer)
        {
            _cells = cells;
            CurrentPlayer = currentPlayer;
        }

        private static int[] Flatten(int[,] b)
        {
            var c = new int[9];
            for (int r = 0; r < 3; r++)
                for (int col = 0; col < 3; col++)
                    c[r * 3 + col] = b[r, col];
            return c;
        }

        public int this[int row, int col] => _cells[row * 3 + col];

        public IReadOnlyList<Cell> LegalMoves()
        {
            if (_legal != null) return _legal;
            _legal = new List<Cell>(9);
            for (int i = 0; i < 9; i++) if (_cells[i] == Empty) _legal.Add(new Cell(i / 3, i % 3));
            return _legal;
        }

        public IGameState<Cell> MakeMove(Cell move)
        {
            var cells = (int[])_cells.Clone();
            cells[move.Row * 3 + move.Col] = CurrentPlayer;
            return new TicTacToeState(cells, -CurrentPlayer);
        }

        public TicTacToeState Play(Cell move) => (TicTacToeState)MakeMove(move);

        public bool IsTerminal() => Winner() != Empty || LegalMoves().Count == 0;

        public GameResult Result(int player)
        {
            int w = Winner();
            if (w == Empty) return LegalMoves().Count == 0 ? GameResult.Draw : GameResult.Ongoing;
            return w == player ? GameResult.Win : GameResult.Loss;
        }

        /// <summary>Chiave compatta (base 3 + giocatore) per la tabella di trasposizione.</summary>
        public object Key
        {
            get
            {
                int k = 0;
                foreach (int v in _cells) k = k * 3 + (v + 1);
                return k * 3 + (CurrentPlayer + 1);
            }
        }

        public static readonly int[][] Lines =
        {
            new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 },
            new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 },
            new[] { 0, 4, 8 }, new[] { 2, 4, 6 },
        };

        /// <summary>1 (X), -1 (O) oppure 0 se nessuno ha vinto.</summary>
        public int Winner() => Winner(out _);

        public int Winner(out int[] line)
        {
            foreach (var l in Lines)
            {
                int a = _cells[l[0]];
                if (a != Empty && a == _cells[l[1]] && a == _cells[l[2]])
                {
                    line = l;
                    return a;
                }
            }
            line = null;
            return Empty;
        }

        /// <summary>Valutazione posizionale: linee aperte, controllo del centro e degli angoli.</summary>
        public double Evaluate(int player)
        {
            int w = Winner();
            if (w == player) return 100;
            if (w == -player) return -100;
            double score = 0;
            foreach (var l in Lines) score += EvaluateLine(l, player);
            if (_cells[4] == player) score += 3;
            foreach (int corner in new[] { 0, 2, 6, 8 }) if (_cells[corner] == player) score += 2;
            return score;
        }

        private double EvaluateLine(int[] line, int player)
        {
            int mine = 0, theirs = 0, empty = 0;
            foreach (int i in line)
            {
                if (_cells[i] == player) mine++;
                else if (_cells[i] == -player) theirs++;
                else empty++;
            }
            if (mine > 0 && theirs > 0) return 0;      // linea "contaminata"
            if (mine == 2 && empty == 1) return 5;     // minaccia
            if (mine == 1 && empty == 2) return 1;     // potenziale
            if (theirs == 2 && empty == 1) return -5;  // minaccia avversaria
            if (theirs == 1 && empty == 2) return -1;
            return 0;
        }

        /// <summary>Centro, poi angoli, poi bordi: l'alpha-beta pota molto di più.</summary>
        public IReadOnlyList<Cell> OrderMoves(IReadOnlyList<Cell> moves)
        {
            var center = new List<Cell>();
            var corners = new List<Cell>();
            var edges = new List<Cell>();
            foreach (var m in moves)
            {
                if (m.Row == 1 && m.Col == 1) center.Add(m);
                else if ((m.Row == 0 || m.Row == 2) && (m.Col == 0 || m.Col == 2)) corners.Add(m);
                else edges.Add(m);
            }
            center.AddRange(corners);
            center.AddRange(edges);
            return center;
        }

        /// <summary>Costruisce uno stato da una stringa compatta come "XO. .X. ..O" (comodo nei test).</summary>
        public static TicTacToeState Parse(string rows, int currentPlayer)
        {
            var b = new int[3, 3];
            var parts = rows.Split(' ');
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    b[r, c] = parts[r][c] == 'X' ? X : parts[r][c] == 'O' ? O : Empty;
            return new TicTacToeState(b, currentPlayer);
        }
    }
}
