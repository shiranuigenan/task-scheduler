# Task Scheduler API (.NET 10)

## Description
Bu proje, onceden tanimli job'lari belirli araliklarla calistiran tek-proje bir task scheduler Web API uygulamasidir.

## Current Project Structure
- Tek csproj: `src/TaskScheduler.Api/TaskScheduler.Api.csproj`
- Uygulama katmanlari ayni proje icinde klasorlenmistir:
  - `TaskScheduler.Application`
  - `TaskScheduler.Domain`
  - `TaskScheduler.Infrastructure`
  - `Controllers`

## Features
- Predefined job handler sistemi (`IJobHandler`, `IJobFactory`)
- JSON tabanli parametre gecisi
- Dakika bazli zamanlama (`IntervalMinutes`)
- Retry mekanizmasi (`RetryCount`)
- Group bazli concurrency control (`GroupKey` + lock)
- `BackgroundService` ile job execution
- Swagger UI ile endpoint testi

## Tech Stack
- .NET 10 Web API
- Entity Framework Core + PostgreSQL (Npgsql)
- BackgroundService (`IHostedService`)
- Swagger (`Swashbuckle.AspNetCore`)

## Run
1. PostgreSQL'de `taskscheduler` veritabanini olustur.
2. Gerekirse baglanti bilgisini `src/TaskScheduler.Api/appsettings.json` icinde guncelle.
3. Calistir:
   - `dotnet run --project src/TaskScheduler.Api/TaskScheduler.Api.csproj`
4. Swagger:
   - `https://localhost:<port>/swagger`