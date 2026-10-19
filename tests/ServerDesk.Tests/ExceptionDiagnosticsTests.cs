using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class ExceptionDiagnosticsTests
{
    [Fact] public void Broken_stack_formatter_does_not_hide_original_exception_or_inner_cause()
    {
        var text=ExceptionDiagnostics.Describe(new BrokenTrace(new IOException("private-canary")));
        Assert.Contains(nameof(BrokenTrace),text); Assert.Contains("<stack trace unavailable>",text);
        Assert.Contains(nameof(IOException),text); Assert.DoesNotContain("private-canary",text);
    }
    [Fact] public void Oversized_trace_is_bounded_even_inside_an_aggregate()
    {
        var text=ExceptionDiagnostics.Describe(new AggregateException(new LargeTrace(),new IOException("private-canary")));
        Assert.InRange(text.Length,1,ExceptionDiagnostics.MaximumCharacters+12);
        Assert.EndsWith("<truncated>",text); Assert.DoesNotContain("private-canary",text);
    }
    [Fact] public void Deep_exception_chain_is_bounded_and_ignores_sensitive_data()
    {
        Exception error=new IOException("private-canary");
        for(var i=0;i<100;i++)error=new InvalidOperationException("private-canary",error);
        error.Data["password"]="private-canary";
        var text=ExceptionDiagnostics.Describe(error);
        Assert.Equal(16,text.Split("HResult=").Length-1); Assert.DoesNotContain("private-canary",text);
    }
    private sealed class BrokenTrace(Exception inner):Exception("private-canary",inner)
    { public override string? StackTrace=>throw new InvalidOperationException("private-canary"); }
    private sealed class LargeTrace:Exception
    { public override string? StackTrace=>new('x',ExceptionDiagnostics.MaximumCharacters*2); }
}
