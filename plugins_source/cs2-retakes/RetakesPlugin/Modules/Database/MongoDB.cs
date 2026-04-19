using MongoDB.Driver;
using MongoDB.Bson;
using RetakesPlugin.Modules.Entities;
using System;

namespace RetakesPlugin.Modules.Database;

public class MongoDB
{
    private readonly IMongoCollection<Player> _players;

    public MongoDB()
    {
        const string connectionString = "mongodb+srv://admin:lQuKfwXHd6Hbl88r@nexus.6njykdt.mongodb.net/?retryWrites=true&w=majority&appName=nexus";

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine("[MongoDB] FATAL: MONGODB_URI not set.");
            throw new Exception("MONGODB_URI not set");
        }

        try
        {
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.ServerApi = new ServerApi(ServerApiVersion.V1);

            var client = new MongoClient(settings);

            // Ping test
            var pingCommand = new BsonDocumentCommand<BsonDocument>(new BsonDocument("ping", 1));
            var result = client.GetDatabase("admin").RunCommand(pingCommand);

            if (result != null)
            {
                Console.WriteLine("[MongoDB] Pinged MongoDB successfully.");
            }
            

            var database = client.GetDatabase("cs2");
            _players = database.GetCollection<Player>("players");

            Console.WriteLine("[MongoDB] Connected and collection loaded.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[MongoDB] Connection failed: " + ex.Message);
            throw;
        }
    }

    public void RegisterPlayer(ulong steamId, string name, string? lastIp)
    {
        Console.WriteLine($"[MongoDB] Registering player: {name} ({steamId})");

        var exists = _players.Find(p => p.SteamId == steamId).Any();
        if (exists)
        {
            Console.WriteLine("[MongoDB] Player already exists.");
            return;
        }

        var now = DateTime.UtcNow;

        var player = new Player
        {
            SteamId = steamId,
            Name = name,
            LastIp = lastIp,
            Online = true,
            CreatedAt = now,
            UpdatedAt = now,
            Vip = new VipInfo
            {
                IsActive = false,
                Tier = 0,
                Expiration = null
            },
            TotalKills = 0,
            TotalDeaths = 0,
            TotalHeadshots = 0,
            MVPs = 0,
            BombsPlanted = 0,
            BombsDefused = 0,
            RoundsPlayed = 0,
            Wins = 0,
            Losses = 0
        };

        _players.InsertOne(player);
        Console.WriteLine("[MongoDB] Player registered.");
    }

    public void SetPlayerOnlineStatus(ulong steamId, bool online, string? lastIp)
    {
        Console.WriteLine($"[MongoDB] Updating online status: {steamId} = {online}");

        var filter = Builders<Player>.Filter.Eq(p => p.SteamId, steamId);
        var update = Builders<Player>.Update
            .Set(p => p.Online, online)
            .Set(p => p.UpdatedAt, DateTime.UtcNow)
            .Set(p => p.LastIp, lastIp);

        _players.UpdateOne(filter, update);
        Console.WriteLine("[MongoDB] Player status updated.");
    }
    
    public Player? GetPlayerBySteamId(ulong steamId)
    {
        try
        {
            var player = _players.Find(p => p.SteamId == steamId).FirstOrDefault();
            Console.WriteLine(player != null
                ? $"[MongoDB] Player found: {player.Name} ({steamId})"
                : $"[MongoDB] Player not found: {steamId}");

            return player;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MongoDB] Error fetching player {steamId}: {ex.Message}");
            return null;
        }
    }

    public void UpdateVipStatus(ulong steamId, bool isActive, int tier, DateTime? expiration)
    {
        Console.WriteLine($"[MongoDB] Updating VIP status for {steamId}");

        var filter = Builders<Player>.Filter.Eq(p => p.SteamId, steamId);
        var update = Builders<Player>.Update
            .Set(p => p.Vip!.IsActive, isActive)
            .Set(p => p.Vip!.Tier, tier)
            .Set(p => p.Vip!.Expiration, expiration)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        _players.UpdateOne(filter, update);
        Console.WriteLine("[MongoDB] VIP status updated.");
    }

    
}
