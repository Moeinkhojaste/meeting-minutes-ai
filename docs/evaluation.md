# Phase 3 evaluation

Do not claim quality from mocked tests or generated fixtures. Use independently
reviewed reference transcripts and annotations.

`ai-service/evaluate_pipeline.py` accepts reference and hypothesis bundles
containing a raw transcript and structured minutes. It writes aggregate metrics
without copying transcript or minutes text:

- raw and normalized WER/CER;
- speaker-attribution accuracy and diarization error rate;
- timestamp mean absolute error;
- decision precision, recall, and F1;
- action-item precision, recall, and F1;
- evidence accuracy;
- hallucination and missing-information rates;
- transcription, minutes, and total latency;
- optional numeric human scores for coverage, factual consistency, and
  readability.

Example:

```powershell
.\.venv\Scripts\python.exe .\ai-service\evaluate_pipeline.py `
  .\Evaluation\reference.json `
  .\Evaluation\hypothesis.json `
  --output .\Evaluation\results\phase-3.json `
  --transcription-latency-seconds 12.4 `
  --minutes-latency-seconds 3.1
```

Keep the existing faster-whisper benchmark as the scientific comparator.
Model selection requires the locked evaluation protocol and human review; a
successful provider call proves integration, not accuracy.
