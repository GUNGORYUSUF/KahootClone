using KahootClone.Domain.Entities;

namespace KahootClone.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id);
    // Giriş yaparken kullanıcının daha önce kayıt olup olmadığını kontrol etmek için
    Task<User?> GetByGoogleIdAsync(string googleId);
    Task CreateAsync(User user);
    Task UpdateAsync(User user);
}