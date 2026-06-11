"""Real-crypto unit tests for src.auth (Step G, decision Q6).

These tests generate an RSA keypair AT RUNTIME and mint test tokens with PyJWT, then let the
REAL jwt.decode enforce issuer/audience/signature. No PEM is committed. The JWKS client is stubbed
to return the runtime public key for the matching kid, so an unknown-kid token (signed by a second
runtime key the stub does not know) exercises the real PyJWKClientError path — the most common
E2E-6 reject path, which fails at get_signing_key_from_jwt BEFORE iss/aud are ever checked.

This UPGRADES the old suite that patched src.auth.jwt.decode and therefore asserted nothing about
real issuer/audience enforcement.
"""
import time
import jwt
import pytest
from cryptography.hazmat.primitives.asymmetric import rsa

from src.auth import (
    validate_token,
    ExternalTokenError,
    MalformedTokenError,
    TokenValidationError,
)

BE_ISSUER = "https://localhost:5001"
BE_AUDIENCE = "poc-internal"
JWKS_URL = "http://be/.well-known/jwks.json"
GOOD_KID = "be-key-1"
UNKNOWN_KID = "external-key-9"


def _new_private_key():
    return rsa.generate_private_key(public_exponent=65537, key_size=2048)


_BE_KEY = _new_private_key()
_EXTERNAL_KEY = _new_private_key()


def _mint_token(private_key, kid, *, iss=BE_ISSUER, aud=BE_AUDIENCE, exp_offset=300):
    now = int(time.time())
    claims = {
        "sub": "alice",
        "iss": iss,
        "aud": aud,
        "iat": now,
        "exp": now + exp_offset,
        "jti": "jti-123",
        "roles": ["reader"],
        "region": "Manhattan",
    }
    return jwt.encode(claims, private_key, algorithm="RS256", headers={"kid": kid})


class _StubJwksClient:
    """Stands in for PyJWKClient: maps known kids to their public key, raises the real
    PyJWKClientError for an unknown kid (the genuine external-token reject path)."""

    def __init__(self, keys_by_kid):
        self._keys_by_kid = keys_by_kid

    def get_signing_key_from_jwt(self, token):
        header = jwt.get_unverified_header(token)
        kid = header.get("kid")
        if kid not in self._keys_by_kid:
            from jwt.exceptions import PyJWKClientError
            raise PyJWKClientError(f"Unable to find a signing key that matches kid={kid}")
        signing_key = type("Key", (), {})()
        signing_key.key = self._keys_by_kid[kid]
        return signing_key


@pytest.fixture
def stub_jwks(monkeypatch):
    client = _StubJwksClient({GOOD_KID: _BE_KEY.public_key()})
    monkeypatch.setattr("src.auth._get_jwks_client", lambda *a, **k: client)
    return client


def test_accepts_be_issued_token(stub_jwks):
    token = _mint_token(_BE_KEY, GOOD_KID)
    claims = validate_token(token, BE_ISSUER, BE_AUDIENCE, jwks_url=JWKS_URL)
    assert claims["sub"] == "alice"
    assert claims["iss"] == BE_ISSUER
    assert claims["aud"] == BE_AUDIENCE
    assert claims["roles"] == ["reader"]
    assert claims["region"] == "Manhattan"


def test_rejects_wrong_issuer_as_external(stub_jwks):
    token = _mint_token(_BE_KEY, GOOD_KID, iss="https://login.microsoftonline.com/tenant/v2.0")
    with pytest.raises(ExternalTokenError) as ei:
        validate_token(token, BE_ISSUER, BE_AUDIENCE, jwks_url=JWKS_URL)
    assert ei.value.iss == "https://login.microsoftonline.com/tenant/v2.0"


def test_rejects_wrong_audience_as_external(stub_jwks):
    token = _mint_token(_BE_KEY, GOOD_KID, aud="poc-other")
    with pytest.raises(ExternalTokenError) as ei:
        validate_token(token, BE_ISSUER, BE_AUDIENCE, jwks_url=JWKS_URL)
    assert ei.value.aud == "poc-other"


def test_rejects_unknown_kid_as_external(stub_jwks):
    token = _mint_token(_EXTERNAL_KEY, UNKNOWN_KID)
    with pytest.raises(ExternalTokenError) as ei:
        validate_token(token, BE_ISSUER, BE_AUDIENCE, jwks_url=JWKS_URL)
    assert ei.value.kid == UNKNOWN_KID
    assert ei.value.iss == BE_ISSUER


def test_expired_be_token_is_malformed_not_external(stub_jwks):
    token = _mint_token(_BE_KEY, GOOD_KID, exp_offset=-10)
    with pytest.raises(MalformedTokenError) as ei:
        validate_token(token, BE_ISSUER, BE_AUDIENCE, jwks_url=JWKS_URL)
    assert not isinstance(ei.value, ExternalTokenError)
    assert "expired" in str(ei.value).lower()


def test_structurally_malformed_token_is_malformed_not_external(stub_jwks):
    with pytest.raises(TokenValidationError) as ei:
        validate_token("not.a.jwt", BE_ISSUER, BE_AUDIENCE, jwks_url=JWKS_URL)
    assert not isinstance(ei.value, ExternalTokenError)
