// Programı kendin yazacaksın kanka, hadi başla!
using System;
using System.ClientModel;
using System.Threading.Tasks;
using  Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ComponentModel;using System.Text.Json;

string apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("⚠️ WARNING: GEMINI_API_KEY environment variable is not set!");
}


Uri endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/");

var options = new OpenAIClientOptions
{
    Endpoint = endpoint
};

var client =new OpenAIClient(new ApiKeyCredential(apiKey), options);
ChatClient openAIChatClient = client.GetChatClient("gemini-2.5-flash");
IChatClient chatClient =  openAIChatClient.AsIChatClient();   
var wizardTools = new WizardTools();

AIAgent reviewerAgent = chatClient.AsAIAgent(
    name: "Arthur",
     instructions: "Sen kıdemli bir kod denetleyicisisin (Code Reviewer). Sana gelen kodları güvenlik, performans ve okunurluk açısından incele. Eğer kod mükemmelse cevabının en sonuna sadece 'ONAYLANDI' yaz. Eksik varsa eksikleri maddeler halinde yaz ve kesinlikle 'ONAYLANDI' yazma."
);

AIAgent agent = chatClient.AsAIAgent(
    name: "Merlin",
     instructions: "Sen Merlin adında C# ve .NET uzmanı bir yazılımcı ajansın. Kullanıcının isteklerine göre temiz, derlenebilir ve güvenli C# kodları yazmalısın. Eğer sana bir kod denetim raporu (hata raporu) gelirse, o hataları inceleyip kodunu baştan güncelleyerek düzeltmelisin."
    ,tools: [
        AIFunctionFactory.Create(wizardTools.GetCurrentDateTime),
        AIFunctionFactory.Create(wizardTools.SumNumbers),
        AIFunctionFactory.Create(wizardTools.ListFiles) 
    ]
);
AgentSession coderSession;
AgentSession reviewerSession;

if(File.Exists("coder_session.json"))
{
  Console.WriteLine("💾 Eski Merlin oturumu dosyadan yükleniyor...");
  string json=await File.ReadAllTextAsync("coder_session.json"); 
  using JsonDocument doc = JsonDocument.Parse(json);
  coderSession =await agent.DeserializeSessionAsync(doc.RootElement);
}
else
{
    coderSession = await agent.CreateSessionAsync();
}

if (File.Exists("reviewer_session.json"))
{
    Console.WriteLine("💾 Eski Arthur oturumu dosyadan yükleniyor... ");
    string json = await File.ReadAllTextAsync("reviewer_session.json");
    using JsonDocument doc = JsonDocument.Parse(json);
    reviewerSession = await reviewerAgent.DeserializeSessionAsync(doc.RootElement);
}
else
{
    reviewerSession = await reviewerAgent.CreateSessionAsync();
}
// 8. Ana iş akışı döngüsü
while (true)
{
    Console.Write("\nYazılacak C# özelliğini girin (Çıkış için 'exit' yazın): ");
    string? request = Console.ReadLine();

    if (string.IsNullOrEmpty(request) || request.Trim().ToLower() == "exit")
    {
        Console.WriteLine("💾 Ajanların hafızası kaydediliyor...");
        
        JsonElement coderState = await agent.SerializeSessionAsync(coderSession);
        await File.WriteAllTextAsync("coder_session.json", coderState.ToString());
        JsonElement reviewerState = await reviewerAgent.SerializeSessionAsync(reviewerSession);
        await File.WriteAllTextAsync("reviewer_session.json", reviewerState.ToString());
        Console.WriteLine("Yazılım ofisi kapatıldı. İyi günler!");
        break;
    }

    string currentPayload = request;
    bool isApproved = false;
    int maxTurns = 3; // Yazılımcı ve testçi en fazla 3 kere paslaşsın

    try
    {
        for (int turn = 1; turn <= maxTurns; turn++)
        {
            // Adım A: Merlin (Yazılımcı) Kod Yazıyor
            Console.WriteLine($"\n--- [Tur {turn}] Merlin Kod Yazıyor... ---");
            AgentResponse coderResponse = await agent.RunAsync(currentPayload,coderSession);
            string code = coderResponse.Text;
            Console.WriteLine($"\n[Merlin'in Yanıtı]:\n{code}");

            // Adım B: Arthur (Denetleyici) Kodu İnceliyor
            Console.WriteLine($"\n--- [Tur {turn}] Arthur Kodu İnceliyor... ---");
            await Task.Delay(4000); 
            AgentResponse reviewerResponse = await reviewerAgent.RunAsync(code, reviewerSession);
            string feedback = reviewerResponse.Text;
            Console.WriteLine($"\n[Arthur'un Yanıtı]:\n{feedback}");

            // Adım C: Onay kontrolü
            if (feedback.Contains("ONAYLANDI"))
            {
                Console.WriteLine("\n🎉 Harika! Kod, Arthur tarafından ONAYLANDI ve yayına hazır!");
                isApproved = true;
                break; // Onaylandıysa döngüden çık
            }
            else
            {
                Console.WriteLine("\n❌ Kod onaylanmadı! Merlin geri bildirimlere göre kodu düzeltecek...");
                
                // Geri bildirimi Merlin'e besliyoruz
                currentPayload = $"Yazdığın kod Arthur tarafından reddedildi.\nVerdiği geri bildirim:\n{feedback}\n\nLütfen bu geri bildirime göre kodu tekrar yaz.";
            }
        }

        if (!isApproved)
        {
            Console.WriteLine("\n⚠️ 3 tur sonunda kod onaylanamadı. İnsan müdahalesi gerekiyor!");
        }
    }
    catch (Exception ex)
    {
        // İki ajandan biri çalışırken hata alırsak programın çökmesini engelliyoruz
        Console.WriteLine($"\n[Hata Oluştu]: {ex.Message}");
    }
}

public class WizardTools
{
     [Description("Sistemin güncel tarih ve saat bilgisini verir.")]
     public string GetCurrentDateTime()
     {
          return DateTime.Now.ToString("dd MMMM yyyy HH:mm:ss");

     }

     [Description("Verilen iki tamsayıyı matematiksel olarak toplar.")]
    public int SumNumbers(int number1, int number2)
    {
        return number1 + number2;
    }
        // Araç 3: Belirtilen klasördeki dosyaları listeleyen araç
    [Description("Belirtilen klasör yolundaki (directory path) dosyaların listesini verir.")]
    public string ListFiles(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                return $"Hata: '{directoryPath}' adında bir klasör bulunamadı.";
            }

            string[] files = Directory.GetFiles(directoryPath);
            // Sadece dosya isimlerini alıp birleştiriyoruz
            var fileNames = new System.Collections.Generic.List<string>();
            foreach (var file in files)
            {
                fileNames.Add(System.IO.Path.GetFileName(file));
            }
            return "Klasördeki Dosyalar:\n" + string.Join("\n", fileNames);
        }
        catch (Exception ex)
        {
            return $"Hata: {ex.Message}";
        }
    }
}