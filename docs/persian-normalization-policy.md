# Persian STT normalization and scoring policy

Policy version: 1

This policy is frozen for the Phase 0 Persian speech-to-text comparison. Any
future change requires a new policy version and a fresh benchmark; historical
results must retain the version used to produce them.

## Immutable raw text

Human references and generated transcripts are UTF-8 source records. The
evaluator never edits or overwrites them. A normalized derivative may be
written only to a separate path. Evaluation JSON and aggregate benchmark files
do not contain private transcript text.

Generated timestamp prefixes in the form
`[HH:MM:SS.mmm --> HH:MM:SS.mmm]` are metadata and are removed before both raw
and normalized scoring. For raw scoring, no other content transformation is
performed except trimming outer whitespace. When an annotated human reference
contains one explicit `<speaker>:` prefix per turn, the evaluator may remove
those prefixes before scoring only through the explicit
`--reference-speaker-labels` option. The annotation remains unchanged in the
source file, and the scoring JSON records whether this option was used.

## Normalization order

The normalized derivative applies these deterministic operations in order:

1. Unicode NFKC normalization.
2. Arabic yeh `ي` and alef maksura `ى` to Persian yeh `ی`.
3. Arabic kaf `ك` to Persian kaf `ک`.
4. Persian and Arabic digit glyphs to ASCII digits. Spoken number words are
   not rewritten.
5. Lowercase Latin characters.
6. Convert the zero-width non-joiner to a word boundary.
7. Remove tatweel and Arabic combining diacritics.
8. Convert every Unicode punctuation character to a boundary.
9. Collapse repeated Unicode whitespace and trim outer whitespace.

This policy intentionally does not stem words, join or split lexical forms
beyond the boundary rules above, expand abbreviations, or convert spoken
numbers.

## Metrics

Raw WER and CER use the timestamp-free source text. Raw CER includes
whitespace. Normalized WER uses whitespace-delimited normalized tokens.
Normalized CER removes all Unicode whitespace from both normalized strings
before comparison.

Every metric set reports the reference and hypothesis sizes and the number of
hits, substitutions, deletions, and insertions. The JSON evaluator output uses
schema version 2 and records the `jiwer` version plus the complete scoring
metadata.

The independently verified reference is the authority. ASR output must never
be used to create or silently correct that reference.
