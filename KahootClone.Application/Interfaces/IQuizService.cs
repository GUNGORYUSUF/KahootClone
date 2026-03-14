using KahootClone.Domain.Entities;

namespace KahootClone.Application.Interfaces;

public interface IQuizService
{
    // Yeni bir oyun oluşturulur ve geriye PIN kodu döndürülür.
    string CreateQuiz(Quiz quiz);
}