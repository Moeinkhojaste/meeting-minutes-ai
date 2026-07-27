# Meeting Minutes AI

Persian title: **سامانه هوشمند تولید صورت‌جلسه**

Meeting Minutes AI is a privacy-first university MVP for converting permitted
Persian or English meeting audio into an editable transcript and structured
meeting minutes. The current repository contains the verified Persian STT
baseline and the buildable Phase 1 project foundations. Product upload,
processing, editing, authentication, and export features intentionally belong
to later phases.

## Repository structure

```text
frontend/    React, TypeScript, and Vite foundation
backend/     ASP.NET Core API foundation
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
- Ollama 0.32.4

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

Validate the private dataset manifest without reading or committing media:

```powershell
.\.venv\Scripts\python.exe .\ai-service\dataset_manifest.py `
  .\Datasets\manifest.local.json
```

The dataset naming, consent, annotation, split-locking, and transcript-free
aggregate-output rules are documented in the
[dataset protocol](docs/dataset-protocol.md).

## Privacy and limitations

- Audio, references, annotations, transcripts, generated evaluation results,
  model files, secrets, and `.env` files stay local and ignored.
- No recording or transcript is sent to an external service by default.
- Only generated or explicitly permitted anonymized fixtures may be committed.
- `large-v3-turbo` is the initial STT checkpoint, not an accuracy claim. Its
  current one-sample normalized WER is 59.37%; broader Phase 2 evaluation is
  required.
- The repository does not yet implement production features, deployment,
  authentication, or data persistence.

The approved scope and exclusions are recorded in the
[project charter](docs/project-charter.md).
