using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using SkiaSharp;

internal static class ProfileArtworkChecks
{
    private static void Verify(bool condition,string message) { if(!condition)throw new Exception(message); }
    private static Image Picture(ProfileVisual view)=>view.Children.OfType<Image>().Single();
    private static Bitmap Loaded(ProfileVisual view)=>Picture(view).Source as Bitmap??throw new Exception("Missing custom artwork");
    private static void Fallback(ProfileVisual view)
    {
        Verify(Picture(view).Source==null && !Picture(view).IsVisible,"Fallback retained an image source");
        var icon=view.Children.OfType<DeskIcon>().Single();
        Verify(icon.IsVisible && icon.Glyph==DeskIcons.Resolve(ProfileIcons.Resolve(view.Profile)),"Wrong protocol/vector fallback");
    }
    private static string Artwork(int index)
    {
        using var bitmap=new SKBitmap(new SKImageInfo(128,128,SKColorType.Rgba8888,SKAlphaType.Premul));
        bitmap.Erase(new SKColor((byte)(index*37),(byte)(index*59),(byte)(index*83),255));
        using var canvas=new SKCanvas(bitmap); using var paint=new SKPaint { Color=SKColors.White };
        canvas.DrawCircle(64,64,12+index%40,paint);
        using var image=SKImage.FromBitmap(bitmap); using var png=image.Encode(SKEncodedImageFormat.Png,100);
        return ProfileImageImport.Normalize(png.ToArray());
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Replace(ProfileVisual view,ServerProfile profile)
    {
        var old=Loaded(view); view.Profile=profile;
        Verify(!ReferenceEquals(old,Loaded(view)),"Changed artwork reused the old bitmap");
        return new WeakReference(old);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Reattach(WrapPanel host,ProfileVisual view)
    {
        var old=Loaded(view); host.Children.Remove(view); Fallback(view); host.Children.Add(view); RedesignChecks.Frames(2);
        Verify(!ReferenceEquals(old,Loaded(view)),"Reattached view retained the detached bitmap");
        return new WeakReference(old);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<WeakReference> Exercise(string root,string language)
    {
        var references=new List<WeakReference>(); var art=Enumerable.Range(0,96).Select(Artwork).ToArray();
        Verify(art.Distinct().Count()==96,"Artwork fixture is not distinct");
        var profile=new ServerProfile { Name="Artwork fixture",Host="fixture.invalid",Username="fixture",CustomIconPng=art[0] };
        var first=new ProfileVisual(profile,32); var second=new ProfileVisual(profile,32);
        Verify(Picture(first).Source==null && Picture(second).Source==null,"Detached views decoded artwork");
        var host=new WrapPanel { Margin=new Thickness(16) }; var window=new Window { Width=960,Height=720,Content=new ScrollViewer { Content=host } };
        host.Children.Add(first); host.Children.Add(second); window.Show(); RedesignChecks.Frames(5);
        try
        {
            var before=Loaded(first); references.Add(new(before));
            Verify(before.PixelSize==new PixelSize(128,128),"Incorrect stored artwork dimensions");
            Verify(!ReferenceEquals(before,Loaded(second)),"Separate views share bitmap ownership");
            for(var i=0;i<30;i++)first.Profile=profile with { Name="node-"+i,Color=i%2==0?"#69CFB6":"#4C9CE5",Protocol=i%2==0?Protocol.SSH:Protocol.RDP };
            Verify(ReferenceEquals(before,Loaded(first)),"Metadata edits decoded the same artwork again");
            first.Profile=first.Profile with { Color="invalid legacy color" };
            Verify(ReferenceEquals(before,Loaded(first)),"Legacy color invalidated otherwise valid artwork");
            references.Add(Replace(first,profile with { CustomIconPng=art[1] }));
            Verify(Loaded(second).PixelSize==new PixelSize(128,128),"Changing one view invalidated the other view");
            references.Add(Reattach(host,first));
            var damaged=Convert.FromBase64String(art[0]); damaged[^5]^=1;
            foreach(var invalid in new string?[]{"",null,"not base64!",Convert.ToBase64String(damaged),new('A',(ProfileAppearance.MaximumIconBytes+2)/3*4+4)})
            {
                first.Profile=profile with { Protocol=Protocol.VNC,CustomIconPng=invalid! }; Fallback(first);
                first.Profile=first.Profile with { Name="Changed metadata" }; Fallback(first);
            }
            first.Profile=profile; Verify(Loaded(first).PixelSize==new PixelSize(128,128),"Valid artwork did not recover after fallback");
            var edit=Dialogs.Edit(window,profile,[]); RedesignChecks.Frames(); var editor=window.OwnedWindows.Single();
            PolishChecks.Page(editor,1);
            var previews=editor.GetVisualDescendants().OfType<ProfileVisual>().ToArray();
            Verify(previews.Length>=2,"Editor fixture lacks header and appearance previews");
            var editorSources=previews.Select(Loaded).ToArray(); references.AddRange(editorSources.Select(b=>new WeakReference(b)));
            var picker=editor.GetLogicalDescendants().OfType<ProfileAppearancePicker>().Single();
            foreach(var color in new[]{"#69CFB6","#4C9CE5","#E5B65C"})picker.ColorInput.Text=color;
            for(var i=0;i<previews.Length;i++)Verify(ReferenceEquals(editorSources[i],Loaded(previews[i])),"Live editor color preview replaced an unchanged bitmap");
            picker.SetImage(art[1]); foreach(var view in previews)Verify(!editorSources.Contains(Loaded(view)),"Editor failed to replace selected artwork");
            picker.SetImage(""); foreach(var view in previews)Fallback(view);
            picker.SetImage(art[0]); foreach(var view in previews)references.Add(new(Loaded(view)));
            editor.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Anuluj").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Wait(edit);
            Verify(edit.Result==null,"Artwork preview modified a cancelled profile"); foreach(var view in previews)Fallback(view);
            host.Children.Clear(); Fallback(first); Fallback(second);
            var views=art.Select((png,index)=>new ProfileVisual(profile with { Name="node-"+index,CustomIconPng=png },32)).ToArray();
            foreach(var view in views)host.Children.Add(new StackPanel { Width=100,Margin=new Thickness(4),Children={view,new TextBlock { Text=view.Profile.Name }} });
            RedesignChecks.Frames(5); var sources=views.Select(Loaded).ToArray(); references.AddRange(sources.Select(b=>new WeakReference(b)));
            var allocated=GC.GetAllocatedBytesForCurrentThread(); var clock=Stopwatch.StartNew();
            for(var pass=0;pass<30;pass++)foreach(var view in views)view.Profile=view.Profile with { Name="node-"+pass,Color=pass%2==0?"#69CFB6":"#4C9CE5" };
            clock.Stop(); allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
            for(var i=0;i<views.Length;i++)Verify(ReferenceEquals(sources[i],Loaded(views[i])),"Unchanged artwork was reloaded during batch metadata edits");
            RedesignChecks.Frames(3); window.CaptureRenderedFrame()!.Save(Path.Combine(root,"profile-artwork-"+language+".png"));
            File.WriteAllText(Path.Combine(root,"profile-artwork-"+language+".json"),JsonSerializer.Serialize(new { Images=views.Length,MetadataChanges=views.Length*30,UnchangedBitmapReplacements=0,ElapsedMilliseconds=clock.Elapsed.TotalMilliseconds,UiThreadAllocatedBytes=allocated }));
            foreach(var view in views)host.Children.Remove(view.Parent as Control??throw new Exception("Missing artwork card"));
            foreach(var view in views)Fallback(view);
        }
        finally { window.Close(); RedesignChecks.Frames(5); Fallback(first); Fallback(second); }
        return references;
    }
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var references=Exercise(root,language);
            for(var pass=0;pass<3;pass++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); RedesignChecks.Frames(2); }
            Verify(references.All(r=>!r.IsAlive),"Detached/closed views retained bitmap objects");
            Console.WriteLine("PASS "+language+": 96 distinct images and 2880 metadata edits reuse bitmaps; live editor cancel/replace/remove, independent ownership, detach/reattach, corrupt/null/oversized fallback and collected closed-view bitmaps");
        }
        AppText.SetLanguage("pl");
    }
}
