"""Run a controlled, human-reference-gated Persian STT benchmark."""

from __future__ import annotations

import argparse
import json
import platform
import statistics
import subprocess
import sys
import tempfile
import threading
import time
import wave
from importlib.metadata import PackageNotFoundError, version
from pathlib import Path
from typing import Sequence

import psutil

from evaluate_transcript import evaluate_files
from transcribe import COMPUTE_TYPE, DEVICE, SUPPORTED_MODEL_NAMES

MINIMUM_WARMUPS = 1
MINIMUM_MEASURED_RUNS = 3
SAMPLE_INTERVAL_SECONDS = 0.1


def parse_arguments(arguments: Sequence[str] | None = None) -> argparse.Namespace:
    """Parse benchmark command-line arguments."""
    parser = argparse.ArgumentParser(
        description=(
            "Benchmark approved Whisper checkpoints after the reference "
            "transcript has been independently verified."
        )
    )
    parser.add_argument("audio_path", type=Path)
    parser.add_argument("reference_path", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--dataset-version", required=True)
    parser.add_argument(
        "--reference-verified",
        action="store_true",
        help="Confirm that the reference was independently checked by listening.",
    )
    parser.add_argument(
        "--reference-speaker-labels",
        action="store_true",
        help="Exclude explicit '<speaker>:' prefixes from reference scoring.",
    )
    parser.add_argument(
        "--models",
        nargs="+",
        choices=SUPPORTED_MODEL_NAMES,
        default=list(SUPPORTED_MODEL_NAMES),
    )
    parser.add_argument("--warmups", type=int, default=MINIMUM_WARMUPS)
    parser.add_argument("--runs", type=int, default=MINIMUM_MEASURED_RUNS)
    return parser.parse_args(arguments)


def validate_benchmark_request(args: argparse.Namespace) -> None:
    """Reject benchmarks that would violate the Phase 0 evidence policy."""
    if not args.reference_verified:
        raise ValueError(
            "Benchmark blocked: independently verify the reference by "
            "listening, then pass --reference-verified."
        )
    if args.warmups < MINIMUM_WARMUPS:
        raise ValueError(
            f"At least {MINIMUM_WARMUPS} warm-up run is required."
        )
    if args.runs < MINIMUM_MEASURED_RUNS:
        raise ValueError(
            f"At least {MINIMUM_MEASURED_RUNS} measured runs are required."
        )
    for path, label in (
        (args.audio_path, "Audio"),
        (args.reference_path, "Reference"),
    ):
        if not path.exists() or not path.is_file():
            raise ValueError(f"{label} file does not exist: {path}")
    if not args.dataset_version.strip():
        raise ValueError("Dataset version cannot be empty.")


def summarize_values(values: Sequence[float]) -> dict[str, float]:
    """Return stable summary statistics for measured values."""
    if not values:
        raise ValueError("At least one value is required.")
    return {
        "mean": statistics.fmean(values),
        "standardDeviation": statistics.pstdev(values),
        "minimum": min(values),
        "maximum": max(values),
    }


def run_benchmark(args: argparse.Namespace) -> dict[str, object]:
    """Run warm-up and measured subprocesses for every requested checkpoint."""
    validate_benchmark_request(args)
    audio_path = args.audio_path.expanduser().resolve()
    reference_path = args.reference_path.expanduser().resolve()
    duration_seconds = _wav_duration_seconds(audio_path)
    benchmark_models: list[dict[str, object]] = []

    with tempfile.TemporaryDirectory(prefix="meeting-minutes-stt-") as temp_name:
        temp_directory = Path(temp_name)
        for model_name in args.models:
            for warmup_index in range(args.warmups):
                warmup_path = temp_directory / (
                    f"{model_name}.warmup-{warmup_index + 1}.transcript.txt"
                )
                warmup = _run_transcription(
                    audio_path,
                    warmup_path,
                    model_name,
                )
                if warmup["status"] != "completed":
                    benchmark_models.append(
                        {
                            "model": model_name,
                            "status": "failed",
                            "stage": "warmup",
                            "failure": warmup["failure"],
                            "runs": [],
                        }
                    )
                    break
            else:
                measured_runs = []
                for run_index in range(args.runs):
                    transcript_path = temp_directory / (
                        f"{model_name}.run-{run_index + 1}.transcript.txt"
                    )
                    measured = _run_transcription(
                        audio_path,
                        transcript_path,
                        model_name,
                    )
                    measured["run"] = run_index + 1
                    if measured["status"] == "completed":
                        measured["rtf"] = (
                            measured["runtimeSeconds"] / duration_seconds
                        )
                        measured["evaluation"] = evaluate_files(
                            reference_path,
                            transcript_path,
                            reference_speaker_labels=(
                                args.reference_speaker_labels
                            ),
                        )
                    measured_runs.append(measured)

                successful_runs = [
                    run for run in measured_runs
                    if run["status"] == "completed"
                ]
                benchmark_models.append(
                    _build_model_result(
                        model_name,
                        measured_runs,
                        successful_runs,
                    )
                )

    return {
        "schemaVersion": 1,
        "datasetVersion": args.dataset_version,
        "referenceVerified": True,
        "audio": {
            "durationSeconds": duration_seconds,
            "privatePathRecorded": False,
        },
        "configuration": {
            "language": "fa",
            "task": "transcribe",
            "device": DEVICE,
            "computeType": COMPUTE_TYPE,
            "warmupsPerModel": args.warmups,
            "measuredRunsPerModel": args.runs,
            "referenceSpeakerLabelsRemoved": (
                args.reference_speaker_labels
            ),
            "samplingIntervalSeconds": SAMPLE_INTERVAL_SECONDS,
        },
        "environment": _environment_metadata(),
        "models": benchmark_models,
        "selection": {
            "selectedModel": None,
            "status": "manual-review-required",
            "rule": (
                "normalized WER, normalized CER, manual factual quality, "
                "then runtime and resource use"
            ),
        },
    }


def _build_model_result(
    model_name: str,
    measured_runs: list[dict[str, object]],
    successful_runs: list[dict[str, object]],
) -> dict[str, object]:
    """Build model-level summaries without hiding failed runs."""
    if len(successful_runs) != len(measured_runs):
        return {
            "model": model_name,
            "status": "failed",
            "runs": measured_runs,
            "manualPersianQuality": None,
            "representativeErrors": [],
        }

    runtime_values = [
        float(run["runtimeSeconds"]) for run in successful_runs
    ]
    rtf_values = [float(run["rtf"]) for run in successful_runs]
    ram_values = [float(run["peakProcessRamMiB"]) for run in successful_runs]
    vram_values = [
        float(run["peakDeviceVramMiB"])
        for run in successful_runs
        if run["peakDeviceVramMiB"] is not None
    ]
    raw_wer_values = [
        float(run["evaluation"]["metrics"]["raw"]["wer"])
        for run in successful_runs
    ]
    raw_cer_values = [
        float(run["evaluation"]["metrics"]["raw"]["cer"])
        for run in successful_runs
    ]
    normalized_wer_values = [
        float(run["evaluation"]["metrics"]["normalized"]["wer"])
        for run in successful_runs
    ]
    normalized_cer_values = [
        float(run["evaluation"]["metrics"]["normalized"]["cer"])
        for run in successful_runs
    ]
    return {
        "model": model_name,
        "status": "completed",
        "summary": {
            "runtimeSeconds": summarize_values(runtime_values),
            "rtf": summarize_values(rtf_values),
            "peakProcessRamMiB": summarize_values(ram_values),
            "peakDeviceVramMiB": (
                summarize_values(vram_values) if vram_values else None
            ),
            "rawWer": summarize_values(raw_wer_values),
            "rawCer": summarize_values(raw_cer_values),
            "normalizedWer": summarize_values(normalized_wer_values),
            "normalizedCer": summarize_values(normalized_cer_values),
        },
        "runs": measured_runs,
        "manualPersianQuality": None,
        "representativeErrors": [],
    }


def _run_transcription(
    audio_path: Path,
    output_path: Path,
    model_name: str,
) -> dict[str, object]:
    """Run transcription in a subprocess while sampling RAM and GPU memory."""
    command = [
        sys.executable,
        str(Path(__file__).with_name("transcribe.py")),
        str(audio_path),
        "--model",
        model_name,
        "--output",
        str(output_path),
    ]
    gpu_sampler = _GpuMemorySampler()
    process = subprocess.Popen(
        command,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
    )
    monitor = psutil.Process(process.pid)
    peak_ram_bytes = 0
    started = time.perf_counter()
    gpu_sampler.start()
    while process.poll() is None:
        peak_ram_bytes = max(peak_ram_bytes, _process_tree_rss(monitor))
        time.sleep(SAMPLE_INTERVAL_SECONDS)
    stdout, stderr = process.communicate()
    runtime_seconds = time.perf_counter() - started
    gpu_sampler.stop()
    peak_ram_bytes = max(peak_ram_bytes, _process_tree_rss(monitor))

    if process.returncode != 0:
        return {
            "status": "failed",
            "runtimeSeconds": runtime_seconds,
            "peakProcessRamMiB": peak_ram_bytes / (1024 * 1024),
            "peakDeviceVramMiB": gpu_sampler.peak_mib,
            "failure": {
                "exitCode": process.returncode,
                "message": _safe_failure_message(
                    stderr or stdout,
                    private_paths=(audio_path, output_path),
                ),
            },
        }
    return {
        "status": "completed",
        "runtimeSeconds": runtime_seconds,
        "peakProcessRamMiB": peak_ram_bytes / (1024 * 1024),
        "peakDeviceVramMiB": gpu_sampler.peak_mib,
        "failure": None,
    }


def _process_tree_rss(process: psutil.Process) -> int:
    """Return current RSS for a process and its children."""
    try:
        processes = [process, *process.children(recursive=True)]
        return sum(item.memory_info().rss for item in processes)
    except (psutil.NoSuchProcess, psutil.AccessDenied):
        return 0


class _GpuMemorySampler:
    """Sample device-level VRAM without making availability a success claim."""

    def __init__(self) -> None:
        self.peak_mib: float | None = None
        self._stop = threading.Event()
        self._thread: threading.Thread | None = None

    def start(self) -> None:
        self._thread = threading.Thread(target=self._sample, daemon=True)
        self._thread.start()

    def stop(self) -> None:
        self._stop.set()
        if self._thread is not None:
            self._thread.join(timeout=2)

    def _sample(self) -> None:
        try:
            import pynvml

            pynvml.nvmlInit()
            handle = pynvml.nvmlDeviceGetHandleByIndex(0)
            while not self._stop.is_set():
                memory = pynvml.nvmlDeviceGetMemoryInfo(handle)
                used_mib = memory.used / (1024 * 1024)
                self.peak_mib = max(self.peak_mib or 0, used_mib)
                self._stop.wait(SAMPLE_INTERVAL_SECONDS)
            pynvml.nvmlShutdown()
        except Exception:
            self.peak_mib = None


def _wav_duration_seconds(audio_path: Path) -> float:
    """Read duration from the current Phase 0 WAV without external tools."""
    if audio_path.suffix.lower() != ".wav":
        raise ValueError(
            "Phase 0 benchmarking currently requires WAV input. "
            "FFprobe-based inspection is introduced in Phase 3."
        )
    with wave.open(str(audio_path), "rb") as audio:
        return audio.getnframes() / audio.getframerate()


def _environment_metadata() -> dict[str, object]:
    """Collect reproducibility metadata without paths or secrets."""
    return {
        "commitHash": _command_output(["git", "rev-parse", "HEAD"]),
        "python": platform.python_version(),
        "platform": platform.platform(),
        "packages": {
            package: _package_version(package)
            for package in (
                "faster-whisper",
                "ctranslate2",
                "jiwer",
                "psutil",
                "nvidia-ml-py",
                "nvidia-cublas-cu12",
                "nvidia-cudnn-cu12",
            )
        },
        "gpu": _command_output(
            [
                "nvidia-smi",
                "--query-gpu=name,driver_version,memory.total",
                "--format=csv,noheader",
            ]
        ),
    }


def _package_version(package: str) -> str | None:
    try:
        return version(package)
    except PackageNotFoundError:
        return None


def _command_output(command: list[str]) -> str | None:
    try:
        result = subprocess.run(
            command,
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
        )
        return result.stdout.strip()
    except (OSError, subprocess.CalledProcessError):
        return None


def _safe_failure_message(
    output: str,
    *,
    private_paths: Sequence[Path] = (),
) -> str:
    """Keep only a short final error line and never include transcript text."""
    lines = [line.strip() for line in output.splitlines() if line.strip()]
    message = lines[-1] if lines else "Transcription failed."
    for path in private_paths:
        message = message.replace(str(path), "<private-path>")
    return message[:500]


def main(arguments: Sequence[str] | None = None) -> int:
    """Run the benchmark and save aggregate evidence as UTF-8 JSON."""
    args = parse_arguments(arguments)
    try:
        result = run_benchmark(args)
        output_path = args.output.expanduser().resolve()
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(
            json.dumps(result, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        print(f"Benchmark saved to: {output_path}")
    except (OSError, ValueError, wave.Error) as error:
        print(f"Error: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
