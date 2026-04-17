using System;
using System.Threading;
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
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<ISingleClientProxy> _mockClientProxy;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly GameHub _gameHub;

    public GameHubTests()
    {
        // Arrange (Hazırlık) - SignalR iç yapısındaki nesneleri sahteliyoruz (Mocking)
        _mockQuizService = new Mock<IQuizService>();
        _mockClients = new Mock<IHubCallerClients>();
        _mockClientProxy = new Mock<ISingleClientProxy>();
        _mockGroups = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();

        _mockClients.Setup(c => c.Caller).Returns(_mockClientProxy.Object);
        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
        _mockContext.Setup(c => c.ConnectionId).Returns("test_conn_id");

        // Hub'ı sahte nesnelerle oluşturuyoruz
        _gameHub = new GameHub(_mockQuizService.Object)
        {
            Clients = _mockClients.Object,
            Groups = _mockGroups.Object,
            Context = _mockContext.Object
        };
    }

    [Fact]
    public async Task JoinAsManager_ValidPin_AddsToGroup()
    {
        // Arrange
        string pin = "123456";
        _mockQuizService.Setup(s => s.GetQuizByPin(pin)).Returns(new Quiz { IsActive = true });

        // Act
        await _gameHub.JoinAsManager(pin);

        // Assert
        _mockGroups.Verify(g => g.AddToGroupAsync("test_conn_id", pin, default), Times.Once);
    }

    [Fact]
    public async Task JoinGame_ValidData_AddsToGroupAndNotifiesPlayers()
    {
        // Arrange
        string pin = "123456";
        string nickname = "Oyuncu1";
        var player = new Player { Nickname = nickname };
        _mockQuizService.Setup(s => s.JoinOrRejoin(pin, nickname, "test_conn_id"))
                        .Returns((player, null)); // Başarılı giriş

        // Act
        await _gameHub.JoinGame(pin, nickname);

        // Assert
        _mockGroups.Verify(g => g.AddToGroupAsync("test_conn_id", pin, default), Times.Once);
        // Diğer oyunculara PlayerJoined mesajı gittiğini doğrula
        _mockClientProxy.Verify(p => p.SendCoreAsync("PlayerJoined", It.Is<object[]>(args => (string)args[0] == nickname), default), Times.Once);
    }

    [Fact]
    public async Task SubmitAnswer_ValidGuids_CallsServiceAndReturnsResult()
    {
        // Arrange
        string pin = "123456";
        string nickname = "Oyuncu1";
        var qId = Guid.NewGuid();
        var oId = Guid.NewGuid();
        
        _mockQuizService.Setup(s => s.SubmitAnswer(pin, nickname, qId, oId)).Returns(true);

        // Act
        await _gameHub.SubmitAnswer(pin, nickname, qId.ToString(), oId.ToString());

        // Assert
        // Arayana (Caller) AnswerResult true mesajı gitmeli
        _mockClientProxy.Verify(p => p.SendCoreAsync("AnswerResult", It.Is<object[]>(args => (bool)args[0] == true), default), Times.Once);
    }

    [Fact]
    public async Task StartGame_ValidPin_CallsServiceAndBroadcastsQuestion()
    {
        // Arrange
        string pin = "123456";
        var quiz = new Quiz { 
            Questions = new List<Question> { new Question { Id = Guid.NewGuid(), Options = new List<Option>() } } 
        };
        _mockQuizService.Setup(s => s.GetQuizByPin(pin)).Returns(quiz);

        // Act
        await _gameHub.StartGame(pin);

        // Assert
        _mockQuizService.Verify(s => s.StartGameFlow(pin), Times.Once);
        // ReceiveQuestion event'i çağrılmalı
        _mockClientProxy.Verify(p => p.SendCoreAsync("ReceiveQuestion", It.IsAny<object[]>(), default), Times.Once);
    }

    [Fact]
    public async Task RejoinAsManager_ValidState_SendsRestoreGameState()
    {
        // Arrange
        string pin = "123456";
        var dummyState = new { Quiz = new { Pin = pin } };
        _mockQuizService.Setup(s => s.GetFullGameState(pin)).Returns(dummyState);

        // Act
        await _gameHub.RejoinAsManager(pin);

        // Assert
        _mockGroups.Verify(g => g.AddToGroupAsync("test_conn_id", pin, default), Times.Once);
        _mockClientProxy.Verify(p => p.SendCoreAsync("RestoreGameState", It.Is<object[]>(args => args[0] == dummyState), default), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_PlayerUnregistered_SendsPlayerLeftMessage()
    {
        // Arrange
        string pin = "123456";
        string nickname = "KopanOyuncu";
        _mockQuizService.Setup(s => s.UnregisterPlayer("test_conn_id")).Returns((pin, nickname));

        // Act
        await _gameHub.OnDisconnectedAsync(null);

        // Assert
        // Gruptaki diğer oyunculara PlayerLeft mesajı gönderildiğini doğrula
        _mockClientProxy.Verify(p => p.SendCoreAsync("PlayerLeft", It.Is<object[]>(args => (string)args[0] == nickname), default), Times.Once);
    }

    [Fact]
    public async Task EndGame_ValidPin_StopsFlowAndSendsGameEnded()
    {
        // Arrange
        string pin = "123456";
        var quiz = new Quiz { Players = new List<Player> { new Player { Nickname = "Kazanan", Score = 1000 } } };
        _mockQuizService.Setup(s => s.GetQuizByPin(pin)).Returns(quiz);

        // Act
        await _gameHub.EndGame(pin);

        // Assert
        _mockQuizService.Verify(s => s.StopGameFlow(pin), Times.Once);
        _mockClientProxy.Verify(p => p.SendCoreAsync("GameEnded", It.IsAny<object[]>(), default), Times.Once);
    }

    [Fact]
    public async Task ResetLobby_ValidPin_AbandonsQuizAndSendsLobbyReset()
    {
        // Arrange
        string pin = "123456";

        // Act
        await _gameHub.ResetLobby(pin);

        // Assert
        _mockClientProxy.Verify(p => p.SendCoreAsync("LobbyReset", It.IsAny<object[]>(), default), Times.Once);
        _mockQuizService.Verify(s => s.AbandonQuiz(pin), Times.Once);
    }

    [Fact]
    public async Task PlayAgain_ValidOldPin_CreatesNewQuizAndRedirects()
    {
        // Arrange
        string oldPin = "123456";
        string newPin = "987654";
        var oldQuiz = new Quiz { Title = "Genel Kültür", Players = new List<Player> { new Player { Nickname = "SadikOyuncu" } } };
        
        _mockQuizService.Setup(s => s.GetQuizByPin(oldPin)).Returns(oldQuiz);
        _mockQuizService.Setup(s => s.CreateQuiz(It.IsAny<Quiz>())).Returns(newPin);

        // Act
        await _gameHub.PlayAgain(oldPin);

        // Assert
        // Yeni oyuna geçiş paketi gönderilmeli
        _mockClientProxy.Verify(p => p.SendCoreAsync("RedirectToNewGame", It.IsAny<object[]>(), default), Times.Once);
    }

    [Fact]
    public async Task ShowLeaderboard_ValidPin_SendsUpdateLeaderboard()
    {
        // Arrange
        string pin = "123456";
        var quiz = new Quiz { Players = new List<Player> { new Player { Nickname = "Lider", Score = 500 } } };
        _mockQuizService.Setup(s => s.GetQuizByPin(pin)).Returns(quiz);

        // Act
        await _gameHub.ShowLeaderboard(pin);

        // Assert
        _mockClientProxy.Verify(p => p.SendCoreAsync("UpdateLeaderboard", It.IsAny<object[]>(), default), Times.Once);
    }

    [Fact]
    public async Task JoinAsManager_InvalidPin_SendsError()
    {
        _mockQuizService.Setup(s => s.GetQuizByPin("invalid")).Returns((Quiz?)null);
        await _gameHub.JoinAsManager("invalid");
        _mockClientProxy.Verify(p => p.SendCoreAsync("Error", It.IsAny<object[]>(), default), Times.Once);
    }

    [Fact]
    public async Task RejoinAsManager_InvalidState_SendsError()
    {
        _mockQuizService.Setup(s => s.GetFullGameState("invalid")).Returns((object?)null);
        await _gameHub.RejoinAsManager("invalid");
        _mockClientProxy.Verify(p => p.SendCoreAsync("Error", It.IsAny<object[]>(), default), Times.Once);
    }

    [Fact]
    public async Task JoinGame_Error_SendsError()
    {
        _mockQuizService.Setup(s => s.JoinOrRejoin("123", "P1", "test_conn_id")).Returns((null, "Hata"));
        await _gameHub.JoinGame("123", "P1");
        _mockClientProxy.Verify(p => p.SendCoreAsync("Error", It.Is<object[]>(a => (string)a[0] == "Hata"), default), Times.Once);
    }

    [Fact]
    public async Task SubmitAnswer_InvalidGuids_SendsFalse()
    {
        await _gameHub.SubmitAnswer("123", "P1", "bad-guid", "bad-guid");
        _mockClientProxy.Verify(p => p.SendCoreAsync("AnswerResult", It.Is<object[]>(a => (bool)a[0] == false), default), Times.Once);
    }

    [Fact]
    public async Task StartGame_NoQuestions_DoesNothing()
    {
        _mockQuizService.Setup(s => s.GetQuizByPin("123")).Returns(new Quiz { Questions = new List<Question>() });
        await _gameHub.StartGame("123");
        _mockQuizService.Verify(s => s.StartGameFlow(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PlayAgain_InvalidPin_DoesNothing()
    {
        _mockQuizService.Setup(s => s.GetQuizByPin("invalid")).Returns((Quiz?)null);
        await _gameHub.PlayAgain("invalid");
        _mockClientProxy.Verify(p => p.SendCoreAsync("RedirectToNewGame", It.IsAny<object[]>(), default), Times.Never);
    }
}