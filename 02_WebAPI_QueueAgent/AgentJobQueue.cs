using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace _04_AgentWebAPI;

// 1. Akışın o anki durumunu belirten Enum
public enum WorkflowState
{
    NotStarted,
    MerlinCoding,
    ArthurReviewing,
    WaitingForHumanApproval,
    Approved,
    Failed
}

// 2. Kuyruğa atılacak iş modeli
public record AgentJob(string JobId, string SessionId, string Prompt);

// 3. Ajanların cevabını saklayacağımız model
public class AgentChatResponse
{
    public string? SessionId { get; set; }
    public bool IsApproved { get; set; }
    public string? Code { get; set; }
    public string? Feedback { get; set; }
}

// 4. İnsanın vereceği onay/red kararı modeli
public record HumanApprovalInput(bool Approved, string? Comment);

// 5. İşin durumunu ve tüm süreç geçmişini tutan ana sınıf
public class JobResult
{
    public string JobId { get; init; }
    public string SessionId { get; init; }
    public string Prompt { get; init; }
    public WorkflowState CurrentState { get; set; } = WorkflowState.NotStarted;
    
    // Süreç çıktıları
    public string Code { get; set; } = "";
    public string Feedback { get; set; } = "";
    public int Turn { get; set; } = 1;
    
    // İnsanın kararı
    public HumanApprovalInput? HumanDecision { get; set; }
    public string? ErrorMessage { get; set; }

    public JobResult(string jobId, string sessionId, string prompt)
    {
        JobId = jobId;
        SessionId = sessionId;
        Prompt = prompt;
    }
}

// 6. Bellek içi kuyruk yönetim sınıfı (Channel)
public class AgentJobQueue
{
    private readonly Channel<AgentJob> _channel = Channel.CreateUnbounded<AgentJob>();

    public async ValueTask EnqueueAsync(AgentJob job)
    {
        await _channel.Writer.WriteAsync(job);
    }

    public async ValueTask<AgentJob> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }
}

// 7. İş durumlarını bellek içi tutacak havuz (Thread-safe ConcurrentDictionary)
public class JobStatusStore
{
    private readonly ConcurrentDictionary<string, JobResult> _jobs = new();

    // Yeni iş yaratma
    public void CreateJob(string jobId, string sessionId, string prompt)
    {
        _jobs[jobId] = new JobResult(jobId, sessionId, prompt);
    }

    // İş durumunu güncelleme
    public void UpdateJob(JobResult jobResult)
    {
        _jobs[jobResult.JobId] = jobResult;
    }

    public JobResult? GetJobStatus(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var result) ? result : null;
    }
}