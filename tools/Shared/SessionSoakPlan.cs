namespace ServerDesk.Verification;

public sealed record SessionSoakPlan(int Seconds=14400,int Cycles=100,int SampleSeconds=15)
{
    public const int Sessions=8;
    public const long ManagedGrowthBudget=32L*1024*1024,PrivateGrowthBudget=128L*1024*1024;
    public const int HandleGrowthBudget=64,ThreadGrowthBudget=16;
    public const double MaximumSampleGapSeconds=60;
    public const double MaximumUiGapSeconds=5;
    public bool IsFourHourPlan=>Seconds>=14400 && Cycles>=100;
    public void Validate()
    {
        if(Seconds is <30 or >86400 || Cycles is <1 or >1000 || SampleSeconds is <1 or >30 || (double)Seconds/Cycles<1)
            throw new ArgumentException("Invalid bounded soak plan.");
    }
    public double CycleDueSeconds(int completed)=>Seconds*(completed+1d)/(Cycles+1);
    public static double CpuPercent(double cpuSeconds,double wallSeconds,int processors)
    {
        if(!double.IsFinite(cpuSeconds) || !double.IsFinite(wallSeconds) || cpuSeconds<0 || wallSeconds<=0 || processors<1)throw new ArgumentOutOfRangeException(nameof(cpuSeconds));
        return 100*cpuSeconds/wallSeconds/processors;
    }
    public static string[] RetentionFailures(SoakResources baseline,SoakResources final)
    {
        List<string> failures=[];
        if(final.ManagedBytes-baseline.ManagedBytes>ManagedGrowthBudget)failures.Add("managed-bytes");
        if(final.PrivateBytes-baseline.PrivateBytes>PrivateGrowthBudget)failures.Add("private-bytes");
        if(final.Handles-baseline.Handles>HandleGrowthBudget)failures.Add("handles");
        if(final.Threads-baseline.Threads>ThreadGrowthBudget)failures.Add("threads");
        return failures.ToArray();
    }
    public string[] CompletionFailures(double elapsed,int cycles,double maximumGap,double maximumUiGap=0)
    {
        List<string> failures=[];
        if(!double.IsFinite(elapsed) || elapsed<Seconds)failures.Add("duration");
        if(cycles<Cycles)failures.Add("cycles");
        if(!double.IsFinite(maximumGap) || maximumGap>MaximumSampleGapSeconds)failures.Add("observation-gap");
        if(!double.IsFinite(maximumUiGap) || maximumUiGap>MaximumUiGapSeconds)failures.Add("ui-heartbeat-gap");
        return failures.ToArray();
    }
}
public sealed record SoakResources(long ManagedBytes,long PrivateBytes,long WorkingSetBytes,int Handles,int Threads);
