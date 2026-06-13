using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;

namespace _04_AgentWebAPI;

public class AgentOrchestrator
{
    private readonly AIAgent _merlin;
    private readonly AIAgent _arthur;

    public AgentOrchestrator(
        [FromKeyedServices("Merlin")] AIAgent merlin,
        [FromKeyedServices("Arthur")] AIAgent arthur)
    {
        _merlin = merlin;
        _arthur = arthur;
    }

    public async Task ExecuteWorkflowAsync(JobResult job, CancellationToken cancellationToken)
    {
        string coderSessionFile = $"session_coder_{job.SessionId}.json";
        string reviewerSessionFile = $"session_reviewer_{job.SessionId}.json";

        AgentSession coderSession;
        AgentSession reviewerSession;

        // Oturumları yükle veya sıfırdan oluştur
        if (File.Exists(coderSessionFile))
        {
            string json = await File.ReadAllTextAsync(coderSessionFile, cancellationToken);
            using JsonDocument doc = JsonDocument.Parse(json);
            coderSession = await _merlin.DeserializeSessionAsync(doc.RootElement, cancellationToken: cancellationToken);
        }
        else
        {
            coderSession = await _merlin.CreateSessionAsync(cancellationToken);
        }

        if (File.Exists(reviewerSessionFile))
        {
            string json = await File.ReadAllTextAsync(reviewerSessionFile, cancellationToken);
            using JsonDocument doc = JsonDocument.Parse(json);
            reviewerSession = await _arthur.DeserializeSessionAsync(doc.RootElement, cancellationToken: cancellationToken);
        }
        else
        {
            reviewerSession = await _arthur.CreateSessionAsync(cancellationToken);
        }

        // DURUM MAKİNESİ (STATE MACHINE) DÖNGÜSÜ
        bool isExecutionPaused = false;

        while (!isExecutionPaused && !cancellationToken.IsCancellationRequested)
        {
            switch (job.CurrentState)
            {
                case WorkflowState.NotStarted:
                    // Akışı Merlin ile başlatıyoruz
                    job.CurrentState = WorkflowState.MerlinCoding;
                    break;

                case WorkflowState.MerlinCoding:
                    if (job.Turn > 3)
                    {
                        job.CurrentState = WorkflowState.Failed;
                        job.ErrorMessage = "Maksimum deneme (3 tur) sınırına ulaşıldı ve kod onaylanmadı.";
                        isExecutionPaused = true;
                        break;
                    }

                    // Merlin'e gönderilecek girdi (eğer ilk tur ise prompt, değilse hata geri bildirimi)
                    string merlinInput = job.Turn == 1 && string.IsNullOrEmpty(job.Feedback)
                        ? job.Prompt 
                        : job.Feedback;

                    // Merlin kod yazar
                    AgentResponse coderResponse = await _merlin.RunAsync(merlinInput, coderSession, cancellationToken: cancellationToken);
                    job.Code = coderResponse.Text;

                    // Arthur'un incelemesi için durumu değiştir
                    job.CurrentState = WorkflowState.ArthurReviewing;
                    break;

                case WorkflowState.ArthurReviewing:
                    await Task.Delay(3000, cancellationToken); // Rate Limit önlemi

                    // Arthur kod inceler
                    AgentResponse reviewerResponse = await _arthur.RunAsync(job.Code, reviewerSession, cancellationToken: cancellationToken);
                    job.Feedback = reviewerResponse.Text;

                    if (job.Feedback.Contains("ONAYLANDI"))
                    {
                        // Kod teknik olarak onaylandı, şimdi insan (yönetici) onayına sunuyoruz!
                        job.CurrentState = WorkflowState.WaitingForHumanApproval;
                    }
                    else
                    {
                        // Reddedildiyse tur sayısını artır ve Merlin'e geri yolla
                        job.Turn++;
                        job.Feedback = $"Yazdığın kod Arthur tarafından reddedildi.\nGeri bildirim:\n{job.Feedback}\n\nLütfen kodu buna göre düzelt.";
                        job.CurrentState = WorkflowState.MerlinCoding;
                    }
                    break;

                case WorkflowState.WaitingForHumanApproval:
                    // Eğer insan kararı henüz gelmediyse çalışmayı ASKIDA (suspends) bırakıp çıkıyoruz
                    if (job.HumanDecision == null)
                    {
                        isExecutionPaused = true;
                        break;
                    }

                    if (job.HumanDecision.Approved)
                    {
                        // İnsan onayladıysa süreç başarıyla biter
                        job.CurrentState = WorkflowState.Approved;
                    }
                    else
                    {
                        // İnsan reddettiyse, insanın yorumlarını Merlin'e besleyip döngüyü MerlinCoding'e geri sarıyoruz
                        job.Turn++;
                        job.Feedback = $"Kod İnsan Yönetici tarafından reddedildi!\nYöneticinin Notu: {job.HumanDecision.Comment}\n\nLütfen kodu bu talimata göre tekrar düzenle.";
                        job.HumanDecision = null; // Kararı sıfırlıyoruz ki bir sonraki onayda yeni karar alınabilsin
                        job.CurrentState = WorkflowState.MerlinCoding;
                    }
                    break;

                case WorkflowState.Approved:
                case WorkflowState.Failed:
                    // Son durumlar, çalışmayı bitir
                    isExecutionPaused = true;
                    break;
            }
        }

        // Oturumları kaydet
        JsonElement coderState = await _merlin.SerializeSessionAsync(coderSession, cancellationToken: cancellationToken);
        await File.WriteAllTextAsync(coderSessionFile, coderState.ToString(), cancellationToken);

        JsonElement reviewerState = await _arthur.SerializeSessionAsync(reviewerSession, cancellationToken: cancellationToken);
        await File.WriteAllTextAsync(reviewerSessionFile, reviewerState.ToString(), cancellationToken);
    }
}