using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroArcade.Logic;

namespace RetroArcade.Tests
{
    /// <summary>L'AI del Tris: correttezza dello stato e forza della ricerca (porting di tests/test_game_ai.py).</summary>
    public class GameAITests
    {
        private const int X = TicTacToeState.X, O = TicTacToeState.O;

        /// <summary>Esplora OGNI risposta umana: true se esiste una linea di gioco che batte l'AI (O).</summary>
        private static bool HumanCanBeat(TicTacToeState state, GameAI<Cell> ai)
        {
            if (state.IsTerminal()) return state.Result(O) == GameResult.Loss;
            if (state.CurrentPlayer == X)
            {
                foreach (var m in state.LegalMoves())
                    if (HumanCanBeat(state.Play(m), ai)) return true;
                return false;
            }
            return HumanCanBeat(state.Play(ai.BestMove(state)), ai);
        }

        [TestCase(X)]
        [TestCase(O)]
        public void HardAI_IsUnbeatable(int firstPlayer)
        {
            var ai = GameAI<Cell>.ForLevel("hard", O, new Random(42));
            Assert.IsFalse(HumanCanBeat(new TicTacToeState(firstPlayer), ai));
        }

        [Test]
        public void AlphaBeta_IsUnbeatableToo()
        {
            var ai = new GameAI<Cell>(AIAlgorithm.AlphaBeta, 9, playerId: O);
            Assert.IsFalse(HumanCanBeat(new TicTacToeState(X), ai));
        }

        [Test]
        public void AI_TakesTheWin()
        {
            var state = TicTacToeState.Parse("OO. XX. X..", O);
            Assert.AreEqual(new Cell(0, 2), GameAI<Cell>.ForLevel("hard", O).BestMove(state));
        }

        [Test]
        public void AI_BlocksTheThreat()
        {
            var state = TicTacToeState.Parse("XX. .O. ...", O);
            Assert.AreEqual(new Cell(0, 2), GameAI<Cell>.ForLevel("hard", O).BestMove(state));
        }

        [Test]
        public void WinnerAndTerminalDetection()
        {
            var s = TicTacToeState.Parse("XXX OO. ...", O);
            Assert.AreEqual(X, s.Winner());
            Assert.IsTrue(s.IsTerminal());
            Assert.AreEqual(GameResult.Win, s.Result(X));
            Assert.AreEqual(GameResult.Loss, s.Result(O));
            var draw = TicTacToeState.Parse("XOX XOO OXX", X);
            Assert.AreEqual(0, draw.Winner());
            Assert.AreEqual(GameResult.Draw, draw.Result(X));
        }

        [Test]
        public void MakeMove_IsImmutable()
        {
            var s = new TicTacToeState();
            var s2 = s.Play(new Cell(1, 1));
            Assert.AreEqual(TicTacToeState.Empty, s[1, 1]);
            Assert.AreEqual(X, s2[1, 1]);
            Assert.AreEqual(O, s2.CurrentPlayer);
        }

        [Test]
        public void BlunderRateOne_PlaysRandomLegalMoves()
        {
            var ai = new GameAI<Cell>(AIAlgorithm.Minimax, 9, playerId: O, blunderRate: 1.0, rng: new Random(3));
            var state = TicTacToeState.Parse("XX. .O. ...", O);
            var moves = new HashSet<Cell>();
            for (int i = 0; i < 40; i++) moves.Add(ai.BestMove(state));
            CollectionAssert.IsSubsetOf(moves, state.LegalMoves());
            Assert.Greater(moves.Count, 1, "deve essere davvero casuale");
        }

        [Test]
        public void Ties_AreBrokenRandomly()
        {
            // Su una scacchiera vuota tutte le mosse pareggiano: semi diversi devono dare scelte diverse.
            var picks = new HashSet<Cell>();
            for (int seed = 0; seed < 20; seed++)
                picks.Add(GameAI<Cell>.ForLevel("hard", X, new Random(seed)).BestMove(new TicTacToeState(X)));
            Assert.Greater(picks.Count, 1);
        }

        [Test]
        public void NoLegalMoves_ReportsNotFound()
        {
            var full = TicTacToeState.Parse("XOX XOO OXX", X);
            GameAI<Cell>.ForLevel("hard", O).BestMove(full, out bool found);
            Assert.IsFalse(found);
        }

        [Test]
        public void Heuristic_PrefersTheCenterOnAnEmptyBoard()
        {
            var ai = new GameAI<Cell>(AIAlgorithm.Heuristic, 1, playerId: X, rng: new Random(1));
            Assert.AreEqual(new Cell(1, 1), ai.BestMove(new TicTacToeState(X)));
        }
    }
}
