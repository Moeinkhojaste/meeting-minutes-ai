# AGENTS.md

## Project Overview

This repository contains **Meeting Minutes AI**, a web application that converts meeting audio into structured meeting minutes.

The system should:

1. Accept uploaded meeting audio files.
2. Convert speech to text.
3. Process and clean the transcript.
4. Extract meeting information such as:

   * Meeting title
   * Date and participants
   * Main discussion topics
   * Decisions
   * Action items
   * Responsible person for each action
   * Deadlines when mentioned
5. Allow users to review and edit the generated minutes.
6. Export or save the final meeting minutes.

The project is intended as a university final project. Prefer clear, maintainable implementations over unnecessary production complexity.

---

## Architecture

Use a modular architecture with clear separation of responsibilities.

Expected components:

* `frontend/` — Web user interface
* `backend/` — Main API, authentication, meeting management, and database access
* `ai-service/` — Speech-to-text and meeting-minutes processing
* `tests/` — Automated tests
* `docs/` — Architecture, API, evaluation, and project documentation

The intended request flow is:

```text
Frontend
   ↓
Backend API
   ↓
AI Service
   ├── Speech-to-text
   ├── Transcript processing
   └── Meeting-minutes generation
   ↓
Backend API
   ↓
Database
```

Do not place AI model execution directly inside controllers or frontend code.

---

## Technology Guidelines

Preferred technologies:

### Frontend

* React
* TypeScript
* Vite
* A simple component-based UI
* REST API communication

### Backend

* ASP.NET Core Web API
* C#
* Entity Framework Core
* PostgreSQL or SQL Server
* Swagger/OpenAPI

### AI Service

* Python
* FastAPI
* Local or free speech-to-text models
* Whisper or faster-whisper for transcription
* Separate modules for transcription, summarization, and information extraction

Do not replace the selected technology stack without an explicit requirement.

---

## Backend Structure

Keep backend dependencies flowing in this direction:

```text
Controllers
    ↓
Application Services
    ↓
Repositories
    ↓
Entity Framework Core
    ↓
Database
```

Recommended backend directories:

```text
backend/
├── Controllers/
├── Services/
├── Repositories/
├── Data/
├── Entities/
├── DTOs/
├── Mappings/
├── Validators/
├── Middleware/
├── Configuration/
└── Tests/
```

Rules:

* Controllers must remain thin.
* Controllers handle HTTP concerns only.
* Business logic belongs in services.
* Database access belongs in repositories or the persistence layer.
* Do not return database entities directly from API endpoints.
* Use DTOs for API requests and responses.
* Use asynchronous methods for database and network operations.
* Pass `CancellationToken` through asynchronous backend operations where practical.
* Use dependency injection.
* Validate uploaded file type and size.
* Return appropriate HTTP status codes.
* Do not expose internal exceptions or stack traces to clients.

---

## AI Service Structure

Recommended AI service directories:

```text
ai-service/
├── app/
│   ├── api/
│   ├── transcription/
│   ├── preprocessing/
│   ├── summarization/
│   ├── extraction/
│   ├── evaluation/
│   ├── models/
│   └── config/
├── tests/
├── requirements.txt
└── main.py
```

Rules:

* Keep transcription separate from summarization.
* Keep information extraction separate from transcript generation.
* Define clear input and output schemas.
* Use type hints.
* Validate API input with Pydantic models.
* Avoid loading the same model for every request.
* Load expensive models once during application startup when possible.
* Store model names and configurable parameters in configuration files or environment variables.
* Do not hardcode machine-specific file paths.
* Handle Persian and English meeting content where possible.
* Preserve the original transcript separately from cleaned or summarized text.

---

## Meeting-Minutes Output Schema

Generated meeting minutes should use a stable structured format similar to:

```json
{
  "title": "Project Planning Meeting",
  "date": null,
  "participants": [],
  "summary": "Short meeting summary",
  "topics": [
    {
      "title": "Topic title",
      "summary": "Discussion summary"
    }
  ],
  "decisions": [
    {
      "text": "Decision text"
    }
  ],
  "actionItems": [
    {
      "task": "Task description",
      "assignee": null,
      "deadline": null
    }
  ]
}
```

Rules:

* Do not invent participants, dates, decisions, assignees, or deadlines.
* Use `null` or an empty collection when information is not present.
* Clearly separate directly extracted information from generated summaries.
* Preserve uncertainty when the transcript is ambiguous.
* Avoid presenting model guesses as confirmed facts.

---

## Domain Model

Likely core entities include:

* `User`
* `Meeting`
* `AudioFile`
* `Transcript`
* `MeetingMinutes`
* `Participant`
* `Decision`
* `ActionItem`

Do not create unnecessary entities before their use cases are defined.

A meeting should be able to retain:

* Original filename
* Stored audio path or object-storage identifier
* Upload date
* Processing status
* Processing error
* Raw transcript
* Cleaned transcript
* Generated meeting minutes
* User-edited final meeting minutes
* Model and configuration metadata used during processing

---

## Processing Status

Use an explicit status instead of several unrelated Boolean fields.

Suggested states:

```text
Uploaded
Queued
Transcribing
GeneratingMinutes
Completed
Failed
```

Processing failures should store a safe error description suitable for debugging without exposing secrets.

---

## API Conventions

Use REST-style endpoints.

Example routes:

```text
POST   /api/meetings
GET    /api/meetings
GET    /api/meetings/{id}
DELETE /api/meetings/{id}

POST   /api/meetings/{id}/audio
POST   /api/meetings/{id}/process

GET    /api/meetings/{id}/transcript
PUT    /api/meetings/{id}/transcript

GET    /api/meetings/{id}/minutes
PUT    /api/meetings/{id}/minutes
```

Use a consistent error response format.

Do not expose internal filesystem paths through API responses.

---

## Security

* Never commit passwords, API keys, tokens, connection strings, or private credentials.
* Use environment variables or local development secret storage.
* Include a `.env.example` file containing variable names without real values.
* Validate all uploaded files.
* Do not trust filename extensions alone.
* Generate safe server-side filenames.
* Prevent path traversal.
* Restrict maximum upload size.
* Escape or safely render transcript and meeting-minute content.
* Confirm resource ownership before returning, modifying, or deleting meetings.
* Avoid logging secrets, authentication tokens, or complete sensitive transcripts.
* Add authentication only through the backend, not through the AI service.

---

## Privacy

Meeting recordings and transcripts may contain private information.

* Do not send recordings or transcripts to external services unless explicitly configured.
* Prefer local model execution for the free version of the project.
* Document whenever data leaves the local system.
* Make deletion behavior clear.
* Delete temporary processing files after they are no longer needed.
* Do not use real private meeting recordings as committed test fixtures.
* Use generated or anonymized sample data in tests.

---

## Evaluation

Do not evaluate the complete system using only one metric.

### Speech-to-text evaluation

Consider:

* Word Error Rate
* Character Error Rate
* Real-time factor
* Processing duration
* Memory usage

### Information extraction evaluation

Consider:

* Precision
* Recall
* F1 score
* Exact match where appropriate

Evaluate these categories separately:

* Participants
* Decisions
* Action items
* Assignees
* Deadlines

### Summary and meeting-minutes evaluation

Consider:

* Coverage of important information
* Factual consistency with the transcript
* Unsupported or hallucinated statements
* Readability
* Human evaluation
* Structured-field completeness

Do not claim that generated minutes are accurate without evaluation evidence.

---

## Testing Requirements

Add tests for important behavior.

Backend tests should cover:

* Service logic
* Validation
* Meeting ownership checks
* Status transitions
* API error responses
* Upload validation
* Mapping between entities and DTOs

AI service tests should cover:

* Audio validation
* Transcript preprocessing
* Structured output parsing
* Missing information
* Invalid model output
* Persian and English sample inputs
* Hallucination-sensitive cases

Frontend tests should cover critical user flows when practical.

For every bug fix, add a regression test when the defect can be reproduced automatically.

Do not remove or weaken tests merely to make the test suite pass.

---

## Coding Standards

### General

* Prefer straightforward code over clever abstractions.
* Keep functions and classes focused.
* Use descriptive names.
* Avoid duplicated business logic.
* Avoid premature optimization.
* Avoid adding dependencies when the standard library or existing dependencies are sufficient.
* Remove unused imports and dead code.
* Do not leave unexplained placeholder implementations.
* Add comments only when they explain reasoning that the code cannot express clearly.

### C#

* Enable nullable reference types.
* Follow standard C# naming conventions.
* Use `async` and `await` correctly.
* Suffix asynchronous methods with `Async`.
* Avoid `.Result` and `.Wait()`.
* Prefer constructor injection.
* Use records for immutable DTOs where appropriate.
* Keep Entity Framework queries efficient.
* Avoid unnecessary repository methods that duplicate `DbContext` behavior without adding value.

### Python

* Follow PEP 8.
* Use type hints for public functions.
* Use Pydantic schemas for API contracts.
* Keep model-specific code behind interfaces or adapter classes.
* Avoid broad `except Exception` blocks unless the exception is logged and converted into a controlled application error.
* Do not silently ignore processing errors.

### TypeScript

* Use strict TypeScript settings.
* Avoid `any` unless there is a documented reason.
* Keep API types explicit.
* Separate API access from UI components.
* Handle loading, empty, success, and error states.
* Do not store sensitive tokens in unsafe browser storage without an explicit security decision.

---

## Commands

Before finalizing a change, run the relevant commands.

### Backend

```bash
dotnet restore
dotnet build
dotnet test
dotnet format --verify-no-changes
```

### AI service

```bash
python -m pytest
python -m ruff check .
python -m mypy app
```

### Frontend

```bash
npm install
npm run lint
npm run test
npm run build
```

Only run commands that are configured in the repository. If a listed tool has not yet been configured, state that instead of inventing successful results.

---

## Database Changes

* Use Entity Framework Core migrations.
* Do not modify an existing migration after it has been shared or applied.
* Create a new migration for schema changes.
* Use meaningful migration names.
* Do not delete user data to resolve a migration problem.
* Do not automatically reset the database without explicit instruction.

---

## Git Rules

* Keep commits focused on one logical change.
* Do not commit generated build output.
* Do not commit uploaded audio files, model weights, databases, logs, or secrets.
* Update `.gitignore` when introducing new generated files.
* Do not rewrite unrelated code during a targeted task.
* Do not alter public APIs without documenting the change.
* Do not push, merge, or create a pull request unless explicitly requested.

Suggested ignored content includes:

```text
.env
.env.*
!.env.example
bin/
obj/
node_modules/
dist/
__pycache__/
.pytest_cache/
.venv/
uploads/
temp/
models/
*.db
*.log
```

---

## Documentation

Update documentation when changing:

* Setup requirements
* Environment variables
* API endpoints
* Database schema
* Processing workflow
* Model selection
* Evaluation methodology
* Commands
* Architecture

The main `README.md` should explain how a new developer can run the entire project locally.

Do not place extensive project documentation inside this file. Keep detailed design decisions under `docs/`.

---

## Agent Behavior

When working in this repository:

1. Inspect the relevant existing code before making changes.
2. Follow the current architecture unless it is clearly broken.
3. Make the smallest coherent change that satisfies the task.
4. Do not create duplicate services, models, DTOs, or utilities.
5. Preserve existing behavior unless the task requires changing it.
6. Validate assumptions against the repository.
7. Run relevant tests and builds after changes.
8. Report commands that were run and their actual results.
9. State clearly when tests could not be run.
10. Do not claim that code works without verification.
11. Do not modify unrelated files.
12. Do not introduce paid APIs into the default implementation.
13. Prefer solutions that can run locally and without recurring costs.
14. Treat Persian-language support as a core requirement, not an optional afterthought.
15. Keep the implementation appropriate for a university MVP.

---

## Current Project Priorities

Implement features in this order unless the task explicitly requires otherwise:

1. Repository and project structure
2. Basic backend API
3. Database and meeting entities
4. Audio upload
5. Python AI service
6. Speech-to-text integration
7. Structured meeting-minutes generation
8. Transcript and minutes review interface
9. Evaluation pipeline
10. Authentication and ownership
11. Export functionality
12. Deployment and final documentation

Do not build advanced collaboration, billing, real-time transcription, or microservice infrastructure before the core MVP works.
