namespace ServerDesk.Core;

public enum TransferState { Queued, Connecting, Running, Completed, CompletedWithSkips, Skipped, Cancelled, Failed }

public sealed record TransferOutcome(int Files=0,int Directories=0,int Skipped=0)
{
    public TransferState State=>Skipped==0?TransferState.Completed:Files+Directories==0?TransferState.Skipped:TransferState.CompletedWithSkips;
    public static TransferOutcome operator +(TransferOutcome left,TransferOutcome right)=>new(checked(left.Files+right.Files),checked(left.Directories+right.Directories),checked(left.Skipped+right.Skipped));
}
