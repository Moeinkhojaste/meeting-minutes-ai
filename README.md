# Meeting Minutes AI

Persian title: **سامانه هوشمند تولید صورت‌جلسه**

Meeting Minutes AI is a university MVP for converting Persian or English
meeting audio into an editable transcript and structured meeting minutes.
Phase 3 adds a Gemini-first Python AI service with local fallbacks for both
stages: faster-whisper for speech-to-text and an Ollama-compatible LLM
(default `qwen2.5:3b-instruct`) for transcript cleaning and minutes
generation.

## Repository structure

```text
frontend/    React, TypeScript, Vite foundation, meeting dashboard, review UI, and export features
backend/     ASP.NET Core Clean Architecture backend and SQL Server persistence
ai-service/  Gemini-first FastAPI AI service with local STT and LLM fallbacks
tests/       Cross-project and backend tests
docs/        Charter, setup, evaluation, and technical documentation
```

## Pinned development environment

- Python 3.12.10
- Node.js 24.18.0 LTS and npm 11.17.0
- .NET SDK 9.0.304 pinned in `global.json` (roll-forward to later majors is
  permitted)
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
- `POST /api/meetings`
- `GET /api/meetings?page=1&pageSize=20`
- `GET /api/meetings/{id}`
- `PUT /api/meetings/{id}`
- `DELETE /api/meetings/{id}`
- `POST /api/meetings/{id}/audio`
- `POST /api/meetings/{id}/process`
- `GET /api/meetings/{id}/transcripts/raw`
- `GET /api/meetings/{id}/transcripts/cleaned`
- `GET /api/meetings/{id}/minutes/generated`

Meeting responses expose the explicit processing status as a lower-camel-case
string and include a `latestRun` view with per-stage provider, model,
fallback, and duration metadata that the frontend renders as a model
provenance card. Updates and deletes require the current quoted `ETag` in an
`If-Match` header; stale versions return `409 Conflict`. Deletion is rejected
while a meeting is queued, transcribing, or generating minutes. Upload and
processing also require `If-Match`. Audio is signature-checked while streaming
to private local storage with a 500 MiB limit. Processing synchronously calls
the AI service in two stages so the raw transcript is committed before minutes
generation. A stage-two failure returns a partial meeting while retaining raw
output. Deletion coordinates removal of both the local object and database
aggregate. Meetings carry an optional `UserId`; requests may pass an
`X-User-Id` header, and meetings owned by another user are hidden or rejected.
This is preliminary ownership support; real authentication is still not
implemented. See [backend AI integration](docs/backend-ai-integration.md).

In development the API listens on `http://localhost:5023` (launch settings),
allows any CORS origin, and exposes the `ETag` header to browsers. The Vite
dev server proxies `/api` requests to it, so the frontend works same-origin by
default.

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
`/v1/transcriptions`, `/v1/minutes`, and `/v1/process`. Transcription is
Gemini-first with faster-whisper fallback. Minutes generation is Gemini-first
with a local LLM fallback served through any OpenAI-compatible endpoint such
as Ollama (`MM_AI_LOCAL_LLM_BASE_URL`, default
`http://localhost:11434/v1`, model `qwen2.5:3b-instruct`). When every minutes
provider fails, `/v1/process` returns a partial result retaining the raw
transcript. Health readiness reports whether Gemini and each local fallback
are configured, and every stage response carries provider, model, fallback,
and prompt-version metadata. See the
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

- Every accepted meeting is sent to Gemini first, including private meetings.
  The free Gemini tier may process or retain data under Google's current terms.
- faster-whisper remains only a technical transcription fallback. Minutes
  generation falls back to a local OpenAI-compatible LLM endpoint
  (`qwen2.5:3b-instruct` through Ollama by default), which keeps that stage on
  the local machine when Gemini fails or is unconfigured.
- Only generated or explicitly permitted anonymized fixtures may be committed.
- `large-v3-turbo` is the initial STT checkpoint, not an accuracy claim. Its
  current one-sample normalized WER is 59.37%; broader Phase 2 evaluation is
  required.
- The repository now contains meeting persistence with preliminary per-user
  ownership (`UserId` column plus `X-User-Id` header checks), CRUD, secure
  local upload, synchronous staged AI orchestration with stage-level
  provider/model provenance shown in the review UI, Persian-first typography
  (Vazirmatn) with bi-directional text alignment, a complete frontend meeting
  dashboard and review UI, and multi-format export (Markdown, JSON, Text,
  Print/PDF). It does not yet implement real authentication, deployment object
  storage, or a background job queue system.

The approved scope and exclusions are recorded in the
[project charter](docs/project-charter.md).
