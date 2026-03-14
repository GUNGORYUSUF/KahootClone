# Kahoot Clone - Gerçek Zamanlı Bilgi Yarışması

Bu proje, yapay zeka destekli yazılım geliştirme dersi kapsamında "Agentic Engineering" yaklaşımları kullanılarak geliştirilen gerçek zamanlı bir bilgi yarışması uygulamasıdır. 

## Kullanılan Teknolojiler
* **Backend:** C# .NET Core Web API
* **Gerçek Zamanlı İletişim:** SignalR (WebSockets)
* **Veritabanı:** MongoDB (Docker Konteyner)
* **Frontend:** HTML5, Bootstrap 5, Vanilla JavaScript
* **Mimari:** Clean Architecture (Temiz Mimari)

## Proje Klasör Yapısı (Temiz Mimari)
Proje, bağımlılıkları en aza indirmek ve sürdürülebilirliği artırmak için 4 ana katmana ayrılmıştır:
* **1. Domain (`KahootClone.Domain`):** Sistemin kalbidir. Oyun, Soru, Oyuncu gibi temel veri şablonları (Entity) burada tutulur. Dış dünyadan tamamen izoledir.
* **2. Application (`KahootClone.Application`):** İş mantığı (Oyun kuralları, PIN üretme vb.) bu katmanda işlenir.
* **3. Infrastructure (`KahootClone.Infrastructure`):** Veritabanı bağlantısı ve fiziksel veri kayıt işlemleri (MongoDB erişimi) burada yapılır.
* **4. Api (`KahootClone.Api`):** Sistemin dışa açılan kapısıdır. İnternet tarayıcısından gelen istekleri karşılar ve ilgili servislere yönlendirir. Görsel arayüzler (wwwroot) bu katmandan sunulur.

## Sistemi Lokal Ortamda Çalıştırma Rehberi

Projeyi bilgisayarınızda ayağa kaldırmak için aşağıdaki adımları sırasıyla uygulayınız:

**Adım 1: Veritabanını Başlatma**
Docker Desktop uygulamasının çalıştığından emin olun. Ana proje dizininde bir terminal açın ve MongoDB'yi arka planda başlatmak için şu komutu girin:
`docker-compose up -d`

**Adım 2: API Motorunu Çalıştırma**
Veritabanı hazır olduktan sonra, .NET uygulamasını ayağa kaldırmak için terminale şu komutu girin:
`dotnet run --project KahootClone.Api`

**Adım 3: Uygulamayı Test Etme (Gerçek Zamanlı Arayüzler)**
Proje ayağa kalktıktan sonra tarayıcınızdan aşağıdaki adreslere giderek sistemi test edebilirsiniz (Port numaranızı terminaldeki çıktıya göre ayarlayınız):
* **Öğretmen Ekranı (Yeni Oyun Kurma):** `http://localhost:5xxx/index.html`
* **Öğrenci Ekranı (Oyuna Katılma):** `http://localhost:5xxx/student.html`
* **Geliştirici Test Arayüzü (Swagger):** `http://localhost:5xxx/swagger`