using System.Reflection;
using System.Runtime.ExceptionServices;

internal static class NetworkBackendChecks
{
    // Reuse the exact deterministic network tests on hosts without an installed SDK.
    // The test framework is a dependency of this development harness, never the app.
    internal static void Run()=>RunFor([typeof(ServerDesk.Tests.NetworkToolsTests),typeof(ServerDesk.Tests.NetworkServicesTests)],"Network Tools");
    internal static void Sorting()=>RunFor([typeof(ServerDesk.Tests.SortingTests),typeof(ServerDesk.Tests.FileListingTests),typeof(ProfileOrderingTests)],"sorting");
    internal static void Transfers()=>RunFor([typeof(ServerDesk.Tests.TransferTraversalTests)],"transfer traversal");
    private static void RunFor(Type[] types,string label)
    {
        var count=0;
        foreach(var type in types)
        foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Instance).Where(method=>method.CustomAttributes.Any(attribute=>attribute.AttributeType.FullName is "Xunit.FactAttribute" or "Xunit.TheoryAttribute")))
        {
            var data=method.CustomAttributes.Where(attribute=>attribute.AttributeType.FullName=="Xunit.InlineDataAttribute").ToArray();
            var cases=data.Length==0?new[]{Array.Empty<object?>()}:data.Select(attribute=>((IEnumerable<CustomAttributeTypedArgument>)attribute.ConstructorArguments[0].Value!).Select(argument=>argument.Value).ToArray()).ToArray();
            foreach(var arguments in cases)
            {
                var parameters=method.GetParameters(); for(var i=0;i<arguments.Length;i++)if(parameters[i].ParameterType.IsEnum)arguments[i]=Enum.ToObject(parameters[i].ParameterType,arguments[i]!);
                var instance=Activator.CreateInstance(type)!;
                try
                {
                    object? result;
                    try { result=method.Invoke(instance,arguments); } catch(TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException??error).Throw(); throw; }
                    if(result is Task task)RedesignChecks.Wait(task);
                    count++; Console.WriteLine("PASS backend "+method.Name);
                }
                finally { (instance as IDisposable)?.Dispose(); }
            }
        }
        Console.WriteLine($"PASS {count} shared {label} backend cases on {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
    }
}
