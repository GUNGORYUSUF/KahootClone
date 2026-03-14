namespace KahootClone.Domain.Entities;

public class Question
{
    // Sorunun benzersiz kimliği tutulur.
    public Guid Id { get; set; }

    // Sorunun metni saklanır.
    public string Text { get; set; } = string.Empty;

    // Soru için verilen cevaplama süresi saniye cinsinden tutulur.
    public int TimeLimitInSeconds { get; set; }

    // Soruya ait cevap şıkları barındırılır.
    public List<Option> Options { get; set; } = new();
}