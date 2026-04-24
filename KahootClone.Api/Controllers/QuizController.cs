using Microsoft.AspNetCore.Mvc;
using KahootClone.Domain.Entities;
using KahootClone.Application.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

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
}