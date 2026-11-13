using System;
using System.Diagnostics;

namespace Mulld.Priorities
{
    public enum PerformanceOperation { NeedsScan, Plan, Validate, Apply, Automation, PanelDraw, Count }

    public struct PerformanceCounter
    {
        public long Samples, TotalTicks, MaxTicks, TotalBytes, MaxBytes;
        public double AverageMs => Samples == 0 ? 0 : TotalTicks * 1000d / Stopwatch.Frequency / Samples;
        public double WorstMs => MaxTicks * 1000d / Stopwatch.Frequency;
        public double AverageBytes => Samples == 0 ? 0 : (double)TotalBytes / Samples;
    }

    // Fixed storage, no timers, logging, process polling or sampling thread.
    // Only synchronous mulld operations are measured, on their calling thread.
    public static class PerformanceMetrics
    {
        private static readonly PerformanceCounter[] counters = new PerformanceCounter[(int)PerformanceOperation.Count];
        private static bool enabled;
        private static int generation;
        public static long SnapshotHits { get; private set; }
        public static bool Enabled
        {
            get => enabled;
            set { if (enabled != value) { enabled = value; generation++; } }
        }
        public static PerformanceCounter Get(PerformanceOperation operation) => counters[(int)operation];
        public static void CacheHit() { if (enabled) SnapshotHits++; }
        public static void Reset() { Array.Clear(counters); SnapshotHits = 0; generation++; }
        public static Scope Measure(PerformanceOperation operation) => enabled ? new Scope(operation, generation) : default;
        internal static void Record(PerformanceOperation operation, long ticks, long bytes)
        {
            ref var counter = ref counters[(int)operation];
            counter.Samples++;
            counter.TotalTicks += ticks;
            counter.TotalBytes += bytes;
            if (ticks > counter.MaxTicks) counter.MaxTicks = ticks;
            if (bytes > counter.MaxBytes) counter.MaxBytes = bytes;
        }
        public readonly struct Scope : IDisposable
        {
            private readonly PerformanceOperation operation;
            private readonly long start, allocated;
            private readonly int epoch;
            private readonly bool active;
            internal Scope(PerformanceOperation operation, int epoch)
            {
                this.operation = operation; this.epoch = epoch; active = true;
                allocated = GC.GetAllocatedBytesForCurrentThread(); start = Stopwatch.GetTimestamp();
            }
            public void Dispose()
            {
                if (!active || !enabled || epoch != generation) return;
                long elapsed = Stopwatch.GetTimestamp() - start;
                Record(operation, elapsed, GC.GetAllocatedBytesForCurrentThread() - allocated);
            }
        }
    }
}
