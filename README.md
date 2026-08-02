# Meeting Minutes AI

Persian title: **سامانه هوشمند تولید صورت‌جلسه**

Meeting Minutes AI is a university MVP for converting Persian or English
meeting audio into an editable transcript and structured meeting minutes.
Phase 3 adds a Gemini-first Python AI service while preserving the verified
local CUDA/faster-whisper baseline as transcription fallback and scientific
comparator.

## Repository structure

```text
frontend/    React, TypeScript, and Vite foundation
backend/     ASP.NET Core Clean Architecture backend and SQL Server persistence
ai-service/  Python STT evaluation and transcription tools
tests/       Cross-project and backend tests
docs/        Charter, setup, evaluation, and technical documentation
```

## Pinned development environment

- Python 3.12.10
- Node.js 24.18.0 LTS and npm 11.17.0
- .NET SDK 10.0.302 LTS
- FFmpeg/FFprobe 8.1.2
- Git 2.50.1 for Windows
- NVIDIA driver 596.49; CUDA-capable RTX 3060 Laptop GPU

See [development setup](docs/development-setup.md) for installation,
verification, and troubleshooting.

## First-time setup

Copy the safe configuration template without committing the resulting file:

```powershell
Copy-Item .env.example .env
```

Create the Python environment:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
python -m pip install -r .\ai-service\requirements.txt
python -m pip install -r .\ai-service\requirements-dev.txt
```

Restore the backend and frontend:

```powershell
dotnet tool restore
dotnet restore .\MeetingMinutesAI.slnx
Set-Location .\frontend
npm ci
Set-Location ..
```

## Quality checks

```powershell
# Backend
dotnet build .\MeetingMinutesAI.slnx --no-restore
dotnet test .\MeetingMinutesAI.slnx --no-build
dotnet format .\MeetingMinutesAI.slnx --verify-no-changes

# AI service
.\.venv\Scripts\python.exe -m pytest .\ai-service
.\.venv\Scripts\python.exe -m ruff check .\ai-service
Push-Location .\ai-service
..\.venv\Scripts\python.exe -m mypy
Pop-Location

# Frontend
Push-Location .\frontend
npm run lint
npm run test
npm run build
Pop-Location
```

## Run the foundations

Apply the committed SQL Server migration to the LocalDB development database:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet tool run dotnet-ef database update `
  --project .\backend\MeetingMinutesAI.Infrastructure `
  --startup-project .\backend\MeetingMinutesAI.Api `
  --context MeetingMinutesDbContext
```

The backend uses enforced Domain, Application, Infrastructure, and API
projects. See [backend architecture](docs/backend-architecture.md) for the
dependency rules, persistence model, and migration workflow.

```powershell
dotnet run --project .\backend\MeetingMinutesAI.Api
```

The backend exposes:

- `GET /health/live`
- `GET /health/ready`

Run the frontend separately:

```powershell
npm --prefix .\frontend run dev
```

Run the selected local Persian STT checkpoint:

```powershell
.\.venv\Scripts\python.exe .\ai-service\transcribe.py `
  "D:\path\meeting.wav"
```

Configuration precedence is CLI argument, process environment, `.env`, then
safe code default. See [configuration](docs/configuration.md).

Run the Phase 3 AI service:

```powershell
Push-Location .\ai-service
..\.venv\Scripts\python.exe -m uvicorn main:app --host 127.0.0.1 --port 8000
Pop-Location
```

The AI service exposes `/health/live`, `/health/ready`,
`/v1/transcriptions`, `/v1/minutes`, and `/v1/process`. See the
[AI-service API](docs/ai-service-api.md).

Validate the private dataset manifest without reading or committing media:

```powershell
.\.venv\Scripts\python.exe .\ai-service\dataset_manifest.py `
  .\Datasets\manifest.local.json
```

The dataset naming, consent, annotation, split-locking, and transcript-free
aggregate-output rules are documented in the
[dataset protocol](docs/dataset-protocol.md).

## Privacy and limitations

- Local copies of audio, references, annotations, transcripts, generated
  evaluation results, model files, secrets, and `.env` files remain ignored.
  Audio and transcripts leave the machine when sent to Gemini.
- Every accepted meeting is sent to Gemini first, including private meetings.
  The free Gemini tier may process or retain data under Google's current terms.
- faster-whisper is only a technical transcription fallback. Transcript
  cleaning and minutes generation have no local fallback in Phase 3.
- Only generated or explicitly permitted anonymized fixtures may be committed.
- `large-v3-turbo` is the initial STT checkpoint, not an accuracy claim. Its
  current one-sample normalized WER is 59.37%; broader Phase 2 evaluation is
  required.
- The repository now contains meeting persistence and its initial migration,
  but it does not yet implement meeting CRUD endpoints, authentication,
  deployment, export, or a review UI.

The approved scope and exclusions are recorded in the
[project charter](docs/project-charter.md).
