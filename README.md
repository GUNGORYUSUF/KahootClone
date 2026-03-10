# Kahoot Clone - Gerçek Zamanlı Bilgi Yarışması

Bu proje, yapay zeka destekli yazılım geliştirme dersi kapsamında "Agentic Engineering" yaklaşımları kullanılarak geliştirilen gerçek zamanlı bir bilgi yarışması uygulamasıdır. 

## Kullanılan Teknolojiler

* Backend: C# .NET Core Web API
* Gerçek Zamanlı İletişim: SignalR (WebSockets)
* Veritabanı: MongoDB (NoSQL)
* Frontend: HTML5, CSS3 (Bootstrap), Vanilla JavaScript
* Mimari: Clean Architecture

## Proje Kuralları ve Geliştirme Yaklaşımı

* Sistem, katılımcıların anlık olarak senkronize olduğu WebSockets altyapısı sunar.
* Geliştirme sürecinde "Vibe Coding" yerine, YZ asistan olarak kullanılmış ve her adım insan denetiminden geçmiştir.
* Güvenlik önlemleri (Injection zafiyetlerine karşı koruma) ve Clean Code prensipleri ön planda tutulmuştur.
* Sırları (secret, password) gizlemek için güvenli ortam yapılandırmaları tercih edilmiştir.

## Mevcut İlerleme

* Proje iskeleti oluşturuldu.
* Temiz Mimari (Clean Architecture) katmanları (Domain, Application, Infrastructure, API) başarıyla kuruldu.
* Git versiyon kontrol sistemi entegre edildi.