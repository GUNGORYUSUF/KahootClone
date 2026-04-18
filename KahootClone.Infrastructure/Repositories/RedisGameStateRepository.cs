using System.Text.Json;
using KahootClone.Application.Interfaces;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace KahootClone.Infrastructure.Repositories;

// AŞAMA 6: RAM yerine Redis (Dağıtık Önbellek) kullanan durum yönetimi sınıfı.
public class RedisGameStateRepository : IGameStateRepository
{
    private readonly IDatabase _db;
    
    // Not: Eşzamanlılık kilitleri (lock) C# doğası gereği senkron çalışır. 
    // Tam dağıtık kilit (RedLock) asenkron mimari gerektirdiği için kilitleri şimdilik yerel (In-Memory) tutuyoruz.
    private static readonly ConcurrentDictionary<string, object> _localLocks = new();

    public RedisGameStateRepository(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public object GetQuizLock(string pin) => _localLocks.GetOrAdd(pin, _ => new object());
    public void RemoveQuizLock(string pin) => _localLocks.TryRemove(pin, out _);

    public bool TryAcquireTickLock(string pin)
    {
        // SETNX: Anahtar (Key) Redis'te yoksa oluşturur ve true döner. 800ms sonra otomatik silinir.
        return _db.StringSet($"tick_lock:{pin}", "locked", TimeSpan.FromMilliseconds(800), When.NotExists);
    }

    public GameStateTracker? GetGameState(string pin)
    {
        var data = _db.StringGet($"gameState:{pin}");
        return data.HasValue ? JsonSerializer.Deserialize<GameStateTracker>(data.ToString()!) : null;
    }

    public void SetGameState(string pin, GameStateTracker state)
    {
        _db.StringSet($"gameState:{pin}", JsonSerializer.Serialize(state));
        // Arka plan servisinin döngüyü işletebilmesi için aktif PIN'i listeye ekle
        _db.SetAdd("activeGames", pin);
    }

    public void RemoveGameState(string pin)
    {
        _db.KeyDelete($"gameState:{pin}");
        _db.SetRemove("activeGames", pin);
    }

    public IEnumerable<KeyValuePair<string, GameStateTracker>> GetAllActiveGames()
    {
        var activePins = _db.SetMembers("activeGames");
        var list = new List<KeyValuePair<string, GameStateTracker>>();
        foreach (var pinVal in activePins)
        {
            string pin = pinVal.ToString();
            var state = GetGameState(pin);
            if (state != null) list.Add(new KeyValuePair<string, GameStateTracker>(pin, state));
        }
        return list;
    }

    public void AddConnection(string connectionId, string pin, string nickname)
    {
        var data = JsonSerializer.Serialize(new { Pin = pin, Nickname = nickname });
        _db.StringSet($"connection:{connectionId}", data);
    }

    public (string Pin, string Nickname)? GetConnection(string connectionId)
    {
        var data = _db.StringGet($"connection:{connectionId}");
        if (!data.HasValue) return null;
        using var doc = JsonDocument.Parse(data.ToString());
        return (doc.RootElement.GetProperty("Pin").GetString()!, doc.RootElement.GetProperty("Nickname").GetString()!);
    }

    public void RemoveConnection(string connectionId) => _db.KeyDelete($"connection:{connectionId}");
}