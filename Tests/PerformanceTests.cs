using System;
using System.Diagnostics;
using Mulld.Priorities;

public static class PerformanceTests
{
    public static string Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
        Check(!PerformanceMetrics.Enabled, "Diagnostics default off");
        PerformanceMetrics.Reset();
        using (PerformanceMetrics.Measure(PerformanceOperation.Plan)) { }
        Check(PerformanceMetrics.Get(PerformanceOperation.Plan).Samples == 0, "Disabled scope records nothing");
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i=0;i<10000;i++) { using var scope=PerformanceMetrics.Measure(PerformanceOperation.Plan); PerformanceMetrics.CacheHit(); }
        Check(GC.GetAllocatedBytesForCurrentThread()==allocated, "Disabled instrumentation allocates zero bytes after initialization");
        PerformanceMetrics.Enabled = true;
        PerformanceMetrics.Record(PerformanceOperation.Plan, Stopwatch.Frequency, 100);
        PerformanceMetrics.Record(PerformanceOperation.Plan, Stopwatch.Frequency * 3, 300);
        var counter=PerformanceMetrics.Get(PerformanceOperation.Plan);
        Check(counter.Samples==2&&counter.AverageMs==2000&&counter.WorstMs==3000&&counter.AverageBytes==200&&counter.MaxBytes==300, "Timings and allocations aggregate correctly");
        using (PerformanceMetrics.Measure(PerformanceOperation.NeedsScan)) { PerformanceMetrics.CacheHit(); }
        Check(PerformanceMetrics.Get(PerformanceOperation.NeedsScan).Samples==1&&PerformanceMetrics.SnapshotHits==1,"Enabled scope records and counts cached reads");
        var stale=PerformanceMetrics.Measure(PerformanceOperation.Apply);PerformanceMetrics.Reset();stale.Dispose();
        Check(PerformanceMetrics.Get(PerformanceOperation.Apply).Samples==0&&PerformanceMetrics.SnapshotHits==0,"Reset clears fixed counters and discards in-flight scope");
        stale=PerformanceMetrics.Measure(PerformanceOperation.Plan);PerformanceMetrics.Enabled=false;stale.Dispose();
        Check(PerformanceMetrics.Get(PerformanceOperation.Plan).Samples==0,"Disable discards in-flight measurement");
        return checks+" diagnostics checks passed.";
    }
}
