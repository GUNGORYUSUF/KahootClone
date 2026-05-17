using Microsoft.AspNetCore.Mvc;
using KahootClone.Domain.Entities;
using KahootClone.Application.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using KahootClone.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;

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
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult CreateQuiz([FromBody] Quiz quiz)
    {
        // YENİ: Yönetici sisteme giriş yapmışsa, ID'sini oyuna "Kurucu" olarak kaydet
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!string.IsNullOrEmpty(userId))
        {
            quiz.CreatorId = userId;
        }

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

    // YENİ: Giriş yapmış yöneticinin kendi kurduğu geçmiş oyunları getirir
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpGet("my-quizzes")]
    [ProducesResponseType(typeof(IEnumerable<Quiz>), StatusCodes.Status200OK)]
    public IActionResult GetMyQuizzes()
    {
        // JWT Token'dan kullanıcı ID'sini güvenli bir şekilde çekiyoruz
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                     ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        
        if (string.IsNullOrEmpty(userId))
            return Unauthorized("Oturum süresi dolmuş veya geçersiz.");

        var myQuizzes = _quizService.GetQuizzesByCreatorId(userId);
        return Ok(myQuizzes);
    }

    // YENİ: Giriş yapmış yöneticinin kendi oyununu tamamen silmesi
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpDelete("{pin}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult DeleteQuiz(string pin)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var quiz = _quizService.GetQuizByPin(pin);
        
        if (quiz == null) return NotFound("Oyun bulunamadı.");
        if (quiz.CreatorId != userIdStr) return Forbid(); // Sadece oluşturan kişi silebilir
        
        _quizService.DeleteQuiz(pin);
        return Ok(new { message = "Oyun başarıyla silindi." });
    }

    [HttpPost("parse-markdown")]
    [ProducesResponseType(typeof(IEnumerable<Question>), StatusCodes.Status200OK)]
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