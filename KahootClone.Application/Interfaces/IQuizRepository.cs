using KahootClone.Domain.Entities;

namespace KahootClone.Application.Interfaces;

public interface IQuizRepository
{
    void Add(Quiz quiz);
    
    // Verilen PIN koduna ait oyun veritabanından getirilir.
    Quiz? GetByPin(string pin);

    // Veritabanındaki mevcut oyun verisi güncellenir.
    void Update(Quiz quiz);
}