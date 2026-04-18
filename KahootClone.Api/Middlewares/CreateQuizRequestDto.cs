using System.ComponentModel.DataAnnotations;

namespace KahootClone.Application.DTOs;

public record CreateQuizRequestDto
{
    [Required(ErrorMessage = "Oyun başlığı boş olamaz.")]
    [MaxLength(100, ErrorMessage = "Oyun başlığı en fazla 100 karakter olabilir.")]
    public string Title { get; init; } = string.Empty;
}