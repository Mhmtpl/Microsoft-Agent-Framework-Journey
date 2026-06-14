using System;
using System.Diagnostics;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using Microsoft.Extensions.Configuration;


class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("📊 OpenTelemetry Gözlemlenebilirlik (Observability) Demosu");
        Console.WriteLine("========================================================\n");

        // 1. OpenTelemetry İzleyici (Tracer) Yapılandırması
        // Ajanın ürettiği verileri yakalamak için hem kendi özel kaynağımızı 
        // hem de .NET'in varsayılan deneysel AI kaynaklarını dinliyoruz.
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("MyAIApplication") // Bizim özel tanımladığımız kaynak
            .AddSource("Microsoft.Extensions.AI") // Varsayılan kaynak
            .AddSource("Experimental.Microsoft.Extensions.AI") // Deneysel sürüm kaynağı
            .AddConsoleExporter() // Yakalanan verileri konsola (terminale) yazdır
            .Build();

        // 2. User Secrets ile API Key'i Okuma
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        string apiKey = config["GEMINI_API_KEY"] ?? "";
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("⚠️ WARNING: GEMINI_API_KEY could not be loaded from User Secrets!");
            return;
        }


        // 3. Chat Client Kurulumu (Gemini bağlantısı ve Telemetry Middleware Entegrasyonu)
        Uri endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/");
        var options = new OpenAIClientOptions { Endpoint = endpoint };
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        
        // ChatClientBuilder ile pipeline oluşturup .UseOpenTelemetry() adımını ekliyoruz.
        // sourceName parametresi ile hangi kanaldan yayın yapacağını belirliyoruz.
        var chatClient = new ChatClientBuilder(client.GetChatClient("gemini-2.5-flash").AsIChatClient())
            .UseOpenTelemetry(sourceName: "MyAIApplication")
            .Build();



        Console.WriteLine("🔄 Gemini'ye istek gönderiliyor...");

        // 4. İstek Gönderimi (Arka planda OpenTelemetry bu işlemi izler, süreyi ve tokenları kaydeder)
        try
        {
            var response = await chatClient.GetResponseAsync("C# nedir, en fazla 5 kelimeyle açıkla.");
            Console.WriteLine($"\n🤖 Ajan Yanıtı: {response.Text}\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ İstek Hata Aldı: {ex.Message}\n");
        }
        
        Console.WriteLine("========================================================");
        Console.WriteLine("Yukarıdaki çıktıları inceleyin. OpenTelemetry, harcanan süreyi (Duration), ");
        Console.WriteLine("olası hata durumlarını (error.type) veya başarı durumunu otomatik yakaladı!");

    }
}
