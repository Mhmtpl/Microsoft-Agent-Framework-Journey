# Microsoft Agent Framework Serisi: Bölüm 2: Web API ve Kuyruk Ajanı - İnsan Onaylı Kod Üretim Akışı

Bu proje, Microsoft Agent Framework'ü kullanarak, birden fazla yapay zeka ajanının (Merlin - Kodlayıcı, Arthur - Kod İnceleyici) iş birliği yaptığı ve sürecin belirli aşamalarında insan onayı gerektiren asenkron bir kod üretim iş akışını gösteren bir ASP.NET Core Web API uygulamasıdır. `System.Threading.Channels` tabanlı bir kuyruk sistemi ve `IHostedService` olarak çalışan bir arka plan servisi ile uzun süreli ajan operasyonları asenkron olarak yönetilmektedir.

## Mimari Bakış
Bu proje, uzun süreli ve durum bilgisi gerektiren ajan tabanlı iş akışlarını yönetmek için sağlam bir mimari sunar. Temel bileşenler şunlardır:

1.  **Web API (Program.cs):**
    *   Kullanıcı isteklerini (`ChatRequest`) alır ve yeni işleri (`AgentJob`) bir kuyruğa ekler.
    *   İşin anlık durumunu (`JobResult`) sorgulamak için bir endpoint sunar.
    *   Ajan akışının "insan onayı" aşamasında, yöneticinin kararını (`HumanApprovalInput`) almak ve süreci yeniden başlatmak için özel bir endpoint (`/api/chat/approve/{jobId}`) sağlar.
    *   Bağımlılık Enjeksiyonu (DI) ile ajanları, kuyruğu, durum deposunu ve orkestratörü yapılandırır. Özellikle `Keyed Services` kullanarak "Merlin" ve "Arthur" ajanlarını farklı rollerle kaydeder.

2.  **Ajan İş Kuyruğu (`AgentJobQueue.cs`):**
    *   `System.Threading.Channels.Channel<T>` yapısını kullanarak işleri bellek içi, thread-safe bir şekilde depolar ve sunar.
    *   API tarafından eklenen işler bu kuyruğa alınır ve arka plan servisi tarafından buradan çekilir.

3.  **İş Durum Deposu (`JobStatusStore.cs`):**
    *   `ConcurrentDictionary<string, JobResult>` kullanarak tüm işlerin durumunu ve sürecin geçmişini bellek içi olarak tutar.
    *   Her işin anlık `WorkflowState`'ini ve diğer detaylarını (üretilen kod, geri bildirimler, tur sayısı, insan kararı) yönetir.

4.  **Ajan Orkestratörü (`AgentOrchestrator.cs`):**
    *   Bu sınıf, ajanlar arasındaki etkileşimi ve iş akışının durum geçişlerini yöneten merkezi bir durum makinesi (state machine) uygular.
    *   **Merlin (Kodlayıcı Ajan):** Kullanıcı prompt'una veya Arthur'dan gelen geri bildirimlere göre C# kodu üretir.
    *   **Arthur (Kod İnceleyici Ajan):** Merlin tarafından yazılan kodu güvenlik, performans ve okunabilirlik açısından inceler. Onaylanırsa "ONAYLANDI" ifadesini döndürür, aksi takdirde eksiklikleri belirtir.
    *   İş akışı sırasında `WorkflowState` enum'unu kullanarak durumu günceller (MerlinCoding, ArthurReviewing, WaitingForHumanApproval vb.).
    *   Ajan oturumlarını dosya sistemine kaydederek stateful etkileşimleri sürdürür.
    *   İnsan onayı beklenen duruma geldiğinde, `ExecuteWorkflowAsync` metodu askıya alınır ve insan kararı gelene kadar bekler. İnsan kararı geldiğinde, iş kuyruğa tekrar eklenir ve orkestratör kaldığı yerden devam eder.

5.  **Ajan İşçisi (`AgentWorker.cs`):**
    *   `IHostedService` arayüzünü uygulayan bir `BackgroundService`'tir.
    *   Uygulama yaşam döngüsü boyunca arka planda sürekli çalışır.
    *   `AgentJobQueue`'dan işleri çeker ve `AgentOrchestrator`'ı tetikleyerek iş akışını başlatır veya devam ettirir.
    *   İşlerin durumunu `JobStatusStore` üzerinden günceller ve loglar.
    *   Hata durumlarını yönetir ve işin `Failed` durumuna geçmesini sağlar.

### Tasarım Kalıpları
*   **Üretici-Tüketici (Producer-Consumer):** API endpoint'leri işleri kuyruğa ekleyen üreticiler, `AgentWorker` ise kuyruktan iş çeken tüketicidir.
*   **Arka Plan İşçisi (Background Worker):** `AgentWorker` bir `IHostedService` olarak uzun süreli asenkron operasyonları ana uygulama döngüsünden ayırır.
*   **Durum Makinesi (State Machine):** `AgentOrchestrator` içindeki `ExecuteWorkflowAsync` metodu, `WorkflowState` enum'u ile durumu yöneten ve geçişleri kontrol eden bir durum makinesi örneğidir.
*   **Orkestrasyon (Orchestration):** `AgentOrchestrator` farklı ajanlar arasındaki iş akışını ve etkileşimi koordine eder.
*   **İnsan-Döngüde (Human-in-the-Loop - HITL):** `WaitingForHumanApproval` durumu ve ilgili API endpoint'i, otomasyon sürecine manuel insan müdahalesini entegre eder.
*   **Bağımlılık Enjeksiyonu (Dependency Injection):** Servislerin yaşam döngüsü ve bağımlılıkları .NET Core'un yerleşik DI mekanizması ile yönetilir. `Keyed Services` kullanımı, aynı türden farklı yapılandırmalara sahip ajanları ayırt etmek için iyi bir örnektir.

## Kurulum ve Çalıştırma
Bu projeyi yerel ortamınızda çalıştırmak için aşağıdaki adımları izleyin:

### Ön Koşullar
*   .NET 10 SDK
*   Bir yapay zeka modeli sağlayıcısı (Örn: Google Gemini) ve API anahtarı.
    *   `Microsoft.Agents.AI` şu anda OpenAI uyumlu istemcileri desteklemektedir. Gemini için Google'ın OpenAI uyumlu endpoint'ini kullanıyoruz.

### Adımlar

1.  **API Anahtarını Yapılandırma:**
    *   Google Gemini için bir API anahtarı edinin.
    *   Bu anahtarı `GEMINI_API_KEY` adıyla bir ortam değişkeni olarak ayarlayın. Örneğin:
        ```bash
        export GEMINI_API_KEY="YOUR_GEMINI_API_KEY" # Linux/macOS
        set GEMINI_API_KEY="YOUR_GEMINI_API_KEY"   # Windows Command Prompt
        $env:GEMINI_API_KEY="YOUR_GEMINI_API_KEY"  # Windows PowerShell
        ```
    *   _Not: Güvenlik için API anahtarınızı asla doğrudan kod içine gömmeyin veya kaynak kontrolüne işlemeyin._

2.  **Projenin Klonlanması:**
    ```bash
    git clone [repo_url]
    cd 04_AgentWebAPI # Veya projenizin bulunduğu dizin
    ```

3.  **Bağımlılıkların Yüklenmesi:**
    ```bash
    dotnet restore
    ```

4.  **Uygulamayı Çalıştırma:**
    ```bash
    dotnet run
    ```
    Uygulama varsayılan olarak `https://localhost:7197` (veya benzer bir port) üzerinde çalışacaktır. Konsolda uygulamanın çalıştığı adresleri görebilirsiniz.

### API Endpoint'leri ile Etkileşim

Uygulama çalıştıktan sonra, Postman, curl veya başka bir API istemcisi kullanarak aşağıdaki endpoint'leri test edebilirsiniz:

#### 1. Asenkron Ajan Akışı Tetikleme (Kuyruğa Atma)
*   **URL:** `POST https://localhost:7197/api/chat/async`
*   **Headers:** `Content-Type: application/json`
*   **Body:**
    ```json
    {
      "sessionId": "my-coding-session-123",
      "prompt": "Bir C# metot yaz. Bu metot iki int parametre alsın ve bu parametreleri toplayıp sonucu dönsün."
    }
    ```
*   **Response:**
    ```json
    {
      "jobId": "a1b2c3d4e5f6...",
      "status": "Pending"
    }
    ```
    `jobId` değerini bir sonraki adımlar için kaydedin.

#### 2. İş Durumu Sorgulama
*   **URL:** `GET https://localhost:7197/api/chat/status/{jobId}`
*   **Örnek:** `GET https://localhost:7197/api/chat/status/a1b2c3d4e5f6...`
*   **Response (Örnek - Onay Bekleniyor):**
    ```json
    {
      "jobId": "a1b2c3d4e5f6...",
      "sessionId": "my-coding-session-123",
      "prompt": "Bir C# metot yaz...",
      "currentState": "WaitingForHumanApproval",
      "code": "// Merlin'in ürettiği C# kodu burada olacak",
      "feedback": "ONAYLANDI", // Arthur onayladı
      "turn": 1,
      "humanDecision": null,
      "errorMessage": null
    }
    ```
    Bu endpoint'i tekrar tekrar çağırarak işin durumunu gözlemleyebilirsiniz. `CurrentState` `WaitingForHumanApproval` olduğunda bir sonraki adıma geçebilirsiniz.

#### 3. İnsan Onayı / Karar Bildirme
*   **URL:** `POST https://localhost:7197/api/chat/approve/{jobId}`
*   **Örnek:** `POST https://localhost:7197/api/chat/approve/a1b2c3d4e5f6...`
*   **Headers:** `Content-Type: application/json`
*   **Body (Onaylama):**
    ```json
    {
      "approved": true,
      "comment": "Kod harika, onaylıyorum!"
    }
    ```
*   **Body (Reddetme):**
    ```json
    {
      "approved": false,
      "comment": "Kod güvenlik açıkları içeriyor, lütfen SQL Injection'a dikkat et."
    }
    ```
*   **Response:**
    ```json
    {
      "message": "Onay kaydedildi, süreç devam ettiriliyor." (veya "Red kaydedildi, Merlin kodu tekrar düzenleyecek."),
      "jobId": "a1b2c3d4e5f6...",
      "currentState": "WaitingForHumanApproval" // Bu yanıt anlık durumu gösterir, arka planda değişecektir
    }
    ```
    Bu işlemden sonra, `GET /api/chat/status/{jobId}` endpoint'ini tekrar sorgulayarak işin durumunun değiştiğini ve akışın devam ettiğini görebilirsiniz (onaylandıysa `Approved`, reddedildiyse tekrar `MerlinCoding`).

Bu README, projenin temel işlevselliğini ve nasıl kullanılacağını açıklar. İyi geliştirmeler!
---