import os
import pytest
from unittest.mock import patch


def _base_env():
    return {
        "ENVIRONMENT": "Development",
        "PORT": "8080",
        "AUTH_AUTHORITY": "https://localhost:5001",
        "AUTH_API_AUDIENCE": "poc-internal",
        "AUTH_JWKS_URL": "http://host.docker.internal:5000/.well-known/jwks.json",
        "AZ_FOUNDRY_PROJECT_ENDPOINT": "https://example.services.ai.azure.com/api/projects/p1",
        "AZ_FOUNDRY_MODEL_DEPLOYMENT_NAME": "gpt-4o",
        "DAB_HOST": "localhost",
        "DAB_PORT": "8000",
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
        assert cfg.auth_issuer == "https://localhost:5001"
        assert cfg.auth_audience == "poc-internal"
        assert cfg.dab_base_url == "http://localhost:8000"


def test_load_config_jwks_url_is_required_str():
    with patch.dict(os.environ, _base_env(), clear=True):
        from src.config import load_config
        cfg = load_config()
        assert isinstance(cfg.auth_jwks_url, str)
        assert cfg.auth_jwks_url.endswith("/.well-known/jwks.json")


def test_load_config_missing_jwks_url_raises():
    env = _base_env()
    del env["AUTH_JWKS_URL"]
    with patch.dict(os.environ, env, clear=True):
        from src.config import load_config
        with pytest.raises(ValueError, match="AUTH_JWKS_URL"):
            load_config()


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
