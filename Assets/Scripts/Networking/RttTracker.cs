using System;

namespace QuestLAN.Networking
{
    /// <summary>Only the requesting machine's monotonic clock is used.</summary>
    public sealed class RttTracker
    {
        private uint sequence;
        private double sentAt;
        private double deadline;
        private bool pending;
        public bool HasSample { get; private set; }
        public bool TimedOut { get; private set; }
        public double CurrentMilliseconds { get; private set; }
        public double SmoothedMilliseconds { get; private set; }
        public int SampleCount { get; private set; }

        public uint Begin(double now)
        {
            sentAt = now;
            deadline = now + 1;
            pending = true;
            return unchecked(++sequence);
        }

        public bool Complete(uint token, double now)
        {
            if (!pending || token != sequence || now < sentAt) return false;
            if (now > deadline) { pending = false; TimedOut = true; return false; }
            CurrentMilliseconds = (now - sentAt) * 1000;
            SmoothedMilliseconds = HasSample ? SmoothedMilliseconds * 0.8 + CurrentMilliseconds * 0.2 : CurrentMilliseconds;
            HasSample = true;
            TimedOut = false;
            pending = false;
            SampleCount++;
            return true;
        }

        public void Tick(double now)
        {
            if (!pending || now <= deadline) return;
            pending = false;
            TimedOut = true;
        }

        public void Reset()
        {
            // Do not reuse a token if a late reply survives a local pause.
            pending = HasSample = TimedOut = false;
            CurrentMilliseconds = SmoothedMilliseconds = 0;
            SampleCount = 0;
        }
    }
}
