using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using ServerDesk.Core;

namespace ServerDesk.App;

internal static partial class Dialogs
{
    internal static async Task<CommandDefinition?> EditCommand(Window owner,CommandDefinition source,IEnumerable<string> categories,ServerProfile? host)
    {
        TextBox name=new TextBox { Text=source.Title,MaxLength=120 },summary=new TextBox { Text=source.Description,MaxLength=400 },explanation=new TextBox { Text=source.Detail,AcceptsReturn=true,MinHeight=65,MaxLength=6000,TextWrapping=Avalonia.Media.TextWrapping.Wrap };
        var template=new TextBox { Name="CommandEditorTemplate",Text=source.Template,AcceptsReturn=true,MinHeight=110,MaxLength=16000,TextWrapping=Avalonia.Media.TextWrapping.Wrap };
        TextBox category=new TextBox { Text=source.Category,MaxLength=160 },sub=new TextBox { Text=source.Subcategory,MaxLength=160 },tags=new TextBox { Text=string.Join(", ",source.Tags),MaxLength=2000 };
        var categoryChoice=new ComboBox { ItemsSource=categories.Distinct().Order().ToArray(),PlaceholderText=L.T("Istniejące kategorie"),HorizontalAlignment=HorizontalAlignment.Stretch }; categoryChoice.SelectionChanged+=(_,_)=> { if(categoryChoice.SelectedItem is string value)category.Text=value; };
        ComboBox platform=new ComboBox { ItemsSource=Enum.GetValues<CommandPlatform>(),SelectedItem=source.Platform,HorizontalAlignment=HorizontalAlignment.Stretch },shell=new ComboBox { ItemsSource=Enum.GetValues<CommandShell>(),SelectedItem=source.Shell,HorizontalAlignment=HorizontalAlignment.Stretch },risk=new ComboBox { ItemsSource=Enum.GetValues<CommandRisk>(),SelectedItem=source.Risk,HorizontalAlignment=HorizontalAlignment.Stretch };
        TextBox tools=new TextBox { Text=string.Join(", ",source.Tools) },family=new TextBox { Text=source.Family },distros=new TextBox { Text=string.Join(", ",source.Distributions) },min=new TextBox { Text=source.MinVersion },max=new TextBox { Text=source.MaxVersion },privileges=new TextBox { Text=source.Privileges };
        var scope=new ComboBox { ItemsSource=new[]{L.T("Globalna"),L.T("Konkretny host"),L.T("Grupa hostów / folder")},SelectedIndex=source.HostId!=null?1:source.HostFolder.Length>0?2:0,HorizontalAlignment=HorizontalAlignment.Stretch };
        var folder=new TextBox { Text=source.HostFolder.Length>0?source.HostFolder:host?.Folder??"",MaxLength=240 };
        var examples=new TextBox { Text=string.Join("\n",source.Examples),AcceptsReturn=true,MinHeight=70,MaxLength=12000 };
        var content=new StackPanel { Spacing=10 };
        void Field(string label,Control control) { content.Children.Add(DesignSystem.Label(L.T(label),true,12)); content.Children.Add(control); }
        Field("Nazwa",name); Field("Krótki opis",summary); Field("Szablon komendy",template);
        content.Children.Add(Text(L.T("Parametry zapisuj jako {{key}}, jako osobny argument bez cudzysłowów. Sekrety podawaj wyłącznie przy użyciu; nie zapisuj ich w szablonie."),12));
        Field("Kategoria / folder",category); content.Children.Add(categoryChoice); Field("Podkategoria",sub); Field("Tagi (przecinki)",tags);
        var settings=new Grid { ColumnDefinitions=new("*,*"),ColumnSpacing=12,RowDefinitions=new("Auto,Auto,Auto,Auto,Auto,Auto"),RowSpacing=5 };
        void Cell(string label,Control c,int row,int col) { var stack=new StackPanel { Spacing=5,Children={DesignSystem.Label(L.T(label),true,12),c} }; Grid.SetRow(stack,row); Grid.SetColumn(stack,col); settings.Children.Add(stack); }
        Cell("System",platform,0,0); Cell("Powłoka",shell,0,1); Cell("Poziom ryzyka",risk,1,0); Cell("Uprawnienia",privileges,1,1); Cell("Zakres komendy",scope,2,0); Cell("Folder hostów",folder,2,1);
        content.Children.Add(settings);
        var parameterRows=new StackPanel { Spacing=12 }; var parameters=new List<CommandParameterEditor>();
        void AddParameter(CommandParameter p)
        {
            if(parameters.Count>=24)return;
            var editor=new CommandParameterEditor(p); parameters.Add(editor); parameterRows.Children.Add(editor.Root);
            editor.Remove.Click+=(_,_)=> { parameters.Remove(editor); parameterRows.Children.Remove(editor.Root); };
        }
        foreach(var p in source.Parameters)AddParameter(p);
        var paramSection=new StackPanel { Spacing=8,Children={Text(L.T("Wartości Code są surowym kodem. Każde użycie wymaga zatwierdzenia; stosuj je wyłącznie, gdy zwykły argument nie wystarcza."),12),parameterRows,Button("Dodaj parametr",()=>AddParameter(new() { Key="parameter"+(parameters.Count+1),Label="Parameter" }))} };
        content.Children.Add(new Expander { Header=L.T("Parametry dynamiczne"),Content=paramSection,IsExpanded=source.Parameters.Length>0,HorizontalAlignment=HorizontalAlignment.Stretch });
        var advanced=new StackPanel { Spacing=6 };
        foreach(var (label,control) in new (string,Control)[]{("Szczegółowe wyjaśnienie",explanation),("Wymagane narzędzia (przecinki)",tools),("Rodzina dystrybucji",family),("Dystrybucje (przecinki)",distros),("Wersja od",min),("Wersja do",max),("Przykłady (po jednym wierszu)",examples)}) { advanced.Children.Add(DesignSystem.Label(L.T(label),true,12)); advanced.Children.Add(control); }
        content.Children.Add(new Expander { Header=L.T("Zgodność i dodatkowe metadane"),Content=advanced,HorizontalAlignment=HorizontalAlignment.Stretch });
        var error=Text("",12); DesignSystem.Resource(error,TextBlock.ForegroundProperty,"Danger");
        var footer=new StackPanel { Spacing=5,Children={error} }; var actions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=8 }; footer.Children.Add(actions);
        var grid=new Grid { RowDefinitions=new("*,Auto"),RowSpacing=10 }; grid.Children.Add(new ScrollViewer { Content=content,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled }); Grid.SetRow(footer,1); grid.Children.Add(footer);
        var dialog=Window(L.T("Własna komenda"),grid); dialog.Width=740; dialog.Height=720; dialog.SizeToContent=SizeToContent.Manual;
        actions.Children.Add(Button("Anuluj",()=>dialog.Close()));
        actions.Children.Add(Button("Zapisz",()=>
        {
            try
            {
                string[] Split(string? value)=>(value??"").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                var edited=source with
                {
                    Name=name.Text??"",NameEn="",Summary=summary.Text??"",SummaryEn="",Explanation=explanation.Text??"",ExplanationEn="",Template=template.Text??"",
                    Category=category.Text??"",Subcategory=sub.Text??"",Tags=Split(tags.Text),Platform=(CommandPlatform)platform.SelectedItem!,Shell=(CommandShell)shell.SelectedItem!,Risk=(CommandRisk)risk.SelectedItem!,
                    Tools=Split(tools.Text),Family=family.Text??"",Distributions=Split(distros.Text),MinVersion=min.Text??"",MaxVersion=max.Text??"",Privileges=privileges.Text??"user",Parameters=parameters.Select(p=>p.Read()).ToArray(),
                    Examples=(examples.Text??"").Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries),HostId=scope.SelectedIndex==1?host?.Id??source.HostId:null,HostFolder=scope.SelectedIndex==2?folder.Text??"":""
                };
                if(scope.SelectedIndex==1&&edited.HostId==null)throw new ArgumentException(L.T("Wybierz aktywny host przed przypisaniem komendy."));
                if(scope.SelectedIndex==2&&string.IsNullOrWhiteSpace(edited.HostFolder))throw new ArgumentException(L.T("Podaj folder grupy hostów."));
                CommandValidation.Validate(edited,true); dialog.Close(edited);
            }
            catch(ArgumentException ex) { error.Text=ex.Message; }
        }));
        return await dialog.ShowDialog<CommandDefinition?>(owner);
    }
    private sealed class CommandParameterEditor
    {
        internal Border Root { get; }
        internal Button Remove { get; }
        private readonly TextBox key,label,defaultValue,choices;
        private readonly ComboBox kind;
        private readonly CheckBox required,secret;
        internal CommandParameterEditor(CommandParameter p)
        {
            key=new();
            key.Text=p.Key;
            key.MaxLength=40;
            key.PlaceholderText="key";
            label=new() { Text=p.Label,PlaceholderText=L.T("Etykieta"),MaxLength=100 };
            kind=new() { ItemsSource=Enum.GetValues<CommandParameterKind>(),SelectedItem=p.Kind,HorizontalAlignment=HorizontalAlignment.Stretch };
            defaultValue=new() { Text=p.Default,MaxLength=2000,PlaceholderText=L.T("Wartość domyślna") }; choices=new() { Text=string.Join(" | ",p.Choices),PlaceholderText=L.T("Opcje oddzielone |"),MaxLength=8000 };
            required=new() { Content=L.T("Wymagany"),IsChecked=p.Required }; secret=new() { Content=L.T("Sekret (bez zapisu wartości)"),IsChecked=p.Secret }; defaultValue.IsEnabled=!p.Secret;
            secret.IsCheckedChanged+=(_,_)=> { defaultValue.IsEnabled=secret.IsChecked!=true; if(secret.IsChecked==true)defaultValue.Text=""; };
            Remove=Button("Usuń parametr",()=> { });
            var grid=new Grid { ColumnDefinitions=new("*,*"),RowDefinitions=new("Auto,Auto,Auto,Auto"),ColumnSpacing=8,RowSpacing=6 };
            grid.Children.Add(key); Grid.SetColumn(label,1); grid.Children.Add(label); Grid.SetRow(kind,1); grid.Children.Add(kind); Grid.SetRow(defaultValue,1); Grid.SetColumn(defaultValue,1); grid.Children.Add(defaultValue);
            Grid.SetRow(choices,2); Grid.SetColumnSpan(choices,2); grid.Children.Add(choices);
            var row=new WrapPanel { Orientation=Orientation.Horizontal,Children={required,secret,Remove} }; foreach(var child in row.Children)child.Margin=new Thickness(0,0,8,0); Grid.SetRow(row,3); Grid.SetColumnSpan(row,2); grid.Children.Add(row);
            Root=new() { Child=grid,Padding=new Thickness(10),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6) }; DesignSystem.Resource(Root,Border.BorderBrushProperty,"BorderDefault");
        }
        internal CommandParameter Read()=>new() { Key=key.Text??"",Label=label.Text??"",Kind=(CommandParameterKind)kind.SelectedItem!,Default=secret.IsChecked==true?"":defaultValue.Text??"",Choices=(choices.Text??"").Split('|',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries),Required=required.IsChecked==true,Secret=secret.IsChecked==true };
    }
    internal static async Task<CommandUserLibrary?> EditCommandCategories(Window owner,CommandUserLibrary original)
    {
        var library=original; var list=new ListBox { MinHeight=150,MaxHeight=260 };
        void Refresh() { list.ItemsSource=library.Categories.Concat(library.Commands.Select(c=>c.Category)).Distinct().Order().ToArray(); }
        Refresh(); TextBox name=new TextBox { PlaceholderText=L.T("Ścieżka kategorii, np. Linux/Produkcja"),MaxLength=160 }; var error=Text("",12);
        var actions=new WrapPanel { Orientation=Orientation.Horizontal };
        actions.Children.Add(Button("Dodaj kategorię",()=> { try { var value=(name.Text??"").Trim(); CommandValidation.Category(value); library=library with { Categories=library.Categories.Append(value).Distinct().ToArray() }; Refresh(); } catch(ArgumentException ex) { error.Text=ex.Message; } }));
        actions.Children.Add(Button("Zmień nazwę / przenieś",()=>
        {
            try
            {
                if(list.SelectedItem is not string old)return; var next=(name.Text??"").Trim(); CommandValidation.Category(next);
                string Map(string value)=>value==old||value.StartsWith(old+"/",StringComparison.Ordinal)?next+value[old.Length..]:value;
                library=library with { Categories=library.Categories.Select(Map).Distinct().ToArray(),Commands=library.Commands.Select(c=>c with { Category=Map(c.Category) }).ToArray() }; CommandValidation.Validate(library); Refresh();
            }
            catch(ArgumentException ex) { error.Text=ex.Message; }
        }));
        actions.Children.Add(Button("Usuń pustą kategorię",()=> { if(list.SelectedItem is not string path)return; if(library.Commands.Any(c=>c.Category==path||c.Category.StartsWith(path+"/",StringComparison.Ordinal))) { error.Text=L.T("Najpierw przenieś komendy z tej kategorii i jej podkategorii."); return; } library=library with { Categories=library.Categories.Where(c=>c!=path&&!c.StartsWith(path+"/",StringComparison.Ordinal)).ToArray() }; Refresh(); }));
        var footer=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=8 }; var stack=new StackPanel { Spacing=10,Children={Text(L.T("Kategorie dotyczą własnych komend. Użyj / do tworzenia podkategorii.")),list,name,actions,error,footer} };
        var dialog=Window(L.T("Kategorie komend"),stack); dialog.Width=620;
        footer.Children.Add(Button("Anuluj",()=>dialog.Close())); footer.Children.Add(Button("Zapisz",()=>dialog.Close(library)));
        return await dialog.ShowDialog<CommandUserLibrary?>(owner);
    }
}
