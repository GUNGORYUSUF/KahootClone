namespace KahootClone.Domain.Entities;
using System.Text.Json.Serialization;

public class Player
{
    // Oyuncunun benzersiz kimliği tutulur.
    [JsonInclude]
    public Guid Id { get; private set; }

    // Ekranda görünecek takma ad saklanır.
    [JsonInclude]
    public string Nickname { get; private set; } = string.Empty;

    // Oyuncunun kazandığı toplam puan tutulur.
    [JsonInclude]
    public int Score { get; private set; }

    // SignalR üzerinden anlık iletişim kurmak için bağlantı kimliği saklanır.
    [JsonInclude]
    public string ConnectionId { get; private set; } = string.Empty;

    // Oyuncunun cevapladığı soruların kimlikleri tutulur (Çift cevap engelleme için).
    [JsonInclude]
    public List<Guid> AnsweredQuestionIds { get; private set; } = new List<Guid>();

    // Oyuncunun Google üzerinden gelen veya varsayılan profil resmi (Avatar) URL'si tutulur.
    [JsonInclude]
    public string? AvatarUrl { get; private set; }

    // ORM/NoSQL (MongoDB) de-serilizasyonu için parametresiz yapıcı metot
    public Player() { }

    // DDD Kapsülleme (Encapsulation) - Nesne oluşturulurken kurallar işletilir
    public Player(Guid id, string nickname, string connectionId, string? avatarUrl = null)
    {
        Id = id;
        Nickname = nickname;
        ConnectionId = connectionId;
        AvatarUrl = avatarUrl;
        Score = 0;
        AnsweredQuestionIds = new List<Guid>();
    }

    // İş Kuralları (Business Logic)
    public void AddScore(int points)
    {
        if (points > 0) Score += points;
    }

    public void MarkQuestionAsAnswered(Guid questionId)
    {
        if (!AnsweredQuestionIds.Contains(questionId))
        {
            AnsweredQuestionIds.Add(questionId);
        }
    }

    public void UpdateConnection(string connectionId)
    {
        ConnectionId = connectionId;
    }

    public void Disconnect()
    {
        ConnectionId = string.Empty;
    }

    public void UpdateAvatar(string avatarUrl)
    {
        if (!string.IsNullOrEmpty(avatarUrl)) AvatarUrl = avatarUrl;
    }
}