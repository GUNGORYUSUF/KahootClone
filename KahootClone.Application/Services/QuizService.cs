using KahootClone.Domain.Entities;
using KahootClone.Application.Interfaces;

namespace KahootClone.Application.Services;

public class QuizService : IQuizService
{
    private readonly IQuizRepository _quizRepository;

    // Kasa arayüzü (Repository) sisteme enjekte edilir.
    public QuizService(IQuizRepository quizRepository)
    {
        _quizRepository = quizRepository;
    }

    public string CreateQuiz(Quiz quiz)
    {
        Random random = new Random();
        string pin = random.Next(100000, 999999).ToString();
        
        quiz.Pin = pin;
        quiz.IsActive = true;

        // Oluşturulan oyun bilgileri veritabanına kalıcı olarak gönderilir.
        _quizRepository.Add(quiz);

        return pin;
    }
}