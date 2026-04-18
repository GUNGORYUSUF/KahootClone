using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using KahootClone.Api.Controllers;
using KahootClone.Application.Interfaces;
using KahootClone.Domain.Entities;
using KahootClone.Application.DTOs;
using Microsoft.Extensions.Configuration;

namespace KahootClone.Tests.Api.Controllers;

public class QuizControllerTests
{
    private readonly Mock<IQuizService> _mockQuizService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly QuizController _controller;

    public QuizControllerTests()
    {
        _mockQuizService = new Mock<IQuizService>();
        _mockConfiguration = new Mock<IConfiguration>();
        
        _mockConfiguration.Setup(c => c["Jwt:Key"]).Returns("TestSecretKey_1234567890123456");
        
        _controller = new QuizController(_mockQuizService.Object, _mockConfiguration.Object);
    }

    [Fact]
    public void CreateQuiz_ValidRequest_ReturnsOkWithPin()
    {
        // Arrange (Hazırlık)
        var requestDto = new CreateQuizRequestDto { Title = "Matematik Testi" }; // AŞAMA 3: Quiz yerine DTO kullanıldı
        string expectedPin = "123456";
        _mockQuizService.Setup(s => s.CreateQuiz(It.IsAny<Quiz>())).Returns(expectedPin);

        // Act (Eylem)
        var result = _controller.CreateQuiz(requestDto); // AŞAMA 3: Controller'a DTO gönderildi

        // Assert (Doğrulama)
        var okResult = Assert.IsType<OkObjectResult>(result); // 200 OK dönmeli
        
        // Dönüş objesinin içindeki özellikleri reflection veya dynamic ile kontrol edebiliriz
        Assert.NotNull(okResult.Value);
    }
}