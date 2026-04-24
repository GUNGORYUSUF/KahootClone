using System;
using System.Threading;
using System.Text.Json;
using KahootClone.Application.Interfaces;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace KahootClone.Infrastructure.Repositories;

// YENİ: Redis üzerinden Redlock (Dağıtık Kilit) mekanizması
public class RedisDistributedLock : IAsyncDisposable
{
    private readonly IDatabase _db;
    private readonly string _key;
    private readonly string _token;

    private RedisDistributedLock(IDatabase db, string key, string token)
    {
        _db = db;
        _key = key;
        _token = token;
    }

    public static async Task<IAsyncDisposable> AcquireAsync(IDatabase db, string pin)
    {
        var key = $"quiz_lock:{pin}";
        var token = Guid.NewGuid().ToString(); // Kilidi kimin aldığını belirtir
        var timeout = TimeSpan.FromSeconds(10); 
        var start = DateTime.UtcNow;

        // Kilidi alana kadar tekrar dene (Spinning)
        while (DateTime.UtcNow - start < timeout)
        {
            if (await db.LockTakeAsync(key, token, TimeSpan.FromSeconds(5)))
            {
                return new RedisDistributedLock(db, key, token); // Başarılı
            }
            await Task.Delay(50); // Sistemi dondurmayan (non-blocking) bekleme
        }
        throw new TimeoutException($"PIN {pin} için dağıtık kilit alınamadı.");
    }

    public async ValueTask DisposeAsync()
    {
        await _db.LockReleaseAsync(_key, _token);
    }
}

// AŞAMA 6: RAM yerine Redis (Dağıtık Önbellek) kullanan durum yönetimi sınıfı.
public class RedisGameStateRepository : IGameStateRepository
{
    private readonly IDatabase _db;
    
    public RedisGameStateRepository(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    // In-memory objesi yerine, Async Disposable Redis kilit nesnesini döndürüyoruz.
    public Task<IAsyncDisposable> AcquireQuizLockAsync(string pin) => RedisDistributedLock.AcquireAsync(_db, pin);
    
    public void RemoveQuizLock(string pin) 
    {
        // Dispose anında LockRelease yapıldığı için özel bir temizliğe gerek kalmadı.
    }

    public Task<bool> TryAcquireTickLockAsync(string pin)
    {
        // SETNX: Anahtar (Key) Redis'te yoksa oluşturur ve true döner. 800ms sonra otomatik silinir.
        return _db.StringSetAsync($"tick_lock:{pin}", "locked", TimeSpan.FromMilliseconds(800), When.NotExists);
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