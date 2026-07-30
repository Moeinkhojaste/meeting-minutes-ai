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
- Gemini Files API objects are deleted in `finally` when a remote file exists.
- Logs exclude audio, transcripts, minutes, prompts, credentials, file URIs,
  and local paths.
- Recordings, transcripts, outputs, references, and evaluation results remain
  ignored and must not be committed.

Remote deletion is best-effort. A failed delete cannot guarantee immediate
removal by the remote service; provider retention rules still apply.
