using System.Globalization;
using System.Text.Json;

namespace ServerDesk.Core;

// Only explicitly marked application messages use this catalog. User data and
// terminal contents never pass through a whole-window text replacement.
public static class AppText
{
    private static readonly IReadOnlyDictionary<string,string> English=ReadCatalog("en");
    public static string Language { get; private set; }="pl";
    public static IReadOnlyDictionary<string,string> EnglishMessages=>English;
    public static bool IsSupported(string? language)=>language is "pl" or "en";
    public static void SetLanguage(string language)
    {
        if(!IsSupported(language))throw new ArgumentException("Unsupported application language.",nameof(language));
        Language=language;
        var culture=CultureInfo.GetCultureInfo(language=="en"?"en-US":"pl-PL");
        CultureInfo.CurrentUICulture=culture; CultureInfo.DefaultThreadCurrentUICulture=culture;
    }
    public static string T(string message)=>T(message,Language);
    // A pending language change must explain itself in the destination language
    // without changing the language or cultures of the running application.
    public static string T(string message,string language)
    {
        if(!IsSupported(language))throw new ArgumentException("Unsupported application language.",nameof(language));
        return language=="en" && English.TryGetValue(message,out var translated)?translated:message;
    }
    public static string F(FormattableString message)=>string.Format(CultureInfo.CurrentCulture,T(message.Format),message.GetArguments());
    public static string F(FormattableString message,string language)=>string.Format(CultureInfo.CurrentCulture,T(message.Format,language),message.GetArguments());
    private static IReadOnlyDictionary<string,string> ReadCatalog(string language)
    {
        using var stream=typeof(AppText).Assembly.GetManifestResourceStream("ServerDesk.Core.Localization."+language+".json")??throw new InvalidDataException("Missing language catalog.");
        return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)??throw new InvalidDataException("Invalid language catalog.");
    }
}
