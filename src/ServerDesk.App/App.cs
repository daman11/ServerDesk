using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Microsoft.Extensions.DependencyInjection;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Serilog;
using System.Reflection;

namespace ServerDesk.App;
public sealed class App : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
        DesignSystem.Initialize(this);
        Styles.Add(new StyleInclude(new Uri("avares://ServerDesk.App/")) { Source = new Uri("avares://Iciclecreek.Avalonia.Terminal/Themes/Generic.axaml") });
    }
    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection(); services.AddSingleton<AppPaths>(); services.AddSingleton<ProfileRepository>(); services.AddSingleton<ICredentialStore,CredentialStore>(); services.AddSingleton<SshConnectionFactory>(); services.AddSingleton<IRdpClient,ExternalRdpClient>(); services.AddSingleton<MainViewModel>(); services.AddSingleton<MainWindow>();
        var provider = services.BuildServiceProvider();
        try
        {
            var language=Task.Run(async()=> { var repository=provider.GetRequiredService<ProfileRepository>(); await repository.InitializeAsync(); if(await repository.ApplyPendingFactorySettingsResetAsync())Log.Information("Factory settings reset applied atomically"); return (await repository.PreferencesAsync()).Language; }).GetAwaiter().GetResult();
            L.SetLanguage(language);
        }
        catch(Exception ex) { ApplicationDiagnostics.Report("Startup.LoadPreferences",ex); L.SetLanguage("pl"); }
        Log.Information("Application started in {Language}; version={Version}; OS={OS}; runtime={Runtime}",L.Language,
            typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            System.Runtime.InteropServices.RuntimeInformation.OSDescription,System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow=provider.GetRequiredService<MainWindow>();
            desktop.Exit+=(_,_)=>
            {
                try { provider.Dispose(); }
                catch(Exception error) { ApplicationDiagnostics.Report("Application.DisposeServices",error); }
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
