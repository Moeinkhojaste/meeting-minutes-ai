# Phase 0 Persian STT benchmark

Status: accepted initial baseline

## Evidence identity

- Dataset version: `persian-current-sample-v1`
- Audio duration: 173.44 seconds
- Audio SHA-256:
  `12c3c28d64c2aecf013f9d8efbef8a222b68e497563b801cca70bef5dfc9d5cd`
- Independently supplied human reference SHA-256:
  `17c924b7831270565e68554e4b2e7ee455f9c22799141c6c3ce8c9247d9bb058`
- Benchmark commit:
  `e7b5549457dfa78edad77099d9c470b1ab8227ab`
- Configuration: Persian transcription, CUDA, `float16`, one warm-up and three
  measured runs per checkpoint
- Scoring: normalization policy version 1; explicit speaker-label prefixes
  removed from the annotated reference before raw and normalized scoring
- Private audio, reference text, generated transcripts, logs, and complete
  benchmark JSON remain local and ignored.

An earlier run at commit `8a6713c` incorrectly scored the reference's speaker
annotations as spoken words. It is invalid and superseded by the result below.

## Environment

- Windows 11 Pro 64-bit, version 10.0.26200
- Python 3.12.10
- NVIDIA GeForce RTX 3060 Laptop GPU, 6,144 MiB
- NVIDIA driver 596.49
- Intel Core i7-11800H, 8 cores and 16 logical processors
- System RAM: 16,167 MiB
- `faster-whisper` 1.2.1
- CTranslate2 4.8.1
- `jiwer` 4.0.0
- `psutil` 7.0.0
- `nvidia-ml-py` 13.580.82
- CUDA 12 cuBLAS wheel 12.9.2.10
- cuDNN 8 wheel 8.9.7.29

## Results

Metrics were identical across the three measured runs for each checkpoint.
Runtime and memory columns report mean ± population standard deviation.
Device VRAM is the peak total device usage observed by NVML, not an isolated
per-process allocation.

| Checkpoint | Raw WER | Raw CER | Normalized WER | Normalized CER | Runtime, s | RTF | Peak RAM, MiB | Peak VRAM, MiB |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `small` | 82.23% | 48.43% | 74.21% | 49.71% | 20.07 ± 0.73 | 0.1157 ± 0.0042 | 658.44 ± 0.28 | 1,129.35 ± 0.00 |
| `medium` | 73.21% | 45.83% | 62.53% | 46.12% | 38.20 ± 0.43 | 0.2202 ± 0.0025 | 1,620.08 ± 17.56 | 2,793.35 ± 0.00 |
| `large-v3-turbo` | 70.03% | 43.96% | 59.37% | 44.50% | 16.14 ± 0.62 | 0.0930 ± 0.0036 | 1,696.83 ± 19.38 | 2,473.35 ± 0.00 |

All twelve executions completed successfully. Failure count was zero.

Normalized word errors for one representative deterministic run:

| Checkpoint | Substitutions | Deletions | Insertions |
| --- | ---: | ---: | ---: |
| `small` | 221 | 49 | 35 |
| `medium` | 175 | 49 | 33 |
| `large-v3-turbo` | 167 | 43 | 34 |

Representative manual error categories included omitted conversational phrases,
misspelled Persian words, English technical-term segmentation, and short
phrase substitutions. Review found no factual-error pattern in
`large-v3-turbo` that outweighed its metric advantage; it also had the most
normalized word hits and the fewest substitutions and deletions.

## Decision

Select `large-v3-turbo` as the initial Persian STT checkpoint.

It ranks first on normalized WER, then normalized CER, passes the manual
quality guardrail, and is also the fastest checkpoint on this machine. Its
observed RAM and VRAM use fit the available 6 GB GPU environment.

This decision is limited to one short, independently transcribed sample.
Normalized WER of 59.37% is not acceptable evidence of production accuracy.
The checkpoint must be evaluated again on the versioned Phase 2 development
set, and the locked final-test set must not be used for tuning.
