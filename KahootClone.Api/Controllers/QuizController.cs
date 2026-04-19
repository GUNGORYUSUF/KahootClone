using Microsoft.AspNetCore.Mvc;
using KahootClone.Application.Interfaces;
using KahootClone.Domain.Entities;
using KahootClone.Application.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using KahootClone.Application.Utils;

namespace KahootClone.Api.Controllers;

// Dışarıdan gelen web isteklerinin bu sınıfa yönlendirilmesi sağlanır.
[ApiController]
[Route("api/[controller]")]
public class QuizController : ControllerBase
{
    private readonly IQuizService _quizService;
    private readonly IConfiguration _configuration;

    // İş mantığı servisi (QuizService) sisteme enjekte edilir.
    public QuizController(IQuizService quizService, IConfiguration configuration)
    {
        _quizService = quizService;
        _configuration = configuration;
    }

    // Yeni bir oyun oluşturmak için POST isteği karşılanır.
    [HttpPost("create")]
    public IActionResult CreateQuiz([FromBody] CreateQuizRequestDto request)
    {
        var quiz = new Quiz { Title = request.Title };

        // YENİ: Dışarıdan gelen DTO sorularını Entity (Domain) sorularına dönüştür (Mapping)
        if (request.Questions != null && request.Questions.Any())
        {
            quiz.Questions = request.Questions.Select(q => new Question
            {
                Id = Guid.NewGuid(),
                Text = q.Text,
                TimeLimitInSeconds = q.TimeLimitInSeconds,
                Options = q.Options.Select(o => new Option
                {
                    Id = Guid.NewGuid(),
                    Text = o.Text,
                    IsCorrect = o.IsCorrect
                }).ToList()
            }).ToList();
        }

        // Gelen oyun bilgileriyle PIN üretme işlemi tetiklenir.
        string pin = _quizService.CreateQuiz(quiz);

        // AŞAMA 5: Yönetici için "Host" yetkisine sahip bir JWT Token üretilir.
        string token = GenerateJwtToken("Host", pin);

        // Üretilen PIN kodu ve başarı mesajı istemciye (tarayıcıya) döndürülür.
        return Ok(new { Pin = pin, Token = token, Message = "Oyun başarıyla oluşturuldu." });
    }

    // YENİ: Markdown metnini alır ve yapılandırılmış QuestionDto listesine çevirerek Frontend'e döner.
    [HttpPost("parse-markdown")]
    public IActionResult ParseMarkdown([FromBody] MarkdownRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.MarkdownText))
        {
            return BadRequest(new { Message = "Markdown metni boş olamaz." });
        }

        var questions = MarkdownQuestionParser.Parse(request.MarkdownText);
        return Ok(questions);
    }

    // AŞAMA 5: İstenilen yetkiye (Role) göre şifreli bir JWT kimlik kartı üretir.
    private string GenerateJwtToken(string role, string pin)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "KahootCloneSuperSecretKey_1234567890123456";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, pin),
            new Claim(ClaimTypes.Role, role), // Yetki (Host)
            new Claim("Pin", pin)
        };

        var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddHours(2), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// YENİ: Sadece Markdown ayrıştırma isteği için kullanılacak küçük Veri Transfer Objesi
public record MarkdownRequestDto
{
    public string MarkdownText { get; init; } = string.Empty;
}