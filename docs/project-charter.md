# Project charter

## Approved titles

- English: **Meeting Minutes AI**
- Persian: **سامانه هوشمند تولید صورت‌جلسه**

## Purpose

Build a maintainable university MVP that converts meeting audio into a
reviewable transcript and structured meeting minutes. The user must be able to
inspect and edit generated content before treating it as final.

## MVP success criteria

1. A Persian multi-speaker recording can be processed through the AI service.
2. Raw and derived transcript forms remain distinct and traceable.
3. Structured minutes never invent missing participants, dates, decisions,
   assignees, or deadlines.
4. Evidence segment IDs connect generated minutes to transcript segments.
5. The frontend supports review and correction before export.
6. Automated tests and a reproducible evaluation set support every quality
   claim.
7. A new developer can recreate the pinned environment from repository
   documentation.

## Privacy and data rules

- Send every accepted recording to Gemini first, including private meetings.
- Use local faster-whisper only when Gemini transcription fails.
- Use Gemini only for transcript cleaning and meeting-minutes generation.
- Clearly disclose free-tier external processing to users.
- Keep private media, transcripts, annotations, logs, generated outputs, model
  weights, tokens, and secrets out of Git.
- Store only anonymized metadata and generated or explicitly permitted test
  fixtures in the repository.
- Do not log full transcripts, tokens, credentials, or private filesystem
  paths.
- Define retention and deletion behavior before product data is persisted.

## Constraints

- University-project scope and understandable architecture.
- The free Gemini API and existing local STT require no paid API by default.
- Persian support is a core requirement.
- The available reference machine has a 6 GB RTX 3060 Laptop GPU.
- Missing facts remain null or empty and uncertainty stays visible.

## Out of scope for Phases 0–3

- Authentication, authorization, and multi-user ownership
- Production database and meeting CRUD
- Deployment, billing, collaboration, and real-time transcription
- Final export workflows and polished product UI
- Claims of production accuracy before the locked final evaluation
