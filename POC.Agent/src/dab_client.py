import json
import logging
import urllib.parse
import requests

_log = logging.getLogger("agent.auth")


def query_dab(
    base_url: str,
    dab_token: str,
    api_role: str,
    entity: str,
    filter: str | None = None,
    select: str | None = None,
    orderby: str | None = None,
    first: int = 20,
) -> str:
    params: dict = {"$first": max(1, min(first, 50))}
    if filter:
        params["$filter"] = filter
    if select:
        params["$select"] = select
    if orderby:
        # Normalize: LLMs may use + for space; OData $orderby needs actual spaces (%20)
        params["$orderby"] = orderby.replace("+", " ")

    # DAB OData parser needs:  $param=value  ($ unencoded, spaces as %20, not +)
    # Default urlencode encodes $ as %24 and space as +, both of which DAB rejects.
    query_string = urllib.parse.urlencode(
        params,
        quote_via=lambda s, safe, encoding=None, errors=None: urllib.parse.quote(str(s), safe="$,"),
    )
    url = f"{base_url}/api/{entity}?{query_string}"

    _log.info("DAB query: GET %s | role=%s", url, api_role)
    resp = requests.get(
        url,
        headers={
            "Authorization": f"Bearer {dab_token}",
            "X-MS-API-ROLE": api_role,
        },
        timeout=30,
    )
    if not resp.ok:
        # DAB returns the real reason (entity not found, auth/role denied, OData parse error)
        # in the body — surface it so the agent's 500 isn't a black box.
        _log.warning("DAB query failed: HTTP %s | url=%s | role=%s | body=%s",
                     resp.status_code, url, api_role, resp.text)
        resp.raise_for_status()
    return json.dumps(resp.json())
