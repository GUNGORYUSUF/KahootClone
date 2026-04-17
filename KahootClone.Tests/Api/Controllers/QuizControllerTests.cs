using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using KahootClone.Api.Controllers;
using KahootClone.Application.Interfaces;
using KahootClone.Domain.Entities;

namespace KahootClone.Tests.Api.Controllers;

public class QuizControllerTests
{
    private readonly Mock<IQuizService> _mockQuizService;
    private readonly QuizController _controller;

    public QuizControllerTests()
    {
        _mockQuizService = new Mock<IQuizService>();
        _controller = new QuizController(_mockQuizService.Object);
    }

    [Fact]
    public void CreateQuiz_ValidRequest_ReturnsOkWithPin()
    {
        // Arrange (Hazırlık)
        var requestQuiz = new Quiz { Title = "Matematik Testi" };
        string expectedPin = "123456";
        _mockQuizService.Setup(s => s.CreateQuiz(It.IsAny<Quiz>())).Returns(expectedPin);

        // Act (Eylem)
        var result = _controller.CreateQuiz(requestQuiz);

        // Assert (Doğrulama)
        var okResult = Assert.IsType<OkObjectResult>(result); // 200 OK dönmeli
        
        // Dönüş objesinin içindeki özellikleri reflection veya dynamic ile kontrol edebiliriz
        Assert.NotNull(okResult.Value);
    }
}