using System.Collections.Generic;
using HeistSim;
using NUnit.Framework;
using UnityEngine;

namespace HeistSim.Tests
{
    /// <summary>Trace parity between the FSM and the reactive behavior tree.</summary>
    public sealed class ParityTests
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
        public void TransitionTableCarriesSeventeenRulesInPriorityOrder()
        {
            var rules = GuardFsm.Rules;
            Assert.That(rules.Length, Is.EqualTo(17));
            Assert.That(rules[0], Is.EqualTo((GuardAction.Idle, GuardAction.Chase, "player_visible")));
            Assert.That(rules[3], Is.EqualTo((GuardAction.Patrol, GuardAction.Chase, "player_visible")));
            Assert.That(rules[7], Is.EqualTo((GuardAction.Chase, GuardAction.Attack, "in_attack_range")));
            Assert.That(rules[16], Is.EqualTo((GuardAction.Return, GuardAction.Patrol, "at_route_start")));
        }

        [Test]
        public void GuardTreeCarriesTwentyOneNodesInPriorityOrder()
        {
            var root = GuardBt.BuildGuardTree(GuardFsm.EvalGuard);
            Assert.That(GuardBt.CountNodes(root), Is.EqualTo(21));

            var selector = (ReactiveSelector)root;
            Assert.That(selector.Children.Count, Is.EqualTo(7));
            Assert.That(selector.Children[0].Name, Is.EqualTo("seq_attack"));
            Assert.That(selector.Children[1].Name, Is.EqualTo("seq_chase"));
            Assert.That(selector.Children[6].Name, Is.EqualTo("idle"));
        }

        [Test]
        public void DetectChaseFromPatrol()
        {
            var driver = new ParityDriver(NewStandard());
            driver.Step(MakePercepts(1), "patrol", GuardAction.Patrol);
            driver.Step(MakePercepts(2, visible: true, dist: 3.2f), "chase", GuardAction.Chase);
        }

        [Test]
        public void AttackInRange()
        {
            var driver = new ParityDriver(NewStandard());
            driver.Step(MakePercepts(1), "patrol", GuardAction.Patrol);
            driver.Step(MakePercepts(2, visible: true, dist: 3.2f), "chase", GuardAction.Chase);
            driver.Step(MakePercepts(3, visible: true, dist: 0.9f), "attack", GuardAction.Attack);
            driver.Step(MakePercepts(4, visible: true, dist: 2.8f), "chase", GuardAction.Chase);
        }

        [Test]
        public void LostSightToSearch()
        {
            var driver = new ParityDriver(NewStandard());
            driver.Step(MakePercepts(1), "patrol", GuardAction.Patrol);
            driver.Step(MakePercepts(2, visible: true, dist: 3.2f), "chase", GuardAction.Chase);
            driver.Step(MakePercepts(3, lostSightTicks: 45), "search", GuardAction.Search);
        }

        [Test]
        public void SearchExpiryToReturn()
        {
            var driver = new ParityDriver(NewStandard());
            driver.Step(MakePercepts(1), "patrol", GuardAction.Patrol);
            driver.Step(MakePercepts(2, visible: true, dist: 3.2f), "chase", GuardAction.Chase);
            driver.Step(MakePercepts(3, lostSightTicks: 45), "search", GuardAction.Search);
            driver.Step(
                MakePercepts(4, lostSightTicks: 50, searchTicks: 90, alert: 0.20f),
                "return",
                GuardAction.Return);
        }

        [Test]
        public void ReturnToPatrolAtRouteStart()
        {
            var driver = new ParityDriver(NewStandard());
            driver.Step(MakePercepts(1), "patrol", GuardAction.Patrol);
            driver.Step(MakePercepts(2, visible: true, dist: 3.2f), "chase", GuardAction.Chase);
            driver.Step(MakePercepts(3, lostSightTicks: 45), "search", GuardAction.Search);
            driver.Step(
                MakePercepts(4, lostSightTicks: 50, searchTicks: 90, alert: 0.20f),
                "return",
                GuardAction.Return);
            driver.Step(MakePercepts(5, atRouteStart: true), "patrol", GuardAction.Patrol);
        }

        [Test]
        public void SoundInterruptsSearch()
        {
            var driver = new ParityDriver(NewStandard());
            driver.Step(MakePercepts(1), "patrol", GuardAction.Patrol);
            driver.Step(MakePercepts(2, visible: true, dist: 3.2f), "chase", GuardAction.Chase);
            driver.Step(MakePercepts(3, lostSightTicks: 45), "search", GuardAction.Search);
            driver.Step(
                MakePercepts(4, lostSightTicks: 46, searchTicks: 1, sound: new SoundEvent(new Vector2(4f, 4f), 2)),
                "investigate",
                GuardAction.Investigate);
        }

        [Test]
        public void VisibleEventPreemptsInvestigate()
        {
            var driver = new ParityDriver(NewStandard());
            driver.Step(MakePercepts(1), "patrol", GuardAction.Patrol);

            var investigate = driver.Step(
                MakePercepts(2, sound: new SoundEvent(new Vector2(4f, 4f), 2)),
                "investigate",
                GuardAction.Investigate);
            Assert.That(investigate.AbortedNodes, Does.Contain("patrol"));
            Assert.That(investigate.AbortedNodes, Does.Contain("seq_patrol"));

            var chase = driver.Step(MakePercepts(3, visible: true, dist: 3.2f), "chase", GuardAction.Chase);
            Assert.That(chase.AbortedNodes, Does.Contain("investigate"));
            Assert.That(chase.AbortedNodes, Does.Contain("seq_investigate"));
            Assert.That(GuardBt.FormatActiveTrace(chase), Does.Contain("preempted("));
        }

        [Test]
        public void SevenStateLifecycleMatches()
        {
            var driver = new ParityDriver(NewStandard());
            driver.Step(MakePercepts(1), "patrol", GuardAction.Patrol);
            driver.Step(
                MakePercepts(2, sound: new SoundEvent(new Vector2(6f, 6f), 4)),
                "investigate",
                GuardAction.Investigate);
            driver.Step(MakePercepts(3, visible: true, dist: 3.2f), "chase", GuardAction.Chase);
            driver.Step(MakePercepts(4, visible: true, dist: 0.9f), "attack", GuardAction.Attack);
            driver.Step(MakePercepts(5, visible: true, dist: 2.8f), "chase", GuardAction.Chase);
            driver.Step(MakePercepts(6, lostSightTicks: 50), "search", GuardAction.Search);
            driver.Step(
                MakePercepts(7, lostSightTicks: 55, searchTicks: 95, alert: 0.20f, atRouteStart: false),
                "return",
                GuardAction.Return);
            driver.Step(MakePercepts(8, atRouteStart: true), "patrol", GuardAction.Patrol);
        }

        [Test]
        public void IdleStartsPatrolOnAssignedRoute()
        {
            var profile = NewStandard();
            var fsm = new GuardFsm(GuardAction.Idle);
            var bt = new GuardBt(GuardFsm.EvalGuard, initialLatch: "idle");
            var percepts = MakePercepts(1);

            var fsmResult = fsm.Tick(percepts, profile);
            var btResult = bt.Tick(percepts, profile);

            Assert.That(fsm.State, Is.EqualTo(GuardAction.Patrol));
            Assert.That(btResult.ActiveLeaf, Is.EqualTo("patrol"));
            Assert.That(fsmResult.Intent.Action, Is.EqualTo(GuardAction.Patrol));
            AssertSameIntent(fsmResult.Intent, btResult.Intent);
        }

        private GuardProfile NewStandard()
        {
            var profile = GuardProfile.CreateDefault("standard");
            _profiles.Add(profile);
            return profile;
        }

        private static GuardPercepts MakePercepts(
            int tick,
            bool visible = false,
            float dist = 5f,
            SoundEvent? sound = null,
            float? alert = null,
            bool arrivedAtTarget = false,
            bool atRouteStart = false,
            int lostSightTicks = 0,
            int searchTicks = 0)
        {
            var resolvedAlert = alert ?? (visible ? 0.55f : 0.12f);
            return new GuardPercepts(
                tick,
                new Vector2(13.5f, 4.5f),
                0f,
                visible,
                visible ? new Vector2(13.5f + dist, 4.5f) : null,
                dist,
                0f,
                sound,
                resolvedAlert,
                new Vector2(13.5f + dist, 4.5f),
                atRouteStart,
                arrivedAtTarget,
                lostSightTicks,
                searchTicks);
        }

        private static void AssertSameIntent(GuardIntent fsmIntent, GuardIntent btIntent)
        {
            Assert.That(btIntent.Action, Is.EqualTo(fsmIntent.Action));
            Assert.That(btIntent.Target, Is.EqualTo(fsmIntent.Target));
            Assert.That(btIntent.Speed, Is.EqualTo(fsmIntent.Speed));
        }

        private sealed class ParityDriver
        {
            private readonly GuardFsm _fsm = new(GuardAction.Patrol);
            private readonly GuardBt _bt;
            private readonly GuardProfile _profile;

            public ParityDriver(GuardProfile profile)
            {
                _profile = profile;
                _bt = new GuardBt(GuardFsm.EvalGuard);
            }

            public BtTickResult Step(GuardPercepts percepts, string expectedState, GuardAction expectedAction)
            {
                var fsmResult = _fsm.Tick(percepts, _profile);
                var btResult = _bt.Tick(percepts, _profile);

                var fsmState = _fsm.State.ToString().ToLowerInvariant();
                Assert.That(fsmState, Is.EqualTo(expectedState), $"tick {percepts.Tick} fsm state");
                Assert.That(btResult.ActiveLeaf, Is.EqualTo(expectedState), $"tick {percepts.Tick} bt state");
                Assert.That(fsmResult.Intent.Action, Is.EqualTo(expectedAction), $"tick {percepts.Tick} fsm action");
                Assert.That(btResult.Intent.Action, Is.EqualTo(expectedAction), $"tick {percepts.Tick} bt action");
                AssertSameIntent(fsmResult.Intent, btResult.Intent);
                return btResult;
            }
        }
    }
}
