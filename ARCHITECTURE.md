# Architecture

## Layout
- Tek proje: `TaskScheduler.Api`
- Katmanlar namespace ve klasor seviyesinde ayridir:
  - API (`Controllers`)
  - Application (`TaskScheduler.Application`)
  - Domain (`TaskScheduler.Domain`)
  - Infrastructure (`TaskScheduler.Infrastructure`)

## Flow
HTTP Request → Controller → Service → Database

Scheduler Flow:
BackgroundService → Task Fetch → Lock Check → Job Resolve → Execute → Update DB

## Core Components

### 1. JobHandler
- Interface: IJobHandler
- Her job ayrı class

### 2. JobFactory
- JobName → Handler mapping

### 3. Scheduler Service
- BackgroundService implementasyonu
- Task polling yapar

### 4. GroupLockManager
- Ayni group'taki task'larin paralel calismasini engeller

## Persistence
- EF Core provider: PostgreSQL (Npgsql)
- `AppDbContext` migrationlari:
  - `TaskScheduler.Infrastructure/Persistence/Migrations`