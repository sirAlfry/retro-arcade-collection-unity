using System;
using System.Linq;
using NUnit.Framework;
using RetroArcade.Logic;

namespace RetroArcade.Tests
{
    public class InvadersTests
    {
        [Test]
        public void Bunker_Erodes()
        {
            var bunker = new Bunker(100, 100, new Random(0));
            int before = bunker.Cells.Count;
            Assert.IsTrue(bunker.Hit(new PRect(bunker.X + 20, bunker.Y, 4, 12)));
            Assert.Less(bunker.Cells.Count, before);
            Assert.IsFalse(bunker.Hit(new PRect(0, 0, 4, 4)));
        }

        [Test]
        public void AlienSprites_HaveTwoFramesOfEqualSize()
        {
            foreach (var kv in InvadersArt.Sprites)
            {
                var a = kv.Value[0];
                var b = kv.Value[1];
                Assert.AreEqual(8, a.Length, kv.Key);
                Assert.AreEqual(8, b.Length, kv.Key);
                CollectionAssert.AreEquivalent(a.Select(r => r.Length).Distinct(), b.Select(r => r.Length).Distinct(), kv.Key);
            }
        }
    }
}
