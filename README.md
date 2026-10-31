# Quillbridge Dynamic Conversation Guide

Live: **http://quillbridge.duckdns.org**

## Login

Password for all accounts: `Test1234`

| SDR | Email |
|---|---|
| Maya Brennan | maya.brennan@quillbridge.com |
| Sam Ortega | sam.ortega@quillbridge.com |
| Elif Demir | elif.demir@quillbridge.com |
| Priya Raman | priya.raman@quillbridge.com |
| Jonas Weber | jonas.weber@quillbridge.com |
| Luca Ferri | luca.ferri@quillbridge.com |

## Run locally

Requires .NET 8 SDK, Node.js 24 and a local SQL Server.

```powershell
# 1. configuration
$env:ConnectionStrings__DefaultConnection = "Server=.;Database=quillbridge;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True"
$env:Jwt__Key = "<any random string, at least 32 characters>"
$env:ANTHROPIC_API_KEY = "<your key>"   # optional; without it guides use templates

# 2. API (creates the database and the SDR accounts on first start)
dotnet run --project api/quillbridge.api --launch-profile http

# 3. web app, in a second terminal
cd web
npm install
npm run dev
```

Open **http://localhost:4200**, sign in, go to Import and upload the CSV.

## Sample file results

486 records read, 420 got a guide. The other 66: 8 duplicates, 23 do not call, 28 outside 200-5,000 employees, 5 existing customers, 2 competitors. All guides can be downloaded from Insights → Export.

## What "dynamic" means

Each guide is built from the prospect's own data: job title, trigger, last contact, accounting system and notes. These decide the opening, the questions, the customer story, the likely objections and the language. Each guide also has a Plan B (switchboard, voicemail, "send an email", wrong person).

## Assumptions

- "5,000+" employees is in range, but the SDR is told to check.
- No SDR or an unknown SDR: the SDR for that region gets the prospect.
- "Stop calling" or a wrong number in the notes counts as do not call.

## Cost

One AI call (one guide) costs about $0.002. The sample file gives 420 guides, so a full run is about $0.84.

## AI usage

- Backend: I designed the structure and the logic. The code was written by AI.
- Frontend: done entirely by AI.
