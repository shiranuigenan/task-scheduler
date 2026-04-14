# Project Rules

- .NET 10 kullanılacak
- Controller-based Web API kullanılacak
- Tek csproj yapisi korunacak (`TaskScheduler.Api`)
- Katman ayrimi klasor/namespace seviyesinde uygulanacak
- PostgreSQL (Npgsql) kullanılacak

## Coding Rules
- SOLID prensiplerine uy
- Veri erişimi ve API katmanında **senkron** I/O kullanılacak (EF Core: `ToList`, `SaveChanges`, `ExecuteDelete` vb.; `*Async` metotlar tercih edilmez)
- `async` / `await` kullanılmaz; **istisna:** `BackgroundService` için `ExecuteAsync` imzası framework gereği korunur, içerik senkron kalır (bekleme `CancellationToken.WaitHandle`, paralel iş `Task.Run`)
- Dependency Injection zorunlu
- Entity'ler direkt API'de kullanılmamalı (DTO kullan)

## Scheduler Rules
- BackgroundService kullanılmalı
- Poll döngüsü senkron; due task çalıştırma `Task.Run` ile fire-and-forget (poll’u bloklamamalı)
- Task execution fire-and-forget ama kontrollü olmalı
- Her task ayrı scope içinde çalışmalı

## Job System Rules
- Tüm job'lar `IJobHandler` arayüzünü implement etmeli (`void Execute(...)`, `Task` dönüşü yok)
- Job selection string JobName üzerinden yapılmalı
- Parametreler JSON string olarak taşınmalı

## Error Handling
- Retry mekanizması zorunlu
- Exception'lar swallow edilmemeli, loglanmalı

## Logging
- ILogger kullanılmalı