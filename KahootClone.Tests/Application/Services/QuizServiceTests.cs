using System;
using System.Collections.Generic;
using Moq;
using Xunit;
using KahootClone.Application.Interfaces;
using KahootClone.Application.Services;
using KahootClone.Domain.Entities;

namespace KahootClone.Tests.Application.Services;

public class QuizServiceTests
{
    // Bağımlılıkların (Dependencies) sahteleri (Mock) ve test edilecek servis
    private readonly Mock<IQuizRepository> _mockQuizRepository;
    private readonly Mock<IGameStateRepository> _mockGameStateRepository;
    private readonly Mock<IMessagePublisher> _mockMessagePublisher;
    private readonly QuizService _quizService;

    public QuizServiceTests()
    {
        // Ortak bağımlılıklar her test öncesi sıfırlanarak izolasyon sağlanır (Test Isolation).
        _mockQuizRepository = new Mock<IQuizRepository>();
        _mockGameStateRepository = new Mock<IGameStateRepository>();
        _mockGameStateRepository.Setup(r => r.TryAcquireTickLockAsync(It.IsAny<string>())).ReturnsAsync(true);
        _mockGameStateRepository.Setup(r => r.AcquireQuizLockAsync(It.IsAny<string>())).ReturnsAsync(new Mock<IAsyncDisposable>().Object);
        _mockMessagePublisher = new Mock<IMessagePublisher>();
        _quizService = new QuizService(_mockQuizRepository.Object, _mockGameStateRepository.Object, _mockMessagePublisher.Object);
    }

    [Fact]
    public void CreateQuiz_ValidData_ReturnsNewQuizIdAndSavesToDatabase()
    {
        // Arrange (Hazırlık)
        var quiz = new Quiz { Title = "Test Bilgi Yarışması" };

        // Act (Eylem)
        var pin = _quizService.CreateQuiz(quiz);

        // Assert (Doğrulama)
        Assert.False(string.IsNullOrWhiteSpace(pin));
        Assert.True(quiz.IsActive);
        Assert.NotEmpty(quiz.Questions);
        // Veritabanına kaydetme metodunun tam olarak 1 kere çağrıldığını doğrula
        _mockQuizRepository.Verify(repo => repo.Add(It.IsAny<Quiz>()), Times.Once);
    }

    [Fact]
    public void CreateQuiz_WithExternalQuestions_UsesProvidedQuestionsAndAssignsGuids()
    {
        // Arrange (Hazırlık)
        var externalQuestion = new Question { Text = "Dış Soru", Options = new List<Option> { new Option { Text = "Şık 1", IsCorrect = true } } };
        var quiz = new Quiz { Title = "Dışarıdan Gelen", Questions = new List<Question> { externalQuestion } };

        // Act (Eylem)
        var pin = _quizService.CreateQuiz(quiz);

        // Assert (Doğrulama)
        Assert.Single(quiz.Questions);
        Assert.Equal("Dış Soru", quiz.Questions[0].Text);
        Assert.NotEqual(Guid.Empty, quiz.Questions[0].Id); // Guid atanmış olmalı
        Assert.Single(quiz.Questions[0].Options);
        Assert.NotEqual(Guid.Empty, quiz.Questions[0].Options[0].Id); // Şıklara da Guid atanmalı
        _mockQuizRepository.Verify(repo => repo.Add(quiz), Times.Once);
    }

    [Fact]
    public void GetQuizByPin_ExistingPin_ReturnsQuizData()
    {
        // Arrange (Hazırlık)
        string pin = "123456";
        var expectedQuiz = new Quiz { Pin = pin, Title = "Mevcut Oyun" };
        _mockQuizRepository.Setup(repo => repo.GetByPin(pin)).Returns(expectedQuiz);

        // Act (Eylem)
        var result = _quizService.GetQuizByPin(pin);

        // Assert (Doğrulama)
        Assert.NotNull(result);
        Assert.Equal(pin, result?.Pin);
        Assert.Equal("Mevcut Oyun", result?.Title);
    }

    [Fact]
    public void GetQuizByPin_NonExistingPin_ReturnsNull()
    {
        // Arrange (Hazırlık) - Veritabanında eşleşmeyen bir PIN
        _mockQuizRepository.Setup(repo => repo.GetByPin(It.IsAny<string>())).Returns((Quiz?)null);

        // Act (Eylem)
        var result = _quizService.GetQuizByPin("999999");

        // Assert (Doğrulama)
        Assert.Null(result);
    }

    [Fact]
    public async Task JoinOrRejoin_InvalidPin_ReturnsErrorMessage()
    {
        // Arrange (Hazırlık)
        _mockQuizRepository.Setup(repo => repo.GetByPin(It.IsAny<string>())).Returns((Quiz?)null);

        // Act (Eylem)
        var (player, errorMessage, _) = await _quizService.JoinOrRejoinAsync("invalid_pin", "Oyuncu1", "conn_id_1");

        // Assert (Doğrulama)
        Assert.Null(player);
        Assert.Equal("Geçersiz PIN veya oyun aktif değil.", errorMessage);
    }

    [Fact]
    public async Task JoinOrRejoin_NewPlayer_AddsPlayerAndReturnsSuccess()
    {
        // Arrange (Hazırlık)
        string pin = "123456";
        var quiz = new Quiz { Pin = pin, IsActive = true, Players = new List<Player>() };
        _mockQuizRepository.Setup(repo => repo.GetByPin(pin)).Returns(quiz);

        // Act (Eylem)
        var (player, errorMessage, _) = await _quizService.JoinOrRejoinAsync(pin, "YeniOyuncu", "conn_id_1");

        // Assert (Doğrulama)
        Assert.Null(errorMessage);
        Assert.NotNull(player);
        Assert.Equal("YeniOyuncu", player?.Nickname);
        Assert.Equal("conn_id_1", player?.ConnectionId);
        _mockQuizRepository.Verify(repo => repo.Update(quiz), Times.Once);
    }

    [Fact]
    public async Task JoinOrRejoin_ExistingPlayerWithWrongToken_ReturnsErrorMessage()
    {
        // Arrange (Hazırlık)
        string pin = "123456";
        var existingPlayer = new Player { Id = Guid.NewGuid(), Nickname = "KopyaOyuncu", ConnectionId = "conn_id_1" };
        var quiz = new Quiz { Pin = pin, IsActive = true, Players = new List<Player> { existingPlayer } };
        _mockQuizRepository.Setup(repo => repo.GetByPin(pin)).Returns(quiz);

        // Act (Eylem)
        var (player, errorMessage, _) = await _quizService.JoinOrRejoinAsync(pin, "KopyaOyuncu", "conn_id_2", "wrong_token");

        // Assert (Doğrulama)
        Assert.Null(player);
        Assert.Equal("Bu takma ad şu anda başka bir oyuncu tarafından kullanılıyor.", errorMessage);
        _mockQuizRepository.Verify(repo => repo.Update(It.IsAny<Quiz>()), Times.Never);
    }

    [Fact]
    public async Task JoinOrRejoin_DisconnectedPlayer_UpdatesConnectionIdAndReturnsPlayer()
    {
        // Arrange (Hazırlık)
        string pin = "123456";
        // ConnectionId boş ise, oyuncu daha önce kopmuş demektir.
        var disconnectedPlayer = new Player { Nickname = "KopanOyuncu", ConnectionId = string.Empty };
        var quiz = new Quiz { Pin = pin, IsActive = true, Players = new List<Player> { disconnectedPlayer } };
        _mockQuizRepository.Setup(repo => repo.GetByPin(pin)).Returns(quiz);

        // Act (Eylem)
        var (player, errorMessage) = await _quizService.JoinOrRejoinAsync(pin, "KopanOyuncu", "new_conn_id");

        // Assert (Doğrulama)
        Assert.Null(errorMessage);
        Assert.NotNull(player);
        Assert.Equal("new_conn_id", player?.ConnectionId);
        _mockQuizRepository.Verify(repo => repo.Update(quiz), Times.Once);
    }

    [Fact]
    public async Task SubmitAnswer_InvalidQuiz_ReturnsFalse()
    {
        // Arrange (Hazırlık)
        _mockQuizRepository.Setup(repo => repo.GetByPin(It.IsAny<string>())).Returns((Quiz?)null);

        // Act (Eylem)
        var result = await _quizService.SubmitAnswerAsync("invalid_pin", "Oyuncu1", Guid.NewGuid(), Guid.NewGuid());

        // Assert (Doğrulama)
        Assert.False(result);
    }

    [Fact]
    public async Task SubmitAnswer_DoubleAnswering_ReturnsFalse()
    {
        // Arrange (Hazırlık) - Uç Durum (Edge Case): Oyuncunun aynı soruya iki kez cevap vermeye çalışması
        string pin = "123456";
        var questionId = Guid.NewGuid();
        var optionId = Guid.NewGuid();

        var player = new Player 
        { 
            Nickname = "KurnazOyuncu", 
            AnsweredQuestionIds = new List<Guid> { questionId } // Oyuncu bu soruyu zaten cevaplamış
        };

        var quiz = new Quiz
        {
            Pin = pin,
            IsActive = true,
            Players = new List<Player> { player },
            Questions = new List<Question> { new Question { Id = questionId } }
        };

        _mockQuizRepository.Setup(repo => repo.GetByPin(pin)).Returns(quiz);

        // Act (Eylem)
        var result = await _quizService.SubmitAnswerAsync(pin, "KurnazOyuncu", questionId, optionId);

        // Assert (Doğrulama)
        Assert.False(result); // Aynı soruyu tekrar cevaplaması engellenmeli
        _mockQuizRepository.Verify(repo => repo.Update(It.IsAny<Quiz>()), Times.Never); // DB'ye yansımamalı
    }

    [Fact]
    public async Task StartGameFlow_ValidQuiz_StartsGameAndUpdatesDatabase()
    {
        // Arrange (Hazırlık)
        // Static sözlüklerde çakışma olmaması için benzersiz PIN kullanıyoruz.
        string pin = "START_123";
        var quiz = new Quiz 
        { 
            Pin = pin, 
            Questions = new List<Question> { new Question { TimeLimitInSeconds = 20 } } 
        };
        _mockQuizRepository.Setup(repo => repo.GetByPin(pin)).Returns(quiz);

        // Act (Eylem)
        await _quizService.StartGameFlowAsync(pin);

        // Assert (Doğrulama)
        _mockQuizRepository.Verify(repo => repo.Update(quiz), Times.Once);
        Assert.NotEqual(default, quiz.CurrentQuestionStartTime);
        
        // Temizlik (Cleanup)
        _quizService.StopGameFlow(pin);
    }

    [Fact]
    public async Task StartGameFlow_InvalidQuiz_DoesNothing()
    {
        // Arrange (Hazırlık)
        string pin = "START_INV";
        _mockQuizRepository.Setup(repo => repo.GetByPin(pin)).Returns((Quiz?)null);

        // Act (Eylem)
        await _quizService.StartGameFlowAsync(pin);

        // Assert (Doğrulama)
        _mockQuizRepository.Verify(repo => repo.Update(It.IsAny<Quiz>()), Times.Never);
    }

    [Fact]
    public async Task UnregisterPlayer_ValidConnection_SetsConnectionIdToEmpty()
    {
        // Arrange (Hazırlık)
        string pin = "UNREG_123";
        string connId = "conn_unreg";
        var quiz = new Quiz { Pin = pin, IsActive = true, Players = new List<Player>() };
        _mockQuizRepository.Setup(repo => repo.GetByPin(pin)).Returns(quiz);
        
        // Haritayı doldurmak için oyuncuyu içeri alıyoruz
        await _quizService.JoinOrRejoinAsync(pin, "TestPlayer", connId);

        // Act (Eylem)
        var result = await _quizService.UnregisterPlayerAsync(connId);

        // Assert (Doğrulama)
        Assert.Equal(pin, result.Pin);
        Assert.Equal("TestPlayer", result.Nickname);
        
        var unregPlayer = quiz.Players.First();
        Assert.Empty(unregPlayer.ConnectionId); // Bağlantı koptuğu için boş olmalı
        _mockQuizRepository.Verify(repo => repo.Update(quiz), Times.AtLeastOnce);
    }

    [Fact]
    public async Task UnregisterPlayer_InvalidConnection_ReturnsNulls()
    {
        // Arrange (Hazırlık)
        string connId = "unknown_conn";

        // Act (Eylem)
        var result = await _quizService.UnregisterPlayerAsync(connId);

        // Assert (Doğrulama)
        Assert.Null(result.Pin);
        Assert.Null(result.Nickname);
    }

    [Fact]
    public async Task AbandonQuiz_ValidQuiz_DeactivatesAndUpdates()
    {
        // Arrange (Hazırlık)
        string pin = "ABANDON_123";
        var quiz = new Quiz { Pin = pin, IsActive = true, Players = new List<Player>() };
        _mockQuizRepository.Setup(repo => repo.GetByPin(pin)).Returns(quiz);

        // Act (Eylem)
        await _quizService.AbandonQuizAsync(pin);

        // Assert (Doğrulama)
        Assert.False(quiz.IsActive);
        _mockQuizRepository.Verify(repo => repo.Update(quiz), Times.Once);
    }

    [Fact]
    public void GetFullGameState_ValidQuiz_ReturnsAnonymousObject()
    {
        // Arrange (Hazırlık)
        string pin = "STATE_123";
        var quiz = new Quiz { Pin = pin, Questions = new List<Question> { new Question { Id = Guid.NewGuid(), Text = "Q1" } } };
        _mockQuizRepository.Setup(repo => repo.GetByPin(pin)).Returns(quiz);

        // Act (Eylem)
        var state = _quizService.GetFullGameState(pin);

        // Assert (Doğrulama)
        Assert.NotNull(state);
    }

    [Fact]
    public async Task ProcessTicks_ActiveGame_ReducesTimeRemainingAndReturnsTimeUpdate()
    {
        // Arrange (Hazırlık)
        string pin = "TICK_123";
        var quiz = new Quiz { Pin = pin, Questions = new List<Question> { new Question { TimeLimitInSeconds = 20 } } };
        _mockQuizRepository.Setup(repo => repo.GetByPin(pin)).Returns(quiz);
        await _quizService.StartGameFlowAsync(pin);

        // Act (Eylem)
        var events = await _quizService.ProcessTicksAsync();

        // Assert (Doğrulama)
        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.Pin == pin && e.EventName == "TimeUpdate");
        _quizService.StopGameFlow(pin); // Temizlik
    }

    [Fact]
    public async Task ProcessTicks_TimeZero_TransitionsToWaitPhase()
    {
        // Arrange (Hazırlık) - Süreyi 1 saniye veriyoruz ki hemen bitsin
        string pin = "TICK_WAIT_123";
        var quiz = new Quiz { Pin = pin, Questions = new List<Question> { new Question { TimeLimitInSeconds = 1, Options = new List<Option>() } } };
        _mockQuizRepository.Setup(repo => repo.GetByPin(pin)).Returns(quiz);
        await _quizService.StartGameFlowAsync(pin);

        // Act (Eylem) - TimeRemaining 1'di. Ticks çalışınca 0'a düşer ve WaitPhase'e geçer
        var events = await _quizService.ProcessTicksAsync();

        // Assert (Doğrulama)
        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.Pin == pin && e.EventName == "WaitPhase");
        _quizService.StopGameFlow(pin); // Temizlik
    }

    [Fact]
    public async Task SubmitAnswer_CorrectAnswer_CalculatesScoreAndReturnsTrue()
    {
        // Arrange (Hazırlık)
        string pin = "SCORE_123";
        var qId = Guid.NewGuid();
        var oId = Guid.NewGuid();
        var player = new Player { Nickname = "HizliOyuncu", Score = 0 };
        
        var quiz = new Quiz { 
            Pin = pin, IsActive = true, 
            CurrentQuestionStartTime = DateTime.UtcNow.AddSeconds(-5), // Soru 5 saniye önce sorulmuş
            Players = new List<Player> { player },
            Questions = new List<Question> { 
                new Question { Id = qId, TimeLimitInSeconds = 20, Options = new List<Option> { new Option { Id = oId, IsCorrect = true } } } 
            } 
        };
        _mockQuizRepository.Setup(r => r.GetByPin(pin)).Returns(quiz);

        // Act (Eylem)
        var result = await _quizService.SubmitAnswerAsync(pin, "HizliOyuncu", qId, oId);

        // Assert (Doğrulama)
        Assert.True(result); // Doğru cevap
        Assert.True(player.Score > 0); // Puan verilmiş olmalı
        _mockQuizRepository.Verify(r => r.Update(quiz), Times.Once);
    }

    [Fact]
    public async Task SubmitAnswer_IncorrectAnswer_ReturnsFalseAndZeroScore()
    {
        // Arrange (Hazırlık)
        string pin = "SCORE_456";
        var qId = Guid.NewGuid();
        var oId = Guid.NewGuid();
        var player = new Player { Nickname = "YanlisOyuncu", Score = 0 };
        
        var quiz = new Quiz { 
            Pin = pin, IsActive = true, 
            Players = new List<Player> { player },
            Questions = new List<Question> { 
                new Question { Id = qId, TimeLimitInSeconds = 20, Options = new List<Option> { new Option { Id = oId, IsCorrect = false } } } 
            } 
        };
        _mockQuizRepository.Setup(r => r.GetByPin(pin)).Returns(quiz);

        // Act (Eylem)
        var result = await _quizService.SubmitAnswerAsync(pin, "YanlisOyuncu", qId, oId);

        // Assert (Doğrulama)
        Assert.False(result); // Yanlış cevap
        Assert.Equal(0, player.Score); // Puan artmamalı
    }

    [Fact]
    public void GetFullGameState_NullQuiz_ReturnsNull()
    {
        _mockQuizRepository.Setup(repo => repo.GetByPin(It.IsAny<string>())).Returns((Quiz?)null);
        Assert.Null(_quizService.GetFullGameState("999999"));
    }

    [Fact]
    public async Task SubmitAnswer_AllPlayersAnswered_ForcesTimeRemainingToZero()
    {
        // Arrange (Hazırlık)
        string pin = "ALL_ANS_123";
        var qId = Guid.NewGuid();
        var oId = Guid.NewGuid();
        var player = new Player { Nickname = "TekOyuncu", ConnectionId = "conn1" };
        
        var quiz = new Quiz { 
            Pin = pin, IsActive = true, 
            CurrentQuestionStartTime = DateTime.UtcNow,
            Players = new List<Player> { player },
            Questions = new List<Question> { new Question { Id = qId, TimeLimitInSeconds = 20, Options = new List<Option> { new Option { Id = oId, IsCorrect = true } } } } 
        };
        _mockQuizRepository.Setup(r => r.GetByPin(pin)).Returns(quiz);
        await _quizService.StartGameFlowAsync(pin); // Oyunu başlat ve Question fazına al

        // Act (Eylem) - Oyuncu cevap verir ve herkes cevaplamış olur
        await _quizService.SubmitAnswerAsync(pin, "TekOyuncu", qId, oId);
        var events = await _quizService.ProcessTicksAsync(); // Süre sıfırlandığı için direkt WaitPhase'e geçmeli

        // Assert (Doğrulama)
        Assert.Contains(events, e => e.EventName == "WaitPhase");
        _quizService.StopGameFlow(pin); // Temizlik
    }

    [Fact]
    public async Task SubmitAnswer_PlayerNotInLobby_ReturnsFalse()
    {
        string pin = "TEST_PIN";
        var qId = Guid.NewGuid();
        var quiz = new Quiz { Pin = pin, IsActive = true, Questions = new List<Question> { new Question { Id = qId } }, Players = new List<Player>() };
        _mockQuizRepository.Setup(r => r.GetByPin(pin)).Returns(quiz);

        var result = await _quizService.SubmitAnswerAsync(pin, "Unknown", qId, Guid.NewGuid());

        Assert.False(result.IsCorrect);
    }

    [Fact]
    public async Task SubmitAnswer_GamePhaseNotQuestion_ReturnsFalse()
    {
        string pin = "TEST_PIN2";
        var qId = Guid.NewGuid();
        var player = new Player { Nickname = "P1" };
        var quiz = new Quiz { Pin = pin, IsActive = true, Questions = new List<Question> { new Question { Id = qId, TimeLimitInSeconds = 0 } }, Players = new List<Player> { player } };
        _mockQuizRepository.Setup(r => r.GetByPin(pin)).Returns(quiz);

        await _quizService.StartGameFlowAsync(pin);
        await _quizService.ProcessTicksAsync(); // Süre 0 olduğu için Transition (WaitPhase) aşamasına geçer.

        var result = await _quizService.SubmitAnswerAsync(pin, "P1", qId, Guid.NewGuid());

        Assert.False(result.IsCorrect);
        _quizService.StopGameFlow(pin);
    }

    [Fact]
    public async Task ProcessTicks_TransitionPhase_NextQuestion()
    {
        string pin = "TRANS_2";
        var quiz = new Quiz { Pin = pin, Questions = new List<Question> { new Question { TimeLimitInSeconds = 0 }, new Question { TimeLimitInSeconds = 20 } } };
        _mockQuizRepository.Setup(r => r.GetByPin(pin)).Returns(quiz);
        
        await _quizService.StartGameFlowAsync(pin);
        await _quizService.ProcessTicksAsync(); // WaitPhase'e girer, süreyi 7 yapar.
        for(int i=0; i<7; i++) await _quizService.ProcessTicksAsync(); // 7 saniye bekler.
        
        var events = await _quizService.ProcessTicksAsync(); // Süre biter, yeni soruya geçer.
        Assert.Contains(events, e => e.EventName == "ReceiveQuestion");
        _quizService.StopGameFlow(pin);
    }

    [Fact]
    public async Task ProcessTicks_TransitionPhase_EndGame()
    {
        string pin = "TRANS_3";
        var quiz = new Quiz { Pin = pin, Questions = new List<Question> { new Question { TimeLimitInSeconds = 0 } } };
        _mockQuizRepository.Setup(r => r.GetByPin(pin)).Returns(quiz);
        
        await _quizService.StartGameFlowAsync(pin);
        await _quizService.ProcessTicksAsync(); // WaitPhase'e girer.
        for(int i=0; i<7; i++) await _quizService.ProcessTicksAsync();
        
        var events = await _quizService.ProcessTicksAsync(); // Başka soru kalmadığı için oyun biter.
        Assert.Contains(events, e => e.EventName == "GameEnded");
        _quizService.StopGameFlow(pin);
    }

    [Fact]
    public async Task GetFullGameState_ActiveGame_ReturnsState()
    {
        string pin = "STATE_ACT";
        var quiz = new Quiz { Pin = pin, Questions = new List<Question> { new Question { Id = Guid.NewGuid(), TimeLimitInSeconds = 20, Options = new List<Option>() } }, Players = new List<Player> { new Player { ConnectionId = "c1", AnsweredQuestionIds = new List<Guid>() } } };
        _mockQuizRepository.Setup(r => r.GetByPin(pin)).Returns(quiz);
        
        await _quizService.StartGameFlowAsync(pin);
        var state = _quizService.GetFullGameState(pin);
        
        Assert.NotNull(state);
        _quizService.StopGameFlow(pin);
    }
}