# API Spec

## GET /api/jobs
Tanimli job'lari listeler

Response:
[
  "SendEmail",
  "Cleanup"
]

---

## POST /api/tasks
Yeni gorev olusturur

Request:
{
  "jobName": "SendEmail",
  "intervalMinutes": 15,
  "groupKey": "email",
  "parameters": {
    "to": "test@mail.com"
  }
}

---

## GET /api/tasks
Tum gorevleri listeler

---

## PUT /api/tasks/{id}/activate
Gorevi aktif eder

---

## DELETE /api/tasks/{id}
Gorevi siler

---

## Swagger
- Development ortaminda Swagger UI aktiftir.
- URL: `/swagger`