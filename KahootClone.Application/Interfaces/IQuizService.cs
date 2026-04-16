using KahootClone.Domain.Entities;

namespace KahootClone.Application.Interfaces;

public interface IQuizService
{
    // Yeni bir oyun oluşturulur ve geriye PIN kodu döndürülür.
    string CreateQuiz(Quiz quiz);
    // PIN koduna göre oyun bilgileri getirilir.
    Quiz? GetQuizByPin(string pin);
    // Oyuncunun verdiği cevap kontrol edilerek puanlaması yapılır.
    bool SubmitAnswer(string pin, string nickname, Guid questionId, Guid optionId);
    // Sorunun oyunculara gönderildiği anın zamanı kaydedilir.
    void StartQuestion(string pin);
    // Oyuncunun oyuna ilk kez katılması veya kopup tekrar bağlanması durumunu yönetir.
    Player? JoinOrRejoin(string pin, string nickname, string connectionId);
}
