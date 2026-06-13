# Microsoft Agent Framework Serisi: Bölüm 1: Console Agent - Akıllı Kodlama Asistanı

## Proje Genel Bakış

Bu proje, Microsoft'un .NET için geliştirdiği deneysel Agent Framework'ünü kullanarak, yapay zeka ajanları arasında bir iş akışı oluşturan basit ama güçlü bir konsol uygulamasıdır. Temel amacı, bir C# ve .NET uzmanı yazılımcı ajanı ("Merlin") ile bir kod denetleyicisi ajanı ("Arthur") arasında dinamik bir etkileşim sağlayarak, kullanıcıdan alınan bir özelliğe göre kod üretmek ve bu kodu otomatik olarak gözden geçirmektir. Proje, Gemini API'sini kullanarak ajanların zekasını güçlendirmektedir.

## Mimari ve Tasarım Kararları

Projenin mimarisi, ajan tabanlı sistemlerin temel prensiplerini ve .NET ekosisteminin modern özelliklerini bir araya getirmektedir:

1.  **Ajan Odaklı Tasarım:**
    *   **Çift Ajan Modeli:** İki ana `AIAgent` örneği bulunur:
        *   **Merlin (Yazılımcı Ajan):** Kullanıcının isteklerine göre C# kodu üretir. `WizardTools` aracılığıyla harici fonksiyonları (mevcut tarih, sayı toplama, dosya listeleme) kullanabilir. Geri bildirimlere göre kodunu güncelleyebilir.
        *   **Arthur (Kod Denetleyicisi Ajan):** Merlin'in ürettiği kodları güvenlik, performans ve okunurluk açısından inceler. Onayladığı takdirde "ONAYLANDI" metnini içeren bir yanıt döner; aksi takdirde eksiklikleri maddeler halinde belirtir.
    *   **Rol Tabanlı Talimatlar (Instructions):** Her ajana, görevlerini net bir şekilde tanımlayan detaylı talimatlar verilmiştir. Bu, ajanların davranışını ve çıktılarını doğrudan şekillendirir.

2.  **Microsoft Agents for .NET (Preview) Framework:**
    *   **`AIAgent` Sınıfı:** Ajanları tanımlamak, onlara talimatlar vermek ve araçlar atamak için temel yapı taşıdır.
    *   **`IChatClient` Entegrasyonu:** `OpenAIClient` üzerinden Gemini modeline bağlanılır ve `AsIChatClient()` uzantı metoduyla `IChatClient` arayüzüne dönüştürülerek ajanlar için bir iletişim kanalı sağlanır.
    *   **Araç Kullanımı (Tooling):** `WizardTools` sınıfı, ajanların dış dünyayla etkileşim kurmasını sağlayan fonksiyonları içerir. `AIFunctionFactory.Create()` metoduyla bu fonksiyonlar ajanlara tanıtılır, bu sayede ajanlar ihtiyaç duyduğunda bunları çağırabilir (Function Calling).

3.  **Oturum Yönetimi ve Durum Koruma (Session Management & Persistence):**
    *   **`AgentSession`:** Her ajanın kendi diyalog geçmişini ve bağlamını tuttuğu bir oturum nesnesidir.
    *   **JSON ile Kalıcılık:** Ajan oturumları (`coder_session.json` ve `reviewer_session.json` dosyaları aracılığıyla) JSON formatında disk üzerinde saklanır ve uygulama her başlatıldığında yüklenir. Bu, ajanların "hafızalarını" koruyarak uzun süreli etkileşimlere olanak tanır. `JsonDocument` ve `DeserializeSessionAsync`/`SerializeSessionAsync` metodları bu işlevi yerine getirir.

4.  **İş Akışı Orkestrasyonu:**
    *   **Döngü Tabanlı Etkileşim:** Ana `while` döngüsü, kullanıcıdan gelen isteklere göre Merlin ve Arthur arasında bir "tur tabanlı" iletişim kurar.
    *   **Geri Bildirim Döngüsü:** Merlin'in kodu Arthur tarafından onaylanana veya maksimum tur sayısına ulaşana kadar bir geri bildirim döngüsü işletilir. Arthur'un geri bildirimi, bir sonraki turda Merlin'e yeni bir talimat olarak gönderilir.
    *   **Asenkron İşlem:** `RunAsync` ve `Task.Delay` kullanılarak ajan çağrıları ve simüle edilmiş bekleme süreleri asenkron olarak yönetilir.

## Kullanılan Teknolojiler

*   **.NET 10 ve C# 14:** Modern .NET platformu ve C# dil özelliklerinden faydalanır.
*   **Microsoft Agents for .NET (Preview):** Ajan tabanlı uygulamalar geliştirmek için deneysel framework.
*   **OpenAI SDK for .NET:** Gemini gibi LLM'lerle entegrasyonu kolaylaştıran kütüphane.
*   **Gemini API:** Büyük dil modeli olarak kullanılır.

## Tasarım Kalıpları ve Yaklaşımlar

*   **Producer-Consumer (Kavramsal):** Ajanlar arasında (Merlin üretir, Arthur tüketir ve geri bildirim üretir) bir producer-consumer ilişkisi kurulmuştur. Mevcut implementation basit olsa da, bu tür sistemler genellikle daha gelişmiş kuyruk yapılarıyla bu kalıbı kullanır.
*   **Chain of Responsibility (Zincir Sorumluluğu - Kavramsal):** Bir ajanın çıktısı, bir sonraki ajanın girdisi haline gelir. Bu, bir dizi işlemin sırayla gerçekleştiği bir zincir oluşturur.
*   **Strategy (Strateji):** Her ajan (Merlin, Arthur), belirli bir "strateji" veya davranış seti (talimatlar ve araçlar) ile yapılandırılmıştır.
*   **Function Calling / Tooling:** Ajanların dış sistemlerle veya özel kodlarla etkileşim kurmasını sağlayan önemli bir yapay zeka kalıbıdır.

## Kurulum ve Çalıştırma

### Ön Koşullar

*   [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) yüklü olmalıdır.
*   Bir Google Gemini API anahtarına sahip olmalısınız.

### Adım 1: Gemini API Anahtarını Ayarlayın

Proje, Gemini API anahtarını ortam değişkenlerinden (`GEMINI_API_KEY`) okur. Anahtarınızı aşağıdaki gibi ayarlayın:

**Windows:**
```bash
setx GEMINI_API_KEY "YOUR_GEMINI_API_KEY"
```

**Linux/macOS (geçici - kalıcı için .bashrc/.zshrc dosyanıza ekleyin):**
```bash
export GEMINI_API_KEY="YOUR_GEMINI_API_KEY"
```

> ⚠️ **UYARI:** `GEMINI_API_KEY` ayarlanmazsa uygulama çalışmaya devam eder ancak Gemini API çağrıları başarısız olur ve ajanlar beklenen şekilde çalışmaz.

### Adım 2: Projeyi Klonlayın ve Çalıştırın

1.  Bu depoyu yerel makinenize klonlayın:
    ```bash
    git clone https://github.com/KULLANICI_ADINIZ/proje_adi.git
    cd proje_adi
    ```
2.  Bağımlılıkları yükleyin ve projeyi çalıştırın:
    ```bash
    dotnet restore
    dotnet run
    ```

### Kullanım

Uygulama başlatıldığında, "Yazılacak C# özelliğini girin:" komutunu göreceksiniz. Buraya istediğiniz C# özelliğini yazarak Merlin'in kod üretmesini ve Arthur'un bu kodu denetlemesini izleyebilirsiniz.

**Örnek Girişler:**
*   "Bir C# metodu yaz. Bu metot, bir string listesi alsın ve bu listedeki tüm stringleri tek bir string olarak birleştirip dönsün. Eğer liste boşsa, boş string dönsün."
*   "Kullanıcının adını alıp ekrana 'Merhaba, [Ad]' yazdıran basit bir konsol uygulaması taslağı oluştur."
*   "İki sayıyı toplayan ve sonucu dönen bir C# fonksiyonu yaz." (Merlin'in `SumNumbers` aracını kullanmasını tetikleyebilir.)

Çıkmak için `exit` yazın. Uygulama çıkmadan önce ajanların hafızasını kaydedecektir.

## Gelecek Geliştirmeler

*   **Daha Kapsamlı Araçlar:** Daha fazla `WizardTools` fonksiyonu ekleyerek ajanların yeteneklerini genişletmek.
*   **Gelişmiş İş Akışları:** Farklı ajan rollerini (örn. testçi, dokümantasyon yazarı) dahil eden daha karmaşık orkestrasyonlar.
*   **UI Geliştirme:** Konsol arayüzü yerine web tabanlı (Blazor) veya masaüstü (WPF/MAUI) bir arayüz entegrasyonu.
*   **Hata Yönetimi:** Ajan etkileşimleri sırasında olası hatalar için daha robus hata işleme stratejileri.

---