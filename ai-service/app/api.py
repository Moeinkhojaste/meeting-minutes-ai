"""FastAPI surface for the Phase 3 AI service."""

from __future__ import annotations

import tempfile
from pathlib import Path
from typing import Annotated, Any
from uuid import uuid4

from fastapi import FastAPI, Form, Request, UploadFile
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from fastapi.routing import APIRoute
from starlette.concurrency import run_in_threadpool
from starlette.exceptions import HTTPException as StarletteHTTPException

from app.audio import prepare_audio
from app.config import Settings, load_settings, parse_mode
from app.errors import AppError, InvalidInputError
from app.orchestration import PipelineService
from app.providers.gemini import GeminiProvider
from app.providers.local_llm import LocalLLMProvider
from app.providers.local_stt import FasterWhisperProvider
from app.schemas import (
    MinutesRequest,
    MinutesResponse,
    ProcessResponse,
    SafeError,
    TranscriptionResponse,
)


def create_app(
    *,
    settings: Settings | None = None,
    pipeline: PipelineService | None = None,
) -> FastAPI:
    """Create an app with injectable dependencies for contract testing."""
    current_settings = settings or load_settings()
    current_pipeline = pipeline or PipelineService(
        current_settings,
        GeminiProvider(current_settings),
        FasterWhisperProvider(current_settings),
        LocalLLMProvider(current_settings),
    )
    app = FastAPI(title="Meeting Minutes AI Service", version="1.0.0")
    app.state.settings = current_settings
    app.state.pipeline = current_pipeline

    @app.middleware("http")
    async def correlation_middleware(request: Request, call_next: Any) -> Any:
        raw_header = request.headers.get("X-Correlation-ID")
        header_id = raw_header.strip() if raw_header else ""
        request.state.correlation_id = (
            header_id if header_id else str(uuid4())
        )
        response = await call_next(request)
        response.headers["X-Correlation-ID"] = request.state.correlation_id
        return response

    @app.exception_handler(AppError)
    async def app_error_handler(request: Request, error: AppError) -> JSONResponse:
        return _error_response(request, error)

    @app.exception_handler(RequestValidationError)
    async def validation_error_handler(
        request: Request, _error: RequestValidationError
    ) -> JSONResponse:
        return _error_response(
            request,
            InvalidInputError("INVALID_REQUEST", "Request validation failed."),
        )

    @app.exception_handler(StarletteHTTPException)
    async def http_error_handler(
        request: Request, error: StarletteHTTPException
    ) -> JSONResponse:
        code = "NOT_FOUND" if error.status_code == 404 else "HTTP_ERROR"
        return _error_response(
            request,
            AppError(code, "Request could not be completed.", error.status_code),
        )

    @app.exception_handler(Exception)
    async def unexpected_error_handler(
        request: Request, _error: Exception
    ) -> JSONResponse:
        return _error_response(
            request,
            AppError(
                "INTERNAL_ERROR",
                "An unexpected error occurred.",
                status_code=500,
                retryable=False,
            ),
        )

    @app.get("/health/live")
    async def live() -> dict[str, str]:
        return {"status": "live"}

    @app.get("/health/ready")
    async def ready() -> dict[str, Any]:
        gemini_configured = bool(current_settings.gemini_api_key)
        return {
            "status": "ready" if gemini_configured else "degraded",
            "transcription": {
                "geminiConfigured": gemini_configured,
                "localFallbackConfigured": True,
            },
            "minutes": {
                "geminiConfigured": gemini_configured,
                "localFallbackConfigured": True,
            },
        }

    @app.post(
        "/v1/transcriptions",
        response_model=TranscriptionResponse,
        responses={422: {"model": SafeError}, 502: {"model": SafeError}},
    )
    async def transcriptions(
        request: Request,
        audio: UploadFile,
        mode: Annotated[str | None, Form()] = None,
    ) -> TranscriptionResponse:
        selected_mode = _mode(mode, current_settings)
        path = await _save_upload(audio, current_settings.max_audio_bytes)
        try:
            with prepare_audio(path, current_settings) as prepared:
                return await run_in_threadpool(
                    current_pipeline.transcribe,
                    prepared,
                    selected_mode,
                    correlation_id=request.state.correlation_id,
                )
        finally:
            path.unlink(missing_ok=True)

    @app.post(
        "/v1/minutes",
        response_model=MinutesResponse,
        responses={422: {"model": SafeError}, 502: {"model": SafeError}},
    )
    async def minutes(
        request: Request,
        body: MinutesRequest,
    ) -> MinutesResponse:
        return await run_in_threadpool(
            current_pipeline.generate_minutes,
            body.rawTranscript,
            body.mode,
            correlation_id=request.state.correlation_id,
        )

    @app.post(
        "/v1/process",
        response_model=ProcessResponse,
        responses={422: {"model": SafeError}, 502: {"model": SafeError}},
    )
    async def process(
        request: Request,
        audio: UploadFile,
        mode: Annotated[str | None, Form()] = None,
    ) -> ProcessResponse:
        selected_mode = _mode(mode, current_settings)
        path = await _save_upload(audio, current_settings.max_audio_bytes)
        try:
            with prepare_audio(path, current_settings) as prepared:
                return await run_in_threadpool(
                    current_pipeline.process,
                    prepared,
                    selected_mode,
                    correlation_id=request.state.correlation_id,
                )
        finally:
            path.unlink(missing_ok=True)

    _hide_head_routes(app)
    return app


def _mode(value: str | None, settings: Settings) -> Any:
    try:
        return parse_mode(value, settings.default_mode)
    except ValueError as error:
        raise InvalidInputError("INVALID_MODE", str(error)) from error


async def _save_upload(upload: UploadFile, maximum_bytes: int) -> Path:
    suffix = Path(upload.filename or "audio").suffix[:12]
    handle = tempfile.NamedTemporaryFile(
        prefix="meeting-minutes-upload-",
        suffix=suffix,
        delete=False,
    )
    path = Path(handle.name)
    total = 0
    try:
        while chunk := await upload.read(1024 * 1024):
            total += len(chunk)
            if total > maximum_bytes:
                raise InvalidInputError(
                    "AUDIO_TOO_LARGE",
                    "Audio file exceeds the configured size limit.",
                )
            handle.write(chunk)
        handle.close()
        return path
    except Exception:
        handle.close()
        path.unlink(missing_ok=True)
        raise
    finally:
        await upload.close()


def _error_response(request: Request, error: AppError) -> JSONResponse:
    correlation_id = getattr(request.state, "correlation_id", str(uuid4()))
    body = SafeError(
        code=error.code,
        message=error.message,
        correlationId=correlation_id,
        retryable=error.retryable,
    )
    return JSONResponse(
        status_code=error.status_code,
        content=body.model_dump(mode="json"),
    )


def _hide_head_routes(app: FastAPI) -> None:
    """Keep OpenAPI focused on the explicitly supported API operations."""
    for route in app.routes:
        if isinstance(route, APIRoute):
            route.include_in_schema = route.path in {
                "/health/live",
                "/health/ready",
                "/v1/transcriptions",
                "/v1/minutes",
                "/v1/process",
            }


app = create_app()
