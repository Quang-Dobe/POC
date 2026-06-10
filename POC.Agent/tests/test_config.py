import os
import pytest
from unittest.mock import patch


def _base_env():
    return {
        "ENVIRONMENT": "Development",
        "PORT": "8080",
        "AUTH_AUTHORITY": "https://localhost:8080/realms/poc",
        "AUTH_API_AUDIENCE": "poc-api",
        "AZ_FOUNDRY_PROJECT_ENDPOINT": "https://example.services.ai.azure.com/api/projects/p1",
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


def test_load_config_happy_path():
    with patch.dict(os.environ, _base_env(), clear=True):
        from src.config import load_config
        cfg = load_config()
        assert cfg.environment == "Development"
        assert cfg.port == 8080
        assert cfg.dab_base_url == "http://localhost:8000"


def test_load_config_dab_base_url_computed_from_host_and_port():
    env = {**_base_env(), "DAB_HOST": "host.docker.internal", "DAB_PORT": "9999"}
    with patch.dict(os.environ, env, clear=True):
        from src.config import load_config
        cfg = load_config()
        assert cfg.dab_base_url == "http://host.docker.internal:9999"


def test_load_config_missing_required_var_raises():
    env = _base_env()
    del env["AZ_FOUNDRY_PROJECT_ENDPOINT"]
    with patch.dict(os.environ, env, clear=True):
        from src.config import load_config
        with pytest.raises(ValueError, match="AZ_FOUNDRY_PROJECT_ENDPOINT"):
            load_config()
