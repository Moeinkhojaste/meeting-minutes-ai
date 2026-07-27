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

Supported input extensions are `.aac`, `.flac`, `.m4a`, `.mp3`, `.ogg`,
`.wav`, `.webm`, and `.wma`. The audio file and generated transcript are local
data and must not be committed.

## Current boundary

`small` is a starting baseline, not a claim that it is the best Persian model.
This prototype does not clean or summarize the transcript, identify speakers,
extract decisions, generate meeting minutes, expose an API, or evaluate
accuracy.
