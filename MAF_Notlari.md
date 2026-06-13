# Microsoft Agent Framework (MAF) Öğrenme Notları 🧙‍♂️📝

Bu not defteri, Microsoft Agent Framework'ü C# ve .NET kullanarak öğrenirken edindiğimiz tüm teorik ve pratik bilgileri konu konu özetlemektedir.

---

## 📁 KONU 1: Çevre Kurulumu ve Proje Yapısı

### 1. Yeni Proje Oluşturma
Terminalde sıfırdan konsol projesi açmak için:
```bash
dotnet new console -n ProjeAdi
```

### 2. Gerekli NuGet Paketleri (MAF ve AI Altyapısı)
Projemizde kullandığımız kütüphaneler:
*   **`Microsoft.Agents.AI`** (prerelease): MAF'ın ana ajan orkestrasyon kütüphanesi.
*   **`Microsoft.Agents.AI.OpenAI`** (prerelease): Ajanların OpenAI uyumlu modellerle konuşmasını sağlayan bağlayıcı.
*   **`Microsoft.Extensions.AI.OpenAI`** (prerelease): .NET'in ortak yapay zeka arayüz köprüsü.

Terminalden yükleme komutları:
```bash
dotnet add package Microsoft.Agents.AI --prerelease
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
dotnet add package Microsoft.Extensions.AI.OpenAI --prerelease
```

---

## 🔌 KONU 2: OpenAI Uyumlu LLM Bağlantısı

Yapay zeka modelleriyle konuşurken resmî OpenAI SDK'sını kullanırız. Ancak araya bir **"Priz Dönüştürücü"** koyarak isteklerimizi Google Gemini gibi diğer OpenAI uyumlu sunuculara yönlendirebiliriz.

```csharp
// 1. API Bilgileri
string apiKey = "API_KEY";
Uri endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/");

// 2. İstemci Seçenekleri (Rotayı değiştirme)
var options = new OpenAIClientOptions { Endpoint = endpoint };

// 3. Bağlantıyı başlatma
var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
```

---

## 🧠 KONU 3: Ortak Arayüz (`IChatClient`) ve Ajan (`AIAgent`)

MAF mimarisinde iki temel katman vardır: beyni temsil eden **bağlantı katmanı** ve karaktere sahip olan **ajan katmanı**.

### 1. IChatClient (Ajanın Beyni)
.NET içindeki tüm yapay zekaları (`Gemini`, `Ollama`, `OpenAI` vb.) ortak bir dile çevirir. `AsIChatClient()` uzantı metoduyla oluşturulur.
```csharp
ChatClient openAIChatClient = client.GetChatClient("gemini-2.5-flash");
IChatClient chatClient = openAIChatClient.AsIChatClient();
```

### 2. AIAgent (Ajanın Kimliği)
Beyne bir isim ve görev talimatı (System Prompt / Instructions) yükler:
```csharp
AIAgent agent = chatClient.AsAIAgent(
    name: "Merlin",
    instructions: "Sen C# ve .NET uzmanı bilge bir büyücüsün. Cevaplarının sonuna 🧙‍♂️ ekle."
);
```

---

## 🛠️ KONU 4: Ajan Eylemleri (Araç Kullanımı / Tools & Functions)

Yapay zekanın kendi kendine bilmediği şeyleri (tarih/saat gibi) öğrenmesi veya kod çalıştırabilmesi için C# metotlarını ajana **"Araç (Tool)"** olarak veririz.

### 1. Araç Sınıfı ve `[Description]` Özniteliği
Metotların üstüne yazılan `[Description]` özniteliği, yapay zekanın o metodun ne işe yaradığını anlamasını sağlar.
```csharp
using System.ComponentModel;

public class WizardTools
{
    [Description("Sistemin güncel tarih ve saat bilgisini verir.")]
    public string GetCurrentDateTime()
    {
        return DateTime.Now.ToString("dd MMMM yyyy HH:mm:ss");
    }
}
```

### 2. Araçları Ajana Teslim Etme
`AIFunctionFactory.Create` kullanarak C# metotlarını ajana bağlarız:
```csharp
var wizardTools = new WizardTools();

AIAgent agent = chatClient.AsAIAgent(
    name: "Merlin",
    instructions: "...",
    tools: [
        AIFunctionFactory.Create(wizardTools.GetCurrentDateTime)
    ]
);
```

---

## 🤝 KONU 5: Çoklu Ajan Orkestrasyonu (Multi-Agent Workflows)

Büyük görevleri bölmek için birden fazla ajan oluşturup onları paslaştırırız (Örn: Yazılımcı Coder ve Denetleyici Reviewer).

### İteratif Geliştirme Döngüsü Mantığı:
1. Kullanıcıdan istek alınır.
2. **Coder Ajanı** istek doğrultusunda ilk kodu yazar.
3. Bu kod **Reviewer Ajanına** gönderilir.
4. Reviewer kodu inceler. Hata varsa geri bildirim üretir, yoksa sonuna `"ONAYLANDI"` yazar.
5. Kodda hata varsa, geri bildirim Coder'a beslenerek döngü başa döner.
6. Kod onaylanana veya maksimum deneme sınırına (Tur sayısı) ulaşılana kadar süreç devam eder.

---

## 🛡️ KONU 6: Güvenlik, Hata Yönetimi ve Gecikme (`Task.Delay`)

### 1. try-catch ile Çökmeleri Önleme
API kotası dolduğunda veya internet koptuğunda uygulamanın kapanmaması için ajan tetiklemelerini `try-catch` bloğuna alırız:
```csharp
try
{
    AgentResponse response = await agent.RunAsync(input);
}
catch (Exception ex)
{
    Console.WriteLine($"[Hata]: {ex.Message}");
}
```

### 2. await Task.Delay (Asenkron Bekleme)
`Thread.Sleep` gibi programı tamamen kilitlemek yerine, işlemciyi bloke etmeden arkaplanda bekleme yapar. Yapay zeka servislerinin dakikalık kota sınırına (Rate Limit - 429) takılmamak için kullanılır.
```csharp
// İstek göndermeden önce sunucuyu yormamak için 4 saniye asenkron olarak uyu
await Task.Delay(4000); 
AgentResponse response = await agent.RunAsync(input);
```

---

## 💾 KONU 7: Ajan Hafızası (AgentSession) ve Oturum Yönetimi

Ajanlar varsayılan olarak hafızasızdır (stateless). Yani her `RunAsync` çağrısı yeni bir sohbet gibi başlar. Sohbet geçmişini (context) korumak için `AgentSession` nesneleri kullanılır.

### 1. Oturum Oluşturma
Her ajan için `CreateSessionAsync()` yardımıyla bir oturum başlatılır:
```csharp
AgentSession coderSession = await agent.CreateSessionAsync();
AgentSession reviewerSession = await reviewerAgent.CreateSessionAsync();
```

### 2. Oturum ile Ajan Çalıştırma
Ajan çağrılırken ikinci parametre olarak oluşturulan oturum geçilir. Böylece ajan tüm geçmiş yazışmaları aklında tutar:
```csharp
AgentResponse coderResponse = await agent.RunAsync(currentPayload, coderSession);
```

---

## 💾 KONU 8: Kalıcı Hafıza (Serialization & Deserialization)

Program kapatıldığında ajanların hafızasının silinmesini istemiyorsak, oturumu diske kaydedip program açıldığında geri okuyabiliriz.

### 1. Oturumu JSON Olarak Kaydetme (Serialization)
`SerializeSessionAsync` metodu oturumu bir `JsonElement` nesnesine dönüştürür. Bunu diske kaydetmek için `.ToString()` ile metne çevirebiliriz:
```csharp
JsonElement serializedState = await agent.SerializeSessionAsync(coderSession);
string jsonText = serializedState.ToString();
await File.WriteAllTextAsync("coder_session.json", jsonText);
```

### 2. Oturumu Dosyadan Geri Yükleme (Deserialization)
Daha önce kaydedilmiş bir JSON dosyası varsa, bu dosyayı okuyup `JsonDocument.Parse` ile ayrıştırarak `DeserializeSessionAsync` yardımıyla oturumu geri yükleriz:
```csharp
if (File.Exists("coder_session.json"))
{
    string jsonText = await File.ReadAllTextAsync("coder_session.json");
    using JsonDocument doc = JsonDocument.Parse(jsonText);
    coderSession = await agent.DeserializeSessionAsync(doc.RootElement);
}
else
{
    coderSession = await agent.CreateSessionAsync();
}
```
