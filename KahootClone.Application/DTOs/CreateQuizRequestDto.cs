using System.ComponentModel.DataAnnotations;

namespace KahootClone.Application.DTOs;

public class CreateQuizRequestDto
{
    [Required]
    public string Title { get; set; } = string.Empty;

    // YENİ: Dışarıdan gönderilecek sorular listesi
    public List<QuestionDto> Questions { get; set; } = new();
}

public class QuestionDto
{
    public string Text { get; set; } = string.Empty;
    public int TimeLimitInSeconds { get; set; } = 20;
    public List<OptionDto> Options { get; set; } = new();
}

public class OptionDto
{
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}