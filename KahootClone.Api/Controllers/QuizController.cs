using Microsoft.AspNetCore.Mvc;
using KahootClone.Domain.Entities;
using KahootClone.Application.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using KahootClone.Application.DTOs;

namespace KahootClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuizController : ControllerBase
{
    private readonly IQuizService _quizService;
    private readonly IConfiguration _configuration;

    public QuizController(IQuizService quizService, IConfiguration configuration)
    {
        _quizService = quizService;
        _configuration = configuration;
    }

    [HttpPost("create")]
    public IActionResult CreateQuiz([FromBody] Quiz quiz)
    {
        var pin = _quizService.CreateQuiz(quiz);

        // GÜVENLİ YÖNTEM: JWT Anahtarı kod içinden değil, yapılandırmadan okunur.
        var jwtKey = _configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(jwtKey))
        {
            // Program.cs'deki gibi, anahtar yoksa sistem hata verir (Fail-Fast).
            throw new InvalidOperationException("Kritik Güvenlik Hatası: JWT Secret Key (Jwt:Key) yapılandırmalarda bulunamadı!");
        }
        var key = Encoding.UTF8.GetBytes(jwtKey);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, pin), new Claim(ClaimTypes.Role, "Host") }),
            Expires = DateTime.UtcNow.AddHours(8),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return Ok(new { pin, token = tokenHandler.WriteToken(token) });
    }

    [HttpPost("parse-markdown")]
    public IActionResult ParseMarkdown([FromBody] MarkdownRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.MarkdownText))
        {
            return BadRequest("Markdown metni boş olamaz.");
        }

        var questions = new List<Question>();
        Question? currentQuestion = null;

        var lines = request.MarkdownText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (trimmedLine.StartsWith("#"))
            {
                currentQuestion = new Question
                {
                    Id = Guid.NewGuid(),
                    Text = trimmedLine.Replace("# Soru:", "").Replace("#", "").Trim(),
                    TimeLimitInSeconds = 20, // Varsayılan süre
                    Options = new List<Option>()
                };
                questions.Add(currentQuestion);
            }
            else if (trimmedLine.StartsWith("Süre:", StringComparison.OrdinalIgnoreCase) && currentQuestion != null)
            {
                var timePart = trimmedLine.Substring(5).Trim();
                if (int.TryParse(timePart, out int timeLimit))
                {
                    currentQuestion.TimeLimitInSeconds = timeLimit;
                }
            }
            else if ((trimmedLine.StartsWith("-") || trimmedLine.StartsWith("*")) && currentQuestion != null)
            {
                bool isCorrect = trimmedLine.EndsWith("(*)");
                string optionText = trimmedLine.Substring(1).Replace("(*)", "").Trim();

                currentQuestion.Options.Add(new Option
                {
                    Id = Guid.NewGuid(),
                    Text = optionText,
                    IsCorrect = isCorrect
                });
            }
        }

        // --- DOĞRULAMA (VALIDATION) KONTROLLERİ ---
        if (questions.Count == 0)
        {
            return BadRequest(new { message = "Geçerli bir soru bulunamadı. Lütfen markdown formatını kontrol edin." });
        }

        for (int i = 0; i < questions.Count; i++)
        {
            var q = questions[i];
            if (q.Options.Count < 2)
            {
                return BadRequest(new { message = $"{i + 1}. soru ('{q.Text}') için en az 2 şık belirtmelisiniz." });
            }

            if (!q.Options.Any(o => o.IsCorrect))
            {
                return BadRequest(new { message = $"{i + 1}. soru ('{q.Text}') için doğru cevap işaretlenmemiş. Lütfen doğru şıkkın sonuna (*) ekleyin." });
            }

            if (q.Options.Count(o => o.IsCorrect) > 1)
            {
                return BadRequest(new { message = $"{i + 1}. soru ('{q.Text}') için birden fazla doğru cevap işaretlenmiş. Sadece bir şıkta (*) olmalıdır." });
            }
        }

        return Ok(questions);
    }
}