# Private evaluation dataset protocol

## Scope and privacy

The real dataset is private and local. Audio, reference transcripts, timestamp
annotations, speaker annotations, and `Datasets/manifest.local.json` must not be
committed. Git contains only schemas, anonymized metadata after review, and
generated fixtures whose publication rights are explicit.

Each participant must consent to the intended university-project use before
recording. Keep the consent record locally and store only `local-record` in the
manifest. A recording without documented consent and usage rights is excluded.
Removing a name from a transcript does not by itself make voice audio anonymous.

## File naming and audio

- Use opaque IDs such as `dev-001` and `final-001`; do not use participant names.
- Store media under ignored `Datasets/audio/`.
- Store independently written UTF-8 references under ignored
  `Datasets/references/`.
- Store reviewed timestamp/speaker annotations under ignored
  `Datasets/annotations/`.
- Preserve the original permitted file. Conversion to mono 16 kHz WAV is a
  derived processing artifact, not a replacement for the original.
- Record the SHA-256 of the original, actual duration, format, sample rate, and
  channel count.

## Required metadata

Every manifest entry records source type, checksum, duration, speaker count,
language/accent, microphone, noise, overlap, consent record, usage rights,
publication status, annotation-review state, split, and lock state.

The development split has at least five recordings. The final-test split has at
least three recordings and every final entry is locked. Across the real set,
cover clean and noisy speech, conversational Persian, multiple speakers,
accent variation, and overlapping speech.

## Human references and annotations

Write the reference while listening to the entire recording without consulting
ASR output. A second listening pass must verify it. For the diarization subset,
review speaker intervals and timestamps against the audio. Never infer real
speaker identities; use anonymous labels such as `SPEAKER_00`.

## Split locking

The local manifest remains `draft` while recordings or annotations are
incomplete. Set it to `locked` only after all final-test entries are fixed and
verified. Do not use final-test audio, references, annotations, metrics, or
errors to select models, prompts, thresholds, or normalization rules.

## Evaluation outputs

Per-record evaluation JSON stays under ignored `Evaluation/results/`. Aggregate
JSON/CSV may contain recording IDs, split, numeric metrics, counts, and error
totals, but never private transcript text, filenames containing identities, or
local paths.
