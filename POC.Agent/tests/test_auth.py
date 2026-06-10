import pytest
import jwt
from unittest.mock import patch, MagicMock
from src.auth import validate_token


def _make_signing_key_mock(key=b"fake"):
    mock = MagicMock()
    mock.key = key
    return mock


def test_validate_token_returns_claims():
    mock_jwks_client = MagicMock()
    mock_jwks_client.get_signing_key_from_jwt.return_value = _make_signing_key_mock()
    expected = {"sub": "alice", "roles": ["reader"], "aud": "poc-api"}

    with patch("src.auth._get_jwks_client", return_value=mock_jwks_client), \
         patch("src.auth.jwt.decode", return_value=expected):
        claims = validate_token("fake.token", "https://kc.example.com/realms/poc", "poc-api")
        assert claims["sub"] == "alice"
        assert claims["roles"] == ["reader"]


def test_validate_token_expired_raises_value_error():
    mock_jwks_client = MagicMock()
    mock_jwks_client.get_signing_key_from_jwt.return_value = _make_signing_key_mock()

    with patch("src.auth._get_jwks_client", return_value=mock_jwks_client), \
         patch("src.auth.jwt.decode", side_effect=jwt.ExpiredSignatureError):
        with pytest.raises(ValueError, match="Token expired"):
            validate_token("expired.token", "https://kc.example.com/realms/poc", "poc-api")


def test_validate_token_invalid_raises_value_error():
    mock_jwks_client = MagicMock()
    mock_jwks_client.get_signing_key_from_jwt.return_value = _make_signing_key_mock()

    with patch("src.auth._get_jwks_client", return_value=mock_jwks_client), \
         patch("src.auth.jwt.decode", side_effect=jwt.InvalidTokenError("bad aud")):
        with pytest.raises(ValueError, match="Invalid token"):
            validate_token("bad.token", "https://kc.example.com/realms/poc", "poc-api")
