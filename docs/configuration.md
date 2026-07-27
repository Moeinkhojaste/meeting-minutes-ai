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

The Python STT CLI implements the same order for `MM_AI_STT_MODEL`. Vite uses
its standard environment and `.env` resolution for `VITE_API_BASE_URL`; build
environment values override `.env`.

## Safe template

Copy `.env.example` to `.env`. The example contains no real token. `.env` is
ignored and must never be committed.

| Variable | Safe default | Purpose |
| --- | --- | --- |
| `ASPNETCORE_URLS` | `http://localhost:5080` | Backend listen URL |
| `VITE_API_BASE_URL` | `http://localhost:5080` | Frontend backend URL |
| `MM_AI_STT_MODEL` | `large-v3-turbo` | Selected Whisper checkpoint |
| `MM_AI_DEVICE` | `cuda` | Required STT device |
| `MM_AI_COMPUTE_TYPE` | `float16` | Required CUDA compute type |
| `MM_AI_MAX_UPLOAD_MIB` | `500` | Future AI-service upload limit |
| `MM_AI_MAX_AUDIO_SECONDS` | `14400` | Future four-hour limit |
| `OLLAMA_HOST` | `http://localhost:11434` | Local Ollama API |
| `MM_AI_OLLAMA_MODEL` | `qwen2.5:7b-instruct-q4_K_M` | Planned local minutes model |
| `HF_TOKEN` | empty | Local pyannote model-access token |

There is no CPU fallback. An invalid device or missing CUDA runtime must produce
a controlled failure.
