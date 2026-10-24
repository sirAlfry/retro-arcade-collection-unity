using System;
using System.Linq;
using NUnit.Framework;
using RetroArcade.Logic;

namespace RetroArcade.Tests
{
    /// <summary>Gli stessi casi di <c>tests/test_puzzles.py</c> (parte Puzzle 15) della versione Python.</summary>
    public class Puzzle15LogicTests
    {
        [Test]
        public void SolvedBoard_IsSolvable() => Assert.IsTrue(Puzzle15Logic.IsSolvable(Puzzle15Logic.Solved()));

        [Test]
        public void OneMoveFromSolution_IsSolvable()
        {
            // Regressione: la prima versione rifiutava proprio queste scacchiere,
            // quindi ogni mescolata era impossibile da risolvere.
            var board = Puzzle15Logic.Solved();
            Puzzle15Logic.Slide(board, 14);
            Assert.IsTrue(Puzzle15Logic.IsSolvable(board));
        }

        [Test]
        public void SwappingTwoTiles_MakesItUnsolvable()
        {
            var board = Puzzle15Logic.Solved();
            (board[13], board[14]) = (board[14], board[13]);
            Assert.IsFalse(Puzzle15Logic.IsSolvable(board));
        }

        [Test]
        public void Shuffle_IsAlwaysSolvableAndNotSolved([Range(0, 49)] int seed)
        {
            var board = Puzzle15Logic.Shuffled(new Random(seed));
            CollectionAssert.AreEqual(Enumerable.Range(0, 16), board.OrderBy(v => v));
            Assert.IsTrue(Puzzle15Logic.IsSolvable(board));
            Assert.IsFalse(Puzzle15Logic.IsSolved(board));
        }

        [Test]
        public void RandomWalks_StaySolvable()
        {
            var rng = new Random(7);
            var board = Puzzle15Logic.Solved();
            for (int i = 0; i < 500; i++)
            {
                Puzzle15Logic.Slide(board, rng.Next(16));
                Assert.IsTrue(Puzzle15Logic.IsSolvable(board));
            }
        }

        [Test]
        public void Slide_MovesAWholeRow()
        {
            var board = Puzzle15Logic.Solved();            // vuoto in 15
            Assert.AreEqual(3, Puzzle15Logic.Slide(board, 12)); // prima tessera dell'ultima riga
            CollectionAssert.AreEqual(new[] { 0, 13, 14, 15 }, board.Skip(12));
        }

        [Test]
        public void Slide_MovesAWholeColumn()
        {
            var board = Puzzle15Logic.Solved();
            Assert.AreEqual(3, Puzzle15Logic.Slide(board, 3)); // la colonna di destra scende
            CollectionAssert.AreEqual(new[] { 0, 4, 8, 12 }, new[] { board[3], board[7], board[11], board[15] });
        }

        [Test]
        public void Slide_RejectsDiagonalTilesAndTheBlank()
        {
            var board = Puzzle15Logic.Solved();
            Assert.AreEqual(0, Puzzle15Logic.Slide(board, 10));
            Assert.AreEqual(0, Puzzle15Logic.Slide(board, 15));
            CollectionAssert.AreEqual(Puzzle15Logic.Solved(), board);
        }

        [Test]
        public void Push_MovesTileInArrowDirection()
        {
            var board = Puzzle15Logic.Solved();
            Assert.AreEqual(1, Puzzle15Logic.Push(board, 1, 0)); // DESTRA: il 15 scivola a destra
            CollectionAssert.AreEqual(new[] { 0, 15 }, board.Skip(14));
            Assert.AreEqual(1, Puzzle15Logic.Push(board, 0, 1)); // GIÙ: la tessera sopra il vuoto scende
            Assert.AreEqual(11, board[14]);
        }

        [Test]
        public void Push_AgainstTheEdge_DoesNothing()
        {
            var board = Puzzle15Logic.Solved(); // vuoto in basso a destra: niente sotto né a destra
            Assert.AreEqual(0, Puzzle15Logic.Push(board, -1, 0));
            Assert.AreEqual(0, Puzzle15Logic.Push(board, 0, -1));
        }

        [Test]
        public void OddWidth_UsesInversionParity()
        {
            Assert.IsTrue(Puzzle15Logic.IsSolvable(Puzzle15Logic.Solved(3), 3));
            var board = Puzzle15Logic.Solved(3);
            (board[0], board[1]) = (board[1], board[0]);
            Assert.IsFalse(Puzzle15Logic.IsSolvable(board, 3));
        }
    }
}
