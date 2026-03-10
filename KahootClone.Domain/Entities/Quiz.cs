namespace KahootClone.Domain.Entities;

public class Quiz
{
    // Oyunun benzersiz kimliği tutulur.
    public Guid Id { get; set; }

    // Oyunun başlığı saklanır (Örn: "Genel Kültür Testi").
    public string Title { get; set; } = string.Empty;

    // Öğrencilerin bağlanması için gereken 6 haneli PIN kodu tutulur.
    public string Pin { get; set; } = string.Empty;

    // Oyunun aktif olup olmadığı durumu kontrol edilir.
    public bool IsActive { get; set; }

    // Oyuna ait soruların listesi barındırılır.
    public List<Question> Questions { get; set; } = new();

    // Oyuna katılan oyuncuların listesi barındırılır.
    public List<Player> Players { get; set; } = new();
}