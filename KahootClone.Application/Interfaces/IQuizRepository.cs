using KahootClone.Domain.Entities;

namespace KahootClone.Application.Interfaces;

public interface IQuizRepository
{
    // Veritabanına yeni oyun ekleme işlemi tanımlanır.
    void Add(Quiz quiz);
}