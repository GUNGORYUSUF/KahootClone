namespace KahootClone.Domain.Entities;

public class Player
{
    // Oyuncunun benzersiz kimliği tutulur.
    public Guid Id { get; set; }

    // Ekranda görünecek takma ad saklanır.
    public string Nickname { get; set; } = string.Empty;

    // Oyuncunun kazandığı toplam puan tutulur.
    public int Score { get; set; }

    // SignalR üzerinden anlık iletişim kurmak için bağlantı kimliği saklanır.
    public string ConnectionId { get; set; } = string.Empty;
}