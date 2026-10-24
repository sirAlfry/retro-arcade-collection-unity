using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroArcade.Logic;

namespace RetroArcade.Tests
{
    public class MemoryLogicTests
    {
        [Test]
        public void Deal_HasPairsAndFitsTheScreen([Range(0, 2)] int sizeIndex)
        {
            var size = MemoryLogic.Sizes[sizeIndex];
            var cards = MemoryLogic.Deal(size, new Random(2));
            Assert.AreEqual(size.Cols * size.Rows, cards.Count);
            var counts = new Dictionary<string, int>();
            foreach (var c in cards) counts[c.Icon] = counts.TryGetValue(c.Icon, out int n) ? n + 1 : 1;
            foreach (var n in counts.Values) Assert.AreEqual(2, n);
            for (int i = 0; i < cards.Count; i++)
            {
                var a = cards[i].Rect;
                Assert.GreaterOrEqual(a.Left, 0);
                Assert.LessOrEqual(a.Right, MemoryLogic.ScreenW);
                Assert.GreaterOrEqual(a.Top, MemoryLogic.HudH);
                Assert.LessOrEqual(a.Bottom, MemoryLogic.ScreenH);
                for (int j = i + 1; j < cards.Count; j++) Assert.IsFalse(a.Collides(cards[j].Rect));
            }
        }

        [Test]
        public void Icons_Are7x7()
        {
            foreach (var (name, art, _) in MemoryLogic.Icons)
            {
                Assert.AreEqual(7, art.Length, name);
                foreach (var row in art) Assert.AreEqual(7, row.Length, name);
            }
        }
    }
}
