namespace KahootClone.Application.Constants;

public static class SignalREvents
{
    public const string Error = "Error";
    public const string RestoreGameState = "RestoreGameState";
    public const string SessionTokenReceived = "SessionTokenReceived";
    public const string PlayerJoined = "PlayerJoined";
    public const string PlayerLeft = "PlayerLeft";
    public const string Kicked = "Kicked";
    public const string GetReady = "GetReady";
    public const string ReceiveQuestion = "ReceiveQuestion";
    public const string GameEnded = "GameEnded";
    public const string UpdateLeaderboard = "UpdateLeaderboard";
    public const string LobbyReset = "LobbyReset";
    public const string RedirectToNewGame = "RedirectToNewGame";
    public const string AnswerResult = "AnswerResult";
    public const string UpdateAnswerCount = "UpdateAnswerCount";
    public const string WaitPhase = "WaitPhase";
    public const string TimeUpdate = "TimeUpdate";
    public const string WaitTimeUpdate = "WaitTimeUpdate";
}