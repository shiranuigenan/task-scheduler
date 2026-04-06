using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskScheduler.Application.Abstractions;
using TaskScheduler.Application.Jobs;
using TaskScheduler.Domain.Entities;

namespace TaskScheduler.Infrastructure.Scheduling;

/// <summary>
/// Bu sınıf uygulama ayağa kalktığında arka planda çalışan "planlayıcı" servisidir.
/// Görevi; periyodik olarak DB'den çalışmaya hazır (due) task'ları çeker, aynı grup anahtarına sahip
/// task'ların paralel çalışmasını engeller (group lock), her task çalıştırmasını ayrı bir DI scope içinde
/// yapar ve başarısızlıklarda retry uygular.
///
/// Önemli davranışlar:
/// - Polling aralığında (örn. 15 sn) çalışır.
/// - Due task'lar için group lock alınır; lock alınamazsa task bu turda atlanır.
/// - Task çalışması `RunTaskSafeAsync` ile başlatılır (await edilmeden başlatıldığı için birden fazla task
///   aynı anda çalışabilir; ancak group aynıysa lock nedeniyle aynı anda çalışmaları engellenir).
/// - Lock, task yürütmesi bitene kadar tutulur; hata olsa bile lock uygun şekilde serbest bırakılır.
/// - Task zaman çizelgesi (`LastRunAt`, `NextRunAt`) her koşulda güncellenir; yani bir handler bulunamazsa
///   veya hatalar retry'lara rağmen sonuçlanırsa bile sonraki çalışma zamanı DB'ye yazılır.
/// </summary>
public sealed class TaskSchedulerBackgroundService(
    IServiceScopeFactory scopeFactory,
    GroupLockManager groupLockManager,
    ILogger<TaskSchedulerBackgroundService> logger) : BackgroundService
{
    // TaskSchedulerBackgroundService'in DB'ye kaç saniyede bir yoklama yapacağını belirler.
    // Bu değer; istek yoğunluğunu ve "due task" gecikmesini doğrudan etkiler.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    // BackgroundService'in ana döngüsü:
    // Uygulama kapanana kadar tekrar tekrar PollOnceAsync çağırır.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Task scheduler started; polling every {Seconds}s", PollInterval.TotalSeconds);

        // stoppingToken iptal edilene kadar polling devam eder.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // DB'den due task'ları çekip ilgili group lock'ları uygulayarak çalışmaları başlatıyoruz.
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Uygulama kapanırken beklenen bir iptal olduğu için döngüyü sessizce sonlandırıyoruz.
                break;
            }
            catch (Exception ex)
            {
                // Tek bir polling turunda beklenmedik bir hata olursa bütün servis ölmesin diye loglanıp
                // sonraki poll turuna geçiyoruz.
                logger.LogError(ex, "Scheduler poll cycle failed");
            }

            try
            {
                // Bir sonraki polling turuna kadar bekle.
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Bekleme sırasında iptal geldiyse çıkıyoruz.
                break;
            }
        }

        // Servis durdurulurken bilgi log'u.
        logger.LogInformation("Task scheduler stopped");
    }

    // Tek bir polling turu için çalışır:
    // - Poll scope oluşturur
    // - IScheduledTaskRepository üzerinden due task'ları çeker
    // - Her due task için group lock alır
    // - Lock alınabilirse task çalıştırmasını (retry dahil) başlatır
    private async Task PollOnceAsync(CancellationToken stoppingToken)
    {
        // Bu tur yalnızca "task tespiti" için kullanılır; bu yüzden ayrı bir scope açıyoruz.
        // Böylece DbContext ve diğer servislerin yaşam döngüsü düzgün yönetilir.
        using var pollScope = scopeFactory.CreateScope();
        var repository = pollScope.ServiceProvider.GetRequiredService<IScheduledTaskRepository>();
        var due = await repository.GetDueTasksAsync(stoppingToken).ConfigureAwait(false);

        // Due task listesindeki her task için:
        // - GroupKey belirlenir
        // - Group lock alınır
        // - Lock varsa task yürütmesi başlatılır
        foreach (var snapshot in due)
        {
            var groupKey = ResolveGroupKey(snapshot);

            // Aynı groupKey'e sahip bir task daha çalışıyorsa lock alınamayacağı için bu task atlanır.
            var lease = await groupLockManager.TryAcquireAsync(groupKey, stoppingToken).ConfigureAwait(false);
            if (lease is null)
                continue;

            // Lock lease'ine sahip olduğumuz için bu task'ın çalışmasını güvenli şekilde başlatıyoruz.
            var taskId = snapshot.Id;

            // Not: await edilmiyor. Bu sayede birden fazla task aynı anda çalışabilir.
            // Ancak aynı groupKey'ler için lock mekanizması seri çalışmayı garanti eder.
            _ = RunTaskSafeAsync(taskId, lease, stoppingToken);
        }
    }

    // GroupKey yoksa (null/boş) task'ın Id'si groupKey olarak kullanılır.
    // Böylece her task kendi "grubunda" bağımsız çalışır.
    private static string ResolveGroupKey(ScheduledTask task) =>
        string.IsNullOrWhiteSpace(task.GroupKey) ? task.Id.ToString() : task.GroupKey.Trim();

    // Lock lease'i güvenli şekilde dispose etmek için "try/catch + finally benzeri" bir sarmalayıcıdır.
    // Amaç: Task çalışırken hata oluşsa bile group lock'ın serbest kalmasını garanti etmektir.
    private async Task RunTaskSafeAsync(Guid taskId, IAsyncDisposable groupLease, CancellationToken stoppingToken)
    {
        try
        {
            // Lease süresince lock tutulur; execute sonrası dispose ile lock serbest bırakılır.
            await using (groupLease)
            {
                // Asıl task yürütme mantığı.
                await RunTaskCoreAsync(taskId, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Uygulama kapanırken iptal beklenen bir durumdur; log spam yapmamak için sessiz geçiyoruz.
        }
        catch (Exception ex)
        {
            // Beklenmedik bir hata oluşursa loglanır.
            // Lock dispose edildiği için başka task'ların çalışması etkilenmez.
            logger.LogError(ex, "Unhandled error running scheduled task {TaskId}", taskId);
        }
    }

    // Task'ın gerçek yürütmesini yapar:
    // - Yeni async scope açar (her execution ayrı scope)
    // - Task'ı DB'den yeniden okur
    // - Task aktif değilse veya NextRunAt henüz dolmadıysa işlem yapmaz
    // - JobFactory üzerinden handler'ı çözer
    // - RetryCount kadar deneme yapar
    // - Sonrasında zaman planını güncelleyip DB'ye yazar
    private async Task RunTaskCoreAsync(Guid taskId, CancellationToken stoppingToken)
    {
        // Her execution'ın kendi scope'u olması; DbContext ve diğer servislerin thread-safety ve yaşam döngüsü açısından doğru olmasını sağlar.
        await using var execScope = scopeFactory.CreateAsyncScope();
        var sp = execScope.ServiceProvider;
        var repository = sp.GetRequiredService<IScheduledTaskRepository>();
        var jobFactory = sp.GetRequiredService<IJobFactory>();
        var log = sp.GetRequiredService<ILogger<TaskSchedulerBackgroundService>>();

        // Task'ı execute anında tekrar DB'den çekiyoruz.
        // Bu; aynı task'ın paralel olarak güncellenmesi gibi senaryolarda daha tutarlı davranmamızı sağlar.
        var task = await repository.GetByIdAsync(taskId, stoppingToken).ConfigureAwait(false);
        if (task is null || !task.IsActive)
            return;

        var now = DateTime.UtcNow;

        // PollOnceAsync turunda "due" seçilmiş olsa bile (zaman yarışı) NextRunAt hâlâ gelecekteyse
        // bu execution'ın çalışmasını iptal ediyoruz.
        if (task.NextRunAt > now)
            return;

        // JobName -> handler eşlemesini çözüyoruz.
        var handler = jobFactory.Resolve(task.JobName);
        if (handler is null)
        {
            // Handler bulunamadıysa job'u çalıştıramayız.
            // Ancak kod, schedule'ı ileri alıp DB'ye yazar; böylece task sonsuza kadar due durumda kalmaz.
            log.LogError("No handler registered for job {JobName} (task {TaskId})", task.JobName, taskId);
            AdvanceSchedule(task, now);
            await repository.UpdateAsync(task, stoppingToken).ConfigureAwait(false);
            return;
        }

        // RetryCount 0 ise en az 1 deneme yapılmasını sağlıyoruz.
        var maxAttempts = Math.Max(1, task.RetryCount);
        Exception? lastError = null;

        // Retry döngüsü:
        // - İlk deneme attempt=1
        // - maxAttempts kez deniyoruz
        // - Başarılı olursa döngüden çıkıyoruz
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            // Servis kapanırken job çalışmaya devam etmesin diye iptal kontrolü yapıyoruz.
            stoppingToken.ThrowIfCancellationRequested();
            try
            {
                // Job handler'ın asıl işi burada çalışır.
                // Handler; Task.ParametersJson içerisindeki parametreleri kendi ihtiyaçlarına göre deserialize eder.
                await handler.ExecuteAsync(task.ParametersJson, stoppingToken).ConfigureAwait(false);
                lastError = null;
                break;
            }
            catch (Exception ex)
            {
                lastError = ex;

                // Her başarısız denemeyi uyarı olarak logluyoruz.
                // Son deneme başarısız olursa aşağıda hata log'u basılıyor.
                log.LogWarning(ex, "Job {JobName} task {TaskId} attempt {Attempt}/{Max} failed",
                    task.JobName, taskId, attempt, maxAttempts);
            }
        }

        // Tüm retry'lar başarısız olduysa son hatayı hata log'u olarak bildiriyoruz.
        if (lastError is not null)
            log.LogError(lastError, "Job {JobName} task {TaskId} exhausted retries", task.JobName, taskId);

        // Job başarılı olsun/olmasın: schedule'ı ileri alıp DB'yi güncelliyoruz.
        // Böylece task bir sonraki interval'da tekrar denenecek.
        AdvanceSchedule(task, DateTime.UtcNow);
        await repository.UpdateAsync(task, stoppingToken).ConfigureAwait(false);
    }

    // Task'ın zaman çizelgesini günceller:
    // - LastRunAt: mevcut zaman
    // - NextRunAt: LastRunAt + IntervalMinutes
    private static void AdvanceSchedule(ScheduledTask task, DateTime now)
    {
        task.LastRunAt = now;
        task.NextRunAt = now.AddMinutes(task.IntervalMinutes);
    }
}
