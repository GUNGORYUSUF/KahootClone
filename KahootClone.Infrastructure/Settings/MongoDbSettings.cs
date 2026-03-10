namespace KahootClone.Infrastructure.Settings;

public class MongoDbSettings
{
    // Veritabanı bağlantı adresi dışarıdan (güvenli ortamdan) alınarak burada tutulur.
    public string ConnectionString { get; set; } = string.Empty;

    // İşlem yapılacak veritabanının adı saklanır.
    public string DatabaseName { get; set; } = string.Empty;
}