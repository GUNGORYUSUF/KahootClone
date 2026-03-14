using Microsoft.AspNetCore.Mvc;
using KahootClone.Application.Interfaces;
using KahootClone.Domain.Entities;

namespace KahootClone.Api.Controllers;

// Dışarıdan gelen web isteklerinin bu sınıfa yönlendirilmesi sağlanır.
[ApiController]
[Route("api/[controller]")]
public class QuizController : ControllerBase
{
    private readonly IQuizService _quizService;

    // İş mantığı servisi (QuizService) sisteme enjekte edilir.
    public QuizController(IQuizService quizService)
    {
        _quizService = quizService;
    }

    // Yeni bir oyun oluşturmak için POST isteği karşılanır.
    [HttpPost("create")]
    public IActionResult CreateQuiz([FromBody] Quiz quiz)
    {
        // Gelen oyun bilgileriyle PIN üretme işlemi tetiklenir.
        string pin = _quizService.CreateQuiz(quiz);

        // Üretilen PIN kodu ve başarı mesajı istemciye (tarayıcıya) döndürülür.
        return Ok(new { Pin = pin, Message = "Oyun başarıyla oluşturuldu." });
    }
}