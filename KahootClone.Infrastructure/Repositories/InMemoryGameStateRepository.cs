using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using KahootClone.Application.Interfaces;

namespace KahootClone.Infrastructure.Repositories;

// AŞAMA 4: Geçici olarak In-Memory (RAM) durum yönetimini sağlayan sınıf.
// İleride bu sınıfın yerine 'RedisGameStateRepository' yazılarak sistem tek satırla ölçeklendirilebilir.
public class InMemoryGameStateRepository : IGameStateRepository
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _quizLocks = new();
    private readonly ConcurrentDictionary<string, GameStateTracker> _activeGames = new();
    private readonly ConcurrentDictionary<string, (string Pin, string Nickname)> _connectionMap = new();

    public async Task<IAsyncDisposable> AcquireQuizLockAsync(string pin)
    {
        var semaphore = _quizLocks.GetOrAdd(pin, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        return new AsyncDisposableLock(semaphore);
    }
    public void RemoveQuizLock(string pin) => _quizLocks.TryRemove(pin, out _);
    public Task<bool> TryAcquireTickLockAsync(string pin) => Task.FromResult(true); // Tek sunuculu sistemde her zaman true döner

    public GameStateTracker? GetGameState(string pin)
    {
        _activeGames.TryGetValue(pin, out var state);
        return state;
    }

    public void SetGameState(string pin, GameStateTracker state) => _activeGames[pin] = state;
    
    public void RemoveGameState(string pin) => _activeGames.TryRemove(pin, out _);

    public IEnumerable<KeyValuePair<string, GameStateTracker>> GetAllActiveGames() => _activeGames.ToArray();

    public void AddConnection(string connectionId, string pin, string nickname) => _connectionMap[connectionId] = (pin, nickname);

    public (string Pin, string Nickname)? GetConnection(string connectionId)
    {
        if (_connectionMap.TryGetValue(connectionId, out var info)) return info;
        return null;
    }

    public void RemoveConnection(string connectionId) => _connectionMap.TryRemove(connectionId, out _);

    // In-Memory kilit işlemlerini "await using" bloğuna uyumlu hale getiren yardımcı sınıf
    private class AsyncDisposableLock : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        public AsyncDisposableLock(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public ValueTask DisposeAsync()
        {
            _semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}