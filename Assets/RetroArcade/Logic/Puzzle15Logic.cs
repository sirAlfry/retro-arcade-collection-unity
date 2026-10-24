using System;

namespace RetroArcade.Logic
{
    /// <summary>
    /// Regole del gioco del 15 (porting della parte "pura" di <c>puzzle15.py</c>, coperta da test).
    /// La scacchiera è un array di <c>size × size</c> valori, riga per riga; 0 = casella vuota.
    ///
    /// <para><b>Regola di risolvibilità</b> (quella giusta: la prima versione Python, e anche il vecchio
    /// porting JS, la sbagliavano e producevano SOLO scacchiere impossibili):</para>
    /// <list type="bullet">
    /// <item>larghezza dispari: risolvibile ⇔ le inversioni sono pari;</item>
    /// <item>larghezza pari: risolvibile ⇔ inversioni + riga del vuoto contata dal BASSO (da 1) è dispari.</item>
    /// </list>
    /// Perché funziona: uno spostamento orizzontale non cambia né le inversioni né la riga del vuoto;
    /// uno verticale sposta il vuoto di una riga e fa "scavalcare" alla tessera altre size−1 tessere,
    /// cambiando le inversioni di un numero dispari (se size è pari). La parità della somma è quindi
    /// invariante, e nella posizione risolta vale 0 + 1 = dispari.
    /// </summary>
    public static class Puzzle15Logic
    {
        public const int Size = 4;

        /// <summary>La scacchiera risolta: 1, 2, …, size²−1, 0.</summary>
        public static int[] Solved(int size = Size)
        {
            var t = new int[size * size];
            for (int i = 0; i < t.Length - 1; i++) t[i] = i + 1;
            return t;
        }

        /// <summary>Coppie (a, b) con a prima di b ma a &gt; b, ignorando il vuoto.</summary>
        public static int Inversions(int[] tiles)
        {
            int inv = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == 0) continue;
                for (int j = i + 1; j < tiles.Length; j++)
                    if (tiles[j] != 0 && tiles[i] > tiles[j]) inv++;
            }
            return inv;
        }

        public static bool IsSolvable(int[] tiles, int size = Size)
        {
            int inv = Inversions(tiles);
            if (size % 2 == 1) return inv % 2 == 0;
            int blankRowFromBottom = size - Array.IndexOf(tiles, 0) / size;
            return (inv + blankRowFromBottom) % 2 == 1;
        }

        public static bool IsSolved(int[] tiles)
        {
            for (int i = 0; i < tiles.Length - 1; i++)
                if (tiles[i] != i + 1) return false;
            return tiles[tiles.Length - 1] == 0;
        }

        /// <summary>Una scacchiera casuale, risolvibile e non già risolta (<c>shuffled</c>).</summary>
        public static int[] Shuffled(Random rng, int size = Size)
        {
            var tiles = Solved(size);
            while (true)
            {
                for (int i = tiles.Length - 1; i > 0; i--) // Fisher–Yates, come random.shuffle
                {
                    int j = rng.Next(i + 1);
                    (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
                }
                if (IsSolvable(tiles, size) && !IsSolved(tiles)) return tiles;
            }
        }

        /// <summary>
        /// Fa scorrere nel vuoto la tessera in <paramref name="index"/> e tutte quelle fra lei e il vuoto,
        /// se stanno sulla stessa riga o colonna (clic su una tessera lontana = sposta la fila intera).
        /// Modifica <paramref name="tiles"/>.
        /// </summary>
        /// <returns>Quante tessere si sono mosse (0 se la mossa non è possibile).</returns>
        public static int Slide(int[] tiles, int index, int size = Size)
        {
            int blank = Array.IndexOf(tiles, 0);
            int br = blank / size, bc = blank % size;
            int r = index / size, c = index % size;
            if (index == blank || (r != br && c != bc)) return 0;
            int step = r == br ? (c > bc ? 1 : -1) : (r > br ? size : -size);
            int moved = 0, pos = blank;
            while (pos != index)
            {
                int next = pos + step;
                (tiles[pos], tiles[next]) = (tiles[next], tiles[pos]);
                pos = next;
                moved++;
            }
            return moved;
        }

        /// <summary>Mossa da tastiera: la tessera accanto al vuoto si muove nella direzione (dx, dy).</summary>
        public static int Push(int[] tiles, int dx, int dy, int size = Size)
        {
            int blank = Array.IndexOf(tiles, 0);
            int r = blank / size - dy, c = blank % size - dx; // la tessera che entrerà nel vuoto
            if (r >= 0 && r < size && c >= 0 && c < size) return Slide(tiles, r * size + c, size);
            return 0;
        }
    }
}
