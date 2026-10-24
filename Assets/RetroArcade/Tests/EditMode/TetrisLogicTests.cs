using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RetroArcade.Logic;

namespace RetroArcade.Tests
{
    /// <summary>Regole del Tetris (porting di tests/test_tetris.py).</summary>
    public class TetrisLogicTests
    {
        private static TetrisLogic Make(int seed = 1) => new TetrisLogic(new Random(seed));

        [Test]
        public void SevenBag_GivesEveryPieceOncePerBag()
        {
            var game = Make();
            var seq = new List<char> { game.Piece.Kind };
            for (int i = 0; i < 13; i++)
            {
                game.Spawn();
                seq.Add(game.Piece.Kind);
            }
            CollectionAssert.AreEquivalent(TetrisLogic.Kinds, seq.Take(7));
            CollectionAssert.AreEquivalent(TetrisLogic.Kinds, seq.Skip(7));
        }

        [Test]
        public void Piece_SpawnsCenteredAtTheTop()
        {
            var xs = Make().Piece.Cells().Select(c => c.X).ToList();
            Assert.GreaterOrEqual(xs.Min(), 3);
            Assert.LessOrEqual(xs.Max(), 6);
        }

        [Test]
        public void Moves_StopAtTheWalls()
        {
            var game = Make();
            while (game.Move(-1)) { }
            Assert.AreEqual(0, game.Piece.Cells().Min(c => c.X));
            Assert.IsFalse(game.Move(-1));
        }

        [Test]
        public void Rotation_UsesWallKicks()
        {
            var game = Make();
            game.Piece = new TetrisPiece('I', 0, 0, 1); // I verticale contro il muro sinistro
            while (game.Move(-1)) { }
            Assert.IsTrue(game.Rotate(1)); // senza kick colliderebbe
            Assert.IsTrue(game.Piece.Cells().All(c => c.X >= 0 && c.X < game.Cols));
        }

        [Test]
        public void HardDrop_ScoresTwoPointsPerRowAndLocks()
        {
            var game = Make();
            int startY = game.Piece.Y;
            int distance = game.HardDrop();
            Assert.Greater(distance, 15);
            Assert.AreEqual(2 * distance, game.Score);
            bool bottomUsed = false;
            for (int x = 0; x < game.Cols; x++) bottomUsed |= game.Board[game.Rows - 1, x] != '\0';
            Assert.IsTrue(bottomUsed);
            Assert.IsNotNull(game.Piece);
            Assert.LessOrEqual(game.Piece.Y, startY + 1); // è già arrivato il pezzo successivo
        }

        [Test]
        public void LineClearScoring_UsesTheLevel()
        {
            var game = Make();
            game.Level = 3;
            for (int x = 0; x < game.Cols - 1; x++) game.Board[game.Rows - 1, x] = 'Z';
            game.Piece = new TetrisPiece('I', game.Cols - 3, 0, 1); // I verticale nell'ultima colonna
            game.HardDrop();
            CollectionAssert.AreEqual(new[] { game.Rows - 1 }, game.ClearingRows);
            Assert.GreaterOrEqual(game.Score, TetrisLogic.LineScores[1] * 3);
            game.Update(1f); // fine animazione
            Assert.IsEmpty(game.ClearingRows);
            for (int x = 0; x < game.Cols - 3; x++) Assert.AreEqual('\0', game.Board[game.Rows - 1, x]);
        }

        [Test]
        public void FourLines_IsATetris()
        {
            var game = Make();
            for (int y = game.Rows - 4; y < game.Rows; y++)
                for (int x = 0; x < game.Cols - 1; x++)
                    game.Board[y, x] = 'J';
            game.Piece = new TetrisPiece('I', game.Cols - 3, 0, 1);
            game.HardDrop();
            Assert.AreEqual(4, game.ClearingRows.Count);
            CollectionAssert.Contains(game.PopEvents(), "lines4");
        }

        [Test]
        public void Hold_OnlyOncePerPiece()
        {
            var game = Make();
            char first = game.Piece.Kind;
            Assert.IsTrue(game.HoldPiece());
            Assert.AreEqual(first, game.Hold);
            Assert.IsFalse(game.HoldPiece());
            game.HardDrop();
            Assert.IsTrue(game.HoldPiece()); // di nuovo permesso dopo il blocco
        }

        [Test]
        public void LockDelay()
        {
            var game = Make();
            while (game.SoftDrop()) { }
            Assert.IsTrue(game.OnGround);
            game.Update(TetrisLogic.LockDelay * 0.5f);
            Assert.IsNotNull(game.Piece);
            Assert.IsTrue(game.OnGround); // ancora spostabile
            game.Update(TetrisLogic.LockDelay);
            CollectionAssert.Contains(game.PopEvents(), "lock");
        }

        [Test]
        public void BlockOut_EndsTheGame()
        {
            var game = Make();
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < game.Cols; x++)
                    game.Board[y, x] = 'O';
            game.Spawn();
            Assert.IsTrue(game.GameOver);
        }

        [Test]
        public void Gravity_GetsFasterWithLevels()
        {
            var values = Enumerable.Range(1, 15).Select(TetrisLogic.GravityInterval).ToList();
            CollectionAssert.AreEqual(values.OrderByDescending(v => v).ToList(), values);
            Assert.AreEqual(1f, values[0]);
            Assert.GreaterOrEqual(values[values.Count - 1], 0.02f);
        }

        [Test]
        public void Srs_RotatingFourTimesReturnsToSpawnShape()
        {
            foreach (char k in TetrisLogic.Kinds)
            {
                var m = TetrisLogic.Rotated(k, 4);
                Assert.AreEqual(TetrisLogic.Shapes[k], m, "pezzo " + k);
            }
        }
    }
}
