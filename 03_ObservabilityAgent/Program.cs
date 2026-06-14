using System;
using System.Diagnostics;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using Microsoft.Extensions.Configuration;
using System.Linq;


class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("📊 OpenTelemetry Gözlemlenebilirlik (Observability) Demosu");
        Console.WriteLine("========================================================\n");

        // 1. OpenTelemetry İzleyici (Tracer) Yapılandırması (Özel Konsol Tasarımı ile)
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("MyAIApplication") // Bizim özel tanımladığımız kaynak
            .AddSource("Microsoft.Extensions.AI") // Varsayılan kaynak
            .AddSource("Experimental.Microsoft.Extensions.AI") // Deneysel sürüm kaynağı
            .AddProcessor(new SimpleActivityExportProcessor(new CustomConsoleExporter()))
            .Build();

        // 2. User Secrets ile API Key'i Okuma
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        // Çevre değişkeni veya Secrets üzerinden anahtarı çek
        string apiKey = config["GEMINI_API_KEY"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("⚠️ WARNING: GEMINI_API_KEY could not be loaded from User Secrets or Environment Variables!");
            return;
        }

        // 3. Chat Client Kurulumu (Gemini bağlantısı ve Telemetry Middleware Entegrasyonu)
        Uri endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/");
        var options = new OpenAIClientOptions { Endpoint = endpoint };
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        
        var chatClient = new ChatClientBuilder(client.GetChatClient("gemini-2.5-flash").AsIChatClient())
            .UseOpenTelemetry(sourceName: "MyAIApplication")
            .Build();

        Console.WriteLine("💬 Yapay Zeka ile Sohbet Başladı (Çıkmak için 'q' veya 'exit' yazın)");
        Console.WriteLine("====================================================================\n");

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("👤 Siz: ");
            Console.ResetColor();

            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) || 
                input.Equals("q", StringComparison.OrdinalIgnoreCase) || 
                input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("\nSohbet sonlandırıldı. İyi günler! 👋");
                break;
            }

            Console.WriteLine("🔄 Yanıt bekleniyor...");

            // 4. İstek Gönderimi (Arkaplanda OpenTelemetry bunu izleyecek)
            try
            {
                var response = await chatClient.GetResponseAsync(input);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"🤖 Ajan: {response.Text}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ İstek Hata Aldı: {ex.Message}");
                Console.ResetColor();
            }
            
            Console.WriteLine("\n────────────────────────────────────────────────────────");
        }
    }
}

// 5. Görsel Açıdan Zengin Özel OpenTelemetry Konsol Çıktısı Sağlayıcı
public class CustomConsoleExporter : BaseExporter<Activity>
{
    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var activity in batch)
        {
            // Sadece AI çağrılarını veya kendi uygulamamızı yakalayalım
            if (activity.Source.Name != "MyAIApplication" && activity.Source.Name != "Microsoft.Extensions.AI")
                continue;

            var tags = activity.TagObjects.ToDictionary(t => t.Key, t => t.Value);

            string model = (tags.TryGetValue("gen_ai.request.model", out var m) ? m?.ToString() : null) ?? activity.DisplayName ?? "Bilinmiyor";
            string server = (tags.TryGetValue("server.address", out var s) ? s?.ToString() : null) ?? "Bilinmiyor";
            string duration = $"{activity.Duration.TotalSeconds:F2} sn";
            string traceId = activity.TraceId.ToString();

            // Token verilerini çekelim
            int inputTokens = 0;
            int outputTokens = 0;
            if (tags.TryGetValue("gen_ai.usage.input_tokens", out var it) && it != null) inputTokens = Convert.ToInt32(it);
            if (tags.TryGetValue("gen_ai.usage.output_tokens", out var ot) && ot != null) outputTokens = Convert.ToInt32(ot);
            int totalTokens = inputTokens + outputTokens;

            bool isSuccess = activity.Status != ActivityStatusCode.Error;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("┌────────────────────────────────────────────────────────┐");
            PrintLine("📊 OPENTELEMETRY AI SPAN REPORT", "", ConsoleColor.Cyan);
            Console.WriteLine("├────────────────────────────────────────────────────────┤");
            Console.ResetColor();

            PrintLine("Model:    ", model);
            PrintLine("Duration: ", duration);
            PrintLine("Server:   ", server);
            PrintLine("Trace ID: ", traceId);

            if (isSuccess && totalTokens > 0)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("├────────────────────────────────────────────────────────┤");
                PrintLine("📈 TOKEN USAGE", "", ConsoleColor.Cyan);
                Console.ResetColor();
                PrintLine("Input:    ", $"{inputTokens} tokens");
                PrintLine("Output:   ", $"{outputTokens} tokens");
                PrintLine("Total:    ", $"{totalTokens} tokens");
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("├────────────────────────────────────────────────────────┤");
            Console.ResetColor();

            if (isSuccess)
            {
                PrintLine("Status:   ", "Success", ConsoleColor.Green);
            }
            else
            {
                string errorType = (tags.TryGetValue("error.type", out var et) ? et?.ToString() : null) ?? "Bilinmiyor";
                if (errorType.Contains("."))
                {
                    errorType = errorType.Split('.').Last() ?? "Bilinmiyor"; // Namespace kısmını kırparak temizle
                }
                PrintLine("Status:   ", $"Error ({errorType})", ConsoleColor.Red);
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("└────────────────────────────────────────────────────────┘");
            Console.ResetColor();
            Console.WriteLine();
        }
        return ExportResult.Success;
    }

    private static void PrintLine(string label, string value, ConsoleColor? color = null)
    {
        int maxLength = 54;
        string line = $"{label}{value}";
        if (line.Length > maxLength)
        {
            line = line.Substring(0, maxLength - 3) + "...";
        }
        string padded = line.PadRight(maxLength);
        Console.Write("│ ");
        if (color.HasValue) Console.ForegroundColor = color.Value;
        Console.Write(padded);
        Console.ResetColor();
        Console.WriteLine(" │");
    }
}
