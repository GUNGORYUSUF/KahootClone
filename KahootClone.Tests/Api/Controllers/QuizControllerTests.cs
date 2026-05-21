using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using KahootClone.Api.Controllers;
using KahootClone.Application.Interfaces;
using KahootClone.Domain.Entities;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

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

        _mockConfiguration.Setup(c => c["Jwt:Key"]).Returns("TestSecretKey_KahootClone_Unit_Test_2024!");

        _controller = new QuizController(_mockQuizService.Object, _mockConfiguration.Object);

        // Controller.User null olmaması için anonim bir HttpContext atanır
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
    }

    [Fact]
    public void CreateQuiz_ValidRequest_ReturnsOkWithPin()
    {
        // Arrange
        var quiz = new Quiz { Title = "Matematik Testi", Questions = new List<Question>() };
        string expectedPin = "123456";
        _mockQuizService.Setup(s => s.CreateQuiz(It.IsAny<Quiz>())).Returns(expectedPin);

        // Act
        var result = _controller.CreateQuiz(quiz);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }
}
