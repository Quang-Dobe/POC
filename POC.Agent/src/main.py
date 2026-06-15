import logging
from fastapi import FastAPI, HTTPException, Request
from pydantic import BaseModel
from sse_starlette.sse import EventSourceResponse

logging.basicConfig(level=logging.INFO)
_log = logging.getLogger("agent.auth")

from .config import load_config
from .auth import validate_token, ExternalTokenError, MalformedTokenError
from .agent import make_project_client, make_openai_client
from .runner import run_agent, run_agent_stream

_config = load_config()
_project = make_project_client(_config)
_openai_client = make_openai_client(_project)

app = FastAPI()

_log.info(
    "auth config: environment=%s issuer=%s audience=%s jwks_url=%s verify_ssl=%s",
    _config.environment,
    _config.auth_issuer,
    _config.auth_audience,
    _config.auth_jwks_url,
    _config.auth_verify_ssl,
)


class AskRequest(BaseModel):
    question: str
    session_id: str | None = None


@app.get("/health")
def health():
    return {"status": "ok"}


def _authenticate(request: Request, route: str) -> tuple[str, str]:
    auth_header = request.headers.get("Authorization", "")
    if not auth_header.startswith("Bearer "):
        _log.warning("401 %s: missing or non-Bearer Authorization header", route)
        raise HTTPException(status_code=401, detail="Missing Bearer token")

    raw_token = auth_header[len("Bearer "):]

    try:
        claims = validate_token(
            raw_token,
            _config.auth_issuer,
            _config.auth_audience,
            verify_ssl=_config.auth_verify_ssl,
            jwks_url=_config.auth_jwks_url,
        )
    except ExternalTokenError as exc:
        _log.warning(
            "EXTERNAL_TOKEN_REJECTED no-fallback: 401 %s rejected an external/non-BE token | "
            "reason=%s kid=%s token.iss=%s token.aud=%s jti=%s | expected.iss=%s expected.aud=%s",
            route,
            exc,
            exc.kid,
            exc.iss,
            exc.aud,
            exc.jti,
            _config.auth_issuer,
            _config.auth_audience,
        )
        raise HTTPException(status_code=401, detail=str(exc))
    except MalformedTokenError as exc:
        _log.warning(
            "401 %s: token validation failed (malformed/expired): %s | issuer=%s audience=%s",
            route,
            exc,
            _config.auth_issuer,
            _config.auth_audience,
        )
        raise HTTPException(status_code=401, detail=str(exc))

    roles: list[str] = claims.get("roles", [])
    api_role = roles[0] if roles else "reader"

    return api_role, raw_token


@app.post("/ask")
def ask(body: AskRequest, request: Request):
    api_role, token = _authenticate(request, "/ask")

    try:
        answer, session_id = run_agent(
            _openai_client,
            body.question,
            body.session_id,
            token,
            api_role,
            _config,
        )
    except TimeoutError as exc:
        raise HTTPException(status_code=504, detail=str(exc))
    except Exception as exc:
        _log.exception("500 /ask: agent run failed: %s", exc)
        raise HTTPException(status_code=500, detail=f"Agent error: {exc}")

    return {"answer": answer, "session_id": session_id}


@app.post("/ask/stream")
def ask_stream(body: AskRequest, request: Request):
    api_role, token = _authenticate(request, "/ask/stream")

    def _event_stream():
        try:
            for chunk in run_agent_stream(
                _openai_client,
                body.question,
                body.session_id,
                token,
                api_role,
                _config,
            ):
                yield {"data": chunk}
        except Exception as exc:
            _log.exception("/ask/stream: agent stream failed mid-flight: %s", exc)
            return

    return EventSourceResponse(_event_stream())


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("src.main:app", host="0.0.0.0", port=_config.port, reload=False)
