# AI Service

Phase 3 exposes a FastAPI and CLI pipeline with two modes:

- `fast`: `gemini-3.5-flash-lite`, with local Whisper `small` STT fallback;
- `quality`: `gemini-3.6-flash`, with local Whisper `large-v3-turbo` STT
  fallback.

The workflow uses two independent stages. Gemini is attempted once for raw
transcription. Eligible Gemini failures fall back once to the existing
CUDA/faster-whisper implementation. The raw transcript is then sent to Gemini
once for cleaning and structured minutes. Stage two has no local LLM fallback.
Qwen, Ollama, and other local LLM runtimes are explicitly deferred.

## Run the API

```powershell
Push-Location .\ai-service
..\.venv\Scripts\python.exe -m uvicorn main:app --host 127.0.0.1 --port 8000
Pop-Location
```

## Run the Phase 3 CLI

```powershell
Push-Location .\ai-service
..\.venv\Scripts\python.exe -m app.cli process `
  "D:\path\meeting.wav" --mode fast --output ".\output\result.json"
Pop-Location
```

The other commands are `transcribe` and `minutes`. A partial `process` result
preserves the raw transcript and exits with code 2 when Gemini stage two fails.

## Existing local transcription tool

This first prototype performs one task only: it converts a local Persian audio
file into a UTF-8 text transcript with segment-level timestamps.

It uses:

- the multilingual Whisper `large-v3-turbo` checkpoint selected by the
  Phase 0 comparison;
- `faster-whisper` as the Whisper inference implementation;
- CTranslate2 as the inference engine;
- NVIDIA CUDA with `float16`;
- `language="fa"` and `task="transcribe"`.

There is deliberately no CPU fallback. If CTranslate2 cannot use CUDA, the
program stops with an error instead of silently running on the CPU.

## GPU prerequisites

The Python package is only one layer of the GPU setup. Windows must also be
able to load compatible NVIDIA CUDA 12 cuBLAS and cuDNN 8 runtime libraries.
Seeing the GPU in `nvidia-smi` does not prove that these runtime DLLs are
available to CTranslate2.

The Windows-only NVIDIA packages pinned in `requirements.txt` install these
libraries inside the project virtual environment. `transcribe.py` makes their
DLL directories visible to Windows only while the program is running; it does
not modify the system-wide `PATH`.

## Setup

From the repository root, create and activate a virtual environment:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r .\ai-service\requirements.txt
```

The first successful run downloads the Whisper `large-v3-turbo` model files.
The model download requires internet access, but the meeting audio itself is
processed locally and is not sent to an external transcription service.

If startup reports that a DLL such as `cublas64_12.dll` or a cuDNN DLL cannot
be loaded, stop and install the matching NVIDIA runtime prerequisites. Do not
change the script to CPU unless CPU execution is an explicit project decision.

## Run

```powershell
python .\ai-service\transcribe.py "D:\path\meeting.mp3"
```

By default, `meeting.transcript.txt` is written beside the input audio:

```text
[00:00:00.000 --> 00:00:04.820] متن فارسی بخش اول
[00:00:04.820 --> 00:00:09.140] متن فارسی بخش دوم
```

Choose another output location with:

```powershell
python .\ai-service\transcribe.py "D:\path\meeting.mp3" --output ".\ai-service\output\meeting.txt"
```

The default is Whisper `large-v3-turbo`, selected from the Phase 0 controlled
comparison. To reproduce or extend the comparison using the same audio and
reference transcript, select one of the approved checkpoints:

```powershell
python .\ai-service\transcribe.py "D:\path\meeting.mp3" `
  --model medium `
  --output ".\ai-service\output\meeting.medium.transcript.txt"
```

Supported comparison checkpoints are `small`, `medium`, and
`large-v3-turbo`. All use CUDA with `float16`; selecting a checkpoint never
enables a CPU fallback.

Supported input extensions are `.aac`, `.flac`, `.m4a`, `.mp3`, `.ogg`,
`.wav`, `.webm`, and `.wma`. The audio file and generated transcript are local
data and must not be committed.

## Local transcription boundary

`large-v3-turbo` is the selected Phase 0 checkpoint for the current machine and
human-verified sample. The measured normalized WER remains high, so this is a
baseline decision rather than an accuracy claim. The standalone
`transcribe.py` prototype does not clean or summarize the transcript. Those
responsibilities belong to the Phase 3 API and CLI.

## Evaluate WER and CER

Accuracy evaluation requires a human-written reference transcript. Do not use
the Whisper output as its own reference, because that would make the reported
accuracy meaningless. Keep private reference transcripts under
`Datasets/references/`; that directory is ignored by Git.

After manually transcribing and checking an audio sample, compare the reference
with the generated timestamped transcript:

```powershell
python .\ai-service\evaluate_transcript.py `
  ".\Datasets\references\sample.reference.txt" `
  ".\sample.transcript.txt" `
  --output ".\Evaluation\results\sample.small.json"
```

The schema-version-2 result contains two explicitly different scores:

- raw WER/CER after removing generated timestamp prefixes and trimming only
  outer whitespace;
- normalized WER/CER after applying the frozen Persian scoring policy.

Raw CER includes whitespace. Normalized CER excludes all Unicode whitespace.
Both metric sets include reference and hypothesis counts plus word-level and
character-level hits, substitutions, deletions, and insertions. The result also
records the exact `jiwer` version and scoring metadata.

The raw source files are never modified. When an auditable normalized
derivative is needed, save it to a separate ignored path:

```powershell
python .\ai-service\evaluate_transcript.py `
  ".\Datasets\references\sample.reference.txt" `
  ".\sample.transcript.txt" `
  --normalized-reference-output `
    ".\Datasets\references\sample.reference.normalized.txt" `
  --normalized-hypothesis-output `
    ".\Transcripts\sample.normalized.txt"
```

The full normalization policy is documented in
`docs/persian-normalization-policy.md`.

## Controlled checkpoint benchmark

Do not benchmark against a draft reference. The runner refuses to start unless
the reference has been independently verified by listening and
`--reference-verified` is passed:

```powershell
python .\ai-service\benchmark_stt.py `
  ".\private-sample.wav" `
  ".\Datasets\references\sample.verified-reference.txt" `
  --dataset-version "persian-dev-v1" `
  --reference-verified `
  --reference-speaker-labels `
  --output ".\Evaluation\results\persian-dev-v1.checkpoints.json"
```

Use `--reference-speaker-labels` only when every annotated reference turn has
an explicit `<speaker>:` prefix. The evaluator then excludes those annotations
from both raw and normalized scoring while leaving the reference file itself
unchanged. Without the flag, all reference text is scored.

For each of `small`, `medium`, and `large-v3-turbo`, the runner performs one
warm-up followed by three measured CUDA `float16` runs. It records raw and
normalized WER/CER, runtime variation, real-time factor, peak process RAM,
device-level VRAM when available, failures, dependency and hardware metadata,
the dataset version, and the current commit. Private paths and transcript text
are not included in the aggregate result.

Checkpoint selection remains a manual gate. Rank normalized WER first,
normalized CER second, factual/manual Persian quality as a guardrail, and only
then runtime and resource use. The earlier single-run values used an
unverified draft reference, so they are provisional diagnostics and are not a
valid model-selection decision.

The accepted Phase 0 result and its limitations are recorded in
`docs/phase-0-stt-benchmark.md`.

## Dataset and aggregate evaluation

Validate the local private manifest:

```powershell
python .\ai-service\dataset_manifest.py `
  .\Datasets\manifest.local.json
```

Combine evaluator result files into transcript-free JSON and CSV:

```powershell
python .\ai-service\aggregate_evaluations.py `
  --dataset-version persian-private-v1 `
  --entry dev-001:development:.\Evaluation\results\dev-001.json `
  --json-output .\Evaluation\results\aggregate.json `
  --csv-output .\Evaluation\results\aggregate.csv
```

Repeat `--entry` for each recording. Outputs contain only opaque recording IDs,
splits, numeric metrics, and error counts; they never copy transcript text.
