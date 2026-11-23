using System.Collections.Generic;
using HeistSim;
using NUnit.Framework;
using UnityEngine;

namespace HeistSim.Tests
{
    /// <summary>Scripted market loop scenarios: a clean steal and the exposure bust.</summary>
    public sealed class HeistGameLoopTests
    {
        private readonly List<GuardProfile> _profiles = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var profile in _profiles)
            {
                Object.DestroyImmediate(profile);
            }

            _profiles.Clear();
        }

        [Test]
        public void ScriptedStealFillsTheBagAndWarmsTheQuota()
        {
            var game = new HeistGame(NewStandard());
            game.SetGuard(1f, 16f);
            game.SetStaff(1f, 16f);
            game.FrozenPatrol = true;
            game.TeleportPlayer(11.0f, 7.5f);

            var hold = new HeistInput(0f, 0f, action: true, crouch: true);
            for (var i = 0; i < 900 && game.Snapshot().BagCount == 0; i++)
            {
                game.Step(1f / 60f, hold);
            }

            var snapshot = game.Snapshot();
            Assert.That(snapshot.BagCount, Is.EqualTo(1), "the held steal completes and fills one bag slot");
            Assert.That(snapshot.Bag[0], Is.EqualTo("m_gondola2"), "the nearest reachable low value loot is taken");
            Assert.That(snapshot.StolenValue, Is.EqualTo(32f), "the truffle oil adds its 32 euro value");
            Assert.That(ObjectiveDone(snapshot, "quota"), Is.False, "32 euro is progress but below the 190 euro quota");
            Assert.That(snapshot.Heat, Is.LessThan(HeistState.Tiers[3].Min), "heat never reaches the Searching tier");
            Assert.That(snapshot.Tier, Is.LessThan(3), "the run stays below tier 3");
            Assert.That(snapshot.Outcome, Is.EqualTo(HeistOutcome.None), "a clean steal does not end the run");
        }

        [Test]
        public void ExposureWithTheGuardOnTopIsBusted()
        {
            var game = new HeistGame(NewStandard());
            game.TeleportPlayer(4.5f, 4.5f);
            game.SetGuard(5.2f, 4.5f);
            game.SetHeat(0.95f);

            game.Step(1f / 60f, HeistInput.None);

            var snapshot = game.Snapshot();
            Assert.That(snapshot.Tier, Is.EqualTo(5), "0.95 heat sits in the Exposure tier");
            Assert.That(snapshot.Visible, Is.True, "a guard 0.7 tiles away sees the thief");
            Assert.That(snapshot.Outcome, Is.EqualTo(HeistOutcome.Busted), "tier 5 sight under 2.4 tiles ends the run");
        }

        [Test]
        public void SwitchingArchKeepsTheRunInPlace()
        {
            var game = new HeistGame(NewStandard());
            game.SetHeat(0.4f);
            game.TeleportPlayer(5f, 5f);
            game.Step(1f / 60f, HeistInput.None);
            var before = game.Snapshot();

            game.SwitchArch();
            var after = game.Snapshot();

            Assert.That(after.Arch, Is.EqualTo(GuardArchitecture.Bt), "the Guard button switches to the BT controller");
            Assert.That(after.Heat, Is.EqualTo(before.Heat), "switching does not change heat");
            Assert.That(after.Player, Is.EqualTo(before.Player), "switching does not move the player");
            Assert.That(after.Guard, Is.EqualTo(before.Guard), "switching does not move the guard");
            Assert.That(after.Tier, Is.EqualTo(before.Tier), "switching does not change the tier");

            game.SwitchArch();
            Assert.That(game.Snapshot().Arch, Is.EqualTo(GuardArchitecture.Fsm), "a second press returns to the FSM controller");
        }

        private GuardProfile NewStandard()
        {
            var profile = GuardProfile.CreateDefault("standard");
            _profiles.Add(profile);
            return profile;
        }

        private static bool ObjectiveDone(HeistSnapshot snapshot, string id)
        {
            foreach (var objective in snapshot.Objectives)
            {
                if (objective.Id == id)
                {
                    return objective.Done;
                }
            }

            return false;
        }
    }
}
