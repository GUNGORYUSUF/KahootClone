using KahootClone.Domain.Entities;

namespace KahootClone.Application.Interfaces;

public interface IQuizService
{
    // Yeni bir oyun oluşturulur ve geriye PIN kodu döndürülür.
    string CreateQuiz(Quiz quiz);
    // PIN koduna göre oyun bilgileri getirilir.
    Quiz? GetQuizByPin(string pin);
    // Öğrencinin verdiği cevap kontrol edilerek puanlaması yapılır.
    bool SubmitAnswer(string pin, string nickname, Guid questionId, Guid optionId);
    // Sorunun öğrencilere gönderildiği anın zamanı kaydedilir.
    void StartQuestion(string pin);
}
