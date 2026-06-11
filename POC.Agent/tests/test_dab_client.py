import json
import urllib.parse
from unittest.mock import patch, MagicMock


def _ok_response(data):
    mock = MagicMock()
    mock.json.return_value = {"value": data}
    mock.ok = True
    mock.raise_for_status = MagicMock()
    return mock


def _called_url(mock_get):
    """The positional URL argument query_dab passes to requests.get.

    dab_client.query_dab deliberately builds the OData query string INTO the URL (with $ unencoded
    and spaces as %20) because DAB rejects the %24/+ encoding the default params= path produces
    (see src/dab_client.py:28-34). So the assertions read the URL, NOT a params= kwarg.
    """
    return mock_get.call_args[0][0]


def _query_params(url):
    """Parse the query string off the built URL back into a name->value dict for assertions."""
    query = urllib.parse.urlsplit(url).query
    return dict(urllib.parse.parse_qsl(query))


def test_query_dab_builds_correct_base_url_and_headers():
    from src.dab_client import query_dab

    with patch("src.dab_client.requests.get", return_value=_ok_response([])) as mock_get:
        query_dab("http://dab:8000", "tok123", "reader", "Trip")

    url = _called_url(mock_get)
    assert url.startswith("http://dab:8000/api/Trip")
    assert "params" not in mock_get.call_args[1]

    headers = mock_get.call_args[1]["headers"]
    assert headers["Authorization"] == "Bearer tok123"
    assert headers["X-MS-API-ROLE"] == "reader"


def test_query_dab_encodes_odata_params_into_url_with_dollar_unencoded():
    from src.dab_client import query_dab

    with patch("src.dab_client.requests.get", return_value=_ok_response([])) as mock_get:
        query_dab(
            "http://dab:8000", "tok", "reader", "Geography",
            filter="City eq 'New York'", select="City,County", orderby="County desc", first=10,
        )

    url = _called_url(mock_get)
    assert "$filter=" in url
    assert "$select=" in url
    assert "$orderby=" in url
    assert "$first=" in url
    assert "%24" not in url

    params = _query_params(url)
    assert params["$filter"] == "City eq 'New York'"
    assert params["$select"] == "City,County"
    assert params["$orderby"] == "County desc"
    assert params["$first"] == "10"


def test_query_dab_caps_first_at_50():
    from src.dab_client import query_dab

    with patch("src.dab_client.requests.get", return_value=_ok_response([])) as mock_get:
        query_dab("http://dab:8000", "tok", "reader", "Trip", first=200)

    params = _query_params(_called_url(mock_get))
    assert params["$first"] == "50"


def test_query_dab_normalizes_orderby_plus_to_space():
    from src.dab_client import query_dab

    with patch("src.dab_client.requests.get", return_value=_ok_response([])) as mock_get:
        query_dab("http://dab:8000", "tok", "reader", "Geography", orderby="County+desc")

    url = _called_url(mock_get)
    params = _query_params(_called_url(mock_get))
    assert params["$orderby"] == "County desc"
    assert "$orderby=County%20desc" in url


def test_query_dab_returns_json_string():
    from src.dab_client import query_dab

    with patch("src.dab_client.requests.get", return_value=_ok_response([{"DateID": 1}])):
        result = query_dab("http://dab:8000", "tok", "reader", "Date")

    parsed = json.loads(result)
    assert parsed["value"][0]["DateID"] == 1
