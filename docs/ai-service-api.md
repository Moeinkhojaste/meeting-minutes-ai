# AI-service API

## Processing modes

| Mode | Gemini model | Local STT fallback |
| --- | --- | --- |
| `fast` | `gemini-3.5-flash-lite` | Whisper `small` |
| `quality` | `gemini-3.6-flash` | Whisper `large-v3-turbo` |

`fast` is the default. A request never switches modes.

## Endpoints

### `GET /health/live`

Returns `{"status":"live"}` when the process can serve HTTP.

### `GET /health/ready`

Returns provider configuration status. A missing Gemini key reports
`degraded`: local transcription can still work, but cleaning and minutes cannot.

### `POST /v1/transcriptions`

Multipart fields:

- `audio`: required audio file;
- `mode`: optional `fast` or `quality`.

Gemini is attempted once. On an eligible Gemini failure, faster-whisper is
attempted once. Metadata identifies the requested mode, primary Gemini model,
actual provider/model, fallback status, and safe fallback reason.

### `POST /v1/minutes`

JSON contains `rawTranscript` and `mode`. Gemini returns a cleaned transcript
and structured minutes in one schema-constrained generation. There is no local
LLM fallback.

### `POST /v1/process`

Multipart input matches `/v1/transcriptions`. A primary-success request makes
exactly two Gemini generation calls: transcription, then cleaning/minutes.

If stage two fails after successful transcription, HTTP 200 returns
`status: "partial"`, preserves the raw transcript, sets cleaned transcript and
minutes to `null`, and includes a safe stage error.

## Errors

Every non-2xx response has exactly:

```json
{
  "code": "ERROR_CODE",
  "message": "Safe message",
  "correlationId": "uuid",
  "retryable": false
}
```

Responses and logs never expose keys, content, prompts, SDK payloads, Gemini
file URIs, or local paths.

## Audio validation

The service checks magic bytes and uses FFprobe to validate the container,
codec, audio stream, duration, and decodability. It accepts at most 500 MiB and
90 minutes by default. Unsupported M4A/WebM/WMA containers are normalized to a
temporary FLAC file. Inputs up to 20 MiB are inline; larger files use Gemini
Files API and are deleted remotely in `finally`.
