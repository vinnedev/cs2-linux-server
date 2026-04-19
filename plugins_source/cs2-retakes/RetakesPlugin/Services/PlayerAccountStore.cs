using MongoDB.Bson;
using MongoDB.Driver;

using RetakesPlugin.Models;
using RetakesPlugin.Utils;

namespace RetakesPlugin.Services;

public class PlayerAccountStore
{
    private readonly IMongoCollection<PlayerAccount> _players;

    private PlayerAccountStore(IMongoCollection<PlayerAccount> players)
    {
        _players = players;
    }

    public static PlayerAccountStore? TryCreate()
    {
        var connectionString = Environment.GetEnvironmentVariable("MONGODB_URI");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Logger.LogInfo("MongoDB", "MONGODB_URI nao configurada; integracao de VIP via MongoDB desativada.");
            return null;
        }

        try
        {
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.ServerApi = new ServerApi(ServerApiVersion.V1);

            var client = new MongoClient(settings);
            var pingCommand = new BsonDocumentCommand<BsonDocument>(new BsonDocument("ping", 1));
            client.GetDatabase("admin").RunCommand(pingCommand);

            var database = client.GetDatabase("cs2");
            var players = database.GetCollection<PlayerAccount>("players");

            Logger.LogInfo("MongoDB", "Integracao MongoDB inicializada com sucesso.");
            return new PlayerAccountStore(players);
        }
        catch (Exception ex)
        {
            Logger.LogException("MongoDB", ex);
            return null;
        }
    }

    public PlayerAccount? GetPlayerBySteamId(ulong steamId)
    {
        try
        {
            return _players.Find(player => player.SteamId == steamId).FirstOrDefault();
        }
        catch (Exception ex)
        {
            Logger.LogException("MongoDB", ex);
            return null;
        }
    }

    public void RegisterPlayer(ulong steamId, string name, string? lastIp)
    {
        try
        {
            var existingPlayer = GetPlayerBySteamId(steamId);
            if (existingPlayer != null)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var player = new PlayerAccount
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
                    Tier = 0
                }
            };

            _players.InsertOne(player);
        }
        catch (Exception ex)
        {
            Logger.LogException("MongoDB", ex);
        }
    }

    public void SetPlayerOnlineStatus(ulong steamId, bool online, string? lastIp)
    {
        try
        {
            var filter = Builders<PlayerAccount>.Filter.Eq(player => player.SteamId, steamId);
            var update = Builders<PlayerAccount>.Update
                .Set(player => player.Online, online)
                .Set(player => player.LastIp, lastIp)
                .Set(player => player.UpdatedAt, DateTime.UtcNow);

            _players.UpdateOne(filter, update);
        }
        catch (Exception ex)
        {
            Logger.LogException("MongoDB", ex);
        }
    }
}
