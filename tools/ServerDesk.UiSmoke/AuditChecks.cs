using Avalonia.Controls;
using Avalonia.LogicalTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class AuditChecks
{
    internal static void Run(MainWindow owner,ProfileRepository repository)
    {
        using var pane=new SessionPane(owner,new ServerProfile { Name="Audit path editing",Host="localhost",Username="audit" },new SshConnectionFactory(repository),new ExternalRdpClient(),repository);
        var window=new Window { Width=900,Height=600,Content=pane }; window.Show(); RedesignChecks.Frames();
        var remotePath=pane.GetLogicalDescendants().OfType<TextBox>().Single(t=>t.Text=="/");
        try
        {
            foreach(var path in new[]{"","h","home/admin","/","/home/żółty katalog",""})
            { remotePath.Text=path; RedesignChecks.Frames(); }
            Console.WriteLine("PASS regression: empty, relative and Unicode remote SFTP path editing");
        }
        finally { pane.Dispose(); window.Close(); }
    }
}
