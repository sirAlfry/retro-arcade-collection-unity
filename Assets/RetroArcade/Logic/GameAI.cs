using System;
using System.Collections.Generic;

namespace RetroArcade.Logic
{
    /// <summary>Risultato di una partita dal punto di vista di un giocatore.</summary>
    public enum GameResult { Ongoing, Win, Loss, Draw }

    /// <summary>Algoritmi disponibili (come <c>AIAlgorithm</c> in Python).</summary>
    public enum AIAlgorithm { Random, Minimax, AlphaBeta, Heuristic }

    /// <summary>
    /// Stato di un gioco a turni a due giocatori (giocatori 1 e -1). Gli stati sono immutabili:
    /// <see cref="MakeMove"/> restituisce un NUOVO stato.
    /// </summary>
    public interface IGameState<TMove>
    {
        IReadOnlyList<TMove> LegalMoves();
        IGameState<TMove> MakeMove(TMove move);
        bool IsTerminal();
        GameResult Result(int player);
        int CurrentPlayer { get; }
        /// <summary>Valutazione euristica della posizione per <paramref name="player"/> (più alto = meglio).</summary>
        double Evaluate(int player);
        /// <summary>Chiave per la tabella di trasposizione (null = niente memoizzazione).</summary>
        object Key { get; }
        /// <summary>Ordina le mosse per potare di più con l'alpha-beta (o le restituisce così come sono).</summary>
        IReadOnlyList<TMove> OrderMoves(IReadOnlyList<TMove> moves);
    }

    /// <summary>
    /// Motore AI generico per giochi a turni: porting di <c>game_ai.GameAI</c>.
    /// <list type="bullet">
    /// <item><b>Random</b>: mossa casuale.</item>
    /// <item><b>Heuristic</b>: valuta solo la posizione dopo ogni mossa (greedy).</item>
    /// <item><b>Minimax</b>: con approfondimento iterativo e tabella di trasposizione.</item>
    /// <item><b>AlphaBeta</b>: potatura alfa-beta con ordinamento delle mosse.</item>
    /// </list>
    /// A parità di valore la mossa è scelta a caso (CPU meno prevedibile); <see cref="BlunderRate"/>
    /// è la probabilità di sbagliare apposta (livelli facili). Solo le profondità COMPLETATE entro il tempo
    /// limite vengono usate: una ricerca interrotta a metà non sovrascrive mai la mossa buona.
    /// </summary>
    public sealed class GameAI<TMove>
    {
        public AIAlgorithm Algorithm { get; }
        /// <summary>Profondità massima di ricerca (1-9).</summary>
        public int Difficulty { get; }
        public double TimeLimitSeconds { get; }
        public int PlayerId { get; }
        public double BlunderRate { get; }
        public int NodesEvaluated { get; private set; }

        private readonly Random _rng;
        private readonly Dictionary<(object, int, bool), double> _memo = new Dictionary<(object, int, bool), double>();
        private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();
        private bool _aborted;

        public GameAI(AIAlgorithm algorithm = AIAlgorithm.Minimax, int difficulty = 3, double timeLimitSeconds = 5.0,
                      int playerId = -1, double blunderRate = 0.0, Random rng = null)
        {
            Algorithm = algorithm;
            Difficulty = Math.Max(1, Math.Min(9, difficulty));
            TimeLimitSeconds = Math.Max(0.01, timeLimitSeconds);
            PlayerId = playerId;
            BlunderRate = Math.Max(0.0, Math.Min(1.0, blunderRate));
            _rng = rng ?? new Random();
        }

        /// <summary>
        /// Preset pronti (<c>GameAI.for_level</c>):
        /// "easy" = euristica greedy che sbaglia una mossa su tre;
        /// "medium" = minimax a profondità 2 con qualche errore;
        /// "hard" = minimax completo, nel Tris imbattibile.
        /// </summary>
        public static GameAI<TMove> ForLevel(string level, int playerId = -1, Random rng = null)
        {
            switch (level)
            {
                case "easy": return new GameAI<TMove>(AIAlgorithm.Heuristic, 1, playerId: playerId, blunderRate: 0.35, rng: rng);
                case "hard": return new GameAI<TMove>(AIAlgorithm.Minimax, 9, playerId: playerId, blunderRate: 0.0, rng: rng);
                default: return new GameAI<TMove>(AIAlgorithm.Minimax, 2, playerId: playerId, blunderRate: 0.12, rng: rng);
            }
        }

        /// <summary>La mossa migliore, oppure <paramref name="found"/> = false se non ci sono mosse.</summary>
        public TMove BestMove(IGameState<TMove> state, out bool found)
        {
            var legal = state.LegalMoves();
            found = legal.Count > 0;
            if (!found) return default;

            NodesEvaluated = 0;
            _memo.Clear();
            _aborted = false;
            _clock.Restart();

            if (BlunderRate > 0 && _rng.NextDouble() < BlunderRate) return Choice(legal);

            switch (Algorithm)
            {
                case AIAlgorithm.Random: return Choice(legal);
                case AIAlgorithm.Minimax: return MinimaxMove(state, legal);
                case AIAlgorithm.AlphaBeta: return AlphaBetaMove(state, legal);
                case AIAlgorithm.Heuristic: return HeuristicMove(state, legal);
                default: return Choice(legal);
            }
        }

        public TMove BestMove(IGameState<TMove> state) => BestMove(state, out _);

        private TMove Choice(IReadOnlyList<TMove> moves) => moves[_rng.Next(moves.Count)];

        /// <summary>Sceglie a caso tra le mosse con il valore massimo (tie-break).</summary>
        private TMove Pick(List<(double value, TMove move)> scored)
        {
            double best = double.NegativeInfinity;
            foreach (var s in scored) best = Math.Max(best, s.value);
            var top = new List<TMove>();
            foreach (var s in scored) if (s.value >= best - 1e-9) top.Add(s.move);
            return top[_rng.Next(top.Count)];
        }

        private TMove MinimaxMove(IGameState<TMove> state, IReadOnlyList<TMove> legal)
        {
            bool haveBest = false;
            TMove best = default;
            int maxDepth = Math.Max(1, Difficulty);
            // Se la profondità copre tutta la partita rimasta (come nel Tris) basta una sola ricerca completa.
            int firstDepth = maxDepth >= legal.Count ? maxDepth : 1;

            for (int depth = firstDepth; depth <= maxDepth; depth++)
            {
                if (TimeUp()) break;
                var scored = new List<(double, TMove)>();
                foreach (var move in legal) scored.Add((Minimax(state.MakeMove(move), depth - 1, false), move));
                if (_aborted) break; // profondità incompleta: si tiene il risultato precedente
                if (scored.Count > 0)
                {
                    best = Pick(scored);
                    haveBest = true;
                }
                bool allDecided = true;
                foreach (var (v, _) in scored) if (Math.Abs(v) < 1000) { allDecided = false; break; }
                if (allDecided) break; // esito già certo per ogni mossa
            }
            return haveBest ? best : Choice(legal);
        }

        private TMove AlphaBetaMove(IGameState<TMove> state, IReadOnlyList<TMove> legal)
        {
            bool haveBest = false;
            TMove best = default;
            int maxDepth = Math.Max(1, Difficulty);
            for (int depth = 1; depth <= maxDepth; depth++)
            {
                if (TimeUp()) break;
                var ordered = state.OrderMoves(legal);
                bool haveDepthBest = false;
                TMove depthBest = default;
                double depthBestValue = double.NegativeInfinity, alpha = double.NegativeInfinity, beta = double.PositiveInfinity;
                foreach (var move in ordered)
                {
                    double value = AlphaBeta(state.MakeMove(move), depth - 1, alpha, beta, false);
                    if (value > depthBestValue)
                    {
                        depthBestValue = value;
                        depthBest = move;
                        haveDepthBest = true;
                    }
                    alpha = Math.Max(alpha, value);
                }
                if (_aborted) break;
                if (haveDepthBest)
                {
                    best = depthBest;
                    haveBest = true;
                }
            }
            return haveBest ? best : Choice(legal);
        }

        private TMove HeuristicMove(IGameState<TMove> state, IReadOnlyList<TMove> legal)
        {
            var scored = new List<(double, TMove)>();
            foreach (var move in legal) scored.Add((state.MakeMove(move).Evaluate(PlayerId), move));
            return scored.Count > 0 ? Pick(scored) : Choice(legal);
        }

        /// <summary>Valore di uno stato finale (vincere prima vale di più) o della valutazione euristica.</summary>
        private double EvaluateTerminal(IGameState<TMove> state, int depth)
        {
            if (!state.IsTerminal()) return state.Evaluate(PlayerId);
            switch (state.Result(PlayerId))
            {
                case GameResult.Win: return 1000 + depth;
                case GameResult.Loss: return -1000 - depth;
                default: return 0;
            }
        }

        private double Minimax(IGameState<TMove> state, int depth, bool maximizing)
        {
            NodesEvaluated++;
            if (TimeUp())
            {
                _aborted = true;
                return EvaluateTerminal(state, depth);
            }
            if (depth == 0 || state.IsTerminal()) return EvaluateTerminal(state, depth);

            object key = state.Key;
            if (key != null && _memo.TryGetValue((key, depth, maximizing), out double cached)) return cached;

            bool any = false;
            double result = maximizing ? double.NegativeInfinity : double.PositiveInfinity;
            foreach (var move in state.LegalMoves())
            {
                double v = Minimax(state.MakeMove(move), depth - 1, !maximizing);
                result = maximizing ? Math.Max(result, v) : Math.Min(result, v);
                any = true;
                if (_aborted) break;
            }
            if (!any) return EvaluateTerminal(state, depth);
            if (key != null && !_aborted) _memo[(key, depth, maximizing)] = result;
            return result;
        }

        private double AlphaBeta(IGameState<TMove> state, int depth, double alpha, double beta, bool maximizing)
        {
            NodesEvaluated++;
            if (TimeUp())
            {
                _aborted = true;
                return EvaluateTerminal(state, depth);
            }
            if (depth == 0 || state.IsTerminal()) return EvaluateTerminal(state, depth);

            if (maximizing)
            {
                double max = double.NegativeInfinity;
                foreach (var move in state.OrderMoves(state.LegalMoves()))
                {
                    double v = AlphaBeta(state.MakeMove(move), depth - 1, alpha, beta, false);
                    max = Math.Max(max, v);
                    alpha = Math.Max(alpha, v);
                    if (beta <= alpha || _aborted) break;
                }
                return max;
            }
            double min = double.PositiveInfinity;
            foreach (var move in state.OrderMoves(state.LegalMoves()))
            {
                double v = AlphaBeta(state.MakeMove(move), depth - 1, alpha, beta, true);
                min = Math.Min(min, v);
                beta = Math.Min(beta, v);
                if (beta <= alpha || _aborted) break;
            }
            return min;
        }

        private bool TimeUp() => _clock.Elapsed.TotalSeconds > TimeLimitSeconds;
    }
}
