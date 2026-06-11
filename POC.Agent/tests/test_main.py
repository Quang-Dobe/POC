import logging
import os
import sys
import importlib
import pytest
from unittest.mock import patch
from fastapi.testclient import TestClient

from src.auth import ExternalTokenError, MalformedTokenError


def _base_env():
    return {
        "ENVIRONMENT": "Development",
        "PORT": "8080",
        "AUTH_AUTHORITY": "https://localhost:5001",
        "AUTH_API_AUDIENCE": "poc-internal",
        "AUTH_JWKS_URL": "http://host.docker.internal:5000/.well-known/jwks.json",
        "AZ_FOUNDRY_PROJECT_ENDPOINT": "https://x.example.com",
        "AZ_FOUNDRY_MODEL_DEPLOYMENT_NAME": "gpt-4o",
        "DAB_HOST": "localhost",
        "DAB_PORT": "8000",
        "AZ_FOUNDRY_TENANT_ID": "tenant-guid",
        "AZ_FOUNDRY_SP_CLIENT_ID": "sp-client-id",
        "AZ_FOUNDRY_SP_CLIENT_SECRET": "sp-secret",
    }


@pytest.fixture
def client():
    with patch.dict(os.environ, _base_env()):
        with patch("src.agent.make_project_client"), \
             patch("src.agent.make_openai_client"):
            sys.modules.pop("src.main", None)
            import src.main as main
            importlib.reload(main)
            yield TestClient(main.app)


def test_health_returns_200_without_auth(client):
    resp = client.get("/health")
    assert resp.status_code == 200
    assert resp.json()["status"] == "ok"


def test_ask_returns_401_without_auth_header(client):
    resp = client.post("/ask", json={"question": "how many trips?"})
    assert resp.status_code == 401


def test_ask_returns_answer_and_forwards_the_inbound_bearer(client):
    fake_claims = {"sub": "alice", "roles": ["reader"]}
    captured = {}

    def _capture_run_agent(openai_client, question, session_id, token, api_role, config):
        captured["token"] = token
        captured["api_role"] = api_role
        return ("42 trips", "thread-1")

    with patch("src.main.validate_token", return_value=fake_claims), \
         patch("src.main.run_agent", side_effect=_capture_run_agent):
        resp = client.post(
            "/ask",
            json={"question": "how many trips?"},
            headers={"Authorization": "Bearer valid.be.jwt"},
        )

    assert resp.status_code == 200
    body = resp.json()
    assert body["answer"] == "42 trips"
    assert body["session_id"] == "thread-1"
    assert captured["token"] == "valid.be.jwt"
    assert captured["api_role"] == "reader"


def test_ask_external_token_rejected_with_loud_no_fallback_marker(client, caplog):
    exc = ExternalTokenError(
        "Invalid token (external/non-BE)",
        kid="external-key-9",
        iss="https://login.microsoftonline.com/tenant/v2.0",
        aud="poc-internal",
        jti="jti-1",
    )
    with caplog.at_level(logging.WARNING, logger="agent.auth"):
        with patch("src.main.validate_token", side_effect=exc):
            resp = client.post(
                "/ask",
                json={"question": "how many trips?"},
                headers={"Authorization": "Bearer raw.external.jwt"},
            )
    assert resp.status_code == 401
    assert "EXTERNAL_TOKEN_REJECTED no-fallback" in caplog.text
    assert "raw.external.jwt" not in caplog.text


def test_ask_expired_be_token_does_not_emit_external_marker(client, caplog):
    exc = MalformedTokenError(
        "Token expired", kid="be-key-1", iss="https://localhost:5001", aud="poc-internal")
    with caplog.at_level(logging.WARNING, logger="agent.auth"):
        with patch("src.main.validate_token", side_effect=exc):
            resp = client.post(
                "/ask",
                json={"question": "how many trips?"},
                headers={"Authorization": "Bearer expired.be.jwt"},
            )
    assert resp.status_code == 401
    assert "EXTERNAL_TOKEN_REJECTED no-fallback" not in caplog.text


def test_no_exchange_token_symbol_remains():
    import src.main as main
    assert not hasattr(main, "exchange_token")
    assert "token_exchange" not in dir(main)


def _data_frames(sse_text: str) -> list[str]:
    """Reassemble the chunk each SSE frame carries the way BE's parser does
    (AgentGatewayClient: accumulate consecutive `data:` lines, rejoin on \\n, flush on blank
    line). Returns one reconstructed chunk per frame — so framing can be asserted exactly."""
    frames: list[str] = []
    current: list[str] = []
    for raw in sse_text.split("\n"):
        line = raw.rstrip("\r")
        if line == "":
            if current:
                frames.append("\n".join(current))
                current = []
            continue
        if line.startswith("data:"):
            value = line[len("data:"):]
            if value.startswith(" "):
                value = value[1:]
            current.append(value)
    if current:
        frames.append("\n".join(current))
    return frames


def test_ask_stream_returns_401_without_auth_header(client):
    resp = client.post("/ask/stream", json={"question": "how many trips?"})
    assert resp.status_code == 401


def test_ask_stream_two_chunks_become_two_separate_data_frames(client):
    fake_claims = {"sub": "alice", "roles": ["reader"]}

    def _fake_stream(*_args, **_kwargs):
        yield "Hello"
        yield "world"

    with patch("src.main.validate_token", return_value=fake_claims), \
         patch("src.main.run_agent_stream", side_effect=_fake_stream):
        resp = client.post(
            "/ask/stream",
            json={"question": "hi"},
            headers={"Authorization": "Bearer valid.be.jwt"},
        )

    assert resp.status_code == 200
    assert resp.headers["content-type"].startswith("text/event-stream")
    assert _data_frames(resp.text) == ["Hello", "world"]
    assert resp.text.replace("\r\n", "\n").count("\n\n") >= 2


def test_ask_stream_multiline_chunk_becomes_multiple_data_lines_in_one_frame(client):
    fake_claims = {"sub": "alice", "roles": ["reader"]}

    def _fake_stream(*_args, **_kwargs):
        yield "line-A\nline-B"

    with patch("src.main.validate_token", return_value=fake_claims), \
         patch("src.main.run_agent_stream", side_effect=_fake_stream):
        resp = client.post(
            "/ask/stream",
            json={"question": "hi"},
            headers={"Authorization": "Bearer valid.be.jwt"},
        )

    assert resp.status_code == 200
    assert _data_frames(resp.text) == ["line-A\nline-B"]
    body = resp.text
    assert "data: line-A" in body
    assert "data: line-B" in body


def test_ask_stream_external_token_rejected_with_loud_no_fallback_marker(client, caplog):
    exc = ExternalTokenError(
        "Invalid token (external/non-BE)",
        kid="external-key-9",
        iss="https://login.microsoftonline.com/tenant/v2.0",
        aud="poc-internal",
        jti="jti-1",
    )
    with caplog.at_level(logging.WARNING, logger="agent.auth"):
        with patch("src.main.validate_token", side_effect=exc):
            resp = client.post(
                "/ask/stream",
                json={"question": "hi"},
                headers={"Authorization": "Bearer raw.external.jwt"},
            )
    assert resp.status_code == 401
    assert "EXTERNAL_TOKEN_REJECTED no-fallback" in caplog.text
    assert "raw.external.jwt" not in caplog.text


def test_ask_stream_validate_token_called_before_response_constructed(client):
    fake_claims = {"sub": "alice", "roles": ["manager"]}
    captured = {}

    def _fake_stream(openai_client, question, session_id, token, api_role, config):
        captured["api_role"] = api_role
        captured["token"] = token
        yield "ok"

    with patch("src.main.validate_token", return_value=fake_claims) as mock_validate, \
         patch("src.main.run_agent_stream", side_effect=_fake_stream):
        resp = client.post(
            "/ask/stream",
            json={"question": "hi"},
            headers={"Authorization": "Bearer valid.be.jwt"},
        )

    assert resp.status_code == 200
    mock_validate.assert_called_once()
    assert captured["api_role"] == "manager"
    assert captured["token"] == "valid.be.jwt"
