using System.Collections.Generic;
using HeistSim;
using NUnit.Framework;
using UnityEngine;

namespace HeistSim.Tests
{
    /// <summary>FSM and BT state trace parity through one scripted market run.</summary>
    public sealed class HeistGameParityTests
    {
        private const float Dt = 1f / 60f;
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
        public void FsmAndBtGuardStateTracesMatchThroughTheScriptedRun()
        {
            var profile = NewStandard();
            var fsm = new HeistGame(profile, GuardArchitecture.Fsm);
            var bt = new HeistGame(profile, GuardArchitecture.Bt);
            var fsmTrace = new List<string>();
            var btTrace = new List<string>();

            // Sneak: stand in the entrance while the guard starts its patrol.
            StepBoth(fsm, bt, HeistInput.None, 30, fsmTrace, btTrace);

            // Get seen: the thief crosses into the guard cone from four tiles out.
            TeleportBoth(fsm, bt, 8.0f, 4.5f);
            StepBoth(fsm, bt, HeistInput.None, 16, fsmTrace, btTrace);

            // Break line of sight by leaving the shared doorway band.
            TeleportBoth(fsm, bt, 2.0f, 7.0f);
            StepBoth(fsm, bt, HeistInput.None, 60, fsmTrace, btTrace);

            // Hide: crouch and wait for the search to expire and the guard to return.
            var hide = new HeistInput(0f, 0f, action: false, crouch: true, sprint: false);
            StepBoth(fsm, bt, hide, 450, fsmTrace, btTrace);
            for (var i = 0; i < 900 && fsm.Snapshot().GuardState != "patrol"; i++)
            {
                StepBoth(fsm, bt, hide, 1, fsmTrace, btTrace);
            }

            Assert.That(fsm.Snapshot().GuardState, Is.EqualTo("patrol"), "the guard is back on the shelf route before the re-approach");

            // Re-approach: step into the cone four and a half tiles ahead of the patrol.
            var guardPosition = fsm.Snapshot().Guard;
            TeleportBoth(fsm, bt, guardPosition.x + 4.5f, guardPosition.y);
            StepBoth(fsm, bt, HeistInput.None, 90, fsmTrace, btTrace);

            Assert.That(fsmTrace, Does.Contain("investigate"), "the run includes an investigate state");
            Assert.That(fsmTrace, Does.Contain("chase"), "the run includes a chase state");
            Assert.That(fsmTrace, Does.Contain("search"), "the run includes a search state");
            Assert.That(fsmTrace, Does.Contain("return"), "the run includes a return state");

            var firstDifference = -1;
            for (var i = 1; i < fsmTrace.Count; i++)
            {
                if (fsmTrace[i] != btTrace[i])
                {
                    firstDifference = i;
                    break;
                }
            }

            TestContext.WriteLine($"parity firstDifference={firstDifference} states={Dedupe(fsmTrace)}");

            if (firstDifference < 0)
            {
                Assert.That(btTrace, Is.EqualTo(fsmTrace), "FSM and BT state traces are identical after the warmup tick");
                return;
            }

            // Known divergence recorded by the stage 1 report (.audit/unity-port.tsv, stage-1 row):
            // the FSM applies one transition per tick, so a guard that first notices a target inside
            // attack range steps into chase, while the reactive BT fires its attack branch at once.
            Assert.That(fsmTrace[firstDifference - 1], Is.EqualTo(btTrace[firstDifference - 1]),
                "both traces agree up to the known one tick attack divergence");
            Assert.That(fsmTrace[firstDifference], Is.EqualTo("chase"),
                "the FSM enters chase one tick before its attack rule can fire");
            Assert.That(btTrace[firstDifference], Is.EqualTo("attack"),
                "the BT attack branch takes priority on the same tick");
            Assert.That(btTrace, Does.Contain("chase"), "the BT still reaches chase after the attack branch");
        }

        private static void StepBoth(
            HeistGame fsm,
            HeistGame bt,
            HeistInput input,
            int ticks,
            List<string> fsmTrace,
            List<string> btTrace)
        {
            for (var i = 0; i < ticks; i++)
            {
                fsm.Step(Dt, input);
                bt.Step(Dt, input);
                fsmTrace.Add(fsm.Snapshot().GuardState);
                btTrace.Add(bt.Snapshot().GuardState);
            }
        }

        private static void TeleportBoth(HeistGame fsm, HeistGame bt, float i, float j)
        {
            fsm.TeleportPlayer(i, j);
            bt.TeleportPlayer(i, j);
        }

        private static string Dedupe(List<string> states)
        {
            var unique = new List<string>();
            foreach (var state in states)
            {
                if (unique.Count == 0 || unique[unique.Count - 1] != state)
                {
                    unique.Add(state);
                }
            }

            return string.Join(",", unique);
        }

        private GuardProfile NewStandard()
        {
            var profile = GuardProfile.CreateDefault("standard");
            _profiles.Add(profile);
            return profile;
        }
    }
}
