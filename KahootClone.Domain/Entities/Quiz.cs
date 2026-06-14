namespace KahootClone.Domain.Entities;
using System.Text.Json.Serialization;

public class Quiz
{
    // Oyunun benzersiz kimliği tutulur.
    public Guid Id { get; set; }

    // Oyunun başlığı saklanır (Örn: "Genel Kültür Testi").
    public string Title { get; set; } = string.Empty;

    // Öğrencilerin bağlanması için gereken 6 haneli PIN kodu tutulur.
    [JsonInclude]
    public string Pin { get; private set; } = string.Empty;

    // Oyunun aktif olup olmadığı durumu kontrol edilir.
    [JsonInclude]
    public bool IsActive { get; private set; }

    // Oyuna ait soruların listesi barındırılır.
    public List<Question> Questions { get; set; } = new();

    // Oyuna katılan oyuncuların listesi barındırılır.
    [JsonInclude]
    public List<Player> Players { get; private set; } = new();

    // Hızlı cevap puanlaması için mevcut sorunun başlama zamanı tutulur.
    [JsonInclude]
    public DateTime CurrentQuestionStartTime { get; private set; }

    // YENİ: Oyunun Google girişi gerektirip gerektirmediğini tutan özellik
    public bool RequireGoogleAuth { get; set; } = false;

    // YENİ: Oyunu oluşturan kişinin (Yöneticinin) kalıcı ID'si
    public string? CreatorId { get; set; }

    // YENİ: Oyunun henüz canlıya alınmamış bir taslak olup olmadığını belirtir
    public bool IsDraft { get; set; } = false;

    // DAĞITIK SİSTEM KORUMASI (OCC): İki sunucunun aynı anda veritabanına yazarak birbirini ezmesini (Lost Update) engeller.
    public int Version { get; set; }

    // DDD Kapsülleme (Encapsulation) - İş Kuralları (Business Logic)
    public void Activate(string pin)
    {
        Pin = pin;
        IsActive = true;
        IsDraft = false;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    // YENİ: Soru bankasına kaydedilen taslak (Draft) oyunlar için durum belirleyici
    public void SetAsDraft(string pin)
    {
        Pin = pin;
        IsActive = false;
        IsDraft = true;
    }

    public void MarkQuestionStartTime(DateTime startTime)
    {
        CurrentQuestionStartTime = startTime;
    }

    public void AddPlayer(Player player)
    {
        if (!Players.Any(p => p.Nickname == player.Nickname))
        {
            Players.Add(player);
        }
    }

    public void RemovePlayer(Player player)
    {
        Players.Remove(player);
    }
}