import pytest
from unittest.mock import patch, MagicMock
from src.config import Config


def _dev_config():
    return Config(
        environment="Development",
        port=8080,
        auth_issuer="https://localhost:8080/realms/poc",
        auth_audience="poc-api",
        foundry_project_endpoint="https://x.example.com",
        model_deployment_name="gpt-4o",
        api_version="2025-05-01",
        agent_client_id="agent-gateway",
        agent_client_secret="dev-secret",
        dab_host="localhost",
        dab_port=8000,
        dab_api_audience="poc-dab",
        dab_api_scope="",
        azure_tenant_id="tenant-guid",
        azure_sp_client_id="sp-client-id",
        azure_sp_client_secret="sp-secret",
    )


def _prod_config():
    return Config(
        environment="Production",
        port=8080,
        auth_issuer="https://login.microsoftonline.com/tenant-id/v2.0",
        auth_audience="api://dab-client-id",
        foundry_project_endpoint="https://x.example.com",
        model_deployment_name="gpt-4o",
        api_version="2025-05-01",
        agent_client_id="agent-app-id",
        agent_client_secret="prod-secret",
        dab_host="dab.example.com",
        dab_port=443,
        dab_api_audience="api://dab-client-id",
        dab_api_scope="api://dab-client-id/.default",
        azure_tenant_id="tenant-guid",
        azure_sp_client_id="sp-client-id",
        azure_sp_client_secret="sp-secret",
    )


def test_dev_exchange_calls_keycloak_endpoint():
    from src.token_exchange import exchange_token

    mock_response = MagicMock()
    mock_response.json.return_value = {"access_token": "dab-token-xyz"}
    mock_response.raise_for_status = MagicMock()

    with patch("src.token_exchange.requests.post", return_value=mock_response) as mock_post:
        result = exchange_token("poc-api-token", _dev_config())

    assert result == "dab-token-xyz"
    call_kwargs = mock_post.call_args
    assert "realms/poc/protocol/openid-connect/token" in call_kwargs[0][0]
    data = call_kwargs[1]["data"]
    assert data["grant_type"] == "urn:ietf:params:oauth:grant-type:token-exchange"
    assert data["subject_token"] == "poc-api-token"
    assert data["audience"] == "poc-dab"
    assert data["client_id"] == "agent-gateway"
    assert data["client_secret"] == "dev-secret"


def test_prod_exchange_calls_entra_obo_endpoint():
    from src.token_exchange import exchange_token

    mock_response = MagicMock()
    mock_response.json.return_value = {"access_token": "entra-dab-token"}
    mock_response.raise_for_status = MagicMock()

    with patch("src.token_exchange.requests.post", return_value=mock_response) as mock_post:
        result = exchange_token("poc-api-token", _prod_config())

    assert result == "entra-dab-token"
    call_kwargs = mock_post.call_args
    assert "oauth2/v2.0/token" in call_kwargs[0][0]
    data = call_kwargs[1]["data"]
    assert data["grant_type"] == "urn:ietf:params:oauth:grant-type:jwt-bearer"
    assert data["assertion"] == "poc-api-token"
    assert data["scope"] == "api://dab-client-id/.default"
    assert data["requested_token_use"] == "on_behalf_of"
