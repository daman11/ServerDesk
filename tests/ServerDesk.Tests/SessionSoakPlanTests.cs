using ServerDesk.Verification;
using Xunit;
namespace ServerDesk.Tests;
public sealed class SessionSoakPlanTests
{
    [Fact] public void DefaultPlanRequestsFourHoursAndOneHundredCompleteCycles()
    { var plan=new SessionSoakPlan(); plan.Validate(); Assert.True(plan.IsFourHourPlan); Assert.Equal(8,SessionSoakPlan.Sessions); Assert.Equal(100,plan.Cycles); }
    [Fact] public void ShortCalibrationNeverQualifiesAsFourHourEvidence()
    { var plan=new SessionSoakPlan(60,3,5); plan.Validate(); Assert.False(plan.IsFourHourPlan); }
    [Fact] public void CyclesAreDistributedAcrossTheRun()
    { var plan=new SessionSoakPlan(); Assert.True(plan.CycleDueSeconds(0)>0); Assert.True(plan.CycleDueSeconds(99)<plan.Seconds); Assert.Equal(2*plan.CycleDueSeconds(0),plan.CycleDueSeconds(1)); }
    [Fact] public void CpuIsNormalizedToTheProcessesLogicalCpuCount()
    { Assert.Equal(25,SessionSoakPlan.CpuPercent(10,10,4)); }
    [Fact] public void InterruptedObservationCannotPassOnElapsedWallTimeAlone()
    { Assert.Contains("observation-gap",new SessionSoakPlan().CompletionFailures(14400,100,120)); }
    [Fact] public void MissingDurationAndCyclesAreSeparateFailures()
    { Assert.Equal(new[]{"duration","cycles"},new SessionSoakPlan().CompletionFailures(100,2,15)); }
    [Fact] public void RetentionUsesPostCleanupBaselineAndIndependentBudgets()
    { var baseline=new SoakResources(100,100,100,100,10); var final=baseline with { ManagedBytes=100+SessionSoakPlan.ManagedGrowthBudget+1,Handles=165 }; Assert.Equal(new[]{"managed-bytes","handles"},SessionSoakPlan.RetentionFailures(baseline,final)); }
    [Fact] public void DecreasingResourcesDoNotFail()
    { Assert.Empty(SessionSoakPlan.RetentionFailures(new(200,200,200,200,20),new(100,100,100,100,10))); }
    [Fact] public void NonFiniteObservationsCannotPass()
    { Assert.Equal(new[]{"duration","observation-gap"},new SessionSoakPlan().CompletionFailures(double.NaN,100,double.NaN)); }
    [Fact] public void InvalidCpuSamplesAreRejected()
    { Assert.Throws<ArgumentOutOfRangeException>(()=>SessionSoakPlan.CpuPercent(double.NaN,1,1)); }
    [Fact] public void LongUiStallIsNotHiddenByTheSampleInterval()
    { Assert.Contains("ui-heartbeat-gap",new SessionSoakPlan().CompletionFailures(14400,100,15,8)); }
    [Theory] [InlineData(0,100,15)] [InlineData(60,100,15)] [InlineData(60,2,60)]
    public void InvalidPlansCannotStart(int seconds,int cycles,int sample)
    { Assert.Throws<ArgumentException>(()=>new SessionSoakPlan(seconds,cycles,sample).Validate()); }
}
