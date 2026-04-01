# Project Rules

- .NET 10 kullanılacak
- Controller-based Web API kullanılacak
- Tek csproj yapisi korunacak (`TaskScheduler.Api`)
- Katman ayrimi klasor/namespace seviyesinde uygulanacak
- PostgreSQL (Npgsql) kullanılacak

## Coding Rules
- SOLID prensiplerine uy
- Tüm I/O işlemleri async/await olmalı
- Dependency Injection zorunlu
- Entity'ler direkt API'de kullanılmamalı (DTO kullan)

## Scheduler Rules
- BackgroundService kullanılmalı
- Task execution fire-and-forget ama kontrollü olmalı
- Her task ayrı scope içinde çalışmalı

## Job System Rules
- Tüm job'lar IJobHandler interface'ini implement etmeli
- Job selection string JobName üzerinden yapılmalı
- Parametreler JSON string olarak taşınmalı

## Error Handling
- Retry mekanizması zorunlu
- Exception'lar swallow edilmemeli, loglanmalı

## Logging
- ILogger kullanılmalı