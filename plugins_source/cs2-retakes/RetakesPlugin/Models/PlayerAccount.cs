using MongoDB.Bson.Serialization.Attributes;

namespace RetakesPlugin.Models;

public class PlayerAccount
{
    [BsonId]
    public ulong SteamId { get; set; }

    public string Name { get; set; } = null!;

    public bool Online { get; set; }

    public string? LastIp { get; set; }

    public int TotalKills { get; set; }

    public int TotalDeaths { get; set; }

    public int TotalHeadshots { get; set; }

    public int MVPs { get; set; }

    public int BombsPlanted { get; set; }

    public int BombsDefused { get; set; }

    public int RoundsPlayed { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    public string? Country { get; set; }

    public string? Rank { get; set; }

    public string? FavoriteWeapon { get; set; }

    public string? Team { get; set; }

    public VipInfo? Vip { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class VipInfo
{
    public bool IsActive { get; set; }

    public int Tier { get; set; }

    public DateTime? Expiration { get; set; }
}
