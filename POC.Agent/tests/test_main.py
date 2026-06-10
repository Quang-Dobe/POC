import os
import sys
import pytest
from unittest.mock import patch, MagicMock
from fastapi.testclient import TestClient


def _base_env():
    return {
        "ENVIRONMENT": "Development",
        "PORT": "8080",
        "AUTH_AUTHORITY": "https://localhost:8080/realms/poc",
        "AUTH_API_AUDIENCE": "poc-api",
        "AZ_FOUNDRY_PROJECT_ENDPOINT": "https://x.example.com",
        "AZ_FOUNDRY_MODEL_DEPLOYMENT_NAME": "gpt-4o",
        "AUTH_AGENT_CLIENT_ID": "agent-gateway",
        "AUTH_AGENT_CLIENT_SECRET": "secret",
        "DAB_HOST": "localhost",
        "DAB_PORT": "8000",
        "AUTH_DAB_CLIENT_ID": "poc-dab",
        "AZ_FOUNDRY_TENANT_ID": "tenant-guid",
        "AZ_FOUNDRY_SP_CLIENT_ID": "sp-client-id",
        "AZ_FOUNDRY_SP_CLIENT_SECRET": "sp-secret",
    }


@pytest.fixture
def client():
    with patch.dict(os.environ, _base_env()):
        # Patch at the source so module-level startup code is intercepted
        # before src.main is imported/reloaded.
        with patch("src.agent.make_project_client"), \
             patch("src.agent.make_openai_client"):
            sys.modules.pop("src.main", None)
            import importlib
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


def test_ask_returns_401_with_invalid_token(client):
    with patch("src.main.validate_token", side_effect=ValueError("Invalid token")):
        resp = client.post(
            "/ask",
            json={"question": "how many trips?"},
            headers={"Authorization": "Bearer bad.token"},
        )
    assert resp.status_code == 401


def test_ask_returns_answer_with_valid_token(client):
    fake_claims = {"sub": "alice", "roles": ["reader"]}

    with patch("src.main.validate_token", return_value=fake_claims), \
         patch("src.main.exchange_token", return_value="dab-tok"), \
         patch("src.main.run_agent", return_value=("42 trips", "thread-1")):
        resp = client.post(
            "/ask",
            json={"question": "how many trips?"},
            headers={"Authorization": "Bearer valid.jwt"},
        )

    assert resp.status_code == 200
    body = resp.json()
    assert body["answer"] == "42 trips"
    assert body["session_id"] == "thread-1"
