# Scheduler Rules

## Execution
- Task'lar BackgroundService içinde çalıştırılmalı
- Polling interval: 10-30 saniye

## Run Condition
- NextRunAt <= now ise çalıştır

## After Execution
- LastRunAt güncellenmeli
- NextRunAt = now + IntervalMinutes

## Retry
- Her task RetryCount kadar denenmeli
- Başarısız olursa tekrar dene
- Tüm retry'ler bitince fail say

## Parallelism
- Farklı GroupKey → paralel çalışabilir
- Aynı GroupKey → aynı anda çalışamaz

## Locking
- In-memory lock kullanılmalı
- Lock, job tamamlanana kadar tutulmalı
- Retry süresince lock bırakılmamalı

## Scope
- Her job execution yeni DI scope içinde olmalı