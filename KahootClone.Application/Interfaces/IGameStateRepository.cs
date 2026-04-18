namespace KahootClone.Application.Interfaces;

// AŞAMA 4: State nesnesi dışarı çıkarıldı ve public yapıldı.
public class GameStateTracker
{
    public GamePhase Phase { get; set; }
    public int CurrentQuestionIndex { get; set; }
    public int TimeRemaining { get; set; }
    public bool AllAnswered { get; set; }
}

public interface IGameStateRepository
{
    object GetQuizLock(string pin);
    void RemoveQuizLock(string pin);

    GameStateTracker? GetGameState(string pin);
    void SetGameState(string pin, GameStateTracker state);
    void RemoveGameState(string pin);
    IEnumerable<KeyValuePair<string, GameStateTracker>> GetAllActiveGames();

    void AddConnection(string connectionId, string pin, string nickname);
    (string Pin, string Nickname)? GetConnection(string connectionId);
    void RemoveConnection(string connectionId);
}