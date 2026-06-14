# Bölüm 3: OpenTelemetry Gözlemlenebilirlik (Observability) & User Secrets

Bu proje, Microsoft Agent Framework ve .NET 10 üzerinde OpenTelemetry entegrasyonu sağlayarak AI uygulamalarında izlenebilirlik (observability) ve yerel güvenlik yönetimi (User Secrets) pratiklerini gösteren bir konsol uygulamasıdır.

## 🚀 Projenin Amacı ve Kazandırdıkları

Yapay zeka modelleriyle çalışan kurumsal sistemlerde iki önemli ihtiyaç vardır:
1. **Güvenlik (Secrets Management):** API anahtarlarının koda gömülmesini veya ortam değişkenleriyle yönetilmesini engelleyerek daha güvenli yerel geliştirme yapmak.
2. **Gözlemlenebilirlik (Observability):** Yapay zekaya yapılan çağrıların kaç saniye sürdüğünü (latency), ne kadar girdi/çıktı token'ı harcadığını (token usage) ve varsa hata detaylarını anlık izleyebilmek.

Bu proje, .NET'in standart **OpenTelemetry** ve **User Secrets** altyapısıyla bu iki ihtiyacı da sıfırdan çözer.

---

## 🛠️ Kullanılan Teknolojiler ve Yapılar

*   **.NET 10 & C# 14**
*   **OpenTelemetry SDK & Console Exporter:** Trace verilerini terminale yazdırmak için kullanılan temel izleme araçları.
*   **Microsoft.Extensions.AI.OpenTelemetry:** IChatClient çağrılarını izleyen yerleşik telemetry middleware'i.
*   **User Secrets (`Microsoft.Extensions.Configuration.UserSecrets`):** API anahtarlarını yerelde güvenli saklama kütüphanesi.

---

## 📡 OpenTelemetry Çıktılarını Anlamak

Uygulamayı çalıştırdığınızda konsola yazdırılan izleme verisinin (Trace Activity) kritik alanları ve anlamları:

*   **`Activity.Duration`:** Gemini API çağrısının tam olarak ne kadar sürdüğünü gösterir (Örn: `00:00:03.55`). Latency analizi için kritiktir.
*   **`gen_ai.usage.input_tokens`:** LLM'e gönderilen prompt'un tükettiği girdi token miktarı.
*   **`gen_ai.usage.output_tokens`:** LLM'in ürettiği yanıtın tükettiği çıktı token miktarı.
*   **`gen_ai.request.model` & `gen_ai.response.model`:** Talebin hangi modele gittiği ve hangi modelden yanıt alındığı.
*   **`error.type` & `StatusCode`:** Eğer API kotası aşılırsa (HTTP 429) veya başka bir hata alınırsa, hata türü ve detayları otomatik olarak buraya kaydedilir.

---

## 💻 Kurulum ve Çalıştırma

### Ön Koşullar
*   .NET 10 SDK yüklü olmalıdır.

### Adım 1: User Secrets Yapılandırması
API anahtarınızı kod dosyalarına veya ortam değişkenlerine yazmak yerine, yerel kullanıcı sırları deposuna kaydedin:

```bash
# Proje klasöründe çalıştırın
dotnet user-secrets set GEMINI_API_KEY "YOUR_GEMINI_API_KEY"
```

### Adım 2: Projeyi Çalıştırma
Uygulamayı başlatın:

```bash
dotnet run
```

Çalıştırma sonrasında hem Gemini'den gelen yanıtı hem de OpenTelemetry'nin arka planda yakalayıp konsola bastığı metrik/iz (trace) detaylarını görebilirsiniz.
