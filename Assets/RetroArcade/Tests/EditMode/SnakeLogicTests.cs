using System;
using NUnit.Framework;
using RetroArcade.Logic;

namespace RetroArcade.Tests
{
    /// <summary>Regole di Snake (porting dei test di tests/test_puzzles.py).</summary>
    public class SnakeLogicTests
    {
        [Test]
        public void EatsAndGrows()
        {
            var game = new SnakeLogic(new Random(1), 10, 10);
            var head = game.Body[0];
            game.Red = new Int2(head.X + 1, head.Y);
            game.Started = true;
            game.Step();
            Assert.AreEqual(SnakeLogic.RedPoints, game.Score);
            Assert.AreEqual(4, game.Body.Count);
        }

        [Test]
        public void IgnoresReverseAndBuffersTurns()
        {
            var game = new SnakeLogic(new Random(1), 20, 20);
            game.Turn(Int2.Left);   // inversione a 180°: ignorata
            Assert.IsEmpty(game.Queue);
            game.Turn(Int2.Up);     // su...
            game.Turn(Int2.Left);   // ...poi sinistra: entrambe tenute
            CollectionAssert.AreEqual(new[] { Int2.Up, Int2.Left }, game.Queue);
        }

        [Test]
        public void DiesOnWalls()
        {
            var game = new SnakeLogic(new Random(1), 6, 6);
            game.Red = new Int2(0, 0);
            for (int i = 0; i < 10; i++) game.Step();
            Assert.IsFalse(game.Alive);
        }

        [Test]
        public void CanFollowItsOwnTail()
        {
            var game = new SnakeLogic(new Random(1), 10, 10);
            game.Body = new System.Collections.Generic.List<Int2> { new Int2(5, 5), new Int2(5, 6), new Int2(4, 6), new Int2(4, 5) };
            game.Direction = Int2.Up;
            game.Red = new Int2(0, 0);
            game.Queue.Add(Int2.Left);
            game.Step();            // entra nella casella che la coda sta lasciando
            Assert.IsTrue(game.Alive);
        }

        [Test]
        public void SpeedGrowsWithScoreUpToTheMaximum()
        {
            var game = new SnakeLogic(new Random(1), 10, 10);
            Assert.AreEqual(8, game.Speed);
            game.Score = 25;
            Assert.AreEqual(13, game.Speed);
            game.Score = 500;
            Assert.AreEqual(SnakeLogic.MaxSpeed, game.Speed);
        }

        [Test]
        public void WaitsForTheFirstKey()
        {
            var game = new SnakeLogic(new Random(1), 10, 10);
            var head = game.Body[0];
            game.Update(5f);
            Assert.AreEqual(head, game.Body[0]);
        }
    }
}
