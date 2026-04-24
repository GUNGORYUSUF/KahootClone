using System.Text.Json.Serialization;
using KahootClone.Domain.Entities;

namespace KahootClone.Application.Interfaces;

// AŞAMA 1: Magic string'leri (sihirli metinleri) engellemek için durumlar (Phase) tip güvenli hale getirildi.
// JsonConverter sayesinde frontend'e (JavaScript) yine "Question", "Transition" şeklinde metin olarak gidecektir.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GamePhase
{
    Question,
    Transition,
    Ended
}

// AŞAMA 4: Arka plan servisinin SignalR üzerinden fırlatacağı veri paketi.
public record GameTickEvent
{
    public string Pin { get; init; } = string.Empty;
    public string EventName { get; init; } = string.Empty;
    public object? Payload { get; init; }
}

public interface IQuizService
{
    // Yeni bir oyun oluşturulur ve geriye PIN kodu döndürülür.
    string CreateQuiz(Quiz quiz);
    // PIN koduna göre oyun bilgileri getirilir.
    Quiz? GetQuizByPin(string pin);
    // Oyuncunun verdiği cevap kontrol edilerek puanlaması yapılır. Artik cevap sayilarini ve kazanilan puani da doner.
    Task<(bool IsCorrect, int AnsweredCount, int TotalCount, int PointsEarned)> SubmitAnswerAsync(string pin, string nickname, Guid questionId, Guid optionId);
    // Oyun akışını otomatik yönetecek döngüye ekler.
    Task StartGameFlowAsync(string pin);
    // Oyunu döngüden çıkarır (Bitirme).
    void StopGameFlow(string pin);
    // Her saniye çağrılır ve oyunların durumunu güncelleyip gerekli SignalR olaylarını döndürür.
    Task<List<GameTickEvent>> ProcessTicksAsync();
    // Oyuncunun oyuna ilk kez katılması veya kopup tekrar bağlanması durumunu yönetir.
    Task<(Player? player, string? errorMessage, string? sessionToken)> JoinOrRejoinAsync(string pin, string nickname, string connectionId, string? sessionToken = null);
    // Yöneticinin sayfayı yenilemesi durumunda oyunun tam durumunu getirir.
    object? GetFullGameState(string pin);
    // Bağlantısı kopan oyuncuyu kayıttan düşürür ve bilgi döndürür.
    Task<(string? Pin, string? Nickname)> UnregisterPlayerAsync(string connectionId);
    // Oyun lobideyken yönetici tarafından iptal edilir.
    Task AbandonQuizAsync(string pin);
}
