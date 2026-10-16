using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ServerDesk.App;
using ServerDesk.Core;
using System.Runtime.InteropServices;

internal static class NativeRdpChecks
{
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    internal static void Run(string root,string[] args)
    {
        var desktop=args.Contains("--desktop"); var password="";
        if(desktop)
        {
            Console.WriteLine("RDP password (hidden, memory only):");
            if(Console.IsInputRedirected)password=Console.ReadLine()??"";
            else
            {
                var text=new System.Text.StringBuilder();
                while(true) { var key=Console.ReadKey(true); if(key.Key==ConsoleKey.Enter)break; if(key.Key==ConsoleKey.Backspace) { if(text.Length>0)text.Length--; } else if(!char.IsControl(key.KeyChar))text.Append(key.KeyChar); }
                password=text.ToString(); text.Clear();
            }
        }
        if(desktop) { NativeRdpWorkspaceChecks.Run(root,args,password); return; }
        var host=new WindowsRdpHost(); var window=new Window { Title="ServerDesk – embedded RDP test",Width=1000,Height=650,Content=host };
        using var done=new CancellationTokenSource(); Exception? failure=null; var step=0; nint handle=0;
        var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(2) };
        timer.Tick+=(_,_)=>
        {
            try
            {
                if(step==0)
                {
                    host.Ready.GetAwaiter().GetResult(); handle=host.WindowHandle; Console.WriteLine("PASS native RDP control created; version="+host.Version); window.Content=null;
                }
                else if(step==1)window.Content=host;
                else
                {
                    if(handle!=host.WindowHandle)throw new Exception("RDP control was recreated when reattached");
                    host.Dispose(); host.Dispose(); if(IsWindow(handle))throw new Exception("Disposed native RDP window still exists");
                    Console.WriteLine("PASS delayed detach/reattach preserves HWND; repeated disposal releases HWND"); timer.Stop(); window.Close(); done.Cancel();
                }
                step++;
            }
            catch(Exception ex) { failure=ex; timer.Stop(); host.Dispose(); window.Close(); done.Cancel(); }
        };
        window.Show(); timer.Start(); Dispatcher.UIThread.MainLoop(done.Token); if(failure!=null)throw failure;
    }
}
