# Backend Clean Architecture

## Dependency rule

The backend is split into four projects with dependencies pointing inward:

```text
MeetingMinutesAI.Api -> MeetingMinutesAI.Application -> MeetingMinutesAI.Domain
MeetingMinutesAI.Api -> MeetingMinutesAI.Infrastructure
MeetingMinutesAI.Infrastructure -> MeetingMinutesAI.Application
MeetingMinutesAI.Infrastructure -> MeetingMinutesAI.Domain
```

- **Domain** owns the meeting aggregate, processing rules, and enums. It has no
  ASP.NET Core or EF Core dependency.
- **Application** owns persistence, audio-storage, AI-client, and orchestration
  boundaries.
- **Infrastructure** implements persistence with EF Core/SQL Server, private
  filesystem audio storage, and the typed AI HTTP client.
- **API** is the composition root. It registers Infrastructure but does not
  expose persistence entities as HTTP contracts.

## Persistence model

`Meeting` is the aggregate root. It owns audio metadata, current raw and
cleaned transcripts, generated and final minutes, evidence relationships, and
processing-attempt metadata. Generated minutes are locked after attachment;
the editable final version is a separate graph. Processing attempts retain
provider and timing metadata without copying historical transcript content.

Deleting a meeting hard-deletes its database aggregate. Cross-graph evidence
foreign keys use SQL Server `NO ACTION` to avoid multiple cascade paths. Audio
bytes are external and represented only by an opaque storage key. Deletion
first moves that local object to private trash, commits the database cascade,
then removes the staged object; a database failure restores the object.

## Meeting CRUD API

The API exposes create, paged list, get, title update, and delete operations at
`/api/meetings`. API contracts are separate from persistence entities. Every
response includes the current lower-camel-case processing status and an opaque
Base64 row-version token; single-resource responses also return that token as
a quoted `ETag`.

`PUT` and `DELETE` require the current `ETag` in `If-Match`. A missing header
returns `428`, a malformed token returns `400`, and a stale token returns
`409`. Status is read-only through CRUD and can change only through aggregate
workflow operations. Deletion returns `409` for `Queued`, `Transcribing`, and
`GeneratingMinutes` meetings.

## Authentication and identity

The backend implements stateless, verifiable JWT (JSON Web Token) authentication:

- `User` is the identity aggregate root in the Domain layer (`MeetingMinutesAI.Domain.Users.User`), storing normalized emails, salted PBKDF2 password hashes, full names, and timestamps.
- Passwords are encrypted using standard PBKDF2 (`HMAC-SHA256`, 128-bit random salt, 100,000 iterations) with constant-time equality checks to eliminate timing attacks.
- Tokens are signed with HMAC-SHA256 using a server-side secret key and contain verified identity claims (`sub`, `email`, `name`, `jti`, `iat`, `exp`).
- `POST /api/auth/register` and `POST /api/auth/login` issue standard Bearer JWT tokens.
- `GET /api/auth/me` verifies the token and returns the current user profile.
- Protected endpoints leverage ASP.NET Core `JwtBearerHandler` and `ClaimsPrincipal`. Meetings associate with the authenticated user ID, enforcing multi-user ownership and isolation.

## Audio and processing

`POST /api/meetings/{id}/audio` streams one multipart `audio` file through a
private temporary file. The adapter enforces the byte limit, hashes the stream,
detects its container from magic bytes, and atomically moves it to an opaque
key outside web roots. Submitted extensions and MIME types are not trusted.

`POST /api/meetings/{id}/process` explicitly runs from Uploaded, Completed,
PartiallyCompleted, or Failed. It persists Queued and Transcribing before
calling `/v1/transcriptions`, then commits the raw transcript and
GeneratingMinutes before `/v1/minutes`. Cleaned transcript and generated
minutes are separate graphs with raw-segment source/evidence links. Stage-one
failure produces Failed; stage-two failure produces PartiallyCompleted with
raw output retained. Runs are never retried or switched to another mode by the
backend. The detailed wire and failure contract is in
[backend AI integration](backend-ai-integration.md).

## Migration workflow

Restore the pinned EF Core 10.0.10 tool, then use Infrastructure as the
migration project and API as the startup project:

```powershell
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT = "Development"

dotnet tool run dotnet-ef migrations add MigrationName `
  --project .\backend\MeetingMinutesAI.Infrastructure `
  --startup-project .\backend\MeetingMinutesAI.Api `
  --context MeetingMinutesDbContext `
  --output-dir .\Persistence\Migrations

dotnet tool run dotnet-ef database update `
  --project .\backend\MeetingMinutesAI.Infrastructure `
  --startup-project .\backend\MeetingMinutesAI.Api `
  --context MeetingMinutesDbContext
```

Migrations are reviewed and applied explicitly. The API does not call
`Database.Migrate` during startup.
