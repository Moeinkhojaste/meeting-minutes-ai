# Phase 3 AI-service architecture

```text
FastAPI or CLI
      |
Audio validation and temporary normalization
      |
Pipeline orchestration
      |
      +-- Stage 1: Gemini transcription
      |                 |
      |                 +-- failure -> CUDA faster-whisper once
      |
      +-- Stage 2: Gemini cleaning and minutes
                        |
                        +-- failure -> safe error, no local LLM
```

Routes and CLI commands only translate I/O. Typed configuration resolves
models and limits. Provider adapters contain SDK/model details. Orchestration
owns ordering, fallback, durations, and partial results. Pydantic schemas own
cross-field validation. Prompts are versioned and delimit meeting content as
untrusted data.

The minutes provider is represented by an interface so another implementation
can be added in a later phase. Phase 3 registers only Gemini and contains no
`LocalMinutesProvider`, Qwen, Ollama, or local LLM runtime.

The existing `transcribe.py` behavior remains available. Its internal
structured-segment function lets the provider adapter reuse the same CUDA,
CTranslate2, model, and no-CPU-fallback behavior.
