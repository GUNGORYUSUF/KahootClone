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
using System.Threading.RateLimiting;
using Serilog;
using OpenTelemetry.Metrics;

// YENİ: Serilog yapılandırması - Logları konsola ve Docker içindeki Seq sunucusuna yollar
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Seq("http://seq:5341") // Seq Docker konteyneri iç ağda 5341 portundan dinler
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog(); // Varsayılan loglayıcı olarak Serilog'u kullan

// MongoDB'nin Guid (Benzersiz Kimlik) veri tipini standart formatta kaydetmesi sağlanır.
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

// Controller (API Uç Noktaları) yapısı sisteme dahil edilir.
builder.Services.AddControllers();

// YENİ: Frontend (tarayıcı) üzerinden API'ye ve SignalR Hub'ına engelsiz erişim için CORS politikası eklendi.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // Tüm kaynaklara (file://, localhost vs) izin ver
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // SignalR (WebSockets) kimlik doğrulaması için zorunludur.
    });
});

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

// YENİ: Sistem Sağlık Kontrolleri (Health Checks) MongoDB ve Redis için yapılandırılır
builder.Services.AddHealthChecks()
    .AddMongoDb(mongoConnectionString ?? "mongodb://localhost:27017", name: "MongoDB")
    .AddRedis(redisConnectionString, name: "Redis");

// YENİ: RabbitMQ Mesajlaşma Servisi (Publisher) ve Arka Plan Tüketicisi (Consumer) eklendi
var rabbitMqHost = builder.Configuration["RabbitMq:HostName"] ?? "localhost";
builder.Services.AddSingleton<IMessagePublisher>(new KahootClone.Infrastructure.Messaging.RabbitMqPublisher(rabbitMqHost));
builder.Services.AddHostedService<GameEndedConsumerService>();

// YENİ: Rate Limiting (DDoS Koruması)
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Nginx arkasında olduğumuz için gerçek IP'yi X-Real-IP başlığından alıyoruz
        var ip = context.Request.Headers["X-Real-IP"].FirstOrDefault() ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10, // Her IP için saniyede maksimum 10 istek izni verilir
            Window = TimeSpan.FromSeconds(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0 // Sınırı aşan istekler kuyruğa alınmaz, anında reddedilir
        });
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests; // 429 Too Many Requests hatası döner
});

// YENİ: OpenTelemetry ve Prometheus Metrikleri Entegrasyonu
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation() // HTTP İstek metrikleri
               .AddRuntimeInstrumentation()    // CPU, RAM, Çöp Toplayıcı (GC) metrikleri
               .AddPrometheusExporter();       // Metrikleri Prometheus formatında dışa aktar
    });

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

// DOCKER ÇÖZÜMÜ: Sadece HTTP (8080) portunu kullandığımız için,
// zorunlu HTTPS yönlendirmesi tarayıcıda bağlantı kopmasına (NetworkError) sebep olur.
// Docker ortamında HTTPS tüneli proxy üzerinden yapıldığından bu yönlendirmeyi kapalı tutuyoruz.
// app.UseHttpsRedirection();

// Statik dosyaların (HTML, CSS, JS) dışarıya sunulması sağlanır.
app.UseStaticFiles();

// CORS izni, Kimlik Doğrulama (Authentication) işlemlerinden hemen önce devreye alınır.
app.UseCors("AllowAll");

// YENİ: Rate Limiter kalkanı aktif edilir.
app.UseRateLimiter();

// AŞAMA 5: Kimlik Doğrulama ve Yetkilendirme kalkanları aktif edilir.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// İstemcilerin bağlanacağı telsiz kulesinin (Hub) adresi belirlenir.
app.MapHub<KahootClone.Api.Hubs.GameHub>("/gamehub");

// YENİ: Sistem sağlık durumunu dışarıya açan uç nokta
app.MapHealthChecks("/health");

app.MapPrometheusScrapingEndpoint(); // Prometheus'un metrikleri çekeceği /metrics uç noktası açılır
app.Run();