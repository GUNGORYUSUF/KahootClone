# Kahoot Clone - Gerçek Zamanlı Bilgi Yarışması

Bu proje, yapay zeka destekli yazılım geliştirme dersi kapsamında "Agentic Engineering" yaklaşımları kullanılarak geliştirilen gerçek zamanlı bir bilgi yarışması uygulamasıdır. 

## Kullanılan Teknolojiler
* **Backend:** C# .NET Core Web API
* **Gerçek Zamanlı İletişim:** SignalR (WebSockets)
* **Veritabanı:** MongoDB (Docker Konteyner)
* **Mimari:** Clean Architecture (Temiz Mimari)

## Proje Klasör Yapısı (Temiz Mimari)
Proje, bağımlılıkları en aza indirmek ve sürdürülebilirliği artırmak için 4 ana katmana ayrılmıştır:
* **1. Domain (`KahootClone.Domain`):** Sistemin kalbidir. Oyun, Soru, Oyuncu gibi temel veri şablonları (Entity) burada tutulur. Dış dünyadan tamamen izoledir.
* **2. Application (`KahootClone.Application`):** İş mantığı (Oyun kuralları, PIN üretme vb.) bu katmanda işlenir.
* **3. Infrastructure (`KahootClone.Infrastructure`):** Veritabanı bağlantısı ve fiziksel veri kayıt işlemleri (MongoDB erişimi) burada yapılır.
* **4. Api (`KahootClone.Api`):** Sistemin dışa açılan kapısıdır. İnternet tarayıcısından gelen istekleri karşılar ve ilgili servislere yönlendirir.

## Sistemi Lokal Ortamda Çalıştırma Rehberi

Projeyi bilgisayarınızda ayağa kaldırmak için aşağıdaki adımları sırasıyla uygulayınız:

**Adım 1: Veritabanını Başlatma**
Docker Desktop uygulamasının çalıştığından emin olun. Ana proje dizininde bir terminal açın ve MongoDB'yi arka planda başlatmak için şu komutu girin:
`docker-compose up -d`

**Adım 2: API Motorunu Çalıştırma**
Veritabanı hazır olduktan sonra, .NET uygulamasını ayağa kaldırmak için terminale şu komutu girin:
`dotnet run --project KahootClone.Api`

**Adım 3: Test Arayüzüne (Swagger) Erişim**
Terminalde belirtilen adresi (Örn: `http://localhost:5xxx`) kopyalayın ve tarayıcınızın adres çubuğuna yapıştırın. Adresin sonuna `/swagger` ekleyerek test arayüzüne ulaşabilirsiniz. (Örn: `http://localhost:5245/swagger`). Bu ekran üzerinden "Yeni Oyun Kur" isteği gönderip PIN üretebilirsiniz.