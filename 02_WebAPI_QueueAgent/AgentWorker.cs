using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace _04_AgentWebAPI;

public class AgentWorker : BackgroundService
{
    private readonly AgentJobQueue _jobQueue;
    private readonly JobStatusStore _statusStore;
    private readonly AgentOrchestrator _orchestrator;
    private readonly ILogger<AgentWorker> _logger;

    public AgentWorker(
        AgentJobQueue jobQueue,
        JobStatusStore statusStore,
        AgentOrchestrator orchestrator,
        ILogger<AgentWorker> logger)
    {
        _jobQueue = jobQueue;
        _statusStore = statusStore;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AgentWorker arka plan servisi başlatıldı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            AgentJob? job = null;
            try
            {
                // Kuyruktan sıradaki işi çek
                job = await _jobQueue.DequeueAsync(stoppingToken);
                _logger.LogInformation("Yeni iş kuyruktan alındı. JobId: {JobId}", job.JobId);

                // Havuzdan işin durum nesnesini al (yoksa oluştur)
                JobResult? jobResult = _statusStore.GetJobStatus(job.JobId);
                if (jobResult == null)
                {
                    _statusStore.CreateJob(job.JobId, job.SessionId, job.Prompt);
                    jobResult = _statusStore.GetJobStatus(job.JobId)!;
                }

                // Eğer durum daha önce askıya alınmışsa, onu çalışıyor (Running) durumuna çekip devam ettir
                if (jobResult.CurrentState == WorkflowState.NotStarted || 
                    jobResult.CurrentState == WorkflowState.WaitingForHumanApproval)
                {
                    // Durum makinesini çalıştır
                    await _orchestrator.ExecuteWorkflowAsync(jobResult, stoppingToken);

                    // Çalışma sonrasındaki güncel duruma göre havuzu güncelle
                    _statusStore.UpdateJob(jobResult);

                    if (jobResult.CurrentState == WorkflowState.WaitingForHumanApproval)
                    {
                        _logger.LogWarning("İş ASKIDA - İnsan onayı bekleniyor. JobId: {JobId}", job.JobId);
                    }
                    else if (jobResult.CurrentState == WorkflowState.Approved)
                    {
                        _logger.LogInformation("İş onaylandı ve BAŞARIYLA TAMAMLANDI. JobId: {JobId}", job.JobId);
                    }
                    else if (jobResult.CurrentState == WorkflowState.Failed)
                    {
                        _logger.LogError("İş BAŞARISIZ OLDU. Hata: {Error}, JobId: {JobId}", jobResult.ErrorMessage, job.JobId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İş işlenirken hata oluştu. JobId: {JobId}", job?.JobId);
                if (job != null)
                {
                    JobResult? jobResult = _statusStore.GetJobStatus(job.JobId);
                    if (jobResult != null)
                    {
                        jobResult.CurrentState = WorkflowState.Failed;
                        jobResult.ErrorMessage = ex.Message;
                        _statusStore.UpdateJob(jobResult);
                    }
                }
            }
        }
    }
}