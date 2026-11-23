using HeistSim;
using NUnit.Framework;

namespace HeistSim.Tests
{
    /// <summary>Timed channel progress, completion, clearing and cancellation.</summary>
    public sealed class ChannelTests
    {
        [Test]
        public void ChannelStillRunningMidHold()
        {
            var st = new HeistState();
            st.StartChannel("steal", "m_x", 2f);

            Assert.That(st.TickChannel(0.5f, false), Is.EqualTo(ChannelResult.Running));
            Assert.That(st.Channel.Progress, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(st.Channel.Kind, Is.EqualTo("steal"));
            Assert.That(st.Channel.Id, Is.EqualTo("m_x"));
        }

        [Test]
        public void ChannelCompletesAtHoldTime()
        {
            var st = new HeistState();
            st.StartChannel("steal", "m_x", 2f);

            Assert.That(st.TickChannel(0.5f, false), Is.EqualTo(ChannelResult.Running));
            Assert.That(st.TickChannel(1.6f, false), Is.EqualTo(ChannelResult.Done));
        }

        [Test]
        public void ChannelCompletesOnTheExactHoldBoundary()
        {
            var st = new HeistState();
            st.StartChannel("steal", "m_x", 2f);

            st.TickChannel(0.5f, false);

            Assert.That(st.TickChannel(1.5f, false), Is.EqualTo(ChannelResult.Done));
        }

        [Test]
        public void ChannelClearedOnCompletion()
        {
            var st = new HeistState();
            st.StartChannel("steal", "m_x", 0.5f);

            st.TickChannel(0.5f, false);

            Assert.That(st.Channel, Is.Null);
        }

        [Test]
        public void CompletedChannelDoesNotCompleteTwice()
        {
            var st = new HeistState();
            st.StartChannel("steal", "m_x", 0.5f);
            st.TickChannel(0.5f, false);

            Assert.That(st.TickChannel(0.5f, false), Is.EqualTo(ChannelResult.None));
        }

        [Test]
        public void ChannelCancelsWhenInterrupted()
        {
            var st = new HeistState();
            st.StartChannel("steal", "m_x", 2f);
            st.TickChannel(0.5f, false);
            var progress = st.Channel.Progress;

            Assert.That(st.TickChannel(0.5f, true), Is.EqualTo(ChannelResult.Cancelled));
            Assert.That(st.Channel, Is.Null);
            Assert.That(progress, Is.EqualTo(0.5f).Within(1e-6f));
        }

        [Test]
        public void InterruptionWinsOverProgress()
        {
            var st = new HeistState();
            st.StartChannel("steal", "m_x", 0.5f);

            Assert.That(st.TickChannel(5f, true), Is.EqualTo(ChannelResult.Cancelled));
            Assert.That(st.Channel, Is.Null);
        }

        [Test]
        public void CancelledChannelTicksNoneAfterwards()
        {
            var st = new HeistState();
            st.StartChannel("steal", "m_x", 2f);
            st.TickChannel(0.1f, true);

            Assert.That(st.TickChannel(0.1f, false), Is.EqualTo(ChannelResult.None));
        }

        [Test]
        public void TickWithoutChannelDoesNothing()
        {
            var st = new HeistState();

            Assert.That(st.TickChannel(1f, false), Is.EqualTo(ChannelResult.None));
            Assert.That(st.TickChannel(1f, true), Is.EqualTo(ChannelResult.None));
        }

        [Test]
        public void HoldIsClampedToTheMinimum()
        {
            var st = new HeistState();
            st.StartChannel("steal", "m_x", 0.05f);

            Assert.That(st.Channel.Hold, Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(st.TickChannel(0.19f, false), Is.EqualTo(ChannelResult.Running));
            Assert.That(st.TickChannel(0.01f, false), Is.EqualTo(ChannelResult.Done));
        }

        [Test]
        public void CancelChannelClearsWithoutCompleting()
        {
            var st = new HeistState();
            st.StartChannel("steal", "m_x", 2f);

            st.CancelChannel();

            Assert.That(st.Channel, Is.Null);
            Assert.That(st.TickChannel(1f, false), Is.EqualTo(ChannelResult.None));
        }
    }
}
