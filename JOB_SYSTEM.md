# Job System

## IJobHandler

Interface:
- string Name
- Task ExecuteAsync(string parametersJson, CancellationToken token)

## Rules

- Her job kendi parametresini deserialize eder
- JSON strongly-typed modele cevrilir
- Hatali JSON exception firlatmalidir

## Example Jobs

### SendEmailJob
Parametre:
{
  "to": "string",
  "subject": "string"
}

### CleanupJob
Parametre:
{
  "days": number
}