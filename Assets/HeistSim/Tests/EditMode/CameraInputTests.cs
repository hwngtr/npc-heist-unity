using HeistSim.Market;
using NUnit.Framework;
using UnityEngine;

namespace HeistSim.Tests
{
    /// <summary>Camera-relative keyboard mapping checks for the market runner.</summary>
    public class CameraInputTests
    {
        private static readonly Vector2 Right = new Vector2(-0.7071068f, 0.7071068f);
        private static readonly Vector2 Up = new Vector2(-0.7071068f, -0.7071068f);

        [Test]
        public void WGoesTowardScreenUp()
        {
            var dir = MarketRunner.MapCameraInput(Right, Up, 0f, 1f);
            Assert.AreEqual(-0.7071068f, dir.x, 1e-5f);
            Assert.AreEqual(-0.7071068f, dir.y, 1e-5f);
        }

        [Test]
        public void DGoesTowardScreenRight()
        {
            var dir = MarketRunner.MapCameraInput(Right, Up, 1f, 0f);
            Assert.AreEqual(-0.7071068f, dir.x, 1e-5f);
            Assert.AreEqual(0.7071068f, dir.y, 1e-5f);
        }

        [Test]
        public void DiagonalIsCappedAtUnitLength()
        {
            var dir = MarketRunner.MapCameraInput(Right, Up, 1f, 1f);
            Assert.LessOrEqual(dir.magnitude, 1f + 1e-5f);
        }

        [Test]
        public void OppositeKeysCancel()
        {
            var dir = MarketRunner.MapCameraInput(Right, Up, 0f, 0f);
            Assert.AreEqual(0f, dir.magnitude, 1e-6f);
        }
    }
}
