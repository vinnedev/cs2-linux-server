using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace RetakesPlugin.Services;

public sealed class ChatMessageService
{
    private static readonly Regex ColorTokenRegex = new(@"\{(?<token>[a-z_]+)\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> ColorTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["default"] = ChatColors.White.ToString(),
        ["white"] = ChatColors.White.ToString(),
        ["green"] = ChatColors.Green.ToString(),
        ["red"] = ChatColors.Red.ToString(),
        ["yellow"] = ChatColors.Yellow.ToString(),
        ["blue"] = ChatColors.Blue.ToString(),
        ["purple"] = ChatColors.Purple.ToString(),
        ["orange"] = ChatColors.Orange.ToString(),
        ["grey"] = ChatColors.Grey.ToString(),
        ["gray"] = ChatColors.Grey.ToString(),
        ["lightred"] = ChatColors.LightRed.ToString(),
        ["light_red"] = ChatColors.LightRed.ToString(),
        ["lightblue"] = ChatColors.LightBlue.ToString(),
        ["light_blue"] = ChatColors.LightBlue.ToString(),
        ["lightpurple"] = ChatColors.LightPurple.ToString(),
        ["light_purple"] = ChatColors.LightPurple.ToString(),
        ["lightyellow"] = ChatColors.LightYellow.ToString(),
        ["light_yellow"] = ChatColors.LightYellow.ToString(),
        ["darkred"] = ChatColors.DarkRed.ToString(),
        ["dark_red"] = ChatColors.DarkRed.ToString(),
        ["darkblue"] = ChatColors.DarkBlue.ToString(),
        ["dark_blue"] = ChatColors.DarkBlue.ToString(),
        ["bluegrey"] = ChatColors.BlueGrey.ToString(),
        ["blue_grey"] = ChatColors.BlueGrey.ToString(),
        ["olive"] = ChatColors.Olive.ToString(),
        ["lime"] = ChatColors.Lime.ToString(),
        ["lightgreen"] = ChatColors.Lime.ToString(),
        ["light_green"] = ChatColors.Lime.ToString(),
        ["gold"] = ChatColors.Gold.ToString(),
        ["silver"] = ChatColors.Silver.ToString(),
        ["magenta"] = ChatColors.Magenta.ToString()
    };

    private readonly RetakesPlugin _plugin;

    public ChatMessageService(RetakesPlugin plugin)
    {
        _plugin = plugin;
    }

    public string Prefix => Format(_plugin.Localizer["retakes.prefix"]);

    public string Format(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        return ColorTokenRegex.Replace(message, match =>
        {
            var token = match.Groups["token"].Value;
            return ColorTokens.TryGetValue(token, out var color) ? color : match.Value;
        });
    }

    public void Send(CCSPlayerController player, string message)
    {
        if (player == null || !player.IsValid)
        {
            return;
        }

        player.PrintToChat(Format(message));
    }

    public void SendPrefixed(CCSPlayerController player, string message)
    {
        Send(player, $"{Prefix} {message}");
    }

    public void Broadcast(string message)
    {
        Server.PrintToChatAll(Format(message));
    }

    public void BroadcastPrefixed(string message)
    {
        Broadcast($"{Prefix} {message}");
    }
}
