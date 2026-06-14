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
using System.ComponentModel.DataAnnotations;

namespace KahootClone.Api.Controllers;

// SRP İhlali Çözümü: DTO sınıfları Controller'ın dışına çıkarılarak izole edildi.
// JSON Model Binding hatalarını (400) önlemek için 'record' yerine standart 'class' kullanıyoruz
public class GoogleLoginRequest
{
    public string Credential { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required(ErrorMessage = "Takma ad zorunludur.")]
    [MaxLength(30, ErrorMessage = "Takma ad en fazla 30 karakter olabilir.")]
    public string Nickname { get; set; } = string.Empty;
    [MaxLength(2000, ErrorMessage = "Avatar URL çok uzun.")]
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

[ApiController]
[Route("api/[controller]")]
public class AuthController(IUserRepository userRepository, IConfiguration configuration, IHttpClientFactory httpClientFactory) : ControllerBase
{

    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        try
        {
            // 1. SRP İhlali Çözümü: HTTP Mantığı özel metoda taşındı.
            var userInfo = await VerifyGoogleTokenAsync(request.Credential);
            if (userInfo == null || string.IsNullOrEmpty(userInfo.Sub))
                return Unauthorized("Google profil bilgileri okunamadı.");

            // 2 & 3. SRP İhlali Çözümü: Kullanıcı bulma/oluşturma mantığı özel metoda taşındı.
            var user = await GetOrCreateUserAsync(userInfo);

            // 4. Uygulamamıza (KahootClone) özel JWT Token üret
            var token = GenerateJwtToken(user);
            return Ok(new { Token = token, User = user });
        }
        catch (Exception)
        {
            return StatusCode(500, "Sunucu tarafında beklenmeyen bir hata oluştu. Lütfen sistem yöneticisiyle iletişime geçin.");
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
        catch (Exception)
        {
            return StatusCode(500, "Sunucu tarafında beklenmeyen bir hata oluştu. Lütfen sistem yöneticisiyle iletişime geçin.");
        }
    }

    private string GenerateJwtToken(User user)
    {
        // Uygulamanın .env veya appsettings.json dosyasındaki JWT şifresini al
        var keyString = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Kritik Güvenlik Hatası: JWT Secret Key (Jwt:Key) yapılandırmalarda bulunamadı!");
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
            Expires = DateTime.UtcNow.AddHours(2), // Token 2 saat geçerli olacak (Olası çalınmalara karşı süre kısaltıldı)
            Issuer = configuration["Jwt:Issuer"],
            Audience = configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }

    private async Task<GoogleUserInfo?> VerifyGoogleTokenAsync(string credential)
    {
        using var httpClient = httpClientFactory.CreateClient();
        var googleApiUrl = configuration["GoogleApi:UserInfoUrl"] ?? "https://www.googleapis.com/oauth2/v3/userinfo";
        var req = new HttpRequestMessage(HttpMethod.Get, googleApiUrl);
        req.Headers.Add("Authorization", $"Bearer {credential}");
        var userInfoResponse = await httpClient.SendAsync(req);
        
        if (!userInfoResponse.IsSuccessStatusCode) 
            return null;

        return await userInfoResponse.Content.ReadFromJsonAsync<GoogleUserInfo>();
    }

    private async Task<User> GetOrCreateUserAsync(GoogleUserInfo userInfo)
    {
        var user = await userRepository.GetByGoogleIdAsync(userInfo.Sub);
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
        return user;
    }
}