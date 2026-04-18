using KahootClone.Application.Interfaces;
using KahootClone.Application.Services;
using KahootClone.Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using KahootClone.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
// MongoDB'nin Guid (Benzersiz Kimlik) veri tipini standart formatta kaydetmesi sağlanır.
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

// Controller (API Uç Noktaları) yapısı sisteme dahil edilir.
builder.Services.AddControllers();
// Gerçek zamanlı iletişim servisi (SignalR) sisteme dahil edilir.
// AŞAMA 2: SignalR hatalarını yakalamak için GlobalHubFilter eklendi.
// AŞAMA 6: Redis Backplane için bağlantı dizesi (Connection String) yukarı taşındı.
var redisConnectionString = builder.Configuration.GetSection("Redis:ConnectionString").Value ?? "localhost:6379";

builder.Services.AddSignalR(options =>
{
    options.AddFilter<KahootClone.Api.Hubs.Filters.GlobalHubFilter>();
}).AddStackExchangeRedis(redisConnectionString, options => {
    options.Configuration.ChannelPrefix = RedisChannel.Literal("KahootCloneApp"); // İsteğe bağlı: Redis içindeki mesajları diğer uygulamalardan ayırmak için ön ek
});

// Swagger (Test Arayüzü) sisteme eklenir.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// AŞAMA 5: JWT Kimlik Doğrulama (Authentication) Servisleri
// Geliştirme ortamı için geçici bir gizli anahtar (Secret Key) belirlenir.
var jwtKey = builder.Configuration["Jwt:Key"] ?? "KahootCloneSuperSecretKey_1234567890123456";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // SignalR (WebSockets) için token'ı URL QueryString üzerinden alma ayarı
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/gamehub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

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

// AŞAMA 6: Redis Bağlantısı ve Dağıtık Durum (State) Yönetimi sisteme dahil edilir.
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddSingleton<IGameStateRepository, KahootClone.Infrastructure.Repositories.RedisGameStateRepository>();

builder.Services.AddHostedService<GameFlowService>();
var app = builder.Build();

// AŞAMA 2: Tüm API isteklerindeki beklenmeyen hataları yakalamak için Global Exception Middleware eklendi.
app.UseMiddleware<KahootClone.Api.Middlewares.GlobalExceptionMiddleware>();

// Geliştirme ortamında test arayüzü (Swagger) aktif edilir.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
// Statik dosyaların (HTML, CSS, JS) dışarıya sunulması sağlanır.
app.UseStaticFiles();

// AŞAMA 5: Kimlik Doğrulama ve Yetkilendirme kalkanları aktif edilir.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// İstemcilerin bağlanacağı telsiz kulesinin (Hub) adresi belirlenir.
app.MapHub<KahootClone.Api.Hubs.GameHub>("/gamehub");
app.Run();