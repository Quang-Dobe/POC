import logging
import requests
from .config import Config

_log = logging.getLogger("agent.auth")


def exchange_token(user_token: str, config: Config) -> str:
    if config.environment == "Development":
        return _keycloak_exchange(user_token, config)
    return _entra_obo(user_token, config)


def _keycloak_exchange(user_token: str, config: Config) -> str:
    # DEV: client_credentials grant — agent-gateway has a poc-dab audience mapper
    # so the resulting token carries aud=poc-dab without token exchange complexity.
    token_endpoint = config.auth_token_endpoint or f"{config.auth_issuer}/protocol/openid-connect/token"
    resp = requests.post(
        token_endpoint,
        data={
            "grant_type": "client_credentials",
            "client_id": config.agent_client_id,
            "client_secret": config.agent_client_secret,
        },
        verify=False,
        timeout=10,
    )
    resp.raise_for_status()
    return resp.json()["access_token"]


def _entra_obo(user_token: str, config: Config) -> str:
    # auth_issuer ends in /v2.0, so don't append /oauth2/v2.0/token to it (that doubles v2.0).
    # Use the explicit token endpoint from config; fall back to deriving it from the tenant host.
    token_endpoint = config.auth_token_endpoint or (
        config.auth_issuer.removesuffix("/v2.0") + "/oauth2/v2.0/token"
    )
    _log.info("OBO token exchange: endpoint=%s scope=%s", token_endpoint, config.dab_api_scope)
    resp = requests.post(
        token_endpoint,
        data={
            "grant_type": "urn:ietf:params:oauth:grant-type:jwt-bearer",
            "assertion": user_token,
            "scope": config.dab_api_scope,
            "client_id": config.agent_client_id,
            "client_secret": config.agent_client_secret,
            "requested_token_use": "on_behalf_of",
        },
        timeout=10,
    )
    if not resp.ok:
        # Azure returns the real reason (AADSTS code + description) in the body, not the status line.
        _log.warning("OBO exchange failed: HTTP %s | endpoint=%s | body=%s",
                     resp.status_code, token_endpoint, resp.text)
        resp.raise_for_status()
    return resp.json()["access_token"]
