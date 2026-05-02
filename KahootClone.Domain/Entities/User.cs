namespace KahootClone.Domain.Entities;

public class User
{
    // Sistemin içindeki benzersiz kimliği
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Google üzerinden gelen kalıcı, benzersiz kimlik
    public string GoogleId { get; set; } = string.Empty;
    
    public string Email { get; set; } = string.Empty;
    
    public string Nickname { get; set; } = string.Empty;
    
    public string? AvatarUrl { get; set; }
    
    // Rol tabanlı yetkilendirme için: "Host" veya "Player"
    public string Role { get; set; } = "Player";
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}