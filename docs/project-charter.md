# Project charter

## Approved titles

- English: **Meeting Minutes AI**
- Persian: **سامانه هوشمند تولید صورت‌جلسه**

## Purpose

Build a maintainable university MVP that locally converts permitted meeting
audio into a reviewable transcript and structured meeting minutes. The user
must be able to inspect and edit generated content before treating it as final.

## MVP success criteria

1. A permitted Persian multi-speaker recording can be processed locally.
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

- Process recordings locally by default.
- Never send audio or transcript content to an external service unless a later
  explicit configuration and documented consent decision allows it.
- Keep private media, transcripts, annotations, logs, generated outputs, model
  weights, tokens, and secrets out of Git.
- Store only anonymized metadata and generated or explicitly permitted test
  fixtures in the repository.
- Do not log full transcripts, tokens, credentials, or private filesystem
  paths.
- Define retention and deletion behavior before product data is persisted.

## Constraints

- University-project scope and understandable architecture.
- Local or free model execution; no paid API is required by default.
- Persian support is a core requirement.
- The available reference machine has a 6 GB RTX 3060 Laptop GPU.
- Missing facts remain null or empty and uncertainty stays visible.

## Out of scope for Phases 0–3

- Authentication, authorization, and multi-user ownership
- Production database and meeting CRUD
- Deployment, billing, collaboration, and real-time transcription
- Final export workflows and polished product UI
- Claims of production accuracy before the locked final evaluation
