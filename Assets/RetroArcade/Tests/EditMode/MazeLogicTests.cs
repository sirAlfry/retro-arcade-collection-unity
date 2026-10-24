using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroArcade.Logic;

namespace RetroArcade.Tests
{
    /// <summary>Labirinti (porting dei test di tests/test_puzzles.py).</summary>
    public class MazeLogicTests
    {
        private static HashSet<Int2> Floor(int[,] g)
        {
            var f = new HashSet<Int2>();
            for (int y = 0; y < g.GetLength(0); y++)
                for (int x = 0; x < g.GetLength(1); x++)
                    if (g[y, x] == 0) f.Add(new Int2(x, y));
            return f;
        }

        [Test]
        public void Maze_IsPerfectAndSolvable([Range(0, 3)] int diffIndex)
        {
            var diff = MazeLogic.Difficulties[diffIndex];
            var grid = MazeLogic.Generate(diff.Cols, diff.Rows, new Random(5));
            var floor = Floor(grid);
            // connesso: la BFS dall'inizio raggiunge ogni cella di pavimento
            var seen = new HashSet<Int2> { Int2.Zero };
            var queue = new Queue<Int2>();
            queue.Enqueue(Int2.Zero);
            int edges = 0;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (floor.Contains(c + Int2.Right)) edges++;
                if (floor.Contains(c + Int2.Down)) edges++;
                foreach (var d in new[] { Int2.Right, Int2.Left, Int2.Down, Int2.Up })
                {
                    var n = c + d;
                    if (floor.Contains(n) && seen.Add(n)) queue.Enqueue(n);
                }
            }
            Assert.IsTrue(seen.SetEquals(floor));
            // un albero ha esattamente V - 1 archi: nessun anello -> labirinto "perfetto"
            Assert.AreEqual(floor.Count - 1, edges);
            var goal = new Int2(grid.GetLength(1) - 1, grid.GetLength(0) - 1);
            var path = MazeLogic.ShortestPath(grid, Int2.Zero, goal);
            Assert.AreEqual(Int2.Zero, path[0]);
            Assert.AreEqual(goal, path[path.Count - 1]);
        }

        [Test]
        public void ShortestPath_OnTinyGrid()
        {
            var grid = new[,] { { 0, 0, 0 }, { 1, 1, 0 }, { 0, 0, 0 } };
            CollectionAssert.AreEqual(new[]
            {
                new Int2(0, 0), new Int2(1, 0), new Int2(2, 0), new Int2(2, 1), new Int2(2, 2), new Int2(1, 2), new Int2(0, 2),
            }, MazeLogic.ShortestPath(grid, new Int2(0, 0), new Int2(0, 2)));
            grid[1, 2] = 1;
            Assert.IsEmpty(MazeLogic.ShortestPath(grid, new Int2(0, 0), new Int2(0, 2)));
        }

        [Test]
        public void EvenSizes_AreMadeOdd()
        {
            var grid = MazeLogic.Generate(10, 8, new Random(1));
            Assert.AreEqual(1, grid.GetLength(0) % 2);
            Assert.AreEqual(1, grid.GetLength(1) % 2);
        }
    }
}
