using NUnit.Framework;
using RetroArcade.Logic;

namespace RetroArcade.Tests
{
    public class BreakoutLevelsTests
    {
        [Test]
        public void EveryLevelFitsTheField([Range(1, 5)] int level)
        {
            var bricks = BreakoutLevels.Build(level);
            Assert.IsNotEmpty(bricks);
            foreach (var b in bricks)
            {
                Assert.IsTrue(BreakoutLevels.Field.Contains(b.Rect), b.Rect.ToString());
                Assert.That(b.Hp, Is.InRange(1, 3));
            }
        }

        [Test]
        public void LayoutsLoopAfterTheLastLevel()
        {
            Assert.AreEqual(BreakoutLevels.Build(1).Count, BreakoutLevels.Build(6).Count);
        }
    }
}
