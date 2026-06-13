using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using _04_AgentWebAPI;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// 1. Chat Client (Gemini Bağlantı Beyni) Kaydı
builder.Services.AddSingleton<IChatClient>(sp =>
{
    string apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
    if (string.IsNullOrEmpty(apiKey))
    {
        Console.WriteLine("⚠️ WARNING: GEMINI_API_KEY environment variable is not set!");
    }

    Uri endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/");
    var options = new OpenAIClientOptions { Endpoint = endpoint };
    var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
    ChatClient openAIChatClient = client.GetChatClient("gemini-2.5-flash");   
    return openAIChatClient.AsIChatClient();   
});

// 2. Keyed Services ile Ajanlarımızın Kayıtları
builder.Services.AddKeyedSingleton<AIAgent>("Merlin", (sp, key) =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    return chatClient.AsAIAgent(
        name: "Merlin",
        instructions: "Sen Merlin adında C# ve .NET uzmanı bir yazılımcı ajansın. Kullanıcının isteklerine göre temiz, derlenebilir ve güvenli C# kodları yazmalısın. Eğer sana bir kod denetim raporu (hata raporu) gelirse, o hataları inceleyip kodunu baştan güncelleyerek düzeltmelisin."
    );
});

builder.Services.AddKeyedSingleton<AIAgent>("Arthur", (sp, key) =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    return chatClient.AsAIAgent(
        name: "Arthur",
        instructions: "Sen kıdemli bir kod denetleyicisisin (Code Reviewer). Sana gelen kodları güvenlik, performans ve okunurluk açısından incele. Eğer kod mükemmelse cevabının en sonuna sadece 'ONAYLANDI' yaz. Eksik varsa eksikleri maddeler halinde yaz ve kesinlikle 'ONAYLANDI' yazma."
    );
});

// 3. Kurumsal Kuyruk ve Orkestrasyon Servis Kayıtları
builder.Services.AddSingleton<AgentJobQueue>();
builder.Services.AddSingleton<JobStatusStore>();
builder.Services.AddSingleton<AgentOrchestrator>();
builder.Services.AddHostedService<AgentWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Varsayılan Hava Durumu Endpoint'i
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// ENDPOINT 1: Asenkron Ajan Akışı Tetikleme (Kuyruğa Atar)
app.MapPost("/api/chat/async", async (
    [FromBody] ChatRequest request,
    [FromServices] AgentJobQueue jobQueue,
    [FromServices] JobStatusStore statusStore) =>
{
    if (string.IsNullOrEmpty(request.Prompt))
    {
        return Results.BadRequest("Prompt boş olamaz.");
    }

    string jobId = Guid.NewGuid().ToString("N");
    string sessionId = request.SessionId ?? "default";

    // Kuyruğa eklemeden önce durum havuzunda işi kaydet
    statusStore.CreateJob(jobId, sessionId, request.Prompt);

    var job = new AgentJob(jobId, sessionId, request.Prompt);
    await jobQueue.EnqueueAsync(job);

    return Results.Accepted($"/api/chat/status/{jobId}", new { JobId = jobId, Status = "Pending" });
});

// ENDPOINT 2: İnsan Onayı / Karar Bildirme (Human-in-the-loop)
app.MapPost("/api/chat/approve/{jobId}", async (
    [FromRoute] string jobId,
    [FromBody] HumanApprovalInput input,
    [FromServices] AgentJobQueue jobQueue,
    [FromServices] JobStatusStore statusStore) =>
{
    JobResult? jobResult = statusStore.GetJobStatus(jobId);
    if (jobResult == null)
    {
        return Results.NotFound($"JobId {jobId} bulunamadı.");
    }

    // İşin durumunun gerçekten onay bekleniyor durumunda olması gerekir
    if (jobResult.CurrentState != WorkflowState.WaitingForHumanApproval)
    {
        return Results.BadRequest($"Bu iş onay bekleyen bir durumda değil. Güncel Durum: {jobResult.CurrentState}");
    }

    // İnsanın kararını iş sonucuna kaydet
    jobResult.HumanDecision = input;

    // Arka plan işçisini uykudan uyandırmak için işi kuyruğa tekrar itiyoruz!
    var job = new AgentJob(jobResult.JobId, jobResult.SessionId, jobResult.Prompt);
    await jobQueue.EnqueueAsync(job);

    return Results.Ok(new 
    { 
        Message = input.Approved ? "Onay kaydedildi, süreç devam ettiriliyor." : "Red kaydedildi, Merlin kodu tekrar düzenleyecek.",
        JobId = jobId,
        CurrentState = jobResult.CurrentState
    });
});

// ENDPOINT 3: İş Durumu Sorgulama
app.MapGet("/api/chat/status/{jobId}", (
    [FromRoute] string jobId,
    [FromServices] JobStatusStore statusStore) =>
{
    var status = statusStore.GetJobStatus(jobId);
    if (status == null)
    {
        return Results.NotFound($"JobId {jobId} bulunamadı.");
    }

    return Results.Ok(status);
});

app.Run();

// DTO Sınıfları
record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public class ChatRequest
{
    public string? SessionId { get; set; }
    public string? Prompt { get; set; }
}