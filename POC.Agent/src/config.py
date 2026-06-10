import os
from dataclasses import dataclass


@dataclass(frozen=True)
class Config:
    environment: str
    port: int
    auth_issuer: str
    auth_audience: str
    foundry_project_endpoint: str
    model_deployment_name: str
    api_version: str
    agent_client_id: str
    agent_client_secret: str
    dab_host: str
    dab_port: int
    dab_api_audience: str
    dab_api_scope: str  # PROD OBO only; empty string in DEV
    azure_tenant_id: str
    azure_sp_client_id: str
    azure_sp_client_secret: str
    # Optional internal URLs — override auth_issuer for actual HTTP calls inside containers
    # where 'localhost' resolves to the container itself, not the host.
    auth_jwks_url: str | None = None
    auth_token_endpoint: str | None = None

    @property
    def dab_base_url(self) -> str:
        return f"http://{self.dab_host}:{self.dab_port}"


def load_config() -> Config:
    required = [
        "AUTH_AUTHORITY",
        "AUTH_API_AUDIENCE",
        "AZ_FOUNDRY_PROJECT_ENDPOINT",
        "AZ_FOUNDRY_MODEL_DEPLOYMENT_NAME",
        "AUTH_AGENT_CLIENT_ID",
        "AUTH_AGENT_CLIENT_SECRET",
        "DAB_HOST",
        "DAB_PORT",
        "AUTH_DAB_CLIENT_ID",
        "AZ_FOUNDRY_TENANT_ID",
        "AZ_FOUNDRY_SP_CLIENT_ID",
        "AZ_FOUNDRY_SP_CLIENT_SECRET",
    ]
    missing = [k for k in required if not os.environ.get(k)]
    if missing:
        raise ValueError(f"Missing required env vars: {', '.join(missing)}")

    return Config(
        environment=os.environ.get("ENVIRONMENT", "Development"),
        port=int(os.environ.get("PORT", "8080")),
        auth_issuer=os.environ["AUTH_AUTHORITY"],
        auth_audience=os.environ["AUTH_API_AUDIENCE"],
        foundry_project_endpoint=os.environ["AZ_FOUNDRY_PROJECT_ENDPOINT"],
        model_deployment_name=os.environ["AZ_FOUNDRY_MODEL_DEPLOYMENT_NAME"],
        api_version=os.environ.get("API_VERSION", "2025-05-01"),
        agent_client_id=os.environ["AUTH_AGENT_CLIENT_ID"],
        agent_client_secret=os.environ["AUTH_AGENT_CLIENT_SECRET"],
        dab_host=os.environ["DAB_HOST"],
        dab_port=int(os.environ["DAB_PORT"]),
        dab_api_audience=os.environ["AUTH_DAB_CLIENT_ID"],
        dab_api_scope=os.environ.get("DAB_API_SCOPE", ""),
        azure_tenant_id=os.environ["AZ_FOUNDRY_TENANT_ID"],
        azure_sp_client_id=os.environ["AZ_FOUNDRY_SP_CLIENT_ID"],
        azure_sp_client_secret=os.environ["AZ_FOUNDRY_SP_CLIENT_SECRET"],
        auth_jwks_url=os.environ.get("AUTH_JWKS_URL") or None,
        auth_token_endpoint=os.environ.get("AUTH_TOKEN_ENDPOINT") or None,
    )
