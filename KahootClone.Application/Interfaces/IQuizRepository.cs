using KahootClone.Domain.Entities;

namespace KahootClone.Application.Interfaces;

public interface IQuizRepository
{
    void Add(Quiz quiz);
    
    // Verilen PIN koduna ait oyun veritabanından getirilir.
    Quiz? GetByPin(string pin);

    // YENİ: Yöneticinin kendi oluşturduğu oyunları (taslaklar dahil) getirir
    List<Quiz> GetByCreatorId(string creatorId);

    // Veritabanındaki mevcut oyun verisi güncellenir.
    void Update(Quiz quiz);

    // YENİ: İstenmeyen veya eski oyunu veritabanından kalıcı olarak siler
    void Delete(string pin);
}