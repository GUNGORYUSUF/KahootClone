using Microsoft.AspNetCore.Mvc;
using KahootClone.Application.Interfaces;
using KahootClone.Domain.Entities;
using Google.Apis.Auth;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace KahootClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IUserRepository userRepository, IConfiguration configuration, IHttpClientFactory httpClientFactory) : ControllerBase
{

    // JSON Model Binding hatalarını (400) önlemek için 'record' yerine standart 'class' kullanıyoruz
    public class GoogleLoginRequest
    {
        public string Credential { get; set; } = string.Empty;
    }
    public class UpdateProfileRequest
    {
        public string Nickname { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
    }

    // Google'dan dönecek JSON verisini karşılamak için model
    public class GoogleUserInfo
    {
        [JsonPropertyName("sub")]
        public string Sub { get; set; } = string.Empty;
        [JsonPropertyName("email")]
        public string? Email { get; set; }
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("picture")]
        public string? Picture { get; set; }
    }

    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        try
        {
            // 1. Frontend'den gelen Access Token'ı Google UserInfo API'si ile doğrula
            using var httpClient = httpClientFactory.CreateClient();
            var googleApiUrl = configuration["GoogleApi:UserInfoUrl"] ?? "https://www.googleapis.com/oauth2/v3/userinfo";
            var req = new HttpRequestMessage(HttpMethod.Get, googleApiUrl);
            req.Headers.Add("Authorization", $"Bearer {request.Credential}");
            var userInfoResponse = await httpClient.SendAsync(req);
            
            if (!userInfoResponse.IsSuccessStatusCode) 
                return Unauthorized("Geçersiz Google Access Token");

            var userInfo = await userInfoResponse.Content.ReadFromJsonAsync<GoogleUserInfo>();
            if (userInfo == null || string.IsNullOrEmpty(userInfo.Sub))
                return Unauthorized("Google profil bilgileri okunamadı.");

            // 2. Veritabanında bu Google ID'sine sahip bir kullanıcı var mı bak
            var user = await userRepository.GetByGoogleIdAsync(userInfo.Sub);

            // 3. Kullanıcı yoksa (İlk defa giriş yapıyorsa) yeni kayıt oluştur
            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    GoogleId = userInfo.Sub,
                    Email = userInfo.Email ?? "",
                    Nickname = userInfo.Name ?? "Oyuncu",
                    AvatarUrl = userInfo.Picture,
                    Role = "Player", // Gelecekte dileyenleri veritabanından 'Host' yapabilirsiniz
                    CreatedAt = DateTime.UtcNow
                };
                await userRepository.CreateAsync(user);
            }

            // 4. Uygulamamıza (KahootClone) özel JWT Token üret
            var token = GenerateJwtToken(user);
            return Ok(new { Token = token, User = user });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Sunucu hatası: {ex.Message}");
        }
    }

    // Doğrudan JWT Bearer şemasını zorunlu kılıyoruz (401 hatalarını engeller)
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            // .NET Core otomatik claim dönüştürmesi yaptığı için tüm olası ID anahtarlarına bakıyoruz
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                         ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                         ?? User.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type.Contains("nameidentifier"))?.Value;
            
            if (string.IsNullOrEmpty(userIdStr)) 
                return Unauthorized("Geçersiz oturum: Token içinden kullanıcı kimliği okunamadı.");

            var user = await userRepository.GetByIdAsync(userIdStr);
            if (user == null) 
                return NotFound("Kullanıcı veritabanında bulunamadı.");

            // Kullanıcı bilgilerini güncelle
            user.Nickname = request.Nickname;
            user.AvatarUrl = request.AvatarUrl;
            await userRepository.UpdateAsync(user);

            // Değişen bilgilerle (Claim) yeni bir Token üret
            var token = GenerateJwtToken(user);
            return Ok(new { Token = token, User = user });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Sunucu hatası: {ex.Message}");
        }
    }

    private string GenerateJwtToken(User user)
    {
        // Uygulamanın .env veya appsettings.json dosyasındaki JWT şifresini al
        var keyString = configuration["Jwt:Key"] ?? "KahootCloneDefaultKey_For_Dev_12345!";
        var key = Encoding.UTF8.GetBytes(keyString);

        // Token içine hangi bilgileri gömeceğimizi (Claim) belirliyoruz
        var claims = new List<Claim>
        {
            // ID kaybolma ihtimaline karşı Token içerisine hem 'sub' hem de 'NameIdentifier' olarak iki kez gömüyoruz
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Nickname),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("AvatarUrl", user.AvatarUrl ?? "")
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(7), // Token 7 gün geçerli olacak
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}