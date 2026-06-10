import json
import pytest
from unittest.mock import patch, MagicMock


def _ok_response(data):
    mock = MagicMock()
    mock.json.return_value = {"value": data}
    mock.raise_for_status = MagicMock()
    return mock


def test_query_dab_builds_correct_url_and_headers():
    from src.dab_client import query_dab

    with patch("src.dab_client.requests.get", return_value=_ok_response([])) as mock_get:
        query_dab("http://dab:8000", "tok123", "reader", "Trip")

    call = mock_get.call_args
    assert call[0][0] == "http://dab:8000/api/Trip"
    headers = call[1]["headers"]
    assert headers["Authorization"] == "Bearer tok123"
    assert headers["X-MS-API-ROLE"] == "reader"


def test_query_dab_passes_odata_params():
    from src.dab_client import query_dab

    with patch("src.dab_client.requests.get", return_value=_ok_response([])) as mock_get:
        query_dab(
            "http://dab:8000", "tok", "reader", "Geography",
            filter="City eq 'New York'", select="City,County", orderby="County desc", first=10,
        )

    params = mock_get.call_args[1]["params"]
    assert params["$filter"] == "City eq 'New York'"
    assert params["$select"] == "City,County"
    assert params["$orderby"] == "County desc"
    assert params["$first"] == 10


def test_query_dab_caps_first_at_50():
    from src.dab_client import query_dab

    with patch("src.dab_client.requests.get", return_value=_ok_response([])) as mock_get:
        query_dab("http://dab:8000", "tok", "reader", "Trip", first=200)

    params = mock_get.call_args[1]["params"]
    assert params["$first"] == 50


def test_query_dab_returns_json_string():
    from src.dab_client import query_dab

    with patch("src.dab_client.requests.get", return_value=_ok_response([{"DateID": 1}])):
        result = query_dab("http://dab:8000", "tok", "reader", "Date")

    parsed = json.loads(result)
    assert parsed["value"][0]["DateID"] == 1
