# MAF İleri Seviye ve Kurumsal Entegrasyon Yol Haritası 🏛️🚀

Bu rehber, Microsoft Agent Framework (MAF) kullanarak kurumsal düzeyde, ölçeklenebilir ve güvenli yapay zeka ajan projeleri geliştirmek için tasarlanmış ileri seviye çalışma programıdır.

---

## 📅 Tahmini Süre: 5 - 7 Gün
*(Günde ortalama 2 saatlik odaklanmış pratik çalışma ile)*

---

## 🗺️ Öğrenme Aşamaları ve Günlük Plan

### 📍 AŞAMA 1: Web API ve Dependency Injection Entegrasyonu (1-2 Gün)
Ajanlarımızı konsol uygulaması olmaktan çıkarıp, kurumsal web servislerine dönüştürüyoruz.
*   **1. Gün:**
    *   ASP.NET Core Web API projesi kurulumu ve yapılandırması.
    *   `Microsoft.Extensions.AI` ve MAF ajanlarının `IServiceCollection` (Dependency Injection) ile kaydedilmesi.
    *   Ajanların Controller sınıflarına constructor üzerinden inject edilmesi.
*   **2. Gün:**
    *   Web API üzerinden ajan tetikleme uç noktalarının (Endpoints) yazılması.
    *   İstek (Request) ve yanıt (Response) modellerinin kurumsal standartlara göre tasarlanması.

### 📍 AŞAMA 2: Gelişmiş Akış Yönetimi (Graph Workflows & Human-in-the-loop) (1-2 Gün)
Basit döngülerin ötesine geçerek, ajanların durum makineleriyle karmaşık görevleri yönetmesini sağlıyoruz.
*   **3. Gün:**
    *   Graf tabanlı akışların (Graph-based Workflows) tasarımı.
    *   Ajanlar arası koşullu geçiş kuralları tanımlama.
*   **4. Gün:**
    *   **Human-in-the-loop (İnsan Onayı):** Ajanların kritik kararlarda veya kod onaylarında insan yöneticiden onay istemesi.
    *   İnsan onayı gelene kadar akış durumunun dondurulması ve onay sonrasında kaldığı yerden devam ettirilmesi.

### 📍 AŞAMA 3: Background Services ve Kuyruk (Queue) Yönetimi (1-2 Gün)
Uzun süren ajan işlemlerinin web sunucusunu kilitlemesini önlemek için asenkron arka plan mimarisi kuruyoruz.
*   **5. Gün:**
    *   `.NET BackgroundService` (Hosted Service) altyapısının kurulması.
    *   Gelen ajan taleplerinin bellek içi kuyruk (System.Threading.Channels) veya dış kuyruk sistemlerine atılması.
*   **6. Gün:**
    *   Arka plan servisinin kuyruğu dinleyerek işleri sırayla ajanlara yaptırması.
    *   İşlemler bittiğinde kullanıcıya asenkron bildirim gönderilmesi.

### 📍 AŞAMA 4: Kurumsal Güvenlik ve Gözlemlenebilirlik (1 Gün)
Projenin canlı ortamda güvenli ve izlenebilir olmasını sağlıyoruz.
*   **7. Gün:**
    *   API anahtarlarının ve hassas verilerin **User Secrets** ve **Key Vault** ile korunması.
    *   **OpenTelemetry Entegrasyonu:** Ajanların harcadığı token miktarlarının, gecikme sürelerinin (latency) ve hata oranlarının Jaeger, Prometheus veya Grafana panellerinden canlı izlenmesi.
