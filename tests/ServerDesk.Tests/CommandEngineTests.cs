using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class CommandEngineTests
{
    internal static CommandDefinition Snippet(string template="echo {{text}}",CommandShell shell=CommandShell.Bash)=>new() { Id="user.example",Origin=CommandOrigin.User,Name="Example",Category="Linux/Work",Template=template,Shell=shell,Parameters=[new() { Key="text",Default="hello" }] };
    [Fact] public void Entire_offline_library_is_valid_versioned_bilingual_and_covers_all_categories()
    {
        var catalog=CommandCatalog.Default; Assert.Equal("1.0.0",catalog.Version); Assert.True(catalog.BuiltIns.Count>=160);
        Assert.Equal(10,catalog.BuiltIns.Select(c=>c.Category).Distinct().Count());
        foreach(var c in catalog.BuiltIns)
        {
            CommandValidation.Validate(c); Assert.NotEmpty(c.Operation); Assert.NotEmpty(c.NameEn); Assert.NotEmpty(c.ExplanationEn);
            var shell=c.Shell==CommandShell.Any?CommandShell.Bash:c.Shell;
            var rendered=CommandRenderer.Render(c,shell,new Dictionary<string,string>(),true); Assert.NotEmpty(rendered.Text); Assert.DoesNotContain("{{",rendered.Text);
        }
        Assert.True(catalog.BuiltIns.Count(c=>c.Platform==CommandPlatform.Windows)>=40);
        Assert.All(catalog.BuiltIns.SelectMany(c=>c.Tools).Distinct(),tool=>Assert.Contains(tool,CommandDetection.ToolNames));
        Assert.Contains(catalog.BuiltIns,c=>c.Risk==CommandRisk.Dangerous); Assert.Contains(catalog.BuiltIns,c=>c.Template.Contains('\n'));
    }
    [Theory]
    [InlineData("ubuntu","debian","apt")][InlineData("debian","debian","apt")]
    [InlineData("rhel","rhel","dnf")][InlineData("rocky","rhel","dnf")][InlineData("almalinux","rhel","dnf")]
    [InlineData("centos","rhel","dnf")][InlineData("fedora","rhel","dnf")][InlineData("arch","arch","pacman")][InlineData("alpine","alpine","apk")]
    public void Distro_inheritance_selects_one_package_variant(string id,string family,string tool)
    {
        var env=CommandEnvironment.Manual(id,shell:CommandShell.Bash) with { Tools=new Dictionary<string,ToolAvailability> { [tool]=ToolAvailability.Available } };
        Assert.Equal(family,env.Family); var all=CommandCatalog.Default.Find(env); var package=Assert.Single(all,m=>m.Command.Operation=="package.list"); Assert.Contains(tool,package.Command.Tools);
        Assert.Contains(all,m=>m.Command.Id=="linux.release"); Assert.Contains(all,m=>m.Command.Id=="general.identity");
        Assert.DoesNotContain(all,m=>m.Command.Platform==CommandPlatform.Windows);
    }
    [Fact] public void Tool_detection_beats_distro_assumptions_and_missing_is_not_unknown()
    {
        var env=CommandEnvironment.Manual("alpine",shell:CommandShell.Bash) with { Tools=new Dictionary<string,ToolAvailability> { ["systemctl"]=ToolAvailability.Available,["rc-service"]=ToolAvailability.NotDetected } };
        Assert.Equal("systemd.status",Assert.Single(CommandCatalog.Default.Find(env),m=>m.Command.Operation=="service.status").Command.Id);
        env=env with { Tools=new Dictionary<string,ToolAvailability> { ["systemctl"]=ToolAvailability.NotDetected,["rc-service"]=ToolAvailability.Available } };
        Assert.Equal("openrc.status",Assert.Single(CommandCatalog.Default.Find(env),m=>m.Command.Operation=="service.status").Command.Id);
        var docker=Assert.Single(CommandCatalog.Default.Find(env),m=>m.Command.Id=="devops.docker-ps"); Assert.Contains("docker",docker.UnknownTools); Assert.Empty(docker.MissingTools);
    }
    [Theory]
    [InlineData("otwarte porty","linux.net.listeners")][InlineData("open ports","linux.net.listeners")]
    [InlineData("sprawdź usługę","systemd.status")][InlineData("check service","systemd.status")]
    [InlineData("nasłuch","linux.net.listeners")][InlineData("systemctl stat","systemd.status")]
    [InlineData("serwis status","systemd.status")][InlineData("pamięć","linux.memory")]
    public void Search_uses_Polish_English_partial_syntax_and_tags(string query,string expected)
    { Assert.Contains(CommandCatalog.Default.Find(CommandEnvironment.Manual("ubuntu"),query:query),m=>m.Command.Id==expected); }
    [Fact] public void Windows_search_and_winget_mean_application_updates_not_Windows_Update()
    {
        var env=CommandEnvironment.Manual("windows11",shell:CommandShell.PowerShell);
        Assert.Contains(CommandCatalog.Default.Find(env,query:"otwarte porty"),m=>m.Command.Template.StartsWith("Get-NetTCPConnection -State Listen"));
        var winget=CommandCatalog.Default.BuiltIns.Single(c=>c.Id=="windows.ps.package-update-all"); Assert.Contains("not Windows Update",winget.ExplanationEn);
        Assert.DoesNotContain(CommandCatalog.Default.Find(env),m=>m.Command.Shell==CommandShell.Cmd);
    }
    [Fact] public void Unknown_linux_keeps_generic_layer_but_does_not_invent_a_package_manager()
    {
        var list=CommandCatalog.Default.Find(CommandEnvironment.Manual("linux")); Assert.Contains(list,m=>m.Command.Id=="linux.kernel"); Assert.DoesNotContain(list,m=>m.Command.Operation=="package.list");
    }
    [Fact] public void Cached_user_search_updates_after_edit_without_stale_ID_results()
    {
        var source=Snippet() with { Name="Before unique",Tags=["oldtag"] }; var env=CommandEnvironment.Manual("linux");
        Assert.Contains(CommandCatalog.Default.Find(env,[source],query:"oldtag"),m=>m.Command.Id==source.Id);
        var edited=source with { Name="After unique",Tags=["newtag"] };
        Assert.DoesNotContain(CommandCatalog.Default.Find(env,[edited],query:"oldtag"),m=>m.Command.Id==source.Id);
        Assert.Contains(CommandCatalog.Default.Find(env,[edited],query:"newtag"),m=>m.Command.Id==source.Id);
    }
    [Fact] public void Version_ranges_and_user_variants_do_not_override_or_duplicate_builtins()
    {
        var catalog=CommandCatalog.Default;
        Assert.DoesNotContain(catalog.Find(CommandEnvironment.Manual("ubuntu","22.04")),m=>m.Command.Id=="ubuntu.netplan.status");
        Assert.Contains(catalog.Find(CommandEnvironment.Manual("ubuntu","24.04")),m=>m.Command.Id=="ubuntu.netplan.status");
        var user=Snippet() with { Operation="linux.kernel" };
        var list=catalog.Find(CommandEnvironment.Manual("linux"),[user]); Assert.Contains(list,m=>m.Command.Id==user.Id); Assert.Contains(list,m=>m.Command.Id=="linux.kernel");
    }
    [Fact] public void Host_and_group_scope_use_ID_and_path_boundaries()
    {
        var id=Guid.NewGuid(); var c=Snippet() with { HostId=id,HostFolder="Prod/Linux" };
        Assert.True(CommandCatalog.Scope(c,id,"Prod/Linux/web")); Assert.False(CommandCatalog.Scope(c,Guid.NewGuid(),"Prod/Linux/web"));
        Assert.False(CommandCatalog.Scope(c,id,"Prod/Linux2")); Assert.False(CommandCatalog.Scope(c,null,""));
        var management=CommandCatalog.Default.Find(CommandEnvironment.Manual("windows11"),[c],includeIncompatible:true,allVariants:true);
        Assert.Contains(management,m=>m.Command.Id==c.Id&&!m.Compatible);
    }
    [Theory]
    [InlineData(CommandShell.Bash,"a'b;$(touch /tmp/never)","'a'\"'\"'b;$(touch /tmp/never)'")]
    [InlineData(CommandShell.Zsh,"a'b","'a'\"'\"'b'")]
    [InlineData(CommandShell.PowerShell,"a'b;$(Get-Process)","'a''b;$(Get-Process)'")]
    [InlineData(CommandShell.PowerShell,"a’b‘$(Get-Process)","'a’’b‘‘$(Get-Process)'")]
    [InlineData(CommandShell.Cmd,"C:\\Program Files\\Demo","\"C:\\Program Files\\Demo\"")]
    public void Shell_arguments_are_quoted_as_data(CommandShell shell,string value,string expected)=>Assert.Equal(expected,CommandRenderer.Quote(value,shell));
    [Fact] public async Task Native_shell_roundtrips_quotes_as_literal_data_without_running_substitutions()
    {
        var value="a’b‘;$(2+3) ' \" example";
        var windows=OperatingSystem.IsWindows();
        var script=windows?"[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false); $serverdeskQuoted="+CommandRenderer.Quote(value,CommandShell.PowerShell)+"; [Console]::Write($serverdeskQuoted)":"printf '%s' "+CommandRenderer.Quote(value,CommandShell.Bash);
        var start=new System.Diagnostics.ProcessStartInfo(windows?"powershell.exe":"/bin/bash") { RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true,StandardOutputEncoding=System.Text.Encoding.UTF8 };
        if(windows) { start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-EncodedCommand"); start.ArgumentList.Add(Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script))); }
        else { start.ArgumentList.Add("-c"); start.ArgumentList.Add(script); }
        using var process=System.Diagnostics.Process.Start(start)!; var output=process.StandardOutput.ReadToEndAsync(); var errors=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(timeout.Token); } catch(OperationCanceledException) { process.Kill(true); throw new TimeoutException(); }
        Assert.Equal(0,process.ExitCode); Assert.Equal("",await errors); Assert.Equal(value,await output);
    }
    [Theory]
    [InlineData("x&whoami")][InlineData("%PATH%")][InlineData("!PROMPT!")][InlineData("a\"b")][InlineData("a^b")][InlineData("a|b")][InlineData("a(b)")]
    [InlineData("C:\\Temp\\")]
    public void Ambiguous_CMD_arguments_are_refused(string value)=>Assert.Throws<ArgumentException>(()=>CommandRenderer.Quote(value,CommandShell.Cmd));
    [Theory]
    [InlineData(CommandParameterKind.Number,"1; id")][InlineData(CommandParameterKind.IpAddress,"localhost")][InlineData(CommandParameterKind.Hostname,"host;id")]
    [InlineData(CommandParameterKind.Text,"--help")][InlineData(CommandParameterKind.Text,"hello\rworld")][InlineData(CommandParameterKind.FilePath,"\u001b[200~")]
    [InlineData(CommandParameterKind.Text,"hello\u202eworld")][InlineData(CommandParameterKind.Text,"hello\u2028world")]
    public void Typed_parameters_reject_invalid_values_and_option_injection(CommandParameterKind kind,string value)=>Assert.Throws<ArgumentException>(()=>CommandRenderer.ValidateValue(new() { Kind=kind },value));
    [Fact] public void Parameters_support_defaults_optional_numbers_IP_paths_choices_and_secrets()
    {
        var c=Snippet(); Assert.Equal("echo 'hello'",CommandRenderer.Render(c,CommandShell.Bash,new Dictionary<string,string>()).Text);
        foreach(var p in new CommandParameter[]{new() { Kind=CommandParameterKind.Number },new() { Kind=CommandParameterKind.IpAddress },new() { Kind=CommandParameterKind.FilePath },new() { Kind=CommandParameterKind.Hostname },new() { Kind=CommandParameterKind.Choice,Choices=["one","two"] }})
            CommandRenderer.ValidateValue(p,p.Kind switch { CommandParameterKind.Number=>"-12.5",CommandParameterKind.IpAddress=>"::1",CommandParameterKind.Hostname=>"example.org",CommandParameterKind.Choice=>"one",_=>"/tmp/a b" });
        CommandRenderer.ValidateValue(new() { Required=false },"");
        c=c with { Parameters=[new() { Key="text",Secret=true }] };
        Assert.True(CommandRenderer.Render(c,CommandShell.PowerShell,new Dictionary<string,string> { ["text"]="private-value" }).ContainsSecret);
        Assert.Throws<ArgumentException>(()=>CommandValidation.Validate(c with { Parameters=[new() { Key="text",Secret=true,Default="no" }] },true));
    }
    [Theory]
    [InlineData("echo '{{text}}'")][InlineData("echo \" {{text}} \"")][InlineData("echo prefix{{text}}")][InlineData("echo {{other}}")]
    [InlineData("cat <<EOF\n{{text}}\nEOF")][InlineData("eval {{text}}")][InlineData("bash -c {{text}}")][InlineData("Invoke-Expression {{text}}")][InlineData("echo ‘ {{text}} ’")]
    [InlineData("bash --norc -ec {{text}}")][InlineData("powershell.exe -NoProfile -Command {{text}}")][InlineData("cmd.exe /d /s /c {{text}}")][InlineData("python3 -I -c {{text}}")]
    public void Ambiguous_template_context_is_rejected(string template)=>Assert.Throws<ArgumentException>(()=>CommandValidation.Validate(Snippet(template)));
    [Fact] public void Raw_code_requires_explicit_consent_and_is_not_silently_quoted()
    {
        var c=Snippet("echo {{text}}") with { Parameters=[new() { Key="text",Kind=CommandParameterKind.Code }] }; var values=new Dictionary<string,string> { ["text"]="$(date)" };
        Assert.Throws<ArgumentException>(()=>CommandRenderer.Render(c,CommandShell.Bash,values)); var rendered=CommandRenderer.Render(c,CommandShell.Bash,values,true); Assert.True(rendered.ContainsCode); Assert.Equal("echo $(date)",rendered.Text);
    }
    [Fact] public void Insert_never_sends_Enter_and_execute_requires_confirmations()
    {
        var session=Guid.NewGuid(); var connection=Guid.NewGuid(); var ticket=new CommandDispatchTicket(session,connection,"id"); var command=new RenderedCommand("echo hi",false,false,false);
        Assert.Equal("echo hi",CommandDispatch.Prepare(ticket,session,connection,command,false,CommandRisk.Dangerous,false,false));
        Assert.Throws<InvalidOperationException>(()=>CommandDispatch.Prepare(ticket,session,connection,command,true,CommandRisk.ReadOnly,false,false));
        Assert.Throws<InvalidOperationException>(()=>CommandDispatch.Prepare(ticket,session,connection,command,true,CommandRisk.Dangerous,true,false));
        Assert.Equal("echo hi\r",CommandDispatch.Prepare(ticket,session,connection,command,true,CommandRisk.Dangerous,true,true));
        Assert.Throws<InvalidOperationException>(()=>CommandDispatch.Prepare(ticket,Guid.NewGuid(),connection,command,false,CommandRisk.ReadOnly,false,false));
        Assert.Throws<InvalidOperationException>(()=>CommandDispatch.Prepare(ticket,session,Guid.NewGuid(),command,false,CommandRisk.ReadOnly,false,false));
        foreach(var text in new[]{"echo a\necho b","echo hi\r","\u001b[31m"})Assert.Throws<InvalidOperationException>(()=>CommandDispatch.Prepare(ticket,session,connection,new(text,false,false,text.Contains('\n')),false,CommandRisk.ReadOnly,false,false));
    }
    [Fact] public void Strict_import_rejects_unknown_settings_origin_bad_params_and_plaintext_credentials()
    {
        var good=new CommandUserLibrary { Commands=[Snippet()],Categories=["Linux/Work"] }; var json=JsonSerializer.Serialize(good,CommandValidation.Json); Assert.Single(CommandStore.Parse(json).Commands);
        Assert.Throws<InvalidDataException>(()=>CommandStore.Parse(json.TrimEnd()[..^1]+",\"startupCommand\":\"unsafe\"}"));
        Assert.Throws<InvalidDataException>(()=>CommandStore.Parse("{\"schemaVersion\":1,\"schemaVersion\":2}"));
        Assert.Throws<ArgumentException>(()=>CommandStore.Parse(JsonSerializer.Serialize(good with { SchemaVersion=2 },CommandValidation.Json)));
        Assert.Throws<ArgumentException>(()=>CommandValidation.Validate(Snippet("curl --header token=literal-secret") with { Parameters=[] },true));
        Assert.Throws<ArgumentException>(()=>CommandValidation.Validate(Snippet() with { Id="builtin.example" },true));
        var imported=CommandStore.Merge(good,good); Assert.Equal(2,imported.Commands.Length); Assert.Equal("user.example",imported.Commands[0].Id); Assert.NotEqual("user.example",imported.Commands[1].Id);
        var copy=CommandStore.Duplicate(good,CommandCatalog.Default.BuiltIns[0]); Assert.Equal(CommandOrigin.User,copy.Commands[1].Origin); Assert.Equal(2,copy.Commands.Length);
    }
}
