using HeistSim;
using NUnit.Framework;

namespace HeistSim.Tests
{
    /// <summary>Detection contribution parity with detection.js and the verify script.</summary>
    public sealed class DetectionTests
    {
        [Test]
        public void GuardVisionAddsHeat()
        {
            var delta = Detection.Contribution(new DetectionInput(guardSees: true), 1f);

            Assert.That(delta, Is.EqualTo(0.45f).Within(1e-6f));
            Assert.That(delta, Is.GreaterThan(0f));
        }

        [Test]
        public void CloseRangeBeatsDistantSight()
        {
            var far = Detection.Contribution(new DetectionInput(guardSees: true), 1f);
            var close = Detection.Contribution(new DetectionInput(guardSees: true, close: true), 1f);

            Assert.That(close, Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(close, Is.GreaterThan(far));
        }

        [Test]
        public void CrouchReducesVisionHeat()
        {
            var seen = Detection.Contribution(new DetectionInput(guardSees: true), 1f);
            var crouched = Detection.Contribution(new DetectionInput(guardSees: true, crouched: true), 1f);

            Assert.That(crouched, Is.EqualTo(0.164f).Within(1e-6f));
            Assert.That(crouched, Is.LessThan(seen));
        }

        [Test]
        public void CameraCountScalesHeat()
        {
            var one = Detection.Contribution(new DetectionInput(cameras: 1), 1f);
            var three = Detection.Contribution(new DetectionInput(cameras: 3), 1f);

            Assert.That(one, Is.EqualTo(0.10f).Within(1e-6f));
            Assert.That(three, Is.EqualTo(0.44f).Within(1e-6f));
            Assert.That(three, Is.GreaterThan(one));
        }

        [Test]
        public void PowerCutReducesCctvHeat()
        {
            var on = Detection.Contribution(new DetectionInput(cameras: 3), 1f);
            var off = Detection.Contribution(new DetectionInput(cameras: 3, camerasDisabled: true), 1f);

            Assert.That(off, Is.EqualTo(0.0065f).Within(1e-6f));
            Assert.That(off, Is.GreaterThan(0f));
            Assert.That(off, Is.LessThan(on));
        }

        [Test]
        public void DecaysWhenNothingDetectsYou()
        {
            var delta = Detection.Contribution(default, 1f);

            Assert.That(delta, Is.EqualTo(-0.07f).Within(1e-6f));
            Assert.That(delta, Is.LessThan(0f));
        }

        [Test]
        public void CrouchedAndUnseenDecaysFaster()
        {
            var standing = Detection.Contribution(default, 1f);
            var crouched = Detection.Contribution(new DetectionInput(crouched: true), 1f);

            Assert.That(crouched, Is.EqualTo(-0.12f).Within(1e-6f));
            Assert.That(crouched, Is.LessThan(standing));
        }

        [Test]
        public void MotionZonesAddHeat()
        {
            var quiet = Detection.Contribution(default, 1f);
            var motion = Detection.Contribution(new DetectionInput(motion: true), 1f);

            Assert.That(motion, Is.EqualTo(0.05f).Within(1e-6f));
            Assert.That(motion, Is.GreaterThan(quiet));
        }

        [Test]
        public void SprintAndTagsAddHeatWithoutCountingAsDetection()
        {
            var sprint = Detection.Contribution(new DetectionInput(sprint: true), 1f);
            var tags = Detection.Contribution(new DetectionInput(tags: 2), 1f);

            Assert.That(sprint, Is.EqualTo(-0.02f).Within(1e-6f));
            Assert.That(tags, Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void StaffReportScalesWhileCrouched()
        {
            var standing = Detection.Contribution(new DetectionInput(staffReport: true), 1f);
            var crouched = Detection.Contribution(new DetectionInput(staffReport: true, crouched: true), 1f);

            Assert.That(standing, Is.EqualTo(0.06f).Within(1e-6f));
            Assert.That(crouched, Is.EqualTo(0.13f * 0.45f - 0.07f).Within(1e-6f));
        }

        [Test]
        public void DeltaScalesLinearlyWithTimeStep()
        {
            var one = Detection.Contribution(new DetectionInput(guardSees: true), 1f);
            var half = Detection.Contribution(new DetectionInput(guardSees: true), 0.5f);

            Assert.That(half, Is.EqualTo(one * 0.5f).Within(1e-6f));
        }

        [Test]
        public void MotionZoneHitAndMiss()
        {
            var zones = new[] { new MotionZone(4f, 5f, 1f) };

            var hit = Detection.InMotionZone(zones, 4.5f, 5.2f);
            var miss = Detection.InMotionZone(zones, 8f, 8f);

            Assert.That(hit.HasValue, Is.True);
            Assert.That(hit.Value.I, Is.EqualTo(4f));
            Assert.That(miss.HasValue, Is.False);
        }

        [Test]
        public void MotionZoneDefaultsToTheWebRadius()
        {
            Assert.That(new MotionZone(1f, 2f).R, Is.EqualTo(1.6f));
        }
    }
}
