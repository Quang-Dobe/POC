import logging
import os
from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import JSONResponse
from pydantic import BaseModel

logging.basicConfig(level=logging.INFO)
_log = logging.getLogger("agent.auth")

from .config import load_config
from .auth import validate_token
from .token_exchange import exchange_token
from .agent import make_project_client, make_openai_client
from .runner import run_agent

# Loaded once at startup — no blocking network call during module load
_config = load_config()
_project = make_project_client(_config)
_openai_client = make_openai_client(_project)

app = FastAPI()

_IS_DEV = _config.environment == "Development"

# One-shot startup dump of the (non-secret) auth params actually in effect.
# Mismatched issuer vs JWKS source is the usual cause of /ask 401s.
_log.info(
    "auth config: environment=%s issuer=%s audience=%s jwks_url=%s verify_ssl=%s",
    _config.environment,
    _config.auth_issuer,
    _config.auth_audience,
    _config.auth_jwks_url,
    not _IS_DEV,
)


class AskRequest(BaseModel):
    question: str
    session_id: str | None = None


@app.get("/health")
def health():
    return {"status": "ok"}


@app.post("/ask")
def ask(body: AskRequest, request: Request):
    auth_header = request.headers.get("Authorization", "")
    if not auth_header.startswith("Bearer "):
        _log.warning("401 /ask: missing or non-Bearer Authorization header")
        raise HTTPException(status_code=401, detail="Missing Bearer token")

    raw_token = auth_header[len("Bearer "):]

    try:
        claims = validate_token(
            raw_token,
            _config.auth_issuer,
            _config.auth_audience,
            verify_ssl=not _IS_DEV,
            jwks_url=_config.auth_jwks_url,
        )
    except ValueError as exc:
        # Log the concrete reason + the params used to validate, so a token/issuer/jwks
        # mismatch is visible in container logs (the response only carries the short detail).
        _log.warning(
            "401 /ask: token validation failed: %s | issuer=%s audience=%s jwks_url=%s",
            exc,
            _config.auth_issuer,
            _config.auth_audience,
            _config.auth_jwks_url,
        )
        raise HTTPException(status_code=401, detail=str(exc))

    roles: list[str] = claims.get("roles", [])
    api_role = roles[0] if roles else "reader"

    try:
        dab_token = exchange_token(raw_token, _config)
    except Exception as exc:
        raise HTTPException(status_code=502, detail=f"Token exchange failed: {exc}")

    try:
        answer, session_id = run_agent(
            _openai_client,
            body.question,
            body.session_id,
            dab_token,
            api_role,
            _config,
        )
    except TimeoutError as exc:
        raise HTTPException(status_code=504, detail=str(exc))
    except Exception as exc:
        # Full traceback to the container log — the response only carries the short detail,
        # which hides where in the tool loop / DAB call it actually broke.
        _log.exception("500 /ask: agent run failed: %s", exc)
        raise HTTPException(status_code=500, detail=f"Agent error: {exc}")

    return {"answer": answer, "session_id": session_id}


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("src.main:app", host="0.0.0.0", port=_config.port, reload=False)
