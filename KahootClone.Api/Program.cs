using KahootClone.Application.Interfaces;
using KahootClone.Application.Services;
using KahootClone.Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using KahootClone.Api.Services;

var builder = WebApplication.CreateBuilder(args);
// MongoDB'nin Guid (Benzersiz Kimlik) veri tipini standart formatta kaydetmesi sağlanır.
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

// Controller (API Uç Noktaları) yapısı sisteme dahil edilir.
builder.Services.AddControllers();
// Gerçek zamanlı iletişim servisi (SignalR) sisteme dahil edilir.
builder.Services.AddSignalR();

// Swagger (Test Arayüzü) sisteme eklenir.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Veritabanı ayarları appsettings.json dosyasından okunarak sisteme tanıtılır.
var mongoConnectionString = builder.Configuration.GetSection("MongoDbSettings:ConnectionString").Value;
var mongoDatabaseName = builder.Configuration.GetSection("MongoDbSettings:DatabaseName").Value;

// MongoDB Context (Bağlantı Merkezi) sisteme tekil (Singleton) olarak kaydedilir.
builder.Services.AddSingleton<MongoDbContext>(sp => 
    new MongoDbContext(mongoConnectionString!, mongoDatabaseName!));

// İş mantığı servislerimiz (QuizService) sisteme tanımlanır.
builder.Services.AddScoped<IQuizService, QuizService>();
// Veritabanı kasa işlemleri (Repository) sisteme tanımlanır.
builder.Services.AddScoped<IQuizRepository, KahootClone.Infrastructure.Repositories.QuizRepository>();
builder.Services.AddHostedService<GameFlowService>();
var app = builder.Build();

// Geliştirme ortamında test arayüzü (Swagger) aktif edilir.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
// Statik dosyaların (HTML, CSS, JS) dışarıya sunulması sağlanır.
app.UseStaticFiles();
app.MapControllers();
// İstemcilerin bağlanacağı telsiz kulesinin (Hub) adresi belirlenir.
app.MapHub<KahootClone.Api.Hubs.GameHub>("/gamehub");
app.Run();