# Kahoot Clone - Dağıtık (Distributed) Gerçek Zamanlı Bilgi Yarışması

Bu proje, yapay zeka destekli yazılım geliştirme ("Agentic Engineering") yaklaşımları kullanılarak geliştirilen, kurumsal seviyede (Enterprise) ölçeklenebilir, yüksek performanslı ve gerçek zamanlı bir bilgi yarışması uygulamasıdır. Temiz Mimari (Clean Architecture), SOLID prensipleri ve Dağıtık Sistem (Distributed System) standartlarına sadık kalınarak tamamen kesintisiz ve güvenli bir kullanıcı deneyimi hedeflenmiştir.

## Kullanılan Teknolojiler
* **Backend:** C# .NET 10.0 Web API (Record ve Immutability patternleri)
* **Gerçek Zamanlı İletişim:** SignalR (WebSockets) + **Redis Backplane**
* **Durum (State) Yönetimi:** Dağıtık Önbellek olarak **Redis** (SETNX Dağıtık Kilit Mimarisi)
* **Veritabanı:** MongoDB (Docker)
* **Güvenlik:** JWT (JSON Web Token) Kimlik Doğrulama ve DTO Doğrulamaları
* **Frontend:** HTML5, Bootstrap 5, Vanilla JavaScript
* **Mimari:** Clean Architecture, Single Responsibility, Yatay Ölçeklenebilirlik, **Tam Docker İzolasyonu**

## Temel Özellikler ve Oyun Mekanikleri
* **Dağıtık Mimari ve Redis Backplane:** Sistem tek bir sunucuya (RAM) bağlı değildir. Redis Backplane sayesinde uygulama onlarca farklı sunucuda (instance) aynı anda çalıştırılsa bile, farklı sunuculardaki oyuncular birbiriyle eşzamanlı olarak aynı oyunu oynayabilir.
* **Güvenlik (JWT & Hile Koruması):** Yalnızca oyunu kuran yöneticiye özel bir "Host Token" üretilir. Oyuncuların (öğrencilerin) geliştirici konsolundan (F12) sahte komutlar göndererek oyunu sabote etmesi engellenmiştir.
* **Dağıtık Kilit (Distributed Lock):** Birden fazla sunucu çalıştığında, oyun döngüsünün (Tick) çakışmasını engellemek için Redis `SETNX` kullanılarak saniyelik görev kilitleri oluşturulmuştur.
* **Tam Otomatik Oyun Akışı:** Yönetici oyunu başlattıktan sonra sistem; soruları, süreleri ve geçiş aralarını insan müdahalesi olmadan arka plan servisleri (HostedService) ile otomatik yönetir.
* **Heyecan Mekanizması (Suspense):** Oyuncular cevap verdiğinde anında sonucu görmek yerine, bekleme odasına alınır ve süre bittiğinde tüm oyuncular sonucu aynı anda öğrenir.
* **Çift Taraflı Liderlik Tablosu:** Oyun bittiğinde sadece yönetici ekranında değil, her oyuncunun kendi cihazında da liderlik tablosu belirir ve oyuncunun kendi ismi yeşil renkle vurgulanır.

## Proje Klasör Yapısı
* **1. Domain:** Sistemin kalbidir. Oyun, Soru, Oyuncu gibi temel veri şablonları burada tutulur. Dış dünyadan tamamen izoledir.
* **2. Application:** İş mantığı, puanlama ve oyun akış kuralları bu katmanda işlenir.
* **3. Infrastructure:** Veritabanı bağlantısı ve fiziksel veri kayıt işlemleri (MongoDB erişimi) burada yapılır.
* **4. Api:** Sistemin dışa açılan kapısıdır. Görsel arayüzler (wwwroot) ve SignalR kulesi bu katmandan yönetilir.

## 🚀 Kurulum ve Çalıştırma Rehberi

### Gereksinimler
* Sadece **Docker Desktop** (Başka hiçbir kuruluma gerek yoktur!)

### Adım 1: Sistemi Tek Tuşla Ayağa Kaldırma (Magic Command)
Proje tamamen Container (Konteyner) mimarisine uygun tasarlanmıştır. Veritabanı, Redis ve API sunucusunu kendi aralarında ağ kurarak otomatik başlatmak için ana dizinde şu komutu çalıştırmanız yeterlidir:
```bash
docker-compose up --build -d
```

### Adım 2: Sunucuyu (API) Başlatma
Altyapı hazır olduktan sonra, .NET uygulamasını derleyip çalıştırmak için terminale şu komutu girin:
```bash
`dotnet run --project KahootClone.Api`
```

### Adım 3: Uygulamayı Test Etme
Tarayıcınızdan aşağıdaki adreslere giderek sistemi test edebilirsiniz (Port numaranızı terminaldeki çıktıya göre ayarlayınız):
* **Yönetici Ekranı:** `http://localhost:5xxx/index.html`
* **Oyuncu Ekranı:** `http://localhost:5xxx/student.html`
* **Geliştirici Arayüzü (Swagger):** `http://localhost:5xxx/swagger`

### 🛠️ Geliştirici Rehberi: Komutlar ve Docker Yönetimi (Cheat Sheet)
Projeyi bilgisayarına indiren bir geliştiricinin arka planda sistemi yönetmek için ihtiyaç duyacağı tüm temel komutlar ve senaryolar aşağıda özetlenmiştir:

**1. Tüm Sistemi Sıfırdan Ayağa Kaldırma:**
```bash
docker-compose up --build -d
```

**2. Sistemi Tamamen Durdurma ve Kapatma:**
```bash
docker-compose down
```

**3. Kod Değişikliği Sonrası Hızlı Güncelleme (Sadece API):**
C# kodlarında bir değişiklik yaptığınızda tüm sistemi kapatmanıza gerek yoktur. Sadece API'yi yeniden derleyip başlatmak için:
```bash
docker-compose up --build -d api
```
*(Bu komut veritabanı ve Redis'e dokunmadan, sadece API imajınızı saniyeler içinde yenileyip ayağa kaldırır).*

**Arka Planda Çalışan API'nin Loglarını (Hatalarını) İzlemek İçin:**
```bash
docker logs -f kahoot_api
```

---

## 🔥 Gelişmiş Test: Dağıtık Sistemi (Redis Backplane) Simüle Etme
Sistemin tek bir bilgisayarda (RAM) değil, yatayda ölçeklenmiş (Horizontal Scaling) bir ağda nasıl kusursuz çalıştığını görmek isterseniz, uygulamayı iki farklı portta çalıştırarak test edebilirsiniz:

1. Terminalde şu komutu çalıştırarak API sunucunuzun sayısını anında **2'ye çıkarın (Scale)**:
   ```bash
   docker-compose up --scale api=2 -d
   ```
2. Tarayıcıda `http://localhost:5252/index.html` adresinden (Sunucu A üzerinden) bir oyun kurun.
3. Farklı bir sekmede `http://localhost:5253/student.html` adresine giderek (Sunucu B üzerinden) ürettiğiniz PIN ile oyuna katılın.

**Sonuç:** Öğrenci tamamen farklı bir sunucuya bağlı olmasına rağmen, Redis Backplane sayesinde yönetici ekranına anında düşecek ve oyun milisaniyelik gecikme olmadan iki farklı sunucu arasında senkronize akacaktır!

---

## 🎮 Kullanıcı Rehberi (Nasıl Oynanır?)

### 🏁 Oyun Nasıl Başlatılır? (Yönetici)
1. **Yönetici Ekranını Açın:** Tarayıcıda `index.html` sayfasını (Yönetici Ekranı) açın.
2. **Yeni Oyun Kur:** "Yeni Oyun Kur" butonuna basarak sistemi hazırlayın.
3. **PIN Paylaşın:** Ekranda büyük harflerle beliren 6 haneli **PIN kodunu** oyuncularla paylaşın.
4. **Oyuncuları Bekle ve Başlat:** Tüm oyuncular katıldığında "Oyunu Başlat" butonuna tıklayarak yarışı başlatın. Oyun bundan sonra otomatik olarak işleyecektir.

### 🕹️ Oyuna Nasıl Katılınır? (Oyuncu)
1. **Oyuncu Ekranını Açın:** Tarayıcıda `student.html` sayfasını açın.
2. **Giriş Yapın:** Yöneticinin verdiği **PIN kodunu** ve kendinize bir **takma ad (Nickname)** girerek lobiye katılın.
3. **Cevapları İşaretleyin:** Sorular ekranınızda belirir. Doğru cevabı en hızlı işaretleyen oyuncu en çok puanı toplar!

## 📊 SonarQube Kod Analizi ve Test Kapsamı (Coverage)
Projenin kod kalitesini, güvenliğini ve test kapsamını ölçmek için SonarQube entegrasyonu mevcuttur.

### 1. Ön Hazırlık (Sadece İlk Kurulumda)
Sistemin çalışabilmesi için bilgisayarınızda SonarScanner aracının global olarak kurulu olması gerekir. Terminalde şu komutu çalıştırarak kurabilirsiniz:
```bash
dotnet tool install --global dotnet-sonarscanner
```
*(Not: Test projesinin rapor üretebilmesi için gereken `coverlet.collector` paketi projede halihazırda yapılandırılmıştır.)*

### 2. Standart Analiz Döngüsü (Kod Değiştikçe Tekrar Edilir)
Ana proje dizininde (Solution `.sln` dosyasının bulunduğu klasör) terminali açıp sırasıyla aşağıdaki 3 adımı uygulamalısınız:

**Adım A: Dinlemeyi Başlat (Begin)**
Bu komut SonarQube'a analiz sürecinin başladığını ve test raporlarının `**/*.opencover.xml` yolunda bulunacağını bildirir:
```bash
dotnet sonarscanner begin /k:"KahootProjesi" /d:sonar.host.url="http://localhost:9000" /d:sonar.login="sqp_***" /d:sonar.cs.opencover.reportsPaths="**/*.opencover.xml"
```

**Adım B: Derle ve Test Et (Build & Test)**
Önce proje derlenir (Derleme sırasında SonarScanner arka planda kod kokularını ve hataları inceler). Ardından test komutu çalışarak tüm Unit Test'leri koşar ve hangi satırların test edildiğini (`opencover.xml` formatında) hesaplar:
```bash
dotnet build
dotnet test --collect:"XPlat Code Coverage;Format=opencover"
```

**Adım C: Raporu Gönder (End)**
Tüm analizleri ve test kapsama dosyalarını paketleyip `localhost:9000` adresindeki SonarQube sunucusuna gönderir:
```bash
dotnet sonarscanner end /d:sonar.login="sqp_***"
```