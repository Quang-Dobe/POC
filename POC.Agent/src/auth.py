import logging
import ssl
import jwt
from jwt import PyJWKClient
import requests

_log = logging.getLogger("agent.auth")

# Module-level caches — one JWKS client per resolved JWKS URI per process.
_jwks_uris: dict[str, str] = {}
_jwks_clients: dict[str, PyJWKClient] = {}


def _unverified_ssl_ctx() -> ssl.SSLContext:
    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    return ctx


def _get_jwks_client(issuer: str, verify_ssl: bool = True, jwks_url: str | None = None) -> PyJWKClient:
    if jwks_url:
        # Skip OIDC discovery — use the supplied JWKS URL directly.
        effective_uri = jwks_url
    else:
        if issuer not in _jwks_uris:
            resp = requests.get(
                f"{issuer}/.well-known/openid-configuration",
                verify=verify_ssl,
                timeout=10,
            )
            resp.raise_for_status()
            _jwks_uris[issuer] = resp.json()["jwks_uri"]
        effective_uri = _jwks_uris[issuer]

    if effective_uri not in _jwks_clients:
        ssl_ctx = None if verify_ssl else _unverified_ssl_ctx()
        _jwks_clients[effective_uri] = PyJWKClient(effective_uri, ssl_context=ssl_ctx)
    return _jwks_clients[effective_uri]


def validate_token(
    token: str,
    issuer: str,
    audience: str,
    verify_ssl: bool = True,
    jwks_url: str | None = None,
) -> dict:
    # Dump what the token actually claims (unverified) vs what we'll enforce — the
    # fastest way to spot an issuer/audience/kid mismatch in the logs.
    try:
        header = jwt.get_unverified_header(token)
        unverified = jwt.decode(token, options={"verify_signature": False})
        _log.info(
            "token introspection: kid=%s alg=%s token.iss=%s token.aud=%s | expected.iss=%s expected.aud=%s",
            header.get("kid"),
            header.get("alg"),
            unverified.get("iss"),
            unverified.get("aud"),
            issuer,
            audience,
        )
    except Exception as exc:  # malformed token — keep going, decode below raises the real error
        _log.warning("token introspection failed (malformed token?): %s", exc)

    client = _get_jwks_client(issuer, verify_ssl, jwks_url)
    try:
        signing_key = client.get_signing_key_from_jwt(token)
        return jwt.decode(
            token,
            signing_key.key,
            algorithms=["RS256"],
            audience=audience,
            issuer=issuer,
        )
    except jwt.ExpiredSignatureError:
        raise ValueError("Token expired")
    except (jwt.InvalidTokenError, Exception) as exc:
        raise ValueError(f"Invalid token: {exc}") from exc
