using System.Collections.Concurrent;
using System.Threading;
using KahootClone.Application.Interfaces;

namespace KahootClone.Infrastructure.Repositories;

// AŞAMA 4: Geçici olarak In-Memory (RAM) durum yönetimini sağlayan sınıf.
// İleride bu sınıfın yerine 'RedisGameStateRepository' yazılarak sistem tek satırla ölçeklendirilebilir.
public class InMemoryGameStateRepository : IGameStateRepository
{
    private readonly ConcurrentDictionary<string, object> _quizLocks = new();
    private readonly ConcurrentDictionary<string, GameStateTracker> _activeGames = new();
    private readonly ConcurrentDictionary<string, (string Pin, string Nickname)> _connectionMap = new();

    public IDisposable AcquireQuizLock(string pin)
    {
        var lockObj = _quizLocks.GetOrAdd(pin, _ => new object());
        Monitor.Enter(lockObj);
        return new DisposableLock(lockObj);
    }
    public void RemoveQuizLock(string pin) => _quizLocks.TryRemove(pin, out _);
    public bool TryAcquireTickLock(string pin) => true; // Tek sunuculu sistemde her zaman true döner

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

    // In-Memory kilit işlemlerini "using" bloğuna uyumlu hale getiren yardımcı sınıf
    private class DisposableLock : IDisposable
    {
        private readonly object _lockObj;
        public DisposableLock(object lockObj)
        {
            _lockObj = lockObj;
        }

        public void Dispose()
        {
            Monitor.Exit(_lockObj);
        }
    }
}