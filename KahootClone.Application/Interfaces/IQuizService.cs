using KahootClone.Domain.Entities;

namespace KahootClone.Application.Interfaces;

// AŞAMA 4: Arka plan servisinin SignalR üzerinden fırlatacağı veri paketi.
public class GameTickEvent
{
    public string Pin { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public object? Payload { get; set; }
}

public interface IQuizService
{
    // Yeni bir oyun oluşturulur ve geriye PIN kodu döndürülür.
    string CreateQuiz(Quiz quiz);
    // PIN koduna göre oyun bilgileri getirilir.
    Quiz? GetQuizByPin(string pin);
    // Oyuncunun verdiği cevap kontrol edilerek puanlaması yapılır. Artik cevap sayilarini ve kazanilan puani da doner.
    (bool IsCorrect, int AnsweredCount, int TotalCount, int PointsEarned) SubmitAnswer(string pin, string nickname, Guid questionId, Guid optionId);
    // Oyun akışını otomatik yönetecek döngüye ekler.
    void StartGameFlow(string pin);
    // Oyunu döngüden çıkarır (Bitirme).
    void StopGameFlow(string pin);
    // Her saniye çağrılır ve oyunların durumunu güncelleyip gerekli SignalR olaylarını döndürür.
    List<GameTickEvent> ProcessTicks();
    // Oyuncunun oyuna ilk kez katılması veya kopup tekrar bağlanması durumunu yönetir.
    (Player? player, string? errorMessage) JoinOrRejoin(string pin, string nickname, string connectionId);
    // Yöneticinin sayfayı yenilemesi durumunda oyunun tam durumunu getirir.
    object? GetFullGameState(string pin);
    // Bağlantısı kopan oyuncuyu kayıttan düşürür ve bilgi döndürür.
    (string? Pin, string? Nickname) UnregisterPlayer(string connectionId);
    // Oyun lobideyken yönetici tarafından iptal edilir.
    void AbandonQuiz(string pin);
}
