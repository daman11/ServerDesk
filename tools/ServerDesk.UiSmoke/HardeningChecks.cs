using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Automation;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class HardeningChecks
{
    internal static void Run(MainWindow main,ProfileRepository repository,string root)
    {
        var label=DesignSystem.Label("Dynamic resource test"); var button=new Button { Content="Control height" }; var panel=new StackPanel { Children={label,button} };
        DesignSystem.Resource(panel,StackPanel.SpacingProperty,"SpacingMedium"); var window=new Window { Width=400,Height=200,Content=panel }; window.Show(); RedesignChecks.Frames();
        var resources=Application.Current!.Resources; var oldFont=resources["FontBody"]; var oldHeight=resources["ControlHeight"]; var oldGap=resources["SpacingMedium"];
        try
        {
            resources["FontBody"]=16d; resources["ControlHeight"]=38d; resources["SpacingMedium"]=12d; RedesignChecks.Frames();
            if(label.FontSize!=16 || button.MinHeight!=38 || panel.Spacing!=12)throw new Exception("Common resource update did not reach existing UI");
        }
        finally { resources["FontBody"]=oldFont; resources["ControlHeight"]=oldHeight; resources["SpacingMedium"]=oldGap; window.Close(); }
        var sort=main.GetLogicalDescendants().OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)=="Sortowanie połączeń"); sort.SelectedIndex=1; RedesignChecks.Frames();
        if(repository.SettingAsync("serverSort").GetAwaiter().GetResult()!="Host")throw new Exception("Server sort choice did not persist"); sort.SelectedIndex=0; RedesignChecks.Frames();
        var sample=repository.ListAsync().GetAwaiter().GetResult().First(); repository.ConnectedAsync(sample).GetAwaiter().GetResult();
        var history=Dialogs.History(main,repository); RedesignChecks.Frames(); var historyWindow=main.OwnedWindows.Single();
        var list=historyWindow.GetLogicalDescendants().OfType<ListBox>().Single(); if(list.ItemCount!=1)throw new Exception("History view did not load the recorded connection");
        historyWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"connection-history.png")); historyWindow.Close(); RedesignChecks.Wait(history);
        var broken=new ProfileRepository(new AppPaths(Path.Combine(root,"uninitialized-settings-"+Guid.NewGuid().ToString("N"))));
        using var pane=new SessionPane(main,new ServerProfile { Name="Settings failure",Host="localhost",Username="audit" },new SshConnectionFactory(repository),new ExternalRdpClient(),broken);
        var errorWindow=new Window { Width=900,Height=400,Content=pane }; errorWindow.Show(); RedesignChecks.Wait(pane.ApplyAppearanceAsync()); RedesignChecks.Frames();
        if(!pane.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.IsVisible && t.Text?.Contains("Nie udało się wczytać ustawień wyglądu")==true))throw new Exception("Appearance failure was swallowed");
        errorWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"appearance-error.png")); pane.Dispose(); errorWindow.Close();
        var freshPaths=new AppPaths(Path.Combine(root,"fresh-start-"+Guid.NewGuid().ToString("N"))); var freshRepo=new ProfileRepository(freshPaths); var freshVm=new MainViewModel(freshRepo); var freshMain=new MainWindow(freshVm,freshRepo,new CredentialStore(freshPaths),new SshConnectionFactory(freshRepo),new ExternalRdpClient()); freshMain.Show(); RedesignChecks.Frames(20); if(freshMain.OwnedWindows.Count>0 || freshVm.Status!="Gotowy")throw new Exception("Fresh startup failed before schema initialization"); freshMain.Close(); RedesignChecks.Frames();
        Console.WriteLine("PASS fresh data directory startup");
        Console.WriteLine("PASS dynamic font/spacing/control-height resources, persisted sort, real history query and visible appearance failure");
    }
}


