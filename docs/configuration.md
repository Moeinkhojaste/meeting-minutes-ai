# Configuration

## Precedence

For a configurable value, the repository uses this order:

1. Explicit CLI argument
2. Existing process environment variable
3. Local `.env` value
4. Safe application default

The backend loads `.env` values into its own process only when the same
environment variable is absent. ASP.NET Core then applies environment variables
and command-line arguments normally, preserving the order above.

The Python CLIs implement the same order for non-secret settings. The Gemini
key is read only from the process environment or ignored `.env`. Vite uses its
standard environment and `.env` resolution for `VITE_API_BASE_URL`.

## Safe template

Copy `.env.example` to `.env`. The example contains no real token. `.env` is
ignored and must never be committed.

| Variable | Safe default | Purpose |
| --- | --- | --- |
| `ASPNETCORE_URLS` | `http://localhost:5080` | Backend listen URL |
| `ConnectionStrings__DefaultConnection` | LocalDB in development | SQL Server connection string override |
| `AudioStorage__RootPath` | user local application data | Private backend audio root; keep outside web roots |
| `AudioStorage__MaxBytes` | `524288000` | Backend streaming upload limit |
| `AIService__BaseUrl` | `http://127.0.0.1:8000` | Backend-to-AI-service URL |
| `AIService__TimeoutSeconds` | `7200` | Per-request typed-client timeout |
| `AIService__MaxResponseBytes` | `67108864` | Maximum buffered AI JSON response |
| `VITE_API_BASE_URL` | `http://localhost:5080` | Frontend backend URL |
| `MM_AI_STT_MODEL` | `large-v3-turbo` | Selected Whisper checkpoint |
| `MM_AI_DEVICE` | `cuda` | Required STT device |
| `MM_AI_COMPUTE_TYPE` | `float16` | Required CUDA compute type |
| `GEMINI_API_KEY` | empty | Secret Gemini API key; never pass on the CLI |
| `MM_AI_DEFAULT_MODE` | `fast` | Default `fast` or `quality` mode |
| `MM_AI_GEMINI_FAST_MODEL` | `gemini-3.5-flash-lite` | Fast Gemini model |
| `MM_AI_GEMINI_QUALITY_MODEL` | `gemini-3.6-flash` | Quality Gemini model |
| `MM_AI_MAX_AUDIO_BYTES` | `524288000` | Maximum upload size |
| `MM_AI_MAX_AUDIO_DURATION_SECONDS` | `5400` | Maximum duration |
| `MM_AI_GEMINI_INLINE_LIMIT_BYTES` | `20971520` | Files API threshold |
| `MM_AI_LOCAL_FAST_STT_MODEL` | `small` | Fast local STT fallback |
| `MM_AI_LOCAL_QUALITY_STT_MODEL` | `large-v3-turbo` | Quality STT fallback |
| `HF_TOKEN` | empty | Local pyannote model-access token |

There is no CPU fallback. An invalid device or missing CUDA runtime must produce
a controlled local-STT failure. There are intentionally no local-minutes model
settings in Phase 3; Qwen and other local LLMs are deferred.

The committed development configuration uses the installed
`(localdb)\MSSQLLocalDB` instance and database `MeetingMinutesAI`. Production
has no connection-string fallback and must provide
`ConnectionStrings__DefaultConnection`. The application never applies EF Core
migrations automatically during startup.
