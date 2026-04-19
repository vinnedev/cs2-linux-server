using CounterStrikeSharp.API.Modules.Utils;

namespace RetakesPlugin.Modules;

public class Translator
{
    private readonly Dictionary<string, string> _translations;

    public Translator(Dictionary<string, string> translations)
    {
        _translations = translations;
    }

    public string this[string key] => Translate(key);
    public string this[string key, params object[] args] => Translate(key, args);

    private const string CenterModifier = "center.";
    private const string HtmlModifier = "html.";

    private string Translate(string key, params object[] args)
    {
        var isCenter = key.StartsWith(CenterModifier);
        var isHtml = key.StartsWith(HtmlModifier);

        if (isCenter)
            key = key[CenterModifier.Length..];
        else if (isHtml)
            key = key[HtmlModifier.Length..];

        if (!_translations.TryGetValue(key, out var value))
            return key;

        var translation = string.Format(value, args);

        return translation
            .Replace("[GREEN]", isCenter ? "" : isHtml ? "<font color='green'>" : ChatColors.Green.ToString())
            .Replace("[RED]", isCenter ? "" : isHtml ? "<font color='red'>" : ChatColors.Red.ToString())
            .Replace("[LIGHT_RED]", isCenter ? "" : isHtml ? "<font color='#FF6666'>" : ChatColors.LightRed.ToString())
            .Replace("[YELLOW]", isCenter ? "" : isHtml ? "<font color='yellow'>" : ChatColors.Yellow.ToString())
            .Replace("[BLUE]", isCenter ? "" : isHtml ? "<font color='blue'>" : ChatColors.Blue.ToString())
            .Replace("[PURPLE]", isCenter ? "" : isHtml ? "<font color='purple'>" : ChatColors.Purple.ToString())
            .Replace("[WHITE]", isCenter ? "" : isHtml ? "<font color='white'>" : ChatColors.White.ToString())
            .Replace("[NORMAL]", isCenter ? "" : isHtml ? "<font color='white'>" : ChatColors.White.ToString());
    }
}