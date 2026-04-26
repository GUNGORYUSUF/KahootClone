using System.Threading.Tasks;

namespace KahootClone.Api.Hubs;

// İstemcilerin (Tarayıcıların) dinlediği tüm olayları tip güvenli hale getiren arayüz
public interface IGameClient
{
    Task Error(string message);
    Task RestoreGameState(object state);
    Task SessionTokenReceived(string token);
    Task PlayerJoined(string nickname);
    Task PlayerLeft(string nickname);
    Task GetReady();
    Task ReceiveQuestion(object question);
    Task GameEnded(object leaderboard);
    Task UpdateLeaderboard(object leaderboard);
    Task LobbyReset();
    Task RedirectToNewGame(object payload);
    Task AnswerResult(object result);
    Task UpdateAnswerCount(object payload);
    Task WaitPhase(object payload);
    Task TimeUpdate(int timeLeft);
    Task WaitTimeUpdate(int waitTime);
}