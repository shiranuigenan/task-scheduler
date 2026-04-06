using TaskScheduler.Application.Abstractions;
using TaskScheduler.Application.Dtos;
using TaskScheduler.Application.Jobs;
using TaskScheduler.Domain.Entities;

namespace TaskScheduler.Application.Services;

public sealed class ScheduledTaskService(
    IScheduledTaskRepository repository,
    IJobFactory jobFactory)
{
    // Kullanıcıya ait task'ları DB'den çekip API response formatına dönüştüren servis metodu.
    // Not: "Controller -> Service -> Repository" akışında asıl veri erişimi repository üzerinden yapılır.
    public async Task<IReadOnlyList<TaskResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Repository üzerinden tüm task'ları alıyoruz.
        var items = await repository.GetAllAsync(cancellationToken);
        // Domain entity -> DTO dönüşümü yapıyoruz (Map metodu).
        return items.Select(Map).ToList();
    }

    // Yeni bir ScheduledTask oluşturur.
    // Bu metot iki önemli kontrol yapar:
    // 1) Gönderilen JobName uygulamada kayıtlı bir handler'a karşılık geliyor mu?
    // 2) Request'ten gelen parametreleri ve zamanlama alanlarını entity'ye dönüştürüyor mu?
    public async Task<TaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        // JobFactory üzerinden job adı çözümleniyor.
        // Eğer handler bulunamazsa task oluşturmayı reddediyoruz.
        if (jobFactory.Resolve(request.JobName) is null)
            throw new InvalidOperationException($"Unknown job: '{request.JobName}'.");

        // Task oluşturulduğu an; yeni task'ın "NextRunAt" değeri olarak ayarlanır.
        // Uygulama Activate endpoint'i ile IsActive=true yapılmadıkça BackgroundService çalıştırmayacaktır.
        var now = DateTime.UtcNow;

        // Request içindeki parameters JSON'u; handler'ların kendi ihtiyaçlarına göre deserialize edeceği ham string olarak tutulur.
        // Parameters null gelirse varsayılan "{}" uygulanır.
        var parametersJson = request.Parameters?.GetRawText() ?? "{}";

        // Domain entity örneğini oluşturuyoruz.
        // Entity; DB'ye yazılacak ve scheduler tarafından daha sonra okunacaktır.
        var entity = new ScheduledTask
        {
            // Guid ile benzersiz task kimliği üretilir.
            Id = Guid.NewGuid(),

            // JobName aynen entity'ye taşınır; scheduler tarafında handler seçimi için kullanılır.
            JobName = request.JobName,

            // Parametreler ham JSON string olarak saklanır.
            ParametersJson = parametersJson,

            // Kullanıcının belirttiği periyot dakika cinsinden tutulur.
            IntervalMinutes = request.IntervalMinutes,

            // NextRunAt; task oluşturulduğu an olarak ayarlanır.
            NextRunAt = now,

            // Create sırasında IsActive=false bırakılır.
            // Activate endpoint'i ile kullanıcı task'ı gerçekten çalıştırılabilir hale getirebilir.
            IsActive = false,

            // RetryCount negatif gelirse güvenli olacak şekilde 0'a çekilir.
            RetryCount = Math.Max(0, request.RetryCount),

            // GroupKey boş/boşluksa null kabul edilir.
            // BackgroundService tarafında GroupKey yoksa task Id group anahtarı olarak kullanılacaktır.
            GroupKey = string.IsNullOrWhiteSpace(request.GroupKey) ? null : request.GroupKey.Trim(),
        };

        // Entity'yi DB'ye ekliyoruz.
        await repository.AddAsync(entity, cancellationToken);
        // Kaydedilen entity'yi DTO'ya çevirip geri dönüyoruz.
        return Map(entity);
    }

    // Belirli bir task'ı aktif hale getirir.
    // Activate işlemi şunları etkiler:
    // - IsActive=true
    // - Eğer NextRunAt geçmişte kaldıysa, onu "şu an" seviyesine çekerek hemen çalışmayı tetikleyebilir.
    public async Task ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Task'ı DB'den alıyoruz. Bulamazsak hata fırlatıyoruz.
        var entity = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Task '{id}' was not found.");

        // Task artık scheduler tarafından çalıştırılabilir.
        entity.IsActive = true;
        var now = DateTime.UtcNow;

        // NextRunAt <= now ise; planlanan süre geçmiş olduğu için hemen çalışacak şekilde güncellenir.
        // (Bu sayede bir task oluşturulup uzun süre sonra activate edilirse beklemez.)
        if (entity.NextRunAt <= now)
            entity.NextRunAt = now;

        // Güncellenmiş entity'yi DB'ye yazarız.
        await repository.UpdateAsync(entity, cancellationToken);
    }

    // Belirli bir task'ı siler.
    // Eğer task bulunamazsa KeyNotFoundException fırlatılır.
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Repository.DeleteAsync geri dönüş değeri: true/false
        // - true: silme başarılı
        // - false: task bulunamadı
        if (!await repository.DeleteAsync(id, cancellationToken))
            throw new KeyNotFoundException($"Task '{id}' was not found.");
    }

    // Domain entity -> DTO dönüşümü.
    // Burada basit bir property eşlemesi yapılıyor.
    // ScheduledTask entity'si, API tarafında dönülecek TaskResponse DTO'suna çevrilir.
    // Scheduler/Controller katmanları Domain entity'yi direkt görmemeli; DTO bunu ayrıştırır.
    private static TaskResponse Map(ScheduledTask task) =>
        new(
            task.Id,
            task.JobName,
            task.ParametersJson,
            task.IntervalMinutes,
            task.NextRunAt,
            task.IsActive,
            task.RetryCount,
            task.GroupKey,
            task.LastRunAt);
}
