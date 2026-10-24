using System;
using System.Collections.Generic;

namespace RetroArcade.Logic
{
    /// <summary>
    /// Labirinti di Maze Runner (porting di <c>generate_maze</c> e <c>shortest_path</c> di <c>maze_runner.py</c>).
    /// Griglia [riga, colonna]: 1 = muro, 0 = pavimento.
    /// </summary>
    public static class MazeLogic
    {
        /// <summary>Una difficoltà: etichetta del menu, chiave del record, dimensioni, modalità al buio.</summary>
        public sealed class Difficulty
        {
            public readonly string Label, Key;
            public readonly int Cols, Rows;
            public readonly bool Dark;

            public Difficulty(string label, string key, int cols, int rows, bool dark = false)
            {
                Label = label; Key = key; Cols = cols; Rows = rows; Dark = dark;
            }
        }

        public static readonly Difficulty[] Difficulties =
        {
            new Difficulty("PICCOLO  21 X 15", "small", 21, 15),
            new Difficulty("MEDIO  31 X 21", "medium", 31, 21),
            new Difficulty("GRANDE  41 X 29", "large", 41, 29),
            new Difficulty("AL BUIO  31 X 21", "dark", 31, 21, dark: true),
        };

        private static readonly Int2[] Steps2 = { new Int2(2, 0), new Int2(-2, 0), new Int2(0, 2), new Int2(0, -2) };
        private static readonly Int2[] Steps1 = { new Int2(1, 0), new Int2(-1, 0), new Int2(0, 1), new Int2(0, -1) };

        /// <summary>
        /// Scava un labirinto "perfetto" (un solo percorso tra due celle qualsiasi) con una ricerca in profondità
        /// iterativa (recursive backtracker). Le celle con coordinate pari sono stanze, le altre muri che vengono
        /// abbattuti tra stanze vicine. Colonne e righe vengono rese dispari, così il bordo resta pieno.
        /// </summary>
        public static int[,] Generate(int cols, int rows, Random rng)
        {
            rng ??= new Random();
            if (cols % 2 == 0) cols++;
            if (rows % 2 == 0) rows++;
            var grid = new int[rows, cols];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    grid[y, x] = 1;
            var stack = new List<Int2> { new Int2(0, 0) };
            grid[0, 0] = 0;
            var neighbours = new List<Int2>(4);
            while (stack.Count > 0)
            {
                var cur = stack[stack.Count - 1];
                neighbours.Clear();
                foreach (var d in Steps2)
                {
                    int nx = cur.X + d.X, ny = cur.Y + d.Y;
                    if (nx >= 0 && nx < cols && ny >= 0 && ny < rows && grid[ny, nx] == 1) neighbours.Add(new Int2(nx, ny));
                }
                if (neighbours.Count > 0)
                {
                    var n = neighbours[rng.Next(neighbours.Count)];
                    grid[(cur.Y + n.Y) / 2, (cur.X + n.X) / 2] = 0;
                    grid[n.Y, n.X] = 0;
                    stack.Add(n);
                }
                else
                {
                    stack.RemoveAt(stack.Count - 1);
                }
            }
            return grid;
        }

        /// <summary>Ricerca in ampiezza (BFS): le celle da <paramref name="start"/> a <paramref name="goal"/> incluse, o vuota.</summary>
        public static List<Int2> ShortestPath(int[,] grid, Int2 start, Int2 goal)
        {
            int rows = grid.GetLength(0), cols = grid.GetLength(1);
            var cameFrom = new Dictionary<Int2, Int2> { [start] = start };
            var queue = new Queue<Int2>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (cur == goal) break;
                foreach (var d in Steps1)
                {
                    var n = cur + d;
                    if (n.X < 0 || n.X >= cols || n.Y < 0 || n.Y >= rows || grid[n.Y, n.X] != 0 || cameFrom.ContainsKey(n)) continue;
                    cameFrom[n] = cur;
                    queue.Enqueue(n);
                }
            }
            var path = new List<Int2>();
            if (!cameFrom.ContainsKey(goal)) return path;
            path.Add(goal);
            while (path[path.Count - 1] != start) path.Add(cameFrom[path[path.Count - 1]]);
            path.Reverse();
            return path;
        }
    }
}
