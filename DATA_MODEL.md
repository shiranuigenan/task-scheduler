# Data Model

## ScheduledTask

- Id (Guid)
- JobName (string)
- ParametersJson (string)
- IntervalMinutes (int)
- NextRunAt (DateTime)
- IsActive (bool)
- RetryCount (int)
- GroupKey (string)
- LastRunAt (DateTime?)

## Notes

- ParametersJson generic olmali (JSON string)
- JobName predefined job'lara karsilik gelmeli
- GroupKey concurrency kontrolu icin kullanilir
- Veri PostgreSQL uzerinde tutulur