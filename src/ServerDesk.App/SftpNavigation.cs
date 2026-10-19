using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using ServerDesk.Core;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private readonly DirectoryNavigationHistory localNavigation=new(OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal),remoteNavigation=new();
    private DirectoryNavigationTarget? localNavigationTarget,remoteNavigationTarget;
    private readonly Dictionary<bool,(Button Back,Button Forward,Button Up,Button Home)> fileNavigationButtons=[];
    private string remoteHome="";
    private int fileRefreshes;
    private bool FilePathConfirmed(bool remote)
    {
        var loaded=remote?loadedRemotePath:loadedLocalPath;
        return loaded.Length>0 && (remote?remotePath:localPath).Text==loaded;
    }
    private string FileDirectory(bool remote)
    {
        if(fileRefreshes>0 || !FilePathConfirmed(remote))throw new InvalidOperationException(L.T("Najpierw otwórz katalog, zatwierdzając ścieżkę klawiszem Enter."));
        return remote?loadedRemotePath:loadedLocalPath;
    }
    private Control FileNavigationBar(bool remote,Control breadcrumbs)
    {
        var prefix=remote?"Remote":"Local";
        Button Nav(string icon,string caption,string name,Func<Task> action)
        {
            var button=DeskIcons.Button(icon,caption,()=> { fileTarget.SelectedIndex=remote?1:0; _=owner.Safe(action); },28);
            Avalonia.Automation.AutomationProperties.SetName(button,(remote?L.T("Zdalny"):L.T("Lokalny"))+" · "+L.T(caption));
            button.Name="Sftp"+prefix+name; button.GotFocus+=(_,_)=>fileTarget.SelectedIndex=remote?1:0; return button;
        }
        var back=Nav("arrow-left","Wstecz · Alt+←","Back",()=>NavigateFileHistoryAsync(remote,false));
        var forward=Nav("arrow-right","Dalej · Alt+→","Forward",()=>NavigateFileHistoryAsync(remote,true));
        var up=Nav("up","Katalog nadrzędny · Alt+↑","Up",()=>NavigateParentAsync(remote));
        var home=Nav("house","Katalog domowy · Alt+Home","Home",()=>NavigateFileHomeAsync(remote));
        fileNavigationButtons[remote]=(back,forward,up,home);
        var buttons=new StackPanel { Orientation=Orientation.Horizontal,Spacing=2,Children={back,forward,up,home} };
        var row=new Grid { ColumnDefinitions=new("Auto,*"),ColumnSpacing=6,Children={buttons,breadcrumbs},ClipToBounds=true }; Grid.SetColumn(breadcrumbs,1);
        UpdateFileNavigation(); return row;
    }
    private Task NavigateFilesAsync(bool remote,string path,DirectoryNavigationTarget? target=null)
    {
        if(remote)remoteNavigationTarget=target; else localNavigationTarget=target;
        (remote?remotePath:localPath).Text=path; return RefreshFilesAsync();
    }
    private Task NavigateFileHistoryAsync(bool remote,bool forward)
    {
        var history=remote?remoteNavigation:localNavigation;
        return (forward?history.Forward:history.Back) is {} target?NavigateFilesAsync(remote,target.Path,target):Task.CompletedTask;
    }
    private Task NavigateFileHomeAsync(bool remote)=>NavigateFilesAsync(remote,remote?remoteHome:Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    private void UpdateFileNavigation()
    {
        foreach(var (remote,buttons) in fileNavigationButtons)
        {
            var history=remote?remoteNavigation:localNavigation; var ready=IsConnected && sftp?.IsConnected==true && fileRefreshes==0 && disposed==0;
            buttons.Back.IsEnabled=ready && history.Back!=null; buttons.Forward.IsEnabled=ready && history.Forward!=null;
            buttons.Home.IsEnabled=ready && (!remote || remoteHome.Length>0);
            buttons.Up.IsEnabled=ready && FilePathConfirmed(remote) && (remote?loadedRemotePath!="/":Directory.GetParent(loadedLocalPath)!=null);
        }
        foreach(var remote in new[]{false,true})
        {
            var list=remote?remoteList:localList; var loaded=remote?loadedRemotePath:loadedLocalPath;
            // Cached rows stay focusable for inspection after transport loss.
            // Mutations remain gated by connection state in UpdateFileSelection.
            list.IsEnabled=fileRefreshes==0 && (loaded.Length==0 || FilePathConfirmed(remote));
        }
        UpdateFileSelection();
    }
    private void ResetFileNavigation()
    {
        localNavigation.Clear(); remoteNavigation.Clear(); localNavigationTarget=remoteNavigationTarget=null; remoteHome="";
        loadedLocalPath=loadedRemotePath=""; localEntries=[]; remoteEntries=[]; localList.ItemsSource=remoteList.ItemsSource=null;
        UpdateFileNavigation();
    }
}
