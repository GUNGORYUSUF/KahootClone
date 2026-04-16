# Kahoot Clone - Gerçek Zamanlı Bilgi Yarışması

Bu proje, yapay zeka destekli yazılım geliştirme dersi kapsamında "Agentic Engineering" yaklaşımları kullanılarak geliştirilen, yüksek performanslı ve gerçek zamanlı bir bilgi yarışması uygulamasıdır. Temiz Mimari (Clean Architecture) prensiplerine sadık kalınarak, tamamen kesintisiz bir kullanıcı deneyimi hedeflenmiştir.

## Kullanılan Teknolojiler
* **Backend:** C# .NET Core Web API
* **Gerçek Zamanlı İletişim:** SignalR (WebSockets)
* **Veritabanı:** MongoDB (Docker Konteyner)
* **Frontend:** HTML5, Bootstrap 5, Vanilla JavaScript
* **Mimari:** Clean Architecture (Temiz Mimari)

## Temel Özellikler ve Oyun Mekanikleri
* **Gerçek Zamanlı Senkronizasyon:** Yönetici ve oyuncu ekranlarındaki süreler milisaniyelik hassasiyetle aynı anda geriye sayar.
* **Tam Otomatik Oyun Akışı:** Yönetici oyunu başlattıktan sonra sistem; soruları, süreleri ve 5 saniyelik geçiş aralarını insan müdahalesi olmadan otomatik yönetir.
* **Heyecan Mekanizması (Suspense):** Oyuncular cevap verdiğinde anında sonucu görmek yerine, bekleme odasına alınır ve süre bittiğinde tüm sınıf sonucu aynı anda öğrenir.
* **Çift Taraflı Liderlik Tablosu:** Oyun bittiğinde sadece yönetici ekranında değil, her oyuncunun kendi cihazında da liderlik tablosu belirir ve oyuncunun kendi ismi yeşil renkle vurgulanır.
* **Hile Koruması:** Doğru cevap verisi oyunculara gönderilmez, doğrulama işlemi sunucunun (Backend) kalbinde güvenle yapılır.

## Proje Klasör Yapısı
* **1. Domain:** Sistemin kalbidir. Oyun, Soru, Oyuncu gibi temel veri şablonları burada tutulur. Dış dünyadan tamamen izoledir.
* **2. Application:** İş mantığı, puanlama ve oyun akış kuralları bu katmanda işlenir.
* **3. Infrastructure:** Veritabanı bağlantısı ve fiziksel veri kayıt işlemleri (MongoDB erişimi) burada yapılır.
* **4. Api:** Sistemin dışa açılan kapısıdır. Görsel arayüzler (wwwroot) ve SignalR kulesi bu katmandan yönetilir.

## Sistemi Lokal Ortamda Çalıştırma Rehberi

**Adım 1: Veritabanını Başlatma**
Docker Desktop uygulamasının çalıştığından emin olun. Ana proje dizininde bir terminal açın ve MongoDB'yi arka planda başlatmak için şu komutu girin:
`docker-compose up -d`

**Adım 2: API Motorunu Çalıştırma**
Veritabanı hazır olduktan sonra, .NET uygulamasını ayağa kaldırmak için terminale şu komutu girin:
`dotnet run --project KahootClone.Api`

**Adım 3: Uygulamayı Test Etme**
Tarayıcınızdan aşağıdaki adreslere giderek sistemi test edebilirsiniz (Port numaranızı terminaldeki çıktıya göre ayarlayınız):
* **Yönetici Ekranı:** `http://localhost:5xxx/index.html`
* **Oyuncu Ekranı:** `http://localhost:5xxx/student.html`
* **Geliştirici Arayüzü (Swagger):** `http://localhost:5xxx/swagger`