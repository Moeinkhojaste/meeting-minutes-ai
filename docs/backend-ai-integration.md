# Backend audio and AI-service integration

## Upload contract

`POST /api/meetings/{id}/audio` requires the current quoted `If-Match` and one
multipart field named `audio`. The backend ignores the submitted MIME type and
extension. It streams at most 500 MiB to a private temporary file, calculates
SHA-256, detects WAV, FLAC, OGG, AIFF, AAC, MP3, WebM, WMA, or M4A magic bytes,
then atomically moves the file to an opaque generated key. API responses expose
only safe display metadata, never the key, hash, or physical path.

The default root is `MeetingMinutesAI/audio` under the current user's local
application-data directory. `AudioStorage__RootPath` can override it. Startup
removes abandoned files from the adapter's private `.tmp` and `.trash`
directories.

## Synchronous staged processing

`POST /api/meetings/{id}/process` accepts optional JSON with `mode` equal to
`fast` or `quality`; `fast` is the default. The endpoint requires `If-Match`
and intentionally executes synchronously:

1. Persist Queued, then Transcribing.
2. Stream the stored audio to `POST /v1/transcriptions`.
3. Validate strict camel-case JSON and persist stage metadata plus raw segments.
4. Persist GeneratingMinutes.
5. Send the raw transcript to `POST /v1/minutes`.
6. Validate every cleaned source and minutes evidence ID against raw segment
   IDs, then persist cleaned transcript and locked generated minutes separately.

The backend forwards its correlation ID in `X-Correlation-ID`, limits response
bytes, and uses a configurable two-hour timeout. It does not retry, change the
requested mode, or provide a stage-two fallback. `fast` maps to
`gemini-3.5-flash-lite`; `quality` maps to `gemini-3.6-flash`.

## Results and failures

A successful run finishes Completed. If stage two fails or returns invalid
references, the committed raw transcript remains available, the run finishes
PartiallyCompleted, and the processing endpoint returns HTTP 200. Cleaned and
minutes endpoints return 404 because those outputs were not persisted.

A stage-one provider rejection returns 422; provider/network failure returns
502; timeout returns 504. The Failed state and safe error are committed before
the response. Error bodies contain only `code`, safe `message`, and the backend
correlation ID. A completed, partial, or failed meeting can be explicitly run
again; attempt history remains, while current generated outputs are replaced.

Output retrieval endpoints are:

- `GET /api/meetings/{id}/transcripts/raw`
- `GET /api/meetings/{id}/transcripts/cleaned`
- `GET /api/meetings/{id}/minutes/generated`

Each returns schema version, external/source/evidence IDs, content, creation
time, opaque version, and the current meeting `ETag` header.
