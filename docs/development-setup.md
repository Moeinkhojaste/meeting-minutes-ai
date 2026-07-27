# Windows development setup

## Verified toolchain

| Tool | Version |
| --- | --- |
| Python | 3.12.10 |
| Node.js | 24.18.0 LTS |
| npm | 11.17.0 |
| .NET SDK | 10.0.302 LTS |
| FFmpeg and FFprobe | 8.1.2 |
| Git for Windows | 2.50.1 |
| Ollama | 0.32.4 |
| NVIDIA driver | 596.49 |
| Driver-reported CUDA capability | 13.2 |
| Python CUDA 12 cuBLAS wheel | 12.9.2.10 |
| Python cuDNN 8 wheel | 8.9.7.29 |

The NVIDIA driver's reported CUDA capability is not proof that CTranslate2 can
load its required DLLs. The AI-service verification must also load the pinned
CUDA 12 cuBLAS and cuDNN 8 runtime libraries from `.venv`.

## Installation sources

- .NET SDK: Microsoft `dotnet-install.ps1`, version 10.0.302
- FFmpeg: Gyan full build 8.1.2 linked from ffmpeg.org
- Ollama: official Windows installer 0.32.4

After installing user-level tools, open a new PowerShell window so the updated
user `PATH` is visible.

## Verification

```powershell
python --version
node --version
npm --version
dotnet --version
ffmpeg -version
ffprobe -version
git --version
nvidia-smi
ollama --version
```

Then follow the setup and quality commands in the root README.

## Troubleshooting

### The wrong .NET SDK is selected

Run `dotnet --list-sdks` and confirm 10.0.302 is visible. `global.json` selects
that SDK. Open a new terminal if the user-local SDK path was just added.

### FFmpeg or Ollama is not found

Open a new terminal and inspect the user `PATH`. FFmpeg's versioned `bin`
directory and `%LOCALAPPDATA%\Programs\Ollama` must be present.

### CUDA is visible but transcription fails

`nvidia-smi` proves driver visibility only. Activate `.venv`, install
`ai-service/requirements.txt`, and verify that CTranslate2 can load
`cublas64_12.dll` and `cudnn64_8.dll`. Do not silently switch to CPU.

### npm is slow

Keep the committed lockfile and use `npm ci`. Avoid regenerating dependencies
unless package versions intentionally change.

### Private data appears in Git status

Move recordings to `Datasets/audio/`, references to `Datasets/references/`,
and generated results to `Evaluation/results/`. Confirm with
`git check-ignore -v <path>` before continuing.
