namespace KahootClone.Application.Interfaces;

// AŞAMA 4: State nesnesi dışarı çıkarıldı ve public yapıldı.
public record GameStateTracker
{
    public GamePhase Phase { get; init; }
    public int CurrentQuestionIndex { get; init; }
    public int TimeRemaining { get; init; }
    public bool AllAnswered { get; init; }
}

public interface IGameStateRepository
{
    Task<IAsyncDisposable> AcquireQuizLockAsync(string pin);
    void RemoveQuizLock(string pin);
    Task<bool> TryAcquireTickLockAsync(string pin);

    GameStateTracker? GetGameState(string pin);
    void SetGameState(string pin, GameStateTracker state);
    void RemoveGameState(string pin);
    IEnumerable<KeyValuePair<string, GameStateTracker>> GetAllActiveGames();

    void AddConnection(string connectionId, string pin, string nickname);
    (string Pin, string Nickname)? GetConnection(string connectionId);
    void RemoveConnection(string connectionId);
}