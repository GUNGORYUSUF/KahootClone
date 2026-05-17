# Kahoot Clone - Dağıtık (Distributed) Gerçek Zamanlı Bilgi Yarışması

Bu proje, yapay zeka destekli yazılım geliştirme ("Agentic Engineering") yaklaşımları kullanılarak geliştirilen, kurumsal seviyede (Enterprise) ölçeklenebilir, yüksek performanslı ve gerçek zamanlı bir bilgi yarışması uygulamasıdır. Temiz Mimari (Clean Architecture), SOLID prensipleri ve Dağıtık Sistem (Distributed System) standartlarına sadık kalınarak tamamen kesintisiz ve güvenli bir kullanıcı deneyimi hedeflenmiştir.

## Kullanılan Teknolojiler
* **Backend:** C# .NET 10.0 Web API (Record ve Immutability patternleri)
* **Gerçek Zamanlı İletişim:** SignalR (WebSockets) + Redis Backplane
* **Durum (State) Yönetimi:** Dağıtık Önbellek olarak Redis (SETNX Dağıtık Kilit Mimarisi)
* **Veritabanı:** MongoDB (Docker)
* **Yük Dengeleyici (Load Balancer):** Nginx (Reverse Proxy, Sticky Sessions / ip_hash)
* **Güvenlik & Dayanıklılık:** JWT Kimlik Doğrulama, .NET Rate Limiting (DDoS Koruması), Health Checks ve Polly (Hata Toleransı / Retry Mekanizması)
* **Asenkron Mesajlaşma:** RabbitMQ (Olay Güdümlü Mimari)
* **İzlenebilirlik (Observability):** OpenTelemetry, Prometheus, Grafana, Seq ve Serilog
* **Frontend:** React 19, TypeScript, Vite, Bootstrap 5, React Router DOM
* **Veri İşleme:** Özel Markdown Ayrıştırıcı (Dışarıdan `.md` / `.txt` Soru Yükleme)
* **Mimari:** Clean Architecture, Single Responsibility, Yatay Ölçeklenebilirlik, Tam Docker İzolasyonu

## Temel Özellikler ve Oyun Mekanikleri
* **Dağıtık Mimari ve Redis Backplane:** Sistem tek bir sunucuya (RAM) bağlı değildir. Redis Backplane sayesinde uygulama onlarca farklı sunucuda (instance) aynı anda çalıştırılsa bile, farklı sunuculardaki oyuncular birbiriyle eşzamanlı olarak aynı oyunu oynayabilir.
* **Güvenlik (JWT & Hile Koruması):** Yalnızca oyunu kuran yöneticiye özel bir "Host Token" üretilir. Oyuncuların (öğrencilerin) geliştirici konsolundan (F12) sahte komutlar göndererek oyunu sabote etmesi engellenmiştir.
* **Dağıtık Kilit (Distributed Lock):** Birden fazla sunucu çalıştığında, oyun döngüsünün (Tick) çakışmasını engellemek için Redis `SETNX` kullanılarak saniyelik görev kilitleri oluşturulmuştur.
* **Tam Otomatik Oyun Akışı:** Yönetici oyunu başlattıktan sonra sistem; soruları, süreleri ve geçiş aralarını insan müdahalesi olmadan arka plan servisleri (HostedService) ile otomatik yönetir.
* **Heyecan Mekanizması (Suspense):** Oyuncular cevap verdiğinde anında sonucu görmek yerine, bekleme odasına alınır ve süre bittiğinde tüm oyuncular sonucu aynı anda öğrenir.
* **Çift Taraflı Liderlik Tablosu:** Oyun bittiğinde sadece yönetici ekranında değil, her oyuncunun kendi cihazında da liderlik tablosu belirir ve oyuncunun kendi ismi yeşil renkle vurgulanır.
* **Asenkron Yük Yönetimi:** Oyun bittiğinde veritabanı kilitlenmelerini önlemek için kayıt işlemleri RabbitMQ kuyruğuna atılır ve arka planda (Worker Service) sessizce işlenir.
* **Kurumsal İzlenebilirlik ve Hata Toleransı:** Sistemdeki saniyelik ağ kopmalarında uygulamanın çökmesini engelleyen Polly zırhı bulunur. Tüm sistemin CPU/RAM metrikleri, hataları ve logları Grafana ve Seq üzerinden canlı izlenebilir.
* **Premium UI/UX Tasarım:** Özel Slate/Navy renk paletiyle oluşturulmuş Dark/Light Mode, Poppins fontu ve akıcı mikro-animasyonlar.
* **İki Yönlü Soru Oluşturucu (Visual & Markdown):** Yöneticinin soruları hem görsel bir formla hem de Markdown editörüyle oluşturabilmesi, düzenleyebilmesi ve `.md` dosyası olarak indirebilmesi.
* **QR Kod ve Hızlı Katılım:** Lobide otomatik oluşan QR kod ile öğrencilerin telefon kameralarından saniyeler içinde oyuna dahil olabilmesi.
* **Sessiz Hata Kurtarma (Rejoin):** Yönetici veya oyuncu yanlışlıkla sekmeyi kapatsa bile SessionStorage ve Backend hafızası sayesinde "Kaldığın Yerden Devam Et" özelliğiyle oyuna aynı saniyeden geri dönebilmesi.
* **Oyuncu Yönetimi:** Yöneticinin lobideki istenmeyen oyuncuları tek tıkla atabilmesi (Kick Player) ve lobiyi tamamen dağıtabilmesi (Reset Lobby).

## Proje Klasör Yapısı
* **1. Domain:** Sistemin kalbidir. Oyun, Soru, Oyuncu gibi temel veri şablonları burada tutulur. Dış dünyadan tamamen izoledir.
* **2. Application:** İş mantığı, puanlama ve oyun akış kuralları bu katmanda işlenir.
* **3. Infrastructure:** Veritabanı bağlantısı ve fiziksel veri kayıt işlemleri (MongoDB erişimi) burada yapılır.
* **4. Api:** Sistemin dışa açılan kapısıdır. Görsel arayüzler (wwwroot) ve SignalR kulesi bu katmandan yönetilir.
* **5. kahoot-frontend:** React, TypeScript ve Vite ile geliştirilmiş, kendi Nginx sunucusu üzerinde koşan modern ön yüz projesi.

## 🚀 Kurulum ve Çalıştırma Rehberi

Sistemi bilgisayarınızda yerel olarak çalıştırmak için Docker Desktop'ın kurulu ve çalışır durumda olması yeterlidir. Ayrıca veritabanı veya Redis kurmanıza gerek yoktur.

### 1. Ön Gereksinimler
- Docker Desktop
- Git (Projeyi indirmek için)

### 2. Adım Adım Çalıştırma
Terminal (veya CMD/PowerShell) ekranını açın ve aşağıdaki komutları sırasıyla çalıştırarak tüm sistemi tek seferde ayağa kaldırın:

> ```bash
> git clone <projenin-github-linki>
> cd KahootProjesi
> docker-compose up --build -d
> ```

### 3. Uygulamaya Erişim
Bağlantı sorunları yaşamamak adına `localhost` yerine doğrudan yerel IP adresi üzerinden erişilmesi tavsiye edilir:
- Ana Sayfa (Seçim Ekranı): `http://localhost:5173`
- Swagger API Dokümantasyonu: `http://127.0.0.1:5252/swagger/index.html`
- Merkezi Loglama (Seq): `http://127.0.0.1:5341`
- Metrik İşlemcisi (Prometheus): `http://127.0.0.1:9090`
- Görsel Kokpit (Grafana): `http://127.0.0.1:3000` *(Kullanıcı: admin / Şifre: admin)*

### Geliştirici Rehberi: Komutlar ve Docker Yönetimi (Cheat Sheet)
Projeyi bilgisayarına indiren bir geliştiricinin arka planda sistemi yönetmek için ihtiyaç duyacağı tüm temel komutlar ve senaryolar aşağıda özetlenmiştir:

**1. Tüm Sistemi Sıfırdan Ayağa Kaldırma:**
> ```bash
> docker-compose up --build -d
> ```

**2. Sistemi Tamamen Durdurma ve Kapatma:**
> ```bash
> docker-compose down
> ```

**3. Dağıtık Sistemi (2 Sunucu) Koruyarak Güncelleme Yapmak**
Nginx yük dengeleyici (Load Balancer) arkasında 2 adet API sunucusu ile sistemi yeniden başlatmak için:
> ```bash
> docker-compose down
> docker-compose up --scale api=2 --build -d
> ```
*(Bu komut veritabanı ve Redis'e dokunmadan, sadece API imajınızı saniyeler içinde yenileyip ayağa kaldırır).*

**Arka Planda Çalışan API'nin Loglarını (Hatalarını) İzlemek İçin:**
> ```bash
> docker-compose logs -f api
> ```

**4. Bozuk Docker Önbelleğini Tamamen Temizlemek**
Eğer "parent snapshot does not exist" gibi önbellek hataları alırsanız, sistemi temizlemek için:
> ```bash
> docker builder prune -a -f
> ```

**5. Projeyi Önbellek Kullanmadan (Sıfırdan) Tekrar Derleme ve Ayağa Kaldırma**
> ```bash
> docker exec -it kahoot_redis redis-cli
> ```
Terminale bağlandıktan sonra MONITOR komutunu yazarak akan verileri canlı izleyebilir veya KEYS * komutuyla RAM'deki oyun durumlarını listeleyebilirsiniz.

---

## Gelişmiş Test: Dağıtık Sistemi (Redis Backplane) Simüle Etme
Sistemin tek bir bilgisayarda (RAM) değil, yatayda ölçeklenmiş (Horizontal Scaling) bir ağda nasıl kusursuz çalıştığını görmek isterseniz, uygulamayı Nginx arkasında birden fazla API ile çalıştırarak test edebilirsiniz:

1. Terminalde şu komutu çalıştırarak API sunucunuzun sayısını anında **2'ye çıkarın (Scale)**:
>   ```bash
>   docker-compose up --scale api=2 -d
>   ```
2. Tarayıcıda `http://localhost:5173` adresine gidin ve **"Oyun Kur (Host)"** butonuna tıklayarak bir oyun başlatın.
3. Farklı bir sekmede (veya telefonunuzda) yine `http://localhost:5173` adresine giderek **"Oyuna Katıl"** butonuna tıklayın ve ürettiğiniz PIN ile oyuna dahil olun.

**Sonuç:** Öğrenci ile Yönetici arka planda Nginx tarafından rastgele farklı API sunucularına (api-1 ve api-2) düşürülse bile, Redis Backplane sayesinde öğrenci anında yönetici ekranında belirecek ve oyun milisaniyelik gecikme olmadan senkronize akacaktır

---

## Kullanıcı Rehberi (Nasıl Oynanır?)
### Kendi Sorularınızı Nasıl Eklersiniz?
Sisteme kendi sorularınızı eklemek oldukça basittir. Yönetici ekranında yer alan metin kutusuna sorularınızı **Markdown** formatında yapıştırabilir veya hazır bir `.txt` / `.md` dosyasını yükleyebilirsiniz.

**Örnek Soru Formatı:**

> ```markdown
> # Soru: Güneş sistemindeki en büyük gezegen hangisidir?
> Süre: 20
> - Mars
> - Venüs
> - Jüpiter (*)
> - Satürn
> 
> # Soru: "Sefiller" romanının ünlü yazarı kimdir?
> Süre: 30
> - Lev Tolstoy
> - Victor Hugo (*)
> - Fyodor Dostoyevski
> - Charles Dickens
> ```
* Her sorunun başına `#` veya `# Soru:` eklenmelidir.
* `Süre: 20` satırı ile sorunun saniye cinsinden süresi belirlenir (Yazılmazsa sistem varsayılan olarak 20 saniye kabul eder).
* Şıklar `-` veya `*` işareti ile alt alta yazılır.
* Doğru cevabın sonuna bir boşluk bırakıp `(*)` işareti konulmalıdır.

### Oyun Nasıl Başlatılır? (Yönetici)
1. **Yönetici Ekranını Açın:** Tarayıcıda `http://localhost:5173` adresine gidin ve **"👨‍🏫 Oyun Kur (Host)"** butonuna tıklayın.
2. **Yeni Oyun Kur:** "Soru Oluşturucu" veya "Markdown" kullanarak sorularınızı hazırlayın ve "Yeni Oyun Kur" butonuna basın.
3. **PIN veya QR Kod Paylaşın:** Ekranda büyük harflerle beliren 6 haneli **PIN kodunu** veya hemen yanındaki **QR Kodu** oyuncularla paylaşın.
4. **Oyuncuları Bekle ve Başlat:** Tüm oyuncular katıldığında "Oyunu Başlat" butonuna tıklayarak yarışı başlatın. Oyun bundan sonra otomatik olarak işleyecektir.

### Oyuna Nasıl Katılınır? (Oyuncu)
1. **Oyuncu Ekranını Açın:** Tarayıcıda `http://localhost:5173` adresine gidip **"🎮 Oyuna Katıl"** butonuna tıklayın veya telefonunuzla yöneticinin ekranındaki QR Kodu okutun.
2. **Giriş Yapın:** Yöneticinin verdiği **PIN kodunu** ve kendinize bir **takma ad (Nickname)** girerek lobiye katılın.
3. **Cevapları İşaretleyin:** Sorular ekranınızda belirir. Doğru cevabı en hızlı işaretleyen oyuncu en çok puanı toplar!

## SonarQube Kod Analizi ve Test Kapsamı (Coverage)
Projenin kod kalitesini, güvenliğini ve test kapsamını ölçmek için SonarQube entegrasyonu mevcuttur.

### 1. Ön Hazırlık (Sadece İlk Kurulumda)
Sistemin çalışabilmesi için bilgisayarınızda SonarScanner aracının global olarak kurulu olması gerekir. Terminalde şu komutu çalıştırarak kurabilirsiniz:

> ```bash
> dotnet tool install --global dotnet-sonarscanner
> ```
*(Not: Test projesinin rapor üretebilmesi için gereken `coverlet.collector` paketi projede halihazırda yapılandırılmıştır.)*

### 2. Standart Analiz Döngüsü (Kod Değiştikçe Tekrar Edilir)
Ana proje dizininde (Solution `.sln` dosyasının bulunduğu klasör) terminali açıp sırasıyla aşağıdaki 3 adımı uygulamalısınız:

**Adım A: Dinlemeyi Başlat (Begin)**
Bu komut SonarQube'a analiz sürecinin başladığını ve test raporlarının `**/*.opencover.xml` yolunda bulunacağını bildirir:

> ```bash
> dotnet sonarscanner begin /k:"KahootProjesi" /d:sonar.host.url="http://localhost:9000" /d:sonar.login="sqp_***" /d:sonar.cs.opencover.reportsPaths="**/*.opencover.xml"
> ```

**Adım B: Derle ve Test Et (Build & Test)**
Önce proje derlenir (Derleme sırasında SonarScanner arka planda kod kokularını ve hataları inceler). Ardından test komutu çalışarak tüm Unit Test'leri koşar ve hangi satırların test edildiğini (`opencover.xml` formatında) hesaplar:

> ```bash
> dotnet build
> dotnet test --collect:"XPlat Code Coverage;Format=opencover"
> ```

**Adım C: Raporu Gönder (End)**
Tüm analizleri ve test kapsama dosyalarını paketleyip `localhost:9000` adresindeki SonarQube sunucusuna gönderir:

> ```bash
> dotnet sonarscanner end /d:sonar.login="sqp_***"
> ```