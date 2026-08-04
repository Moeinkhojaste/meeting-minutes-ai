# Privacy and external processing

## Phase 3 disclosure

Every valid meeting is sent to Gemini first, including meetings containing
private information. There is no consent or sensitivity gate in the Phase 3
API. Users must understand the Gemini account tier and current Google terms
before submitting recordings.

Local faster-whisper is a technical transcription fallback, not the default.
Transcript cleaning and meeting-minutes generation always require Gemini.

## Local handling

- API keys stay in the process environment or ignored `.env`.
- Uploaded and normalized temporary files are deleted in `finally`.
- Backend uploads are kept under a private local application-data directory,
  outside web roots. They use OS filesystem access protection where supported;
  application-level encryption at rest is not claimed.
- Gemini Files API objects are deleted in `finally` when a remote file exists.
- Logs exclude audio, transcripts, minutes, prompts, credentials, file URIs,
  and local paths.
- Recordings, transcripts, outputs, references, and evaluation results remain
  ignored and must not be committed.

The backend database retains meeting metadata, raw and cleaned transcript
segments, generated minutes, an optional user-finalized copy, evidence links,
and processing metadata until the meeting is explicitly deleted. The initial
schema stores only an opaque storage key for audio; audio bytes remain outside
SQL Server. Meeting deletion now coordinates local audio removal with database
aggregate deletion through a reversible trash move. There is no automatic
expiry or soft deletion. Authentication and ownership enforcement remain
deferred, so local filesystem hardening does not make the current API suitable
for untrusted multi-user deployment.

Remote deletion is best-effort. A failed delete cannot guarantee immediate
removal by the remote service; provider retention rules still apply.
