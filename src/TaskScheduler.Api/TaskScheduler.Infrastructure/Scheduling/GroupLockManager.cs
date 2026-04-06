using System.Collections.Concurrent;

namespace TaskScheduler.Infrastructure.Scheduling;

/// <summary>
/// Aynı `groupKey`'e sahip task'ların uygulama içinde aynı anda çalışmasını engellemek için
/// bellek içi (in-memory) kilitleme mekanizması sağlar.
///
/// Uygulama bir BackgroundService ile DB'den due task'ları okur; her due task için önce group lock
/// denenir. Lock alınamazsa task bu poll turunda çalıştırılmaz.
///
/// Lock alındığında, çağıran tarafa "lease" gibi davranan bir `IAsyncDisposable` döner.
/// Lease dispose edilince ilgili semaphore serbest bırakılır (lock release edilir).
/// </summary>
public sealed class GroupLockManager
{
    // groupKey -> o group için tek slotlu semaphore mapping.
    // ConcurrentDictionary kullanmamızın nedeni; aynı anda birden fazla thread (veya task) lock denemesi yapabilir.
    // SemaphoreSlim (1,1) ile "aynı group'tan en fazla 1 çalışsın" davranışı elde edilir.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    // Belirli bir groupKey için kilidi "try" şeklinde alır.
    // Önemli: WaitAsync(0) kullanıldığı için bu metod asla beklemez; lock varsa hemen alır,
    // yoksa null döner. Böylece scheduler aynı poll turunda gereksiz beklemeye girmez.
    public async ValueTask<IAsyncDisposable?> TryAcquireAsync(string groupKey, CancellationToken cancellationToken = default)
    {
        // groupKey için bir semaphore yoksa oluşturur.
        // OrdinalIgnoreCase ile groupKey büyük/küçük harf farkından bağımsız kabul edilir.
        var sem = _locks.GetOrAdd(groupKey, _ => new SemaphoreSlim(1, 1));

        // Lock alınabilir durumda değilse (başka bir task o group'u kullanıyorsa) hemen başarısız olur.
        if (!await sem.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return null;

        // Lock alındıktan sonra semaphore'ı serbest bırakmak için bir "lease" döndürüyoruz.
        // Bu lease, scheduler tarafında Task bitince dispose edilir.
        return new Releaser(sem);
    }

    // Lock lease'ini dispose edince semaphore.Release çağıran küçük yardımcı sınıf.
    // DisposeAsync implement ederek 'using/await using' akışlarıyla düzgün serbest bırakma sağlar.
    private sealed class Releaser(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        // Dispose birden fazla kez çağrılabilir (hata/şartlar). Çift release engellemek için kullanılır.
        private int _disposed;

        // SemaphoreSlim.Release birden fazla kez çağrıldığında izin sayısı artabilir ve kilitleme bozulur.
        // Interlocked.Exchange ile atomic şekilde yalnızca ilk dispose'da release yapılır.
        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
