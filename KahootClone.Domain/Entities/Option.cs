namespace KahootClone.Domain.Entities;

public class Option
{
    // Şıkkın benzersiz kimliği tutulur.
    public Guid Id { get; set; }

    // Şıkkın metni saklanır (Örn: "Ankara").
    public string Text { get; set; } = string.Empty;

    // Bu şıkkın doğru cevap olup olmadığı durumu tutulur.
    public bool IsCorrect { get; set; }
}