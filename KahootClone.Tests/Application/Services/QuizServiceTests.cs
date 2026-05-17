using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using KahootClone.Application.Services;
using KahootClone.Application.Interfaces;
using KahootClone.Domain.Entities;

namespace KahootClone.Tests.Application.Services;

public class QuizServiceTests
{
    // Bağımlılıkları taklit (Mock) etmek için değişkenler
    private readonly Mock<IQuizRepository> _mockQuizRepo;
    private readonly Mock<IGameStateRepository> _mockGameStateRepo;
    private readonly Mock<IMessagePublisher> _mockPublisher;
    private readonly QuizService _quizService;

    public QuizServiceTests()
    {
        // Her testten önce sıfır, taze mock nesneleri oluşturulur
        _mockQuizRepo = new Mock<IQuizRepository>();
        _mockGameStateRepo = new Mock<IGameStateRepository>();
        _mockPublisher = new Mock<IMessagePublisher>();
        
        // Test edilecek gerçek servis, sahte bağımlılıklarla ayağa kaldırılır
        _quizService = new QuizService(_mockQuizRepo.Object, _mockGameStateRepo.Object, _mockPublisher.Object);
    }

    [Fact]
    public void CreateQuiz_ShouldGeneratePin_AndSaveToRepository()
    {
        // 1. Arrange (Hazırlık)
        var quiz = new Quiz 
        { 
            Title = "Test Quiz",
            CreatorId = "user123"
        };

        // PIN benzersizlik kontrolü için veritabanında bu PIN'in olmadığını (null) varsayıyoruz
        _mockQuizRepo.Setup(r => r.GetByPin(It.IsAny<string>())).Returns((Quiz?)null);

        // 2. Act (Eylem)
        var generatedPin = _quizService.CreateQuiz(quiz);

        // 3. Assert (Doğrulama)
        Assert.False(string.IsNullOrEmpty(generatedPin), "Üretilen PIN boş olamaz.");
        Assert.True(quiz.IsActive, "Yeni oluşturulan oyun aktif (IsActive = true) olarak işaretlenmelidir.");
        Assert.Equal(generatedPin, quiz.Pin);
        Assert.NotEmpty(quiz.Questions); // Dışarıdan soru gelmediği için varsayılan örnek soruları üretmeli
        
        // Gerçekten veritabanına kayıt işlemi (Add metodu) tam 1 kez çağırıldı mı?
        _mockQuizRepo.Verify(r => r.Add(It.Is<Quiz>(q => q == quiz)), Times.Once);
    }

    [Fact]
    public async Task SubmitAnswerAsync_ShouldReturnTrueAndPoints_WhenAnswerIsCorrect()
    {
        // 1. Arrange (Hazırlık)
        var pin = "123456";
        var nickname = "TestPlayer";
        var questionId = Guid.NewGuid();
        var correctOptionId = Guid.NewGuid();

        var quiz = new Quiz
        {
            Pin = pin,
            IsActive = true,
            Players = new List<Player> { new Player { Nickname = nickname, Score = 0 } },
            Questions = new List<Question>
            {
                new Question
                {
                    Id = questionId,
                    TimeLimitInSeconds = 20,
                    Options = new List<Option>
                    {
                        new Option { Id = correctOptionId, IsCorrect = true },
                        new Option { Id = Guid.NewGuid(), IsCorrect = false }
                    }
                }
            }
        };

        _mockQuizRepo.Setup(r => r.GetByPin(pin)).Returns(quiz);

        // 2. Act (Eylem)
        var (isCorrect, answeredCount, totalCount, points) = await _quizService.SubmitAnswerAsync(pin, nickname, questionId, correctOptionId);

        // 3. Assert (Doğrulama)
        Assert.True(isCorrect);
        Assert.True(points > 0, "Doğru cevap verildiğinde puan 0'dan büyük olmalıdır.");
        Assert.Equal(1, answeredCount);
        _mockQuizRepo.Verify(r => r.Update(It.Is<Quiz>(q => q.Pin == pin)), Times.Once);
    }
}