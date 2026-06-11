import os
from dataclasses import dataclass


@dataclass(frozen=True)
class Config:
    environment: str
    port: int
    auth_issuer: str
    auth_audience: str
    auth_jwks_url: str
    foundry_project_endpoint: str
    model_deployment_name: str
    api_version: str
    dab_host: str
    dab_port: int
    azure_tenant_id: str
    azure_sp_client_id: str
    azure_sp_client_secret: str

    @property
    def dab_base_url(self) -> str:
        return f"http://{self.dab_host}:{self.dab_port}"


def load_config() -> Config:
    required = [
        "AUTH_AUTHORITY",
        "AUTH_API_AUDIENCE",
        "AUTH_JWKS_URL",
        "AZ_FOUNDRY_PROJECT_ENDPOINT",
        "AZ_FOUNDRY_MODEL_DEPLOYMENT_NAME",
        "DAB_HOST",
        "DAB_PORT",
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
        auth_jwks_url=os.environ["AUTH_JWKS_URL"],
        foundry_project_endpoint=os.environ["AZ_FOUNDRY_PROJECT_ENDPOINT"],
        model_deployment_name=os.environ["AZ_FOUNDRY_MODEL_DEPLOYMENT_NAME"],
        api_version=os.environ.get("API_VERSION", "2025-05-01"),
        dab_host=os.environ["DAB_HOST"],
        dab_port=int(os.environ["DAB_PORT"]),
        azure_tenant_id=os.environ["AZ_FOUNDRY_TENANT_ID"],
        azure_sp_client_id=os.environ["AZ_FOUNDRY_SP_CLIENT_ID"],
        azure_sp_client_secret=os.environ["AZ_FOUNDRY_SP_CLIENT_SECRET"],
    )
