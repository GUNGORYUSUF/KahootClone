using Microsoft.AspNetCore.SignalR;

namespace KahootClone.Api.Hubs;

// İstemciler (tarayıcılar) ile sunucu arasındaki gerçek zamanlı iletişim yönetilir.
public class GameHub : Hub
{
    // Bir oyuncu oyuna (PIN koduna göre oluşturulan odaya) katıldığında bu metot tetiklenir.
    public async Task JoinGame(string pin, string nickname)
    {
        // Oyuncu, PIN koduna ait özel gruba (odaya) dahil edilir.
        await Groups.AddToGroupAsync(Context.ConnectionId, pin);

        // Odadaki diğer kişilere (öğretmenin ekranına) yeni bir oyuncunun katıldığı bilgisi anlık olarak gönderilir.
        await Clients.Group(pin).SendAsync("PlayerJoined", nickname);
    }
}