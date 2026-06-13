import logging
import ssl
import jwt
from jwt import PyJWKClient
from jwt.exceptions import PyJWKClientError

_log = logging.getLogger("agent.auth")

_jwks_clients: dict[str, PyJWKClient] = {}


class TokenValidationError(ValueError):
    def __init__(self, message: str, kid: str | None = None,
                 iss: str | None = None, aud=None, jti: str | None = None):
        super().__init__(message)
        self.kid = kid
        self.iss = iss
        self.aud = aud
        self.jti = jti


class ExternalTokenError(TokenValidationError):
    pass


class MalformedTokenError(TokenValidationError):
    pass


def _unverified_ssl_ctx() -> ssl.SSLContext:
    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    return ctx


def _get_jwks_client(jwks_url: str, verify_ssl: bool = True) -> PyJWKClient:
    if jwks_url not in _jwks_clients:
        ssl_ctx = None if verify_ssl else _unverified_ssl_ctx()
        _jwks_clients[jwks_url] = PyJWKClient(jwks_url, ssl_context=ssl_ctx)
    return _jwks_clients[jwks_url]


def validate_token(
    token: str,
    issuer: str,
    audience: str,
    verify_ssl: bool = True,
    jwks_url: str | None = None,
) -> dict:
    kid: str | None = None
    token_iss: str | None = None
    token_aud = None
    token_jti: str | None = None
    try:
        header = jwt.get_unverified_header(token)
        unverified = jwt.decode(token, options={"verify_signature": False})
        kid = header.get("kid")
        token_iss = unverified.get("iss")
        token_aud = unverified.get("aud")
        token_jti = unverified.get("jti")
        _log.info(
            "token introspection: kid=%s alg=%s token.iss=%s token.aud=%s jti=%s | expected.iss=%s expected.aud=%s",
            kid,
            header.get("alg"),
            token_iss,
            token_aud,
            token_jti,
            issuer,
            audience,
        )
    except Exception as exc:
        _log.warning("token introspection failed (malformed token?): %s", exc)

    client = _get_jwks_client(jwks_url or issuer, verify_ssl)
    try:
        signing_key = client.get_signing_key_from_jwt(token)
    except PyJWKClientError as exc:
        raise ExternalTokenError(
            f"Signing key not found (unknown kid): {exc}",
            kid=kid, iss=token_iss, aud=token_aud, jti=token_jti,
        ) from exc
    except jwt.InvalidTokenError as exc:
        raise MalformedTokenError(
            f"Invalid token: {exc}",
            kid=kid, iss=token_iss, aud=token_aud, jti=token_jti,
        ) from exc

    try:
        return jwt.decode(
            token,
            signing_key.key,
            algorithms=["RS256"],
            audience=audience,
            issuer=issuer,
        )
    except jwt.ExpiredSignatureError as exc:
        raise MalformedTokenError(
            "Token expired",
            kid=kid, iss=token_iss, aud=token_aud, jti=token_jti,
        ) from exc
    except (jwt.InvalidSignatureError, jwt.InvalidIssuerError, jwt.InvalidAudienceError) as exc:
        raise ExternalTokenError(
            f"Invalid token (external/non-BE): {exc}",
            kid=kid, iss=token_iss, aud=token_aud, jti=token_jti,
        ) from exc
    except jwt.InvalidTokenError as exc:
        raise MalformedTokenError(
            f"Invalid token: {exc}",
            kid=kid, iss=token_iss, aud=token_aud, jti=token_jti,
        ) from exc
