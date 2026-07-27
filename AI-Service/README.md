# Persian Transcription Prototype

This first prototype performs one task only: it converts a local Persian audio
file into a UTF-8 text transcript with segment-level timestamps.

It uses:

- the multilingual Whisper `small` checkpoint;
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
python -m pip install -r .\AI-Service\requirements.txt
```

The first successful run downloads the Whisper `small` model files. The model
download requires internet access, but the meeting audio itself is processed
locally and is not sent to an external transcription service.

If startup reports that a DLL such as `cublas64_12.dll` or a cuDNN DLL cannot
be loaded, stop and install the matching NVIDIA runtime prerequisites. Do not
change the script to CPU unless CPU execution is an explicit project decision.

## Run

```powershell
python .\AI-Service\transcribe.py "D:\path\meeting.mp3"
```

By default, `meeting.transcript.txt` is written beside the input audio:

```text
[00:00:00.000 --> 00:00:04.820] متن فارسی بخش اول
[00:00:04.820 --> 00:00:09.140] متن فارسی بخش دوم
```

Choose another output location with:

```powershell
python .\AI-Service\transcribe.py "D:\path\meeting.mp3" --output ".\AI-Service\output\meeting.txt"
```

The default remains Whisper `small`. For a controlled comparison using the
same audio and reference transcript, select one of the approved checkpoints:

```powershell
python .\AI-Service\transcribe.py "D:\path\meeting.mp3" `
  --model medium `
  --output ".\AI-Service\output\meeting.medium.transcript.txt"
```

Supported comparison checkpoints are `small`, `medium`, and
`large-v3-turbo`. All use CUDA with `float16`; selecting a checkpoint never
enables a CPU fallback.

Supported input extensions are `.aac`, `.flac`, `.m4a`, `.mp3`, `.ogg`,
`.wav`, `.webm`, and `.wma`. The audio file and generated transcript are local
data and must not be committed.

## Current boundary

`small` is a starting baseline, not a claim that it is the best Persian model.
This prototype does not clean or summarize the transcript, identify speakers,
extract decisions, generate meeting minutes, or expose an API.

## Evaluate WER and CER

Accuracy evaluation requires a human-written reference transcript. Do not use
the Whisper output as its own reference, because that would make the reported
accuracy meaningless. Keep private reference transcripts under
`Datasets/references/`; that directory is ignored by Git.

After manually transcribing and checking an audio sample, compare the reference
with the generated timestamped transcript:

```powershell
python .\AI-Service\evaluate_transcript.py `
  ".\Datasets\references\sample.reference.txt" `
  ".\sample.transcript.txt" `
  --output ".\Evaluation\results\sample.small.json"
```

The command prints and optionally saves:

- Word Error Rate (WER);
- Character Error Rate (CER), excluding whitespace;
- reference and hypothesis word/character counts;
- word-level and character-level error counts;
- the exact `jiwer` version and normalization configuration.

Before scoring, the evaluator applies Unicode NFKC normalization, maps Arabic
`ي`, `ى`, and `ك` to Persian `ی`, `ی`, and `ک`, removes Unicode punctuation,
turns the Persian zero-width non-joiner into a word boundary, and collapses
whitespace. It does not rewrite words or numbers. The generated transcript may
contain the timestamp format produced by `transcribe.py`; those prefixes are
removed automatically.

The JSON result is suitable for copying into the project Experiment Log.

## Initial checkpoint comparison

The first controlled comparison used the same 173.44-second Persian sample,
locally corrected draft reference, CUDA `float16` configuration, and
normalization rules for every checkpoint:

| Checkpoint | WER | CER | Runtime |
| --- | ---: | ---: | ---: |
| `small` | 48.82% | 13.60% | 19.96 s |
| `medium` | 29.01% | 7.25% | 40.55 s |
| `large-v3-turbo` | 24.29% | 5.44% | 18.52 s |

`large-v3-turbo` is the initial checkpoint because it produced the lowest WER
and CER and the shortest runtime on the available RTX 3060 Laptop GPU. This is
a provisional single-sample decision, not a final quality claim. The draft
reference must first be independently checked by listening to the complete
audio. Then repeat the comparison on the fixed 5–10-sample development set
before final model selection.

Peak VRAM is not reported for these runs because `nvidia-smi` returned
unavailable or zero memory values under Windows WDDM. All three checkpoints
did complete successfully on the 6 GB GPU; a future benchmark should use a
reliable VRAM measurement method.
