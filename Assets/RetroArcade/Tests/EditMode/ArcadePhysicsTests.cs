using NUnit.Framework;
using RetroArcade.Logic;

namespace RetroArcade.Tests
{
    /// <summary>Aiuti di fisica (porting dei test di tests/test_core.py).</summary>
    public class ArcadePhysicsTests
    {
        [Test]
        public void PongPrediction_HandlesBounces()
        {
            // in linea retta
            Assert.AreEqual(50f, ArcadePhysics.PredictY(0, 50, 10, 0, 100, 0, 100), 1e-4f);
            // un rimbalzo sul fondo: 50 + 100 = 150 -> specchiato a 50
            Assert.AreEqual(50f, ArcadePhysics.PredictY(0, 50, 10, 10, 100, 0, 100), 1e-4f);
            // la palla si allontana -> centro
            Assert.AreEqual(50f, ArcadePhysics.PredictY(50, 10, -5, 0, 100, 0, 100), 1e-4f);
        }

        [Test]
        public void CircleRectCollision()
        {
            var rect = new PRect(10, 10, 20, 10);
            Assert.IsTrue(ArcadePhysics.CircleHitsRect(5, 15, 6, rect));
            Assert.IsFalse(ArcadePhysics.CircleHitsRect(0, 0, 5, rect));
            Assert.IsTrue(ArcadePhysics.CircleHitsRect(20, 15, 1, rect));
        }

        [Test]
        public void PRect_BehavesLikePygameRect()
        {
            var r = PRect.FromCenter(34, 300, 12, 90);
            Assert.AreEqual(28, r.X);
            Assert.AreEqual(255, r.Y);
            Assert.IsTrue(r.Collides(new PRect(39, 290, 16, 16)));
            Assert.IsFalse(r.Collides(new PRect(40, 290, 16, 16))); // bordo destro escluso
            var inner = new PRect(0, 0, 150, 150).Inflate(-63, -63);
            Assert.AreEqual(new PRect(31, 31, 87, 87), inner);
        }
    }
}
