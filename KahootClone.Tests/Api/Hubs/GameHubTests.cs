using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;
using KahootClone.Api.Hubs;
using KahootClone.Application.Interfaces;
using KahootClone.Domain.Entities;
using System.Collections.Generic;

namespace KahootClone.Tests.Api.Hubs;

public class GameHubTests
{
    private readonly Mock<IQuizService> _mockQuizService;
    // GameHub : Hub<IGameClient> olduğu için tipli versiyon kullanılır
    private readonly Mock<IHubCallerClients<IGameClient>> _mockClients;
    private readonly Mock<IGameClient> _mockClientProxy;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly GameHub _gameHub;

    public GameHubTests()
    {
        _mockQuizService    = new Mock<IQuizService>();
        _mockClients        = new Mock<IHubCallerClients<IGameClient>>();
        _mockClientProxy    = new Mock<IGameClient>();
        _mockGroups         = new Mock<IGroupManager>();
        _mockContext        = new Mock<HubCallerContext>();

        // IGameClient ve IGroupManager metotları Task döndürdüğünden varsayılan Task.CompletedTask gerekir
        _mockClientProxy.SetReturnsDefault<Task>(Task.CompletedTask);
        _mockGroups.SetReturnsDefault<Task>(Task.CompletedTask);

        _mockClients.Setup(c => c.Caller).Returns(_mockClientProxy.Object);
        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
        _mockContext.Setup(c => c.ConnectionId).Returns("test_conn_id");

        _gameHub = new GameHub(_mockQuizService.Object)
        {
            Clients = _mockClients.Object,
            Groups  = _mockGroups.Object,
            Context = _mockContext.Object
        };
    }

    [Fact]
    public async Task JoinAsManager_ValidPin_AddsToGroup()
    {
        string pin = "123456";
        _mockQuizService.Setup(s => s.GetQuizByPin(pin)).Returns(new Quiz { IsActive = true });

        await _gameHub.JoinAsManager(pin);

        _mockGroups.Verify(g => g.AddToGroupAsync("test_conn_id", pin, default), Times.Once);
    }

    [Fact]
    public async Task JoinGame_ValidData_AddsToGroupAndNotifiesPlayers()
    {
        string pin = "123456";
        string nickname = "Oyuncu1";
        var player = new Player { Nickname = nickname };

        _mockQuizService
            .Setup(s => s.JoinOrRejoinAsync(pin, nickname, "test_conn_id", null, null, null))
            .ReturnsAsync((player, (string?)null, "fake_session_token"));

        await _gameHub.JoinGame(pin, nickname);

        _mockGroups.Verify(g => g.AddToGroupAsync("test_conn_id", pin, default), Times.Once);
        _mockClientProxy.Verify(p => p.PlayerJoined(It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task SubmitAnswer_ValidGuids_CallsServiceAndReturnsResult()
    {
        string pin = "123456";
        string nickname = "Oyuncu1";
        var qId = Guid.NewGuid();
        var oId = Guid.NewGuid();

        _mockQuizService
            .Setup(s => s.SubmitAnswerAsync(pin, nickname, qId, oId))
            .ReturnsAsync((true, 1, 10, 100));

        await _gameHub.SubmitAnswer(pin, nickname, qId.ToString(), oId.ToString());

        _mockClientProxy.Verify(p => p.AnswerResult(It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task StartGame_ValidPin_CallsServiceAndBroadcastsQuestion()
    {
        string pin = "123456";
        var quiz = new Quiz
        {
            Questions = new List<Question> { new Question { Id = Guid.NewGuid(), Options = new List<Option>() } }
        };
        _mockQuizService.Setup(s => s.GetQuizByPin(pin)).Returns(quiz);
        _mockQuizService.Setup(s => s.StartGameFlowAsync(pin)).Returns(Task.CompletedTask);

        await _gameHub.StartGame(pin);

        _mockQuizService.Verify(s => s.StartGameFlowAsync(pin), Times.Once);
        _mockClientProxy.Verify(p => p.ReceiveQuestion(It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task RejoinAsManager_ValidState_SendsRestoreGameState()
    {
        string pin = "123456";
        var dummyState = new { Quiz = new { Pin = pin } };
        _mockQuizService.Setup(s => s.GetFullGameState(pin)).Returns(dummyState);

        await _gameHub.RejoinAsManager(pin);

        _mockGroups.Verify(g => g.AddToGroupAsync("test_conn_id", pin, default), Times.Once);
        _mockClientProxy.Verify(p => p.RestoreGameState(dummyState), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_PlayerUnregistered_SendsPlayerLeftMessage()
    {
        string pin = "123456";
        string nickname = "KopanOyuncu";

        _mockQuizService
            .Setup(s => s.UnregisterPlayerAsync("test_conn_id"))
            .ReturnsAsync(((string?)pin, (string?)nickname));

        await _gameHub.OnDisconnectedAsync(null);

        _mockClientProxy.Verify(p => p.PlayerLeft(nickname), Times.Once);
    }

    [Fact]
    public async Task EndGame_ValidPin_StopsFlowAndSendsGameEnded()
    {
        string pin = "123456";
        var quiz = new Quiz { Players = new List<Player> { new Player { Nickname = "Kazanan", Score = 1000 } } };
        _mockQuizService.Setup(s => s.GetQuizByPin(pin)).Returns(quiz);

        await _gameHub.EndGame(pin);

        _mockQuizService.Verify(s => s.StopGameFlow(pin), Times.Once);
        _mockClientProxy.Verify(p => p.GameEnded(It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task ResetLobby_ValidPin_AbandonsQuizAndSendsLobbyReset()
    {
        string pin = "123456";
        _mockQuizService.Setup(s => s.AbandonQuizAsync(pin)).Returns(Task.CompletedTask);

        await _gameHub.ResetLobby(pin);

        _mockClientProxy.Verify(p => p.LobbyReset(), Times.Once);
        _mockQuizService.Verify(s => s.AbandonQuizAsync(pin), Times.Once);
    }

    [Fact]
    public async Task PlayAgain_ValidOldPin_CreatesNewQuizAndRedirects()
    {
        string oldPin = "123456";
        string newPin = "987654";
        var oldQuiz = new Quiz
        {
            Title = "Genel Kültür",
            Players = new List<Player> { new Player { Nickname = "SadikOyuncu" } }
        };

        _mockQuizService.Setup(s => s.GetQuizByPin(oldPin)).Returns(oldQuiz);
        _mockQuizService.Setup(s => s.CreateQuiz(It.IsAny<Quiz>())).Returns(newPin);

        await _gameHub.PlayAgain(oldPin);

        _mockClientProxy.Verify(p => p.RedirectToNewGame(It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task ShowLeaderboard_ValidPin_SendsUpdateLeaderboard()
    {
        string pin = "123456";
        var quiz = new Quiz { Players = new List<Player> { new Player { Nickname = "Lider", Score = 500 } } };
        _mockQuizService.Setup(s => s.GetQuizByPin(pin)).Returns(quiz);

        await _gameHub.ShowLeaderboard(pin);

        _mockClientProxy.Verify(p => p.UpdateLeaderboard(It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task JoinAsManager_InvalidPin_SendsError()
    {
        _mockQuizService.Setup(s => s.GetQuizByPin("invalid")).Returns((Quiz?)null);

        await _gameHub.JoinAsManager("invalid");

        _mockClientProxy.Verify(p => p.Error(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RejoinAsManager_InvalidState_SendsError()
    {
        _mockQuizService.Setup(s => s.GetFullGameState("invalid")).Returns((object?)null);

        await _gameHub.RejoinAsManager("invalid");

        _mockClientProxy.Verify(p => p.Error(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task JoinGame_Error_SendsError()
    {
        _mockQuizService
            .Setup(s => s.JoinOrRejoinAsync("123", "P1", "test_conn_id", null, null, null))
            .ReturnsAsync(((Player?)null, "Hata", (string?)null));

        await _gameHub.JoinGame("123", "P1");

        _mockClientProxy.Verify(p => p.Error("Hata"), Times.Once);
    }

    [Fact]
    public async Task SubmitAnswer_InvalidGuids_SendsFalse()
    {
        await _gameHub.SubmitAnswer("123", "P1", "bad-guid", "bad-guid");

        _mockClientProxy.Verify(p => p.AnswerResult(false), Times.Once);
    }

    [Fact]
    public async Task StartGame_NoQuestions_DoesNothing()
    {
        _mockQuizService.Setup(s => s.GetQuizByPin("123")).Returns(new Quiz { Questions = new List<Question>() });

        await _gameHub.StartGame("123");

        _mockQuizService.Verify(s => s.StartGameFlowAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PlayAgain_InvalidPin_DoesNothing()
    {
        _mockQuizService.Setup(s => s.GetQuizByPin("invalid")).Returns((Quiz?)null);

        await _gameHub.PlayAgain("invalid");

        _mockClientProxy.Verify(p => p.RedirectToNewGame(It.IsAny<object>()), Times.Never);
    }
}
