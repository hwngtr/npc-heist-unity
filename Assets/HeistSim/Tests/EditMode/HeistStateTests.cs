using HeistSim;
using NUnit.Framework;

namespace HeistSim.Tests
{
    /// <summary>Heat tiers, decay, bag capacity, objectives and the initial state from heist.js.</summary>
    public sealed class HeistStateTests
    {
        [Test]
        public void TierThresholdsMatchTheVerifyScript()
        {
            Assert.That(HeistState.TierOf(0.05f), Is.EqualTo(0));
            Assert.That(HeistState.TierOf(0.12f), Is.EqualTo(1));
            Assert.That(HeistState.TierOf(0.32f), Is.EqualTo(2));
            Assert.That(HeistState.TierOf(0.52f), Is.EqualTo(3));
            Assert.That(HeistState.TierOf(0.72f), Is.EqualTo(4));
            Assert.That(HeistState.TierOf(0.90f), Is.EqualTo(5));
        }

        [Test]
        public void TierThresholdsAreInclusiveLowerBounds()
        {
            Assert.That(HeistState.TierOf(0.119f), Is.EqualTo(0));
            Assert.That(HeistState.TierOf(0.319f), Is.EqualTo(1));
            Assert.That(HeistState.TierOf(0.519f), Is.EqualTo(2));
            Assert.That(HeistState.TierOf(0.719f), Is.EqualTo(3));
            Assert.That(HeistState.TierOf(0.899f), Is.EqualTo(4));
            Assert.That(HeistState.TierOf(1f), Is.EqualTo(5));
        }

        [Test]
        public void TierTableCarriesTheSixWebLabels()
        {
            Assert.That(HeistState.Tiers.Length, Is.EqualTo(6));
            Assert.That(HeistState.Tiers[0].Min, Is.EqualTo(0.00f));
            Assert.That(HeistState.Tiers[1].Min, Is.EqualTo(0.12f));
            Assert.That(HeistState.Tiers[2].Min, Is.EqualTo(0.32f));
            Assert.That(HeistState.Tiers[3].Min, Is.EqualTo(0.52f));
            Assert.That(HeistState.Tiers[4].Min, Is.EqualTo(0.72f));
            Assert.That(HeistState.Tiers[5].Min, Is.EqualTo(0.90f));
            Assert.That(HeistState.Tiers[5].Label, Is.EqualTo("Exposure"));
            Assert.That(HeistState.Tiers[5].Css, Is.EqualTo("exposure"));
        }

        [Test]
        public void InitialStateStartsCuriousWithThreeSlots()
        {
            var st = new HeistState(3, new[] { "power", "escape" });

            Assert.That(st.Heat, Is.EqualTo(0.12f));
            Assert.That(st.Tier, Is.EqualTo(1));
            Assert.That(st.BagCap, Is.EqualTo(3));
            Assert.That(st.Phase, Is.EqualTo("case"));
            Assert.That(st.Bag.Count, Is.EqualTo(0));
            Assert.That(st.Channel, Is.Null);
            Assert.That(st.Objectives.Count, Is.EqualTo(2));
        }

        [Test]
        public void ZeroBagCapFallsBackToThree()
        {
            Assert.That(new HeistState(0).BagCap, Is.EqualTo(3));
        }

        [Test]
        public void HeatClampsHighAndLow()
        {
            var st = new HeistState();

            st.SetHeat(2f);
            Assert.That(st.Heat, Is.EqualTo(1f));
            Assert.That(st.Tier, Is.EqualTo(5));

            st.SetHeat(-1f);
            Assert.That(st.Heat, Is.EqualTo(0f));
            Assert.That(st.Tier, Is.EqualTo(0));
        }

        [Test]
        public void NonFiniteHeatIsTreatedAsZero()
        {
            var st = new HeistState();

            st.SetHeat(float.NaN);
            Assert.That(st.Heat, Is.EqualTo(0f));
            Assert.That(st.Tier, Is.EqualTo(0));

            st.SetHeat(float.PositiveInfinity);
            Assert.That(st.Heat, Is.EqualTo(0f));
        }

        [Test]
        public void AddHeatAdvancesTier()
        {
            var st = new HeistState();
            st.SetHeat(0f);

            st.AddHeat(0.4f);

            Assert.That(st.Heat, Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(st.Tier, Is.EqualTo(2));
        }

        [Test]
        public void AddHeatClampsAtOne()
        {
            var st = new HeistState();
            st.SetHeat(0.95f);

            st.AddHeat(0.3f);

            Assert.That(st.Heat, Is.EqualTo(1f));
            Assert.That(st.Tier, Is.EqualTo(5));
        }

        [Test]
        public void CrouchedDecayBeatsStandingDecay()
        {
            var standing = new HeistState();
            var crouched = new HeistState();
            standing.SetHeat(0.6f);
            crouched.SetHeat(0.6f);

            standing.Decay(1f, false);
            crouched.Decay(1f, true);

            Assert.That(standing.Heat, Is.EqualTo(0.53f).Within(1e-6f));
            Assert.That(crouched.Heat, Is.EqualTo(0.48f).Within(1e-6f));
            Assert.That(crouched.Heat, Is.LessThan(standing.Heat));
        }

        [Test]
        public void DecayFloorsAtZero()
        {
            var st = new HeistState();
            st.SetHeat(0.05f);

            st.Decay(5f, false);

            Assert.That(st.Heat, Is.EqualTo(0f));
            Assert.That(st.Tier, Is.EqualTo(0));
        }

        [Test]
        public void ThreeGoodsFitAndTheFourthIsRefused()
        {
            var st = new HeistState(3);

            Assert.That(st.CanCarry, Is.True);
            Assert.That(st.PutInBag("a"), Is.True);
            Assert.That(st.PutInBag("b"), Is.True);
            Assert.That(st.PutInBag("c"), Is.True);
            Assert.That(st.CanCarry, Is.False);
            Assert.That(st.PutInBag("d"), Is.False);
            Assert.That(st.BagCount, Is.EqualTo(3));
            Assert.That(st.Bag, Is.EqualTo(new[] { "a", "b", "c" }));
        }

        [Test]
        public void CompleteObjectiveMarksDoneAndRepeatsAreRefused()
        {
            var st = new HeistState(3, new[] { "power" });

            Assert.That(st.CompleteObjective("power"), Is.True);
            Assert.That(st.Objective("power").Done, Is.True);
            Assert.That(st.CompleteObjective("power"), Is.False);
            Assert.That(st.CompleteObjective("missing"), Is.False);
        }

        [Test]
        public void NonEscapeObjectivesGateTheExit()
        {
            var st = new HeistState(3, new[] { "power", "vault", "escape" });

            Assert.That(st.RequiredMet(), Is.False);
            st.CompleteObjective("power");
            Assert.That(st.RequiredMet(), Is.False);
            st.CompleteObjective("vault");
            Assert.That(st.RequiredMet(), Is.True);
        }

        [Test]
        public void EscapeObjectiveDoesNotBlockItself()
        {
            var st = new HeistState(3, new[] { "power", "escape" });
            st.CompleteObjective("power");

            Assert.That(st.RequiredMet(), Is.True);
            st.CompleteObjective("escape");
            Assert.That(st.RequiredMet(), Is.True);
        }

        [Test]
        public void AnEscapeOnlyHeistIsAlreadyComplete()
        {
            var st = new HeistState(3, new[] { "escape" });

            Assert.That(st.RequiredMet(), Is.True);
        }
    }
}
