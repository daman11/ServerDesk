using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using SkiaSharp;

internal static class PersonalizationChecks
{
    internal static void Run(MainWindow owner,string root)
    {
        using var art=new SKBitmap(new SKImageInfo(320,120,SKColorType.Rgba8888,SKAlphaType.Premul));
        using(var canvas=new SKCanvas(art)) { canvas.Clear(SKColors.Transparent); using var paint=new SKPaint { Color=SKColor.Parse("#E5B65C") }; canvas.DrawRoundRect(new SKRect(0,0,120,120),20,20,paint); paint.Color=SKColor.Parse("#4C9CE5"); canvas.DrawRoundRect(new SKRect(150,0,320,120),20,20,paint); }
        using var image=SKImage.FromBitmap(art); using var png=image.Encode(SKEncodedImageFormat.Png,100); using var source=new MemoryStream(png.ToArray());
        var importing=ProfileImageImport.ImportAsync(source); RedesignChecks.Wait(importing); var normalized=importing.GetAwaiter().GetResult(); ProfileAppearance.Validate("#E5B65C",normalized);
        using(var check=SKBitmap.Decode(Convert.FromBase64String(normalized))) { if(check.Width!=128 || check.Height!=128 || check.GetPixel(64,0).Alpha!=0 || check.GetPixel(20,64).Alpha<200)throw new Exception("Custom image aspect ratio/transparency not preserved"); }
        foreach(var format in new[]{SKEncodedImageFormat.Jpeg,SKEncodedImageFormat.Webp}) { using var encoded=image.Encode(format,90); ProfileAppearance.Validate("",ProfileImageImport.Normalize(encoded.ToArray())); }
        try { ProfileImageImport.Normalize([1,2,3,4]); throw new Exception("Corrupt image accepted"); } catch(InvalidDataException) { }
        try { ProfileImageImport.Normalize(new byte[ProfileImageImport.MaximumSourceBytes+1]); throw new Exception("Oversize image accepted"); } catch(InvalidDataException) { }
        using(var wide=new SKBitmap(5000,1)) { wide.Erase(SKColors.Red); using var wideImage=SKImage.FromBitmap(wide); using var widePng=wideImage.Encode(SKEncodedImageFormat.Png,100); try { ProfileImageImport.Normalize(widePng.ToArray()); throw new Exception("Excessive source dimensions accepted"); } catch(InvalidDataException) { } }
        var edited=Dialogs.Edit(owner,new ServerProfile { Name="Own artwork",Host="localhost",Username="admin" },[]); RedesignChecks.Frames(); var editor=owner.OwnedWindows.Single(); var picker=editor.GetLogicalDescendants().OfType<ProfileAppearancePicker>().Single(); picker.SetImage(normalized);
        picker.ColorInput.Text="invalid draft"; picker.SetImage("");
        if(picker.CustomIconPng!="" || picker.ColorInput.Text!="invalid draft")throw new Exception("Removing artwork failed while color was being edited");
        picker.SetImage(normalized);
        editor.GetLogicalDescendants().OfType<TextBox>().Single(b=>AutomationProperties.GetName(b)=="Kolor serwera").Text="#E5B65C"; PolishChecks.Page(editor,1); RedesignChecks.Frames();
        if(!editor.GetVisualDescendants().OfType<Image>().Any(i=>i.IsVisible && i.Source!=null))throw new Exception("Custom artwork preview absent");
        editor.CaptureRenderedFrame()!.Save(Path.Combine(root,"profile-appearance.png")); editor.GetLogicalDescendants().OfType<Button>().Single(b=>b.Tag as string=="Zapisz").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Wait(edited);
        var profile=edited.GetAwaiter().GetResult()??throw new Exception("Appearance dialog did not save"); if(profile.Color!="#E5B65C" || profile.CustomIconPng!=normalized)throw new Exception("Dialog lost color/custom icon");
        var repo=new ProfileRepository(new AppPaths(Path.Combine(root,"docking-data-"+Guid.NewGuid().ToString("N")))); repo.InitializeAsync().GetAwaiter().GetResult(); repo.SaveAsync(profile).GetAwaiter().GetResult();
        var vm=new MainViewModel(repo); var main=new MainWindow(vm,repo,new CredentialStore(new AppPaths(Path.Combine(root,"docking-secrets-"+Guid.NewGuid().ToString("N")))),new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
        var profiles=new[]{profile,profile with { Id=Guid.NewGuid(),Name="Database",IconId="database",CustomIconPng="",Color="#69CFB6" },profile with { Id=Guid.NewGuid(),Name="Cloud",IconId="cloud",CustomIconPng="",Color="#EB8181" }};
        var panes=profiles.Select(p=>new SessionPane(main,p,new SshConnectionFactory(repo),new ExternalRdpClient(),repo)).ToArray(); main.RegisterPane(panes[0]); main.RegisterPane(panes[1]); RedesignChecks.Frames(); var parent=panes[0].Parent;
        main.SetWorkspaceMode(WorkspaceMode.Columns); RedesignChecks.Frames();
        if(!panes.Take(2).All(p=>p.IsVisible && p.IsActive) || !ReferenceEquals(parent,panes[0].Parent) || !ReferenceEquals(parent,panes[1].Parent))throw new Exception("Split did not keep visible sessions in their original parent");
        if(panes[0].Bounds.Right>panes[1].Bounds.Left)throw new Exception("Column panes overlap"); main.CaptureRenderedFrame()!.Save(Path.Combine(root,"split-columns.png"));
        main.RegisterPane(panes[2]); RedesignChecks.Frames(); main.KeyPress(Key.Tab,RawInputModifiers.Control,PhysicalKey.Tab,"\t"); RedesignChecks.Frames(); if(main.Workspace.Focused!=panes[1].WorkspaceId)throw new Exception("Ctrl+Tab did not cycle in focused group");
        main.SetWorkspaceMode(WorkspaceMode.Rows); RedesignChecks.Frames(); if(panes[0].Bounds.Bottom>panes[1].Bounds.Top)throw new Exception("Row panes overlap"); main.CaptureRenderedFrame()!.Save(Path.Combine(root,"split-rows.png"));
        main.Width=900; main.Height=600; main.SetRenderScaling(1.5); RedesignChecks.Frames(); main.CaptureRenderedFrame()!.Save(Path.Combine(root,"split-dpi150.png")); main.SetRenderScaling(1); main.Width=1320; main.Height=840;
        main.DockSession(panes[0],SessionDock.Floating); RedesignChecks.Frames(); var floating=main.OwnedWindows.Single(); if(main.Workspace.Location(panes[0].WorkspaceId)!=SessionDock.Floating || !panes[0].IsVisible)throw new Exception("Session did not detach"); floating.CaptureRenderedFrame()!.Save(Path.Combine(root,"floating-session.png"));
        floating.Close(); RedesignChecks.Frames(); if(main.OwnedWindows.Count!=0 || main.Workspace.Location(panes[0].WorkspaceId)!=SessionDock.Primary || !ReferenceEquals(parent,panes[0].Parent))throw new Exception("Closing floating window did not redock its session");
        main.SetWorkspaceMode(WorkspaceMode.Single); RedesignChecks.Frames(); if(main.Workspace.Primary.Count!=3 || main.Workspace.Secondary.Count!=0)throw new Exception("Merging panels lost sessions");
        var tab=main.GetVisualDescendants().OfType<TabItem>().Single(t=>ReferenceEquals(t.Tag,panes[2])); var dockMenu=((IEnumerable<MenuItem>)tab.ContextMenu!.ItemsSource!).Single(m=>m.Header as string=="Dokuj do panelu 2"); dockMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); RedesignChecks.Frames(); if(main.Workspace.Location(panes[2].WorkspaceId)!=SessionDock.Secondary)throw new Exception("Tab docking menu failed");
        panes[2].UpdatePresentation(profiles[2] with { CustomIconPng=normalized,Color="#66C5D6",Name="Updated artwork" }); RedesignChecks.Frames(); if(!main.GetVisualDescendants().OfType<ProfileVisual>().Any(p=>p.Profile.CustomIconPng==normalized && p.Profile.Name=="Updated artwork"))throw new Exception("Live presentation did not update custom image");
        DesignSystem.Apply("light"); RedesignChecks.Frames(); main.CaptureRenderedFrame()!.Save(Path.Combine(root,"split-light.png")); DesignSystem.Apply("dark");
        var close=main.CloseSessionAsync(panes[0]); RedesignChecks.Frames(); Confirm(main); RedesignChecks.Wait(close); if(main.OpenSessions.Count!=2 || main.Workspace.Primary.Concat(main.Workspace.Secondary).Count()!=2)throw new Exception("Closing one docked session affected others");
        main.Workspace.SetRatio(.62); main.SetWorkspaceMode(WorkspaceMode.Rows); RedesignChecks.Wait(main.SaveWorkspaceAsync()); foreach(var pane in main.OpenSessions.ToArray())main.DockSession(pane,SessionDock.Floating); RedesignChecks.Frames(); if(main.OwnedWindows.Count!=2)throw new Exception("Multiple floating sessions not isolated");
        main.Close(); RedesignChecks.Frames(); var cancel=main.OwnedWindows.Single(w=>w.GetLogicalDescendants().OfType<Button>().Any(b=>b.Tag as string=="Potwierdź")); cancel.GetLogicalDescendants().OfType<Button>().Single(b=>b.Tag as string=="Anuluj").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(); if(main.OwnedWindows.Count!=2 || main.OpenSessions.Count!=2 || main.Workspace.Floating.Count!=2)throw new Exception("Cancelling shutdown disturbed floating sessions");
        main.Close(); RedesignChecks.Frames(); Confirm(main); RedesignChecks.Frames(20); if(main.OwnedWindows.Count!=0)throw new Exception("Main shutdown left floating windows");
        var restored=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(new AppPaths(Path.Combine(root,"docking-secrets-restored-"+Guid.NewGuid().ToString("N")))),new SshConnectionFactory(repo),new ExternalRdpClient()); restored.Show(); RedesignChecks.Frames(20);
        if(restored.Workspace.Mode!=WorkspaceMode.Rows || Math.Abs(restored.Workspace.Ratio-.62)>.001 || restored.OpenSessions.Count!=0)throw new Exception("Layout restoration failed or auto-connected sessions"); restored.Close(); RedesignChecks.Frames();
        Console.WriteLine("PASS PNG/JPEG/WebP normalization, transparent aspect ratio, bounded/corrupt input, profile color/image save, split columns/rows/DPI, group keyboard cycling, docking menu, floating window redock, live appearance, isolated close and persisted layout without automatic connections");
    }
    internal static void Network(MainWindow main,string root)
    {
        var panes=main.OpenSessions.ToArray(); var terminals=panes.Select(p=>p.Children.OfType<TerminalControl>().Single()).ToArray(); var ids=terminals.Select(t=>t.SessionId).ToArray(); var emulators=terminals.Select(t=>t.Terminal).ToArray();
        panes[0].UpdatePresentation(panes[0].Profile with { Color="#E5B65C",IconId="server" }); panes[1].UpdatePresentation(panes[1].Profile with { Color="#69CFB6",IconId="database" });
        main.FocusSession(panes[1]); main.SetWorkspaceMode(WorkspaceMode.Columns); RedesignChecks.Wait(terminals[0].SendInputAsync("printf '\\nDOCK-CONTINUITY: same SSH session\\n'; echo shell-pid=$$\r",CancellationToken.None)); RedesignChecks.Wait(panes[1].ShowAsync(2)); RedesignChecks.Frames(240);
        Verify(); main.CaptureRenderedFrame()!.Save(Path.Combine(root,"live-split-columns.png")); main.SetWorkspaceMode(WorkspaceMode.Rows); RedesignChecks.Frames(); Verify(); main.CaptureRenderedFrame()!.Save(Path.Combine(root,"live-split-rows.png"));
        main.DockSession(panes[0],SessionDock.Floating); RedesignChecks.Frames(30); Verify(); var floating=main.OwnedWindows.Single(); RedesignChecks.Wait(terminals[0].SendInputAsync("printf 'SSH continues in floating window\\n'\r",CancellationToken.None)); RedesignChecks.Frames(30); floating.CaptureRenderedFrame()!.Save(Path.Combine(root,"live-floating.png"));
        floating.Close(); RedesignChecks.Frames(30); Verify(); main.SetWorkspaceMode(WorkspaceMode.Single); RedesignChecks.Wait(panes[1].ShowAsync(0)); main.FocusSession(panes[1]); RedesignChecks.Frames(); Verify();
        void Verify() { for(var i=0;i<panes.Length;i++)if(!panes[i].IsConnected || !ReferenceEquals(emulators[i],terminals[i].Terminal) || ids[i]!=terminals[i].SessionId)throw new Exception("Docking replaced/disconnected SSH or its terminal"); }
        Console.WriteLine("PASS real SSH continuity across split directions, floating window and redock: same live transport/terminal session IDs, monitoring concurrently visible, input after reparent");
    }
    private static void Confirm(MainWindow main)
    {
        var dialog=main.OwnedWindows.Single(w=>w.GetLogicalDescendants().OfType<Button>().Any(b=>b.Tag as string=="Potwierdź")); dialog.GetLogicalDescendants().OfType<Button>().Single(b=>b.Tag as string=="Potwierdź").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
}
