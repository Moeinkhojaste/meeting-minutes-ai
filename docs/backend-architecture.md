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
- **Application** owns the `IMeetingRepository` and `IUnitOfWork` boundaries.
- **Infrastructure** implements those boundaries with EF Core and SQL Server.
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
bytes are external and represented only by an opaque storage key, so the later
deletion use case must remove the external object explicitly.

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
`GeneratingMinutes` meetings. Until an audio-storage adapter is implemented,
deletion removes the database aggregate but does not claim to remove an
external audio object.

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
