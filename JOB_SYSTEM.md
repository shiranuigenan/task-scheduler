# Job System

## IJobHandler

Interface:
- `string Name`
- `void Execute(string parametersJson, CancellationToken cancellationToken)`

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